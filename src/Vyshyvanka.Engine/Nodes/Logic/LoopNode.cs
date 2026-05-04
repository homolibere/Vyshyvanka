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
            // Empty array — emit on the "done" port with summary data
            context.NodeOutputs.Set(Id, "done", JsonSerializer.SerializeToElement(new
            {
                items = Array.Empty<object>(),
                totalCount = 0,
                processedCount = 0,
                isComplete = true
            }));

            return Task.FromResult(SuccessOutput(new
                { items = Array.Empty<object>(), totalCount = 0, isComplete = true, outputPort = "done" }));
        }

        // Build per-item metadata
        var loopItems = new List<object>();
        for (int i = 0; i < items.Count; i++)
        {
            loopItems.Add(new
            {
                index = i,
                item = JsonSerializer.Deserialize<object>(items[i].GetRawText()),
                isFirst = i == 0,
                isLast = i == items.Count - 1
            });
        }

        // Store the full items array on the "done" port for downstream summary use
        context.NodeOutputs.Set(Id, "done", JsonSerializer.SerializeToElement(new
        {
            items = loopItems,
            totalCount = items.Count,
            processedCount = items.Count,
            batchSize,
            isComplete = true
        }));

        // The "item" port carries the first item directly so downstream nodes
        // connected to it receive usable data in the current single-pass model.
        // Return the first item as the node output routed to the "item" port.
        var firstItem = JsonSerializer.Deserialize<object>(items[0].GetRawText());
        return Task.FromResult(SuccessOutput(new
        {
            index = 0,
            item = firstItem,
            isFirst = true,
            isLast = items.Count == 1,
            totalCount = items.Count,
            outputPort = "item"
        }));
    }
}
