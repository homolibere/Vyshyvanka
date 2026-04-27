using System.Web;
using FlowForge.Core.Attributes;
using FlowForge.Core.Enums;
using FlowForge.Core.Interfaces;

namespace FlowForge.Plugin.GitLab.Nodes;

/// <summary>
/// Manages GitLab issues — create, get, edit, comment, and lock.
/// </summary>
[NodeDefinition(
    Name = "GitLab Issue",
    Description = "Create, get, edit, comment on, and lock GitLab issues",
    Icon = "fa-brands fa-gitlab")]
[NodeInput("input", DisplayName = "Input", Type = PortType.Object)]
[NodeOutput("output", DisplayName = "Output", Type = PortType.Object)]
[RequiresCredential(CredentialType.ApiKey)]
[ConfigurationProperty("operation", "string", Description = "Operation: create, get, edit, comment, lock", IsRequired = true)]
[ConfigurationProperty("projectId", "string", Description = "Project ID or URL-encoded path (e.g. namespace/project)", IsRequired = true)]
[ConfigurationProperty("issueIid", "number", Description = "Issue internal ID. Required for get, edit, comment, lock.")]
[ConfigurationProperty("title", "string", Description = "Issue title. Required for create.")]
[ConfigurationProperty("description", "string", Description = "Issue description (Markdown).")]
[ConfigurationProperty("assigneeIds", "array", Description = "Array of user IDs to assign.")]
[ConfigurationProperty("labels", "string", Description = "Comma-separated label names.")]
[ConfigurationProperty("milestoneId", "number", Description = "Milestone ID.")]
[ConfigurationProperty("confidential", "boolean", Description = "Whether the issue is confidential.")]
[ConfigurationProperty("stateEvent", "string", Description = "State event for edit: close or reopen.")]
[ConfigurationProperty("body", "string", Description = "Comment body (Markdown). Required for comment operation.")]
public class GitLabIssueNode : BaseGitLabNode
{
    public override string Type => "gitlab-issue";
    public override NodeCategory Category => NodeCategory.Action;

    public GitLabIssueNode() { }
    internal GitLabIssueNode(HttpClient? httpClient) : base(httpClient) { }

    public override async Task<NodeOutput> ExecuteAsync(NodeInput input, IExecutionContext context)
    {
        try
        {
            var operation = GetRequiredConfigValue<string>(input, "operation").ToLowerInvariant();
            var projectId = EncodeProject(GetRequiredConfigValue<string>(input, "projectId"));
            var (apiBase, token) = await ResolveCredentialsAsync(input, context);

            return operation switch
            {
                "create" => await CreateAsync(input, projectId, apiBase, token, context.CancellationToken),
                "get" => await GetAsync(input, projectId, apiBase, token, context.CancellationToken),
                "edit" => await EditAsync(input, projectId, apiBase, token, context.CancellationToken),
                "comment" => await CommentAsync(input, projectId, apiBase, token, context.CancellationToken),
                "lock" => await LockAsync(input, projectId, apiBase, token, context.CancellationToken),
                _ => FailureOutput($"Unsupported operation '{operation}'. Use: create, get, edit, comment, lock.")
            };
        }
        catch (Exception ex)
        {
            return FailureOutput($"GitLab Issue error: {ex.Message}");
        }
    }

    private async Task<NodeOutput> CreateAsync(
        NodeInput input, string projectId, string apiBase, string token, CancellationToken ct)
    {
        var body = new Dictionary<string, object>
        {
            ["title"] = GetRequiredConfigValue<string>(input, "title")
        };

        AddOptionalField(body, input, "description");
        AddOptionalField(body, input, "labels");
        AddOptionalField(body, input, "confidential");
        AddOptionalField(body, input, "milestoneId", "milestone_id");

        var assigneeIds = GetConfigValue<int[]>(input, "assigneeIds");
        if (assigneeIds is { Length: > 0 })
            body["assignee_ids"] = assigneeIds;

        var response = await SendGitLabRequestAsync(
            HttpMethod.Post, $"projects/{projectId}/issues", apiBase, token, body, ct);

        return response.IsSuccess
            ? SuccessOutput(new { success = true, issue = response.Data })
            : FailureOutput($"Create failed ({response.StatusCode}): {response.ErrorBody}");
    }

    private async Task<NodeOutput> GetAsync(
        NodeInput input, string projectId, string apiBase, string token, CancellationToken ct)
    {
        var iid = GetRequiredConfigValue<int>(input, "issueIid");
        var response = await SendGitLabRequestAsync(
            HttpMethod.Get, $"projects/{projectId}/issues/{iid}", apiBase, token, null, ct);

        return response.IsSuccess
            ? SuccessOutput(new { success = true, issue = response.Data })
            : FailureOutput($"Get failed ({response.StatusCode}): {response.ErrorBody}");
    }

    private async Task<NodeOutput> EditAsync(
        NodeInput input, string projectId, string apiBase, string token, CancellationToken ct)
    {
        var iid = GetRequiredConfigValue<int>(input, "issueIid");
        var body = new Dictionary<string, object>();

        AddOptionalField(body, input, "title");
        AddOptionalField(body, input, "description");
        AddOptionalField(body, input, "labels");
        AddOptionalField(body, input, "confidential");
        AddOptionalField(body, input, "milestoneId", "milestone_id");
        AddOptionalField(body, input, "stateEvent", "state_event");

        var assigneeIds = GetConfigValue<int[]>(input, "assigneeIds");
        if (assigneeIds is { Length: > 0 })
            body["assignee_ids"] = assigneeIds;

        var response = await SendGitLabRequestAsync(
            HttpMethod.Put, $"projects/{projectId}/issues/{iid}", apiBase, token, body, ct);

        return response.IsSuccess
            ? SuccessOutput(new { success = true, issue = response.Data })
            : FailureOutput($"Edit failed ({response.StatusCode}): {response.ErrorBody}");
    }

    private async Task<NodeOutput> CommentAsync(
        NodeInput input, string projectId, string apiBase, string token, CancellationToken ct)
    {
        var iid = GetRequiredConfigValue<int>(input, "issueIid");
        var commentBody = GetRequiredConfigValue<string>(input, "body");

        var body = new Dictionary<string, object> { ["body"] = commentBody };
        var response = await SendGitLabRequestAsync(
            HttpMethod.Post, $"projects/{projectId}/issues/{iid}/notes", apiBase, token, body, ct);

        return response.IsSuccess
            ? SuccessOutput(new { success = true, note = response.Data })
            : FailureOutput($"Comment failed ({response.StatusCode}): {response.ErrorBody}");
    }

    private async Task<NodeOutput> LockAsync(
        NodeInput input, string projectId, string apiBase, string token, CancellationToken ct)
    {
        var iid = GetRequiredConfigValue<int>(input, "issueIid");
        var body = new Dictionary<string, object> { ["discussion_locked"] = true };

        var response = await SendGitLabRequestAsync(
            HttpMethod.Put, $"projects/{projectId}/issues/{iid}", apiBase, token, body, ct);

        return response.IsSuccess
            ? SuccessOutput(new { success = true, locked = true, issueIid = iid })
            : FailureOutput($"Lock failed ({response.StatusCode}): {response.ErrorBody}");
    }

    private static void AddOptionalField(
        Dictionary<string, object> body, NodeInput input, string configKey, string? apiKey = null)
    {
        var value = GetConfigValue<object>(input, configKey);
        if (value is not null)
            body[apiKey ?? configKey] = value;
    }
}
