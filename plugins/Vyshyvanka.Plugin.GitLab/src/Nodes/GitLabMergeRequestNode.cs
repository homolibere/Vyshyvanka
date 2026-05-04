using System.Web;
using Microsoft.Extensions.Logging;
using Vyshyvanka.Core.Attributes;
using Vyshyvanka.Core.Enums;
using Vyshyvanka.Core.Interfaces;

namespace Vyshyvanka.Plugin.GitLab.Nodes;

/// <summary>
/// Manages GitLab merge requests — create, get, list, update, merge, approve, and comment.
/// </summary>
[NodeDefinition(
    Name = "GitLab Merge Request",
    Description = "Create, get, list, update, merge, approve, and comment on GitLab merge requests",
    Icon = "fa-brands fa-gitlab")]
[NodeInput("input", DisplayName = "Input", Type = PortType.Object)]
[NodeOutput("output", DisplayName = "Output", Type = PortType.Object)]
[RequiresCredential(CredentialType.ApiKey)]
[ConfigurationProperty("operation", "string",
    Description = "Operation: create, get, getAll, update, merge, approve, comment", IsRequired = true)]
[ConfigurationProperty("projectId", "string", Description = "Project ID or URL-encoded path", IsRequired = true)]
[ConfigurationProperty("mergeRequestIid", "number",
    Description = "Merge request internal ID. Required for get, update, merge, approve, comment.")]
[ConfigurationProperty("title", "string", Description = "Merge request title. Required for create.")]
[ConfigurationProperty("sourceBranch", "string", Description = "Source branch. Required for create.")]
[ConfigurationProperty("targetBranch", "string", Description = "Target branch (default: main).")]
[ConfigurationProperty("description", "string", Description = "Merge request description (Markdown).")]
[ConfigurationProperty("assigneeId", "number", Description = "Assignee user ID.")]
[ConfigurationProperty("reviewerIds", "array", Description = "Array of reviewer user IDs.")]
[ConfigurationProperty("labels", "string", Description = "Comma-separated label names.")]
[ConfigurationProperty("milestoneId", "number", Description = "Milestone ID.")]
[ConfigurationProperty("squash", "boolean", Description = "Squash commits on merge.")]
[ConfigurationProperty("removeSourceBranch", "boolean", Description = "Delete source branch after merge.")]
[ConfigurationProperty("stateEvent", "string", Description = "State event for update: close or reopen.")]
[ConfigurationProperty("mergeCommitMessage", "string", Description = "Custom merge commit message.")]
[ConfigurationProperty("mergeWhenPipelineSucceeds", "boolean",
    Description = "Merge when pipeline succeeds (default: false).")]
[ConfigurationProperty("body", "string", Description = "Comment body (Markdown). Required for comment operation.")]
[ConfigurationProperty("state", "string",
    Description = "Filter for getAll: opened, closed, merged, all (default: all).")]
[ConfigurationProperty("orderBy", "string", Description = "Order by: created_at, updated_at (default: created_at).")]
[ConfigurationProperty("sort", "string", Description = "Sort direction: asc, desc (default: desc).")]
[ConfigurationProperty("perPage", "number", Description = "Results per page for getAll (default: 20, max 100).")]
[ConfigurationProperty("page", "number", Description = "Page number for getAll (default: 1).")]
public class GitLabMergeRequestNode : BaseGitLabNode
{
    public override string Type => "gitlab-merge-request";
    public override NodeCategory Category => NodeCategory.Action;

    public GitLabMergeRequestNode()
    {
    }

    internal GitLabMergeRequestNode(HttpClient? httpClient) : base(httpClient)
    {
    }

