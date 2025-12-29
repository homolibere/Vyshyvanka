using System.Text.Json;
using FlowForge.Core.Interfaces;
using FlowForge.Engine.Nodes.Base;
using FlowForge.Engine.Registry;

namespace FlowForge.Engine.Nodes.Logic;

/// <summary>
/// A logic node that iterates over an array and executes downstream nodes for each item.
/// </summary>
[NodeDefinition(
    Name = "Loop",
    Description = "Iterate over an array and process each item",
    Icon = "fa-solid fa-rotate")]
[NodeInput("input", DisplayName = "Input Array", IsRequired = true)]
[NodeOutput("item", DisplayName = "Current Item")]
[NodeOutput("done", DisplayName = "Loop Complete")]
[ConfigurationProperty("field", "string", Description = "Field path to the array to iterate")]
[ConfigurationProperty("batchSize", "number", Description = "Number of items to process in parallel")]
public class LoopNode : BaseLogicNode
{
    private string _id = Guid.NewGuid().ToString();

    /// <inheritdoc />
    public override string Id => _id;

    /// <inheritdoc />
    public override string Type => "loop";

    /// <inheritdoc />
    public override Task<NodeOutput> ExecuteAsync(NodeInput input, IExecutionContext context)
    {
        var field = GetConfigValue<string>(input, "field");
        var batchSize = GetConfigValue<int?>(input, "batchSize") ?? 1;

        // Get the array to iterate
        JsonElement arrayElement;
        if (!string.IsNullOrEmpty(field))
        {
            arrayElement = GetNestedProperty(input.Data, field);
        }
        else
        {
            arrayElement = input.Data;
        }

        if (arrayElement.ValueKind != JsonValueKind.Array)
        {
            return Task.FromResult(FailureOutput($"Expected array but got {arrayElement.ValueKind}"));
        }

        var items = arrayElement.EnumerateArray().ToList();

        if (items.Count == 0)
        {
            // Empty array - go directly to done output
            var emptyResult = new LoopOutput
            {
                Items = [],
                TotalCount = 0,
                ProcessedCount = 0,
                IsComplete = true,
                OutputPort = "done"
            };
            return Task.FromResult(SuccessOutput(emptyResult));
        }

        // Process items and return loop output
        var loopItems = new List<LoopItem>();
        for (int i = 0; i < items.Count; i++)
        {
            loopItems.Add(new LoopItem
            {
                Index = i,
                Item = JsonSerializer.Deserialize<object>(items[i].GetRawText()),
                IsFirst = i == 0,
                IsLast = i == items.Count - 1
            });
        }

        var result = new LoopOutput
        {
            Items = loopItems,
            TotalCount = items.Count,
            ProcessedCount = items.Count,
            BatchSize = batchSize,
            IsComplete = true,
            OutputPort = "item"
        };

        return Task.FromResult(SuccessOutput(result));
    }
}

/// <summary>
/// Output from a loop node containing iteration data.
/// </summary>
public record LoopOutput
{
    /// <summary>Items to iterate over.</summary>
    public List<LoopItem> Items { get; init; } = [];

    /// <summary>Total number of items.</summary>
    public int TotalCount { get; init; }

    /// <summary>Number of items processed.</summary>
    public int ProcessedCount { get; init; }

    /// <summary>Batch size for parallel processing.</summary>
    public int BatchSize { get; init; } = 1;

    /// <summary>Whether the loop is complete.</summary>
    public bool IsComplete { get; init; }

    /// <summary>Output port to use.</summary>
    public string OutputPort { get; init; } = "item";
}

/// <summary>
/// Represents a single item in a loop iteration.
/// </summary>
public record LoopItem
{
    /// <summary>Zero-based index of the item.</summary>
    public int Index { get; init; }

    /// <summary>The item data.</summary>
    public object? Item { get; init; }

    /// <summary>Whether this is the first item.</summary>
    public bool IsFirst { get; init; }

    /// <summary>Whether this is the last item.</summary>
    public bool IsLast { get; init; }
}
