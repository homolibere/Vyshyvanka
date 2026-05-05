using System.Collections.Concurrent;
using System.Reflection;
using System.Text.Json;
using System.Text.RegularExpressions;
using Jint;
using Jint.Native;
using Microsoft.CodeAnalysis.CSharp.Scripting;
using Microsoft.CodeAnalysis.Scripting;
using Microsoft.Extensions.Logging;
using Vyshyvanka.Core.Attributes;
using Vyshyvanka.Core.Interfaces;
using Vyshyvanka.Engine.Nodes.Base;
using JintEngine = Jint.Engine;

namespace Vyshyvanka.Engine.Nodes.Actions;

/// <summary>
/// An action node that executes user-provided C# or JavaScript code.
/// C# uses Roslyn scripting; JavaScript uses the Jint interpreter.
/// Supports two execution modes: run once for all input, or run once per item.
/// </summary>
[NodeDefinition(
    Name = "Code",
    Description = "Execute custom C# or JavaScript code to transform data, implement logic, or perform calculations",
    Icon = "fa-solid fa-code")]
[NodeInput("input", DisplayName = "Input", IsRequired = false)]
[NodeOutput("output", DisplayName = "Output")]
[ConfigurationProperty("language", "string", Description = "Programming language", IsRequired = false,
    Options = "csharp,javascript")]
[ConfigurationProperty("code", "code", Description = "Code to execute. Return a value to pass it to the output.",
    IsRequired = true)]
[ConfigurationProperty("mode", "string", Description = "Execution mode", IsRequired = false,
    Options = "runOnce,runForEachItem")]
[ConfigurationProperty("timeout", "number", Description = "Execution timeout in seconds (default: 30)")]
public class CodeNode : BaseActionNode
{
    private static readonly ConcurrentDictionary<string, Script<object>> CSharpScriptCache = new();
    private static readonly ScriptOptions DefaultScriptOptions = CreateScriptOptions();

    private readonly string _id = Guid.NewGuid().ToString();

    /// <inheritdoc />
    public override string Id => _id;

    /// <inheritdoc />
    public override string Type => "code";

    /// <inheritdoc />
    public override async Task<NodeOutput> ExecuteAsync(NodeInput input, IExecutionContext context)
    {
        try
        {
            var language = GetConfigValue<string>(input, "language") ?? "csharp";
            var code = GetRequiredConfigValue<string>(input, "code");
            var mode = GetConfigValue<string>(input, "mode") ?? "runOnce";
            var timeoutSeconds = GetConfigValue<int?>(input, "timeout") ?? 30;

            if (string.IsNullOrWhiteSpace(code))
            {
                return FailureOutput("Code cannot be empty");
            }

            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(context.CancellationToken);
            timeoutCts.CancelAfter(TimeSpan.FromSeconds(timeoutSeconds));

            return language.ToLowerInvariant() switch
            {
                "javascript" or "js" => ExecuteJavaScript(code, mode, input, context, timeoutCts.Token),
                _ => await ExecuteCSharpAsync(code, mode, input, context, timeoutCts.Token)
            };
        }
        catch (CompilationErrorException ex)
        {
            var errors = string.Join(Environment.NewLine, ex.Diagnostics.Select(d => d.ToString()));
            return FailureOutput($"Compilation error:{Environment.NewLine}{errors}");
        }
        catch (Jint.Runtime.JavaScriptException ex)
        {
            return FailureOutput($"JavaScript error: {ex.Message}");
        }
        catch (OperationCanceledException) when (!context.CancellationToken.IsCancellationRequested)
        {
            return FailureOutput("Code execution timed out");
        }
        catch (OperationCanceledException)
        {
            return FailureOutput("Code execution was cancelled");
        }
        catch (Exception ex)
        {
            return FailureOutput($"Code execution error: {ex.Message}");
        }
    }

    #region C# Execution

    private static async Task<NodeOutput> ExecuteCSharpAsync(
        string code,
        string mode,
        NodeInput input,
        IExecutionContext context,
        CancellationToken ct)
    {
        var globals = new CodeNodeGlobals(
            input.Data,
            context.ExecutionId,
            context.WorkflowId,
            context.Logger);

        if (mode == "runForEachItem")
        {
            var items = globals.GetItems();
            var results = new List<object?>(items.Length);

            for (var i = 0; i < items.Length; i++)
            {
                ct.ThrowIfCancellationRequested();
                globals.CurrentItem = items[i];
                globals.ItemIndex = i;

                var result = await RunCSharpScriptAsync(code, globals, ct);
                results.Add(result);
            }

            return BuildCSharpOutput(results, globals);
        }

        var singleResult = await RunCSharpScriptAsync(code, globals, ct);
        return BuildCSharpOutput(singleResult, globals);
    }

    private static async Task<object?> RunCSharpScriptAsync(
        string code,
        CodeNodeGlobals globals,
        CancellationToken ct)
    {
        var script = CSharpScriptCache.GetOrAdd(code, static c =>
            CSharpScript.Create<object>(c, DefaultScriptOptions, typeof(CodeNodeGlobals)));

        var state = await script.RunAsync(globals, cancellationToken: ct);

        if (state.Exception is not null)
            throw state.Exception;

        return state.ReturnValue;
    }

    private static NodeOutput BuildCSharpOutput(object? result, CodeNodeGlobals globals)
    {
        var outputData = new Dictionary<string, object?>
        {
            ["result"] = result,
            ["logs"] = globals.Logs
        };

        return SuccessOutput(outputData);
    }

