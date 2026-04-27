using System.Text.Json;
using FlowForge.Core.Enums;
using FlowForge.Core.Interfaces;

namespace FlowForge.Engine.Nodes.Base;

/// <summary>
/// Abstract base class for trigger nodes that initiate workflow execution.
/// </summary>
public abstract class BaseTriggerNode : BaseNode, ITriggerNode
{
    /// <inheritdoc />
    public override NodeCategory Category => NodeCategory.Trigger;

    /// <inheritdoc />
    public abstract Task<bool> ShouldTriggerAsync(TriggerContext context);

    /// <summary>
    /// Extracts trigger data from the context and returns it as node output.
    /// Default implementation passes through the trigger data.
    /// </summary>
    public override Task<NodeOutput> ExecuteAsync(NodeInput input, IExecutionContext context)
    {
        // By default, trigger nodes pass through their trigger data
        return Task.FromResult(SuccessOutput(input.Data));
    }

    /// <summary>
    /// Gets a value from the trigger context data.
    /// </summary>
    protected static T? GetTriggerValue<T>(TriggerContext context, string key)
    {
        if (context.TriggerData.ValueKind == JsonValueKind.Undefined ||
            context.TriggerData.ValueKind == JsonValueKind.Null)
        {
            return default;
        }

        if (context.TriggerData.TryGetProperty(key, out var value))
        {
            return JsonSerializer.Deserialize<T>(value.GetRawText());
        }

        return default;
    }
}
