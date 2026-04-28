using System.Text.Json;
using Vyshyvanka.Core.Attributes;
using Vyshyvanka.Core.Enums;
using Vyshyvanka.Core.Interfaces;

namespace Vyshyvanka.Plugin.GitLab.Nodes;

/// <summary>
/// Trigger node that fires when a GitLab webhook event is received.
/// Supports filtering by event type (push, merge request, issue, pipeline, tag, etc.).
/// </summary>
/// <remarks>
/// GitLab sends a <c>X-Gitlab-Event</c> header identifying the event type.
/// The webhook payload is passed through as the trigger output, with the event
/// type and project info extracted into a structured envelope.
/// </remarks>
[NodeDefinition(
    Name = "GitLab Webhook Trigger",
    Description = "Trigger a workflow when a GitLab webhook event is received (push, merge request, issue, pipeline, tag, etc.)",
    Icon = "fa-brands fa-gitlab")]
[NodeOutput("output", DisplayName = "Webhook Payload", Type = PortType.Object)]
[ConfigurationProperty("events", "string",
    Description = "Comma-separated event types to accept (e.g. push,merge_request,pipeline). Leave empty to accept all. " +
                  "Values: push, tag_push, issue, note, merge_request, wiki_page, pipeline, build, deployment, " +
                  "release, member, subgroup, feature_flag, emoji")]
[ConfigurationProperty("secretToken", "string",
    Description = "Secret token to validate the X-Gitlab-Token header. Leave empty to skip validation.")]
[ConfigurationProperty("projectFilter", "string",
    Description = "Optional project path or ID to filter events for (e.g. namespace/my-project).")]
public class GitLabWebhookTriggerNode : ITriggerNode
{
    private readonly string _id = Guid.NewGuid().ToString();

    public string Id => _id;
    public string Type => "gitlab-webhook-trigger";
    public NodeCategory Category => NodeCategory.Trigger;

    /// <summary>
    /// Determines whether the incoming webhook payload is a GitLab event
    /// that matches the configured filters.
    /// </summary>
    public Task<bool> ShouldTriggerAsync(TriggerContext context)
    {
        if (context.TriggerData.ValueKind is JsonValueKind.Undefined or JsonValueKind.Null)
            return Task.FromResult(false);

        // Must be a webhook trigger
        var triggerType = GetTriggerValue<string>(context, "triggerType");
        if (triggerType != "webhook")
            return Task.FromResult(false);

        // Check for GitLab event header
        var headers = GetTriggerValue<JsonElement?>(context, "headers");
        if (headers is null || headers.Value.ValueKind != JsonValueKind.Object)
            return Task.FromResult(false);

        var hasGitLabHeader = headers.Value.TryGetProperty("X-Gitlab-Event", out _)
                           || headers.Value.TryGetProperty("x-gitlab-event", out _);

        return Task.FromResult(hasGitLabHeader);
    }

