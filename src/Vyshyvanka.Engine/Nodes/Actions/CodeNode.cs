using System.Text.Json;
using Jint;
using Jint.Native;
using Jsonata.Net.Native;
using Microsoft.Extensions.Logging;
using Vyshyvanka.Core.Attributes;
using Vyshyvanka.Core.Interfaces;
using Vyshyvanka.Engine.Nodes.Base;
using JintEngine = Jint.Engine;

namespace Vyshyvanka.Engine.Nodes.Actions;

/// <summary>
/// An action node that executes user-provided code to transform data.
/// Supports two languages:
/// - JavaScript (Jint) — general-purpose scripting in a secure sandbox
/// - JSONata — a declarative expression language purpose-built for JSON transformation
/// Both runtimes are inherently safe with no access to the host system.
/// </summary>
[NodeDefinition(
    Name = "Code",
    Description = "Execute custom code to transform data, implement logic, or perform calculations",
    Icon = "fa-solid fa-code")]
[NodeInput("input", DisplayName = "Input", IsRequired = false)]
[NodeOutput("output", DisplayName = "Output")]
[ConfigurationProperty("language", "string", Description = "Programming language", IsRequired = false,
    Options = "javascript,jsonata")]
[ConfigurationProperty("code", "code", Description = "Code to execute. Return a value to pass it to the output.",
    IsRequired = true)]
[ConfigurationProperty("mode", "string", Description = "Execution mode (JavaScript only)", IsRequired = false,
    Options = "runOnce,runForEachItem")]
[ConfigurationProperty("timeout", "number", Description = "Execution timeout in seconds (default: 30)")]
public class CodeNode : BaseActionNode
{
    private readonly string _id = Guid.NewGuid().ToString();

    /// <inheritdoc />
    public override string Id => _id;

    /// <inheritdoc />
    public override string Type => "code";

    /// <inheritdoc />
    public override Task<NodeOutput> ExecuteAsync(NodeInput input, IExecutionContext context)
    {
        try
        {
            var language = GetConfigValue<string>(input, "language") ?? "javascript";
            var code = GetRequiredConfigValue<string>(input, "code");
            var mode = GetConfigValue<string>(input, "mode") ?? "runOnce";
            var timeoutSeconds = GetConfigValue<int?>(input, "timeout") ?? 30;

            if (string.IsNullOrWhiteSpace(code))
            {
                return Task.FromResult(FailureOutput("Code cannot be empty"));
            }

            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(context.CancellationToken);
            timeoutCts.CancelAfter(TimeSpan.FromSeconds(timeoutSeconds));

            var result = language.ToLowerInvariant() switch
            {
                "jsonata" => ExecuteJsonata(code, mode, input),
                _ => ExecuteJavaScript(code, mode, input, context, timeoutCts.Token)
            };

            return Task.FromResult(result);
        }
        catch (Jint.Runtime.JavaScriptException ex)
        {
            return Task.FromResult(FailureOutput($"JavaScript error: {ex.Message}"));
        }
        catch (JsonataException ex)
        {
            return Task.FromResult(FailureOutput($"JSONata error: {ex.Message}"));
        }
        catch (OperationCanceledException) when (!context.CancellationToken.IsCancellationRequested)
        {
            return Task.FromResult(FailureOutput("Code execution timed out"));
        }
        catch (OperationCanceledException)
        {
            return Task.FromResult(FailureOutput("Code execution was cancelled"));
        }
        catch (Exception ex)
        {
            return Task.FromResult(FailureOutput($"Code execution error: {ex.Message}"));
        }
    }

    #region JSONata Execution

    private static NodeOutput ExecuteJsonata(string expression, string mode, NodeInput input)
    {
        if (mode == "runForEachItem")
        {
            return ExecuteJsonataForEachItem(expression, input);
        }

        return ExecuteJsonataOnce(expression, input);
    }

    private static NodeOutput ExecuteJsonataOnce(string expression, NodeInput input)
    {
        var query = new JsonataQuery(expression);

        var inputJson = input.Data.ValueKind != JsonValueKind.Undefined
            ? input.Data.GetRawText()
            : "null";

        var resultJson = query.Eval(inputJson);

        object? result = resultJson != "undefined"
            ? JsonSerializer.Deserialize<object>(resultJson)
            : null;

        var outputData = new Dictionary<string, object?>
        {
            ["result"] = result
        };

        return SuccessOutput(outputData);
    }

    private static NodeOutput ExecuteJsonataForEachItem(string expression, NodeInput input)
    {
        var query = new JsonataQuery(expression);

        var inputJson = input.Data.ValueKind != JsonValueKind.Undefined
            ? input.Data.GetRawText()
            : "null";

        // Parse input to check if it's an array
        using var doc = JsonDocument.Parse(inputJson);
        var results = new List<object?>();

        if (doc.RootElement.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in doc.RootElement.EnumerateArray())
            {
                var itemJson = item.GetRawText();
                var resultJson = query.Eval(itemJson);
                results.Add(resultJson != "undefined"
                    ? JsonSerializer.Deserialize<object>(resultJson)
                    : null);
            }
        }
        else
        {
            // Single item — evaluate once
            var resultJson = query.Eval(inputJson);
            results.Add(resultJson != "undefined"
                ? JsonSerializer.Deserialize<object>(resultJson)
                : null);
        }

        var outputData = new Dictionary<string, object?>
        {
            ["result"] = results
        };

        return SuccessOutput(outputData);
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
        return JsonSerializer.Deserialize<object>(jsonString);
    }

    #endregion
}
