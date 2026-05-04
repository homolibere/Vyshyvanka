using System.Text.Json;
using Vyshyvanka.Core.Interfaces;
using Vyshyvanka.Engine.Nodes.Base;
using Vyshyvanka.Core.Attributes;

namespace Vyshyvanka.Engine.Nodes.Logic;

/// <summary>
/// A logic node that iterates over an array and executes downstream nodes for each item.
/// </summary>
[NodeDefinition(
    Name = "Loop",
    Description = "Iterate over an array and process each item",
    Icon = "fa-solid fa-rotate")]
[NodeInput("input", DisplayName = "Input Array", IsRequired = true)]
[NodeOutput("item", DisplayName = "Output")]
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

        var items = arrayElement.EnumerateArray().Select(e => e.Clone()).ToList();

        if (items.Count == 0)
        {
            return Task.FromResult(SuccessOutput(new
                { items = Array.Empty<object>(), totalCount = 0, isComplete = true, outputPort = "done" }));
        }

        // Return the items array wrapped in metadata. The engine detects the
        // __loopItems marker and handles per-item iteration of downstream nodes.
        return Task.FromResult(SuccessOutput(new
        {
            __loopItems = items.Select((e, i) => JsonSerializer.Deserialize<object>(e.GetRawText())).ToList(),
            totalCount = items.Count,
            outputPort = "item"
        }));
    }
}
