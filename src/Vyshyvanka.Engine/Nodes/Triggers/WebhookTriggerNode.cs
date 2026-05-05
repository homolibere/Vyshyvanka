using System.Text.Json;
using Vyshyvanka.Core.Interfaces;
using Vyshyvanka.Engine.Nodes.Base;
using Vyshyvanka.Core.Attributes;

namespace Vyshyvanka.Engine.Nodes.Triggers;

/// <summary>
/// A trigger node that activates when an HTTP webhook request is received.
/// </summary>
[NodeDefinition(
    Name = "Webhook Trigger",
    Description = "Trigger workflow when an HTTP request is received",
    Icon = "fa-solid fa-tower-broadcast")]
[NodeOutput("output", DisplayName = "Request Data")]
[ConfigurationProperty("path", "string", Description = "Webhook URL path", IsRequired = true)]
[ConfigurationProperty("method", "string", Description = "HTTP method to accept (GET, POST, PUT, DELETE, or ANY)",
    Options = "GET,POST,PUT,DELETE,ANY")]
[ConfigurationProperty("responseMode", "string", Description = "When to respond: 'immediate' or 'lastNode'",
    Options = "immediate,lastNode")]
public class WebhookTriggerNode : BaseTriggerNode
{
    private string _id = Guid.NewGuid().ToString();

    /// <inheritdoc />
    public override string Id => _id;

    /// <inheritdoc />
    public override string Type => "webhook-trigger";

    /// <inheritdoc />
    public override Task<bool> ShouldTriggerAsync(TriggerContext context)
    {
        // Check if the trigger context contains webhook data
        if (context.TriggerData.ValueKind == JsonValueKind.Undefined ||
            context.TriggerData.ValueKind == JsonValueKind.Null)
        {
            return Task.FromResult(false);
        }

        // Verify this is a webhook trigger
        var triggerType = GetTriggerValue<string>(context, "triggerType");
        return Task.FromResult(triggerType == "webhook");
    }

    /// <inheritdoc />
    public override Task<NodeOutput> ExecuteAsync(NodeInput input, IExecutionContext context)
    {
        // Extract webhook request data and pass it downstream
        var webhookData = new Dictionary<string, object?>
        {
            ["headers"] = ExtractProperty(input.Data, "headers"),
            ["body"] = ExtractProperty(input.Data, "body"),
            ["query"] = ExtractProperty(input.Data, "query"),
            ["method"] = ExtractProperty(input.Data, "method"),
            ["path"] = ExtractProperty(input.Data, "path"),
            ["timestamp"] = DateTime.UtcNow
        };

        return Task.FromResult(SuccessOutput(webhookData));
    }

    private static object? ExtractProperty(JsonElement element, string propertyName)
    {
        if (element.ValueKind != JsonValueKind.Object)
            return null;

        if (element.TryGetProperty(propertyName, out var value))
        {
            return value.ValueKind switch
            {
                JsonValueKind.String => value.GetString(),
                JsonValueKind.Number => value.GetDecimal(),
                JsonValueKind.True => true,
                JsonValueKind.False => false,
                JsonValueKind.Null => null,
                _ => JsonSerializer.Deserialize<object>(value.GetRawText())
            };
        }

        return null;
    }
}