    private static ScriptOptions CreateScriptOptions()
    {
        return ScriptOptions.Default
            .WithReferences(
                typeof(object).Assembly,
                typeof(Enumerable).Assembly,
                typeof(JsonElement).Assembly,
                typeof(JsonSerializer).Assembly,
                typeof(List<>).Assembly,
                typeof(Dictionary<,>).Assembly,
                typeof(Math).Assembly,
                typeof(Convert).Assembly,
                typeof(Guid).Assembly,
                typeof(DateTime).Assembly,
                typeof(TimeSpan).Assembly,
                typeof(Regex).Assembly,
                Assembly.Load("System.Runtime"),
                Assembly.Load("System.Collections"),
                Assembly.Load("System.Linq"),
                Assembly.Load("System.Linq.Expressions"))
            .WithImports(
                "System",
                "System.Collections.Generic",
                "System.Linq",
                "System.Text.Json",
                "System.Text.RegularExpressions",
                "System.Threading.Tasks");
    }

    #endregion

    #region JavaScript Execution

    private static NodeOutput ExecuteJavaScript(
        string code,
        string mode,
        NodeInput input,
        IExecutionContext context,
        CancellationToken ct)
    {
        var logs = new List<string>();

        var engine = new JintEngine(options =>
        {
            options.TimeoutInterval(TimeSpan.FromSeconds(30));
            options.MaxStatements(100_000);
            options.LimitMemory(64_000_000); // 64 MB
            options.LimitRecursion(256);
            options.CancellationToken(ct);
        });

        // Parse input data to a JS-friendly object
        var inputJson = input.Data.ValueKind != JsonValueKind.Undefined
            ? input.Data.GetRawText()
            : "null";

        var jsonParser = new Jint.Native.Json.JsonParser(engine);
        var jsInput = jsonParser.Parse(inputJson);

        // Set up globals
        engine.SetValue("input", jsInput);
        engine.SetValue("executionId", context.ExecutionId.ToString());
        engine.SetValue("workflowId", context.WorkflowId.ToString());
        engine.SetValue("log", (Action<string>)(message =>
        {
            logs.Add(message);
            context.Logger.LogInformation("[CodeNode/JS] {Message}", message);
        }));

        // Helper: getItems() returns input as array
        engine.SetValue("getItems", (Func<JsValue>)(() =>
        {
            if (jsInput is { Type: Jint.Runtime.Types.Object } && jsInput.IsArray())
                return jsInput;

            var arr = engine.Intrinsics.Array.Construct(1);
            arr.Set(0, jsInput);
            return arr;
        }));

        // Helper: toJson(value) serializes to JSON string
        engine.SetValue("toJson", (Func<JsValue, string>)(value =>
        {
            var serializer = new Jint.Native.Json.JsonSerializer(engine);
            var result = serializer.Serialize(value, JsValue.Undefined, JsValue.Undefined);
            return result?.AsString() ?? "null";
        }));

        if (mode == "runForEachItem")
        {
            return ExecuteJavaScriptForEachItem(engine, code, jsInput, logs);
        }

        return ExecuteJavaScriptOnce(engine, code, logs);
    }

    private static NodeOutput ExecuteJavaScriptOnce(JintEngine engine, string code, List<string> logs)
    {
        // Wrap in an IIFE so `return` works at top level
        var wrappedCode = $"(function() {{ {code} }})()";
        var result = engine.Evaluate(wrappedCode);

        return BuildJavaScriptOutput(result, engine, logs);
    }

    private static NodeOutput ExecuteJavaScriptForEachItem(
        JintEngine engine,
        string code,
        JsValue jsInput,
        List<string> logs)
    {
        var items = jsInput.IsArray()
            ? jsInput.AsArray()
            : null;

        var results = new List<object?>();

        if (items is not null)
        {
            var length = (int)items.Length;
            for (var i = 0; i < length; i++)
            {
                engine.SetValue("currentItem", items.Get((uint)i));
                engine.SetValue("itemIndex", i);

                var wrappedCode = $"(function() {{ {code} }})()";
                var result = engine.Evaluate(wrappedCode);
                results.Add(ConvertJsValue(result, engine));
            }
        }
        else
        {
            engine.SetValue("currentItem", jsInput);
            engine.SetValue("itemIndex", 0);

            var wrappedCode = $"(function() {{ {code} }})()";
            var result = engine.Evaluate(wrappedCode);
            results.Add(ConvertJsValue(result, engine));
        }

        var outputData = new Dictionary<string, object?>
        {
            ["result"] = results,
            ["logs"] = logs
        };

        return SuccessOutput(outputData);
    }

    private static NodeOutput BuildJavaScriptOutput(JsValue result, JintEngine engine, List<string> logs)
    {
        var outputData = new Dictionary<string, object?>
        {
            ["result"] = ConvertJsValue(result, engine),
            ["logs"] = logs
        };

        return SuccessOutput(outputData);
    }

    private static object? ConvertJsValue(JsValue value, JintEngine engine)
    {
        if (value is null || value.IsNull() || value.IsUndefined())
            return null;

        if (value.IsBoolean())
            return value.AsBoolean();

        if (value.IsNumber())
            return value.AsNumber();

        if (value.IsString())
            return value.AsString();

        // For objects/arrays, serialize to JSON then deserialize to get .NET types
        var serializer = new Jint.Native.Json.JsonSerializer(engine);
        var json = serializer.Serialize(value, JsValue.Undefined, JsValue.Undefined);
        if (json is null || json.IsNull() || json.IsUndefined())
            return null;

        var jsonString = json.AsString();
        return System.Text.Json.JsonSerializer.Deserialize<object>(jsonString);
    }

    #endregion
}
