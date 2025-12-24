using System.Text.Json;
using FlowForge.Core.Interfaces;
using FlowForge.Engine.Nodes.Base;
using FlowForge.Engine.Registry;

namespace FlowForge.Engine.Nodes.Triggers;

/// <summary>
/// A trigger node that is manually activated by a user or API call.
/// </summary>
[NodeDefinition(
    Name = "Manual Trigger",
    Description = "Manually trigger a workflow execution",
    Icon = "play")]
[NodeOutput("output", DisplayName = "Output")]
public class ManualTriggerNode : BaseTriggerNode
{
    private string _id = Guid.NewGuid().ToString();

    /// <inheritdoc />
    public override string Id => _id;

    /// <inheritdoc />
    public override string Type => "manual-trigger";

    /// <inheritdoc />
    public override Task<bool> ShouldTriggerAsync(TriggerContext context)
    {
        // Manual triggers always return true when invoked
        // The actual triggering is controlled by the execution service
        return Task.FromResult(true);
    }

    /// <inheritdoc />
    public override Task<NodeOutput> ExecuteAsync(NodeInput input, IExecutionContext context)
    {
        // Pass through any input data provided during manual execution
        var outputData = input.Data.ValueKind != JsonValueKind.Undefined 
            ? input.Data 
            : JsonSerializer.SerializeToElement(new { triggered = true, timestamp = DateTime.UtcNow });
        
        return Task.FromResult(SuccessOutput(outputData));
    }
}