    public override async Task<NodeOutput> ExecuteAsync(NodeInput input, IExecutionContext context)
    {
        var logger = CreateLogger(context);

        try
        {
            var operation = GetRequiredConfigValue<string>(input, "operation").ToLowerInvariant();
            var projectId = EncodeProject(GetRequiredConfigValue<string>(input, "projectId"));
            var (apiBase, token) = await ResolveCredentialsAsync(input, context);

            logger.LogInformation("GitLab merge request operation: {Operation} on project {ProjectId}", operation,
                projectId);

            return operation switch
            {
                "create" => await CreateAsync(input, projectId, apiBase, token, context.CancellationToken),
                "get" => await GetAsync(input, projectId, apiBase, token, context.CancellationToken),
                "getall" => await GetAllAsync(input, projectId, apiBase, token, context.CancellationToken),
                "update" => await UpdateAsync(input, projectId, apiBase, token, context.CancellationToken),
                "merge" => await MergeAsync(input, projectId, apiBase, token, context.CancellationToken),
                "approve" => await ApproveAsync(input, projectId, apiBase, token, context.CancellationToken),
                "comment" => await CommentAsync(input, projectId, apiBase, token, context.CancellationToken),
                _ => FailureOutput(
                    $"Unsupported operation '{operation}'. Use: create, get, getAll, update, merge, approve, comment.")
            };
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "GitLab merge request operation failed");
            return FailureOutput($"GitLab Merge Request error: {ex.Message}");
        }
    }

    private async Task<NodeOutput> CreateAsync(
        NodeInput input, string projectId, string apiBase, string token, CancellationToken ct)
    {
        var body = new Dictionary<string, object>
        {
            ["title"] = GetRequiredConfigValue<string>(input, "title"),
            ["source_branch"] = GetRequiredConfigValue<string>(input, "sourceBranch"),
            ["target_branch"] = GetConfigValue<string>(input, "targetBranch") ?? "main"
        };

        AddOptional(body, input, "description");
        AddOptional(body, input, "assigneeId", "assignee_id");
        AddOptional(body, input, "labels");
        AddOptional(body, input, "milestoneId", "milestone_id");
        AddOptional(body, input, "squash");
        AddOptional(body, input, "removeSourceBranch", "remove_source_branch");

        var reviewerIds = GetConfigValue<int[]>(input, "reviewerIds");
        if (reviewerIds is { Length: > 0 })
            body["reviewer_ids"] = reviewerIds;

        var response = await SendGitLabRequestAsync(
            HttpMethod.Post, $"projects/{projectId}/merge_requests", apiBase, token, body, ct);

        return response.IsSuccess
            ? SuccessOutput(new { success = true, mergeRequest = response.Data })
            : FailureOutput($"Create failed ({response.StatusCode}): {response.ErrorBody}");
    }

    private async Task<NodeOutput> GetAsync(
        NodeInput input, string projectId, string apiBase, string token, CancellationToken ct)
    {
        var iid = GetRequiredConfigValue<int>(input, "mergeRequestIid");
        var response = await SendGitLabRequestAsync(
            HttpMethod.Get, $"projects/{projectId}/merge_requests/{iid}", apiBase, token, null, ct);

        return response.IsSuccess
            ? SuccessOutput(new { success = true, mergeRequest = response.Data })
            : FailureOutput($"Get failed ({response.StatusCode}): {response.ErrorBody}");
    }

    private async Task<NodeOutput> GetAllAsync(
        NodeInput input, string projectId, string apiBase, string token, CancellationToken ct)
    {
        var state = GetConfigValue<string>(input, "state") ?? "all";
        var orderBy = GetConfigValue<string>(input, "orderBy") ?? "created_at";
        var sort = GetConfigValue<string>(input, "sort") ?? "desc";
        var perPage = Math.Min(GetConfigValue<int?>(input, "perPage") ?? 20, 100);
        var page = GetConfigValue<int?>(input, "page") ?? 1;

        var query = $"projects/{projectId}/merge_requests" +
                    $"?state={HttpUtility.UrlEncode(state)}" +
                    $"&order_by={HttpUtility.UrlEncode(orderBy)}" +
                    $"&sort={HttpUtility.UrlEncode(sort)}" +
                    $"&per_page={perPage}&page={page}";

        var response = await SendGitLabRequestAsync(HttpMethod.Get, query, apiBase, token, null, ct);

        return response.IsSuccess
            ? SuccessOutput(new { success = true, mergeRequests = response.Data })
            : FailureOutput($"Get all failed ({response.StatusCode}): {response.ErrorBody}");
    }

    private async Task<NodeOutput> UpdateAsync(
        NodeInput input, string projectId, string apiBase, string token, CancellationToken ct)
    {
        var iid = GetRequiredConfigValue<int>(input, "mergeRequestIid");
        var body = new Dictionary<string, object>();

        AddOptional(body, input, "title");
        AddOptional(body, input, "description");
        AddOptional(body, input, "targetBranch", "target_branch");
        AddOptional(body, input, "assigneeId", "assignee_id");
        AddOptional(body, input, "labels");
        AddOptional(body, input, "milestoneId", "milestone_id");
        AddOptional(body, input, "squash");
        AddOptional(body, input, "removeSourceBranch", "remove_source_branch");
        AddOptional(body, input, "stateEvent", "state_event");

        var reviewerIds = GetConfigValue<int[]>(input, "reviewerIds");
        if (reviewerIds is { Length: > 0 })
            body["reviewer_ids"] = reviewerIds;

        var response = await SendGitLabRequestAsync(
            HttpMethod.Put, $"projects/{projectId}/merge_requests/{iid}", apiBase, token, body, ct);

        return response.IsSuccess
            ? SuccessOutput(new { success = true, mergeRequest = response.Data })
            : FailureOutput($"Update failed ({response.StatusCode}): {response.ErrorBody}");
    }

    private async Task<NodeOutput> MergeAsync(
        NodeInput input, string projectId, string apiBase, string token, CancellationToken ct)
    {
        var iid = GetRequiredConfigValue<int>(input, "mergeRequestIid");
        var body = new Dictionary<string, object>();

        AddOptional(body, input, "mergeCommitMessage", "merge_commit_message");
        AddOptional(body, input, "squash");
        AddOptional(body, input, "removeSourceBranch", "should_remove_source_branch");
        AddOptional(body, input, "mergeWhenPipelineSucceeds", "merge_when_pipeline_succeeds");

        var response = await SendGitLabRequestAsync(
            HttpMethod.Put, $"projects/{projectId}/merge_requests/{iid}/merge", apiBase, token, body, ct);

        return response.IsSuccess
            ? SuccessOutput(new { success = true, mergeRequest = response.Data })
            : FailureOutput($"Merge failed ({response.StatusCode}): {response.ErrorBody}");
    }

    private async Task<NodeOutput> ApproveAsync(
        NodeInput input, string projectId, string apiBase, string token, CancellationToken ct)
    {
        var iid = GetRequiredConfigValue<int>(input, "mergeRequestIid");
        var response = await SendGitLabRequestAsync(
            HttpMethod.Post, $"projects/{projectId}/merge_requests/{iid}/approve", apiBase, token, new { }, ct);

        return response.IsSuccess
            ? SuccessOutput(new { success = true, approved = true, mergeRequestIid = iid })
            : FailureOutput($"Approve failed ({response.StatusCode}): {response.ErrorBody}");
    }

    private async Task<NodeOutput> CommentAsync(
        NodeInput input, string projectId, string apiBase, string token, CancellationToken ct)
    {
        var iid = GetRequiredConfigValue<int>(input, "mergeRequestIid");
        var commentBody = GetRequiredConfigValue<string>(input, "body");

        var body = new Dictionary<string, object> { ["body"] = commentBody };
        var response = await SendGitLabRequestAsync(
            HttpMethod.Post, $"projects/{projectId}/merge_requests/{iid}/notes", apiBase, token, body, ct);

        return response.IsSuccess
            ? SuccessOutput(new { success = true, note = response.Data })
            : FailureOutput($"Comment failed ({response.StatusCode}): {response.ErrorBody}");
    }

    private static void AddOptional(
        Dictionary<string, object> body, NodeInput input, string configKey, string? apiKey = null)
    {
        var value = GetConfigValue<object>(input, configKey);
        if (value is not null)
            body[apiKey ?? configKey] = value;
    }
}
