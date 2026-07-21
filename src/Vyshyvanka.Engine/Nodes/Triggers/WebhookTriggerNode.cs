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
        // The webhook controller stores request data in context.Variables["webhook"].
        // Trigger nodes have no incoming connections, so input.Data is empty.
        var source = GetWebhookDataSource(input, context);

        var webhookData = new Dictionary<string, object?>
        {
            ["headers"] = ExtractProperty(source, "headers"),
            ["body"] = ExtractProperty(source, "body"),
            ["query"] = ExtractProperty(source, "query"),
            ["method"] = ExtractProperty(source, "method"),
            ["path"] = ExtractProperty(source, "path"),
            ["timestamp"] = DateTime.UtcNow
        };

        return Task.FromResult(SuccessOutput(webhookData));
    }

    /// <summary>
    /// Resolves the webhook data source — prefers context.Variables["webhook"] (set by
    /// WebhookController), falls back to input.Data for backwards compatibility.
    /// </summary>
    private static JsonElement GetWebhookDataSource(NodeInput input, IExecutionContext context)
    {
        if (context.Variables.TryGetValue("webhook", out var webhookVar) &&
            webhookVar is JsonElement webhookElement &&
            webhookElement.ValueKind == JsonValueKind.Object)
        {
            return webhookElement;
        }

        return input.Data;
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