    /// <summary>
    /// Extracts the GitLab event payload, validates the secret token and event filters,
    /// and passes a structured envelope downstream.
    /// </summary>
    public Task<NodeOutput> ExecuteAsync(NodeInput input, IExecutionContext context)
    {
        try
        {
            var headers = ExtractObject(input.Data, "headers");
            var body = ExtractObject(input.Data, "body");

            // ── Secret token validation ─────────────────────────────
            var expectedSecret = GetConfigValue<string>(input, "secretToken");
            if (!string.IsNullOrWhiteSpace(expectedSecret))
            {
                var receivedToken = GetHeader(headers, "X-Gitlab-Token");
                if (receivedToken != expectedSecret)
                    return Task.FromResult(Failure("Secret token mismatch — webhook rejected."));
            }

            // ── Event type extraction & filtering ───────────────────
            var gitlabEvent = GetHeader(headers, "X-Gitlab-Event") ?? "unknown";
            var eventKey = NormalizeEventName(gitlabEvent);

            var allowedEvents = GetConfigValue<string>(input, "events");
            if (!string.IsNullOrWhiteSpace(allowedEvents))
            {
                var allowed = allowedEvents
                    .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                    .Select(e => e.ToLowerInvariant());

                if (!allowed.Contains(eventKey))
                    return Task.FromResult(Failure($"Event '{eventKey}' not in allowed list."));
            }

            // ── Project filter ──────────────────────────────────────
            var projectFilter = GetConfigValue<string>(input, "projectFilter");
            if (!string.IsNullOrWhiteSpace(projectFilter) && body.ValueKind == JsonValueKind.Object)
            {
                var projectPath = body.TryGetProperty("project", out var proj)
                    && proj.TryGetProperty("path_with_namespace", out var pwn)
                        ? pwn.GetString()
                        : null;

                var projectId = proj.ValueKind == JsonValueKind.Object
                    && proj.TryGetProperty("id", out var pid)
                        ? pid.ToString()
                        : null;

                if (projectPath != projectFilter && projectId != projectFilter)
                    return Task.FromResult(Failure($"Event project does not match filter '{projectFilter}'."));
            }

            // ── Build output envelope ───────────────────────────────
            var output = new Dictionary<string, object?>
            {
                ["event"] = eventKey,
                ["gitlabEvent"] = gitlabEvent,
                ["body"] = body.ValueKind != JsonValueKind.Undefined
                    ? JsonSerializer.Deserialize<object>(body.GetRawText()) : null,
                ["headers"] = headers.ValueKind != JsonValueKind.Undefined
                    ? JsonSerializer.Deserialize<object>(headers.GetRawText()) : null,
                ["timestamp"] = DateTime.UtcNow
            };

            return Task.FromResult(new NodeOutput
            {
                Data = JsonSerializer.SerializeToElement(output),
                Success = true
            });
        }
        catch (Exception ex)
        {
            return Task.FromResult(Failure($"GitLab Webhook Trigger error: {ex.Message}"));
        }
    }

    // ── Helpers ──────────────────────────────────────────────────────────

    private static NodeOutput Failure(string message) => new()
    {
        Data = default,
        Success = false,
        ErrorMessage = message
    };

    private static T? GetConfigValue<T>(NodeInput input, string key)
    {
        if (input.Configuration.ValueKind is JsonValueKind.Undefined or JsonValueKind.Null)
            return default;

        return input.Configuration.TryGetProperty(key, out var value)
            ? JsonSerializer.Deserialize<T>(value.GetRawText())
            : default;
    }

    private static T? GetTriggerValue<T>(TriggerContext context, string key)
    {
        if (context.TriggerData.ValueKind is JsonValueKind.Undefined or JsonValueKind.Null)
            return default;

        return context.TriggerData.TryGetProperty(key, out var value)
            ? JsonSerializer.Deserialize<T>(value.GetRawText())
            : default;
    }

    private static JsonElement ExtractObject(JsonElement element, string propertyName) =>
        element.ValueKind == JsonValueKind.Object && element.TryGetProperty(propertyName, out var value)
            ? value
            : default;

    private static string? GetHeader(JsonElement headers, string name)
    {
        if (headers.ValueKind != JsonValueKind.Object)
            return null;

        // Try exact case, then lowercase
        if (headers.TryGetProperty(name, out var val))
            return val.GetString();

        if (headers.TryGetProperty(name.ToLowerInvariant(), out val))
            return val.GetString();

        return null;
    }

    /// <summary>
    /// Normalizes "Push Hook" → "push", "Merge Request Hook" → "merge_request", etc.
    /// </summary>
    private static string NormalizeEventName(string gitlabEvent) =>
        gitlabEvent
            .Replace(" Hook", "", StringComparison.OrdinalIgnoreCase)
            .Replace(" Event", "", StringComparison.OrdinalIgnoreCase)
            .Trim()
            .ToLowerInvariant()
            .Replace(' ', '_');
}
