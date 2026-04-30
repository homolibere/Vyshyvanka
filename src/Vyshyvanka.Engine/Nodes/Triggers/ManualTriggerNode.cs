using System.Text.Json;
using Vyshyvanka.Core.Interfaces;
using Vyshyvanka.Engine.Nodes.Base;
using Vyshyvanka.Core.Attributes;

namespace Vyshyvanka.Engine.Nodes.Triggers;

/// <summary>
/// A trigger node that is manually activated by a user or API call.
/// When configured with test data, outputs that data. Otherwise passes through
/// any data provided at trigger time, or a default payload.
/// </summary>
[NodeDefinition(
    Name = "Manual Trigger",
    Description = "Manually trigger a workflow execution",
    Icon = "fa-solid fa-play")]
[NodeOutput("output", DisplayName = "Output")]
[ConfigurationProperty("testData", "object",
    Description = "Custom JSON payload to output when triggered. Leave empty to pass through trigger data.")]
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
        return Task.FromResult(true);
    }

    /// <inheritdoc />
    public override Task<NodeOutput> ExecuteAsync(NodeInput input, IExecutionContext context)
    {
        // Priority: runtime trigger data > configured test data > default payload
        if (input.Data.ValueKind is not JsonValueKind.Undefined and not JsonValueKind.Null)
        {
            return Task.FromResult(SuccessOutput(input.Data));
        }

        var testData = GetConfigValue<JsonElement?>(input, "testData");
        if (testData is { ValueKind: not JsonValueKind.Undefined and not JsonValueKind.Null })
        {
            return Task.FromResult(SuccessOutput(testData.Value));
        }

        var defaultPayload = JsonSerializer.SerializeToElement(new
        {
            triggered = true,
            timestamp = DateTime.UtcNow
        });

        return Task.FromResult(SuccessOutput(defaultPayload));
    }
}
