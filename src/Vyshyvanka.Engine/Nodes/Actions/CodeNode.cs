using System.Collections.Concurrent;
using System.Reflection;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.CodeAnalysis.CSharp.Scripting;
using Microsoft.CodeAnalysis.Scripting;
using Vyshyvanka.Core.Attributes;
using Vyshyvanka.Core.Interfaces;
using Vyshyvanka.Engine.Nodes.Base;

namespace Vyshyvanka.Engine.Nodes.Actions;

/// <summary>
/// An action node that executes user-provided C# code using Roslyn scripting.
/// Supports two execution modes: run once for all input, or run once per item.
/// </summary>
[NodeDefinition(
    Name = "Code",
    Description = "Execute custom C# code to transform data, implement logic, or perform calculations",
    Icon = "fa-solid fa-code")]
[NodeInput("input", DisplayName = "Input", IsRequired = false)]
[NodeOutput("output", DisplayName = "Output")]
[ConfigurationProperty("code", "code", Description = "C# code to execute. Return a value to pass it to the output.",
    IsRequired = true)]
[ConfigurationProperty("mode", "string", Description = "Execution mode", IsRequired = false,
    Options = "runOnce,runForEachItem")]
[ConfigurationProperty("timeout", "number", Description = "Execution timeout in seconds (default: 30)")]
public class CodeNode : BaseActionNode
{
    private static readonly ConcurrentDictionary<string, Script<object>> ScriptCache = new();

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
            var code = GetRequiredConfigValue<string>(input, "code");
            var mode = GetConfigValue<string>(input, "mode") ?? "runOnce";
            var timeoutSeconds = GetConfigValue<int?>(input, "timeout") ?? 30;

            if (string.IsNullOrWhiteSpace(code))
            {
                return FailureOutput("Code cannot be empty");
            }

            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(context.CancellationToken);
            timeoutCts.CancelAfter(TimeSpan.FromSeconds(timeoutSeconds));

            return mode switch
            {
                "runForEachItem" => await ExecuteForEachItemAsync(code, input, context, timeoutCts.Token),
                _ => await ExecuteOnceAsync(code, input, context, timeoutCts.Token)
            };
        }
        catch (CompilationErrorException ex)
        {
            var errors = string.Join(Environment.NewLine, ex.Diagnostics.Select(d => d.ToString()));
            return FailureOutput($"Compilation error:{Environment.NewLine}{errors}");
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

    private async Task<NodeOutput> ExecuteOnceAsync(
        string code,
        NodeInput input,
        IExecutionContext context,
        CancellationToken ct)
    {
        var globals = new CodeNodeGlobals(
            input.Data,
            context.ExecutionId,
            context.WorkflowId,
            context.Logger);

        var result = await RunScriptAsync(code, globals, ct);

        return BuildOutput(result, globals);
    }

    private async Task<NodeOutput> ExecuteForEachItemAsync(
        string code,
        NodeInput input,
        IExecutionContext context,
        CancellationToken ct)
    {
        var globals = new CodeNodeGlobals(
            input.Data,
            context.ExecutionId,
            context.WorkflowId,
            context.Logger);

        var items = globals.GetItems();
        var results = new List<object?>(items.Length);

        for (var i = 0; i < items.Length; i++)
        {
            ct.ThrowIfCancellationRequested();

            globals.CurrentItem = items[i];
            globals.ItemIndex = i;

            var result = await RunScriptAsync(code, globals, ct);
            results.Add(result);
        }

        return BuildOutput(results, globals);
    }

    private static async Task<object?> RunScriptAsync(
        string code,
        CodeNodeGlobals globals,
        CancellationToken ct)
    {
        var script = GetOrCreateScript(code);
        var state = await script.RunAsync(globals, cancellationToken: ct);

        if (state.Exception is not null)
        {
            throw state.Exception;
        }

        return state.ReturnValue;
    }

    private static Script<object> GetOrCreateScript(string code)
    {
        return ScriptCache.GetOrAdd(code, static c =>
            CSharpScript.Create<object>(c, DefaultScriptOptions, typeof(CodeNodeGlobals)));
    }

    private static NodeOutput BuildOutput(object? result, CodeNodeGlobals globals)
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
                typeof(object).Assembly, // System.Runtime
                typeof(Enumerable).Assembly, // System.Linq
                typeof(JsonElement).Assembly, // System.Text.Json
                typeof(JsonSerializer).Assembly, // System.Text.Json
                typeof(List<>).Assembly, // System.Collections
                typeof(Dictionary<,>).Assembly, // System.Collections.Generic
                typeof(Math).Assembly, // System.Runtime.Extensions
                typeof(Convert).Assembly, // System.Convert
                typeof(Guid).Assembly, // System.Runtime
                typeof(DateTime).Assembly, // System.Runtime
                typeof(TimeSpan).Assembly, // System.Runtime
                typeof(Regex).Assembly, // System.Text.RegularExpressions
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
}
