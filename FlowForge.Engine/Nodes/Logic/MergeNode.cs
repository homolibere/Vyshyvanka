using System.Text.Json;
using FlowForge.Core.Interfaces;
using FlowForge.Engine.Nodes.Base;
using FlowForge.Engine.Registry;

namespace FlowForge.Engine.Nodes.Logic;

/// <summary>
/// A logic node that merges data from multiple input branches.
/// </summary>
[NodeDefinition(
    Name = "Merge",
    Description = "Merge data from multiple input branches into a single output",
    Icon = "git-merge")]
[NodeInput("input1", DisplayName = "Input 1")]
[NodeInput("input2", DisplayName = "Input 2")]
[NodeOutput("output", DisplayName = "Merged Output")]
[ConfigurationProperty("mode", "string", Description = "Merge mode: 'waitAll', 'passThrough', or 'combine'")]
[ConfigurationProperty("combineMode", "string", Description = "How to combine: 'array', 'object', or 'append'")]
public class MergeNode : BaseLogicNode
{
    private string _id = Guid.NewGuid().ToString();
    private readonly List<JsonElement> _pendingInputs = [];
    private readonly object _lock = new();

    /// <inheritdoc />
    public override string Id => _id;

    /// <inheritdoc />
    public override string Type => "merge";

    /// <inheritdoc />
    public override Task<NodeOutput> ExecuteAsync(NodeInput input, IExecutionContext context)
    {
        var mode = GetConfigValue<string>(input, "mode") ?? "passThrough";
        var combineMode = GetConfigValue<string>(input, "combineMode") ?? "array";

        return mode.ToLowerInvariant() switch
        {
            "waitall" => ExecuteWaitAllAsync(input, combineMode),
            "combine" => ExecuteCombineAsync(input, combineMode),
            _ => ExecutePassThroughAsync(input)
        };
    }

    private Task<NodeOutput> ExecutePassThroughAsync(NodeInput input)
    {
        // Simply pass through the input data
        return Task.FromResult(SuccessOutput(input.Data));
    }

    private Task<NodeOutput> ExecuteWaitAllAsync(NodeInput input, string combineMode)
    {
        lock (_lock)
        {
            _pendingInputs.Add(input.Data);
            
            // For waitAll, we need to wait for all inputs
            // This is a simplified implementation - in production, 
            // the workflow engine would handle multi-input coordination
            var expectedInputs = GetConfigValue<int?>(input, "expectedInputs") ?? 2;
            
            if (_pendingInputs.Count >= expectedInputs)
            {
                var merged = CombineInputs(_pendingInputs, combineMode);
                _pendingInputs.Clear();
                return Task.FromResult(SuccessOutput(merged));
            }
        }

        // Return a pending status - workflow engine should handle this
        return Task.FromResult(new NodeOutput
        {
            Data = JsonSerializer.SerializeToElement(new { status = "waiting", received = _pendingInputs.Count }),
            Success = true
        });
    }

    private Task<NodeOutput> ExecuteCombineAsync(NodeInput input, string combineMode)
    {
        lock (_lock)
        {
            _pendingInputs.Add(input.Data);
            var merged = CombineInputs(_pendingInputs, combineMode);
            return Task.FromResult(SuccessOutput(merged));
        }
    }

    private static object CombineInputs(List<JsonElement> inputs, string combineMode)
    {
        return combineMode.ToLowerInvariant() switch
        {
            "array" => inputs.Select(i => JsonSerializer.Deserialize<object>(i.GetRawText())).ToList(),
            "object" => CombineAsObject(inputs),
            "append" => CombineAsAppend(inputs),
            _ => inputs.Select(i => JsonSerializer.Deserialize<object>(i.GetRawText())).ToList()
        };
    }

    private static Dictionary<string, object?> CombineAsObject(List<JsonElement> inputs)
    {
        var result = new Dictionary<string, object?>();
        
        for (int i = 0; i < inputs.Count; i++)
        {
            var input = inputs[i];
            if (input.ValueKind == JsonValueKind.Object)
            {
                foreach (var prop in input.EnumerateObject())
                {
                    result[prop.Name] = JsonSerializer.Deserialize<object>(prop.Value.GetRawText());
                }
            }
            else
            {
                result[$"input{i + 1}"] = JsonSerializer.Deserialize<object>(input.GetRawText());
            }
        }
        
        return result;
    }

    private static List<object?> CombineAsAppend(List<JsonElement> inputs)
    {
        var result = new List<object?>();
        
        foreach (var input in inputs)
        {
            if (input.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in input.EnumerateArray())
                {
                    result.Add(JsonSerializer.Deserialize<object>(item.GetRawText()));
                }
            }
            else
            {
                result.Add(JsonSerializer.Deserialize<object>(input.GetRawText()));
            }
        }
        
        return result;
    }
}
