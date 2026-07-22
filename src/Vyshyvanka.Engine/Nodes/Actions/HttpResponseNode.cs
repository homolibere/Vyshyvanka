using System.Text.Json;
using Vyshyvanka.Core.Attributes;
using Vyshyvanka.Core.Enums;
using Vyshyvanka.Core.Interfaces;
using Vyshyvanka.Engine.Nodes.Base;

namespace Vyshyvanka.Engine.Nodes.Actions;

/// <summary>
/// Returns a synchronous HTTP response to the webhook caller.
/// Can only be used in workflows triggered by a Webhook Trigger with responseMode set to "lastNode".
/// Only one HTTP Response node may fire per workflow execution.
/// </summary>
[NodeDefinition(
    Name = "HTTP Response",
    Description = "Return a synchronous HTTP response to the webhook trigger caller",
    Icon = "fa-solid fa-reply")]
[NodeInput("input", DisplayName = "Input")]
[NodeOutput("output", DisplayName = "Output", Type = PortType.Object)]
[ConfigurationProperty("statusCode", "number", Description = "HTTP status code to return (default: 200)")]
[ConfigurationProperty("contentType", "string", Description = "Content-Type header value",
    Options = "application/json,text/plain,text/html,application/xml")]
[ConfigurationProperty("headers", "object", Description = "Additional response headers as key-value pairs")]
[ConfigurationProperty("body", "string", Description = "Response body. If omitted, the input data is serialized as JSON. Supports expressions.")]
public class HttpResponseNode : BaseActionNode
{
    private readonly string _id = Guid.NewGuid().ToString();

    /// <inheritdoc />
    public override string Id => _id;

    /// <inheritdoc />
    public override string Type => "http-response";

    /// <inheritdoc />
    public override async Task<NodeOutput> ExecuteAsync(NodeInput input, IExecutionContext context)
    {
        try
        {
            var webhookResponse = context.WebhookResponse;
            if (webhookResponse is null)
            {
                return FailureOutput(
                    "HTTP Response node can only be used in workflows triggered by a Webhook Trigger with responseMode \"lastNode\".");
            }

            if (webhookResponse.IsResponseSent)
            {
                return FailureOutput(
                    "A response has already been sent. Only one HTTP Response node can fire per execution.");
            }

            var statusCode = GetConfigValue<int?>(input, "statusCode") ?? 200;
            var contentType = GetConfigValue<string>(input, "contentType") ?? "application/json";
            var configuredHeaders = GetConfigValue<Dictionary<string, string>>(input, "headers");
            var body = GetConfigValue<string>(input, "body");

            // If no explicit body configured, serialize the input data as JSON
            if (body is null && input.Data.ValueKind is not JsonValueKind.Undefined and not JsonValueKind.Null)
            {
                body = input.Data.GetRawText();
            }

            // Build final headers
            var headers = new Dictionary<string, string>
            {
                ["Content-Type"] = contentType
            };

            if (configuredHeaders is not null)
            {
                foreach (var (key, value) in configuredHeaders)
                {
                    headers[key] = value;
                }
            }

            // Write the response
            await webhookResponse.WriteAsync(statusCode, headers, body, context.CancellationToken);

            var result = new Dictionary<string, object?>
            {
                ["responseSent"] = true,
                ["statusCode"] = statusCode,
                ["bodyLength"] = body?.Length ?? 0
            };

            return SuccessOutput(result);
        }
        catch (InvalidOperationException ex)
        {
            return FailureOutput(ex.Message);
        }
        catch (OperationCanceledException)
        {
            return FailureOutput("Response write was cancelled");
        }
        catch (Exception ex)
        {
            return FailureOutput($"Failed to write HTTP response: {ex.Message}");
        }
    }
}
