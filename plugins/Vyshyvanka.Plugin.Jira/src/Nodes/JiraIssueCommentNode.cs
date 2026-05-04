using System.Text.Json;
using Microsoft.Extensions.Logging;
using Vyshyvanka.Core.Attributes;
using Vyshyvanka.Core.Enums;
using Vyshyvanka.Core.Interfaces;

namespace Vyshyvanka.Plugin.Jira.Nodes;

/// <summary>
/// Manages comments on Jira issues — add, get, list, update, and remove.
/// </summary>
[NodeDefinition(
    Name = "Jira Issue Comment",
    Description = "Add, get, list, update, and remove comments on Jira issues",
    Icon = "fa-brands fa-jira")]
[NodeInput("input", DisplayName = "Input", Type = PortType.Object)]
[NodeOutput("output", DisplayName = "Output", Type = PortType.Object)]
[RequiresCredential(CredentialType.BasicAuth)]
[ConfigurationProperty("operation", "string", Description = "Operation: add, get, getAll, update, remove",
    IsRequired = true)]
[ConfigurationProperty("issueIdOrKey", "string", Description = "Issue ID or key (e.g. PROJ-123)", IsRequired = true)]
[ConfigurationProperty("commentId", "string", Description = "Comment ID. Required for get, update, remove.")]
[ConfigurationProperty("body", "string",
    Description = "Comment body (plain text, converted to ADF). Required for add and update.")]
[ConfigurationProperty("maxResults", "number", Description = "Max results for getAll (default 50).")]
[ConfigurationProperty("startAt", "number", Description = "Pagination offset for getAll (default 0).")]
public class JiraIssueCommentNode : BaseJiraNode
{
    public override string Type => "jira-issue-comment";
    public override NodeCategory Category => NodeCategory.Action;

    public JiraIssueCommentNode()
    {
    }

    internal JiraIssueCommentNode(HttpClient? httpClient) : base(httpClient)
    {
    }

    public override async Task<NodeOutput> ExecuteAsync(NodeInput input, IExecutionContext context)
    {
        var logger = CreateLogger(context);

        try
        {
            var operation = GetRequiredConfigValue<string>(input, "operation").ToLowerInvariant();
            var issueKey = GetRequiredConfigValue<string>(input, "issueIdOrKey");
            var (baseUrl, auth) = await ResolveCredentialsAsync(input, context);

            logger.LogInformation("Jira comment operation: {Operation} on {IssueKey}", operation, issueKey);

            return operation switch
            {
                "add" => await AddAsync(input, issueKey, baseUrl, auth, context.CancellationToken),
                "get" => await GetAsync(input, issueKey, baseUrl, auth, context.CancellationToken),
                "getall" => await GetAllAsync(input, issueKey, baseUrl, auth, context.CancellationToken),
                "update" => await UpdateAsync(input, issueKey, baseUrl, auth, context.CancellationToken),
                "remove" => await RemoveAsync(input, issueKey, baseUrl, auth, context.CancellationToken),
                _ => FailureOutput($"Unsupported operation '{operation}'. Use: add, get, getAll, update, remove.")
            };
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Jira comment operation failed");
            return FailureOutput($"Jira Comment error: {ex.Message}");
        }
    }

    private async Task<NodeOutput> AddAsync(
        NodeInput input, string issueKey, string baseUrl,
        System.Net.Http.Headers.AuthenticationHeaderValue auth, CancellationToken ct)
    {
        var bodyText = GetRequiredConfigValue<string>(input, "body");
        var body = new { body = ToAdf(bodyText) };

        var response = await SendJiraRequestAsync(
            HttpMethod.Post, $"issue/{issueKey}/comment", baseUrl, auth, body, ct);

        return response.IsSuccess
            ? SuccessOutput(new { success = true, comment = response.Data })
            : FailureOutput($"Add comment failed ({response.StatusCode}): {response.ErrorBody}");
    }

    private async Task<NodeOutput> GetAsync(
        NodeInput input, string issueKey, string baseUrl,
        System.Net.Http.Headers.AuthenticationHeaderValue auth, CancellationToken ct)
    {
        var commentId = GetRequiredConfigValue<string>(input, "commentId");
        var response = await SendJiraRequestAsync(
            HttpMethod.Get, $"issue/{issueKey}/comment/{commentId}", baseUrl, auth, null, ct);

        return response.IsSuccess
            ? SuccessOutput(new { success = true, comment = response.Data })
            : FailureOutput($"Get comment failed ({response.StatusCode}): {response.ErrorBody}");
    }

    private async Task<NodeOutput> GetAllAsync(
        NodeInput input, string issueKey, string baseUrl,
        System.Net.Http.Headers.AuthenticationHeaderValue auth, CancellationToken ct)
    {
        var maxResults = GetConfigValue<int?>(input, "maxResults") ?? 50;
        var startAt = GetConfigValue<int?>(input, "startAt") ?? 0;

        var response = await SendJiraRequestAsync(
            HttpMethod.Get, $"issue/{issueKey}/comment?maxResults={maxResults}&startAt={startAt}",
            baseUrl, auth, null, ct);

        return response.IsSuccess
            ? SuccessOutput(new { success = true, comments = response.Data })
            : FailureOutput($"Get comments failed ({response.StatusCode}): {response.ErrorBody}");
    }

    private async Task<NodeOutput> UpdateAsync(
        NodeInput input, string issueKey, string baseUrl,
        System.Net.Http.Headers.AuthenticationHeaderValue auth, CancellationToken ct)
    {
        var commentId = GetRequiredConfigValue<string>(input, "commentId");
        var bodyText = GetRequiredConfigValue<string>(input, "body");
        var body = new { body = ToAdf(bodyText) };

        var response = await SendJiraRequestAsync(
            HttpMethod.Put, $"issue/{issueKey}/comment/{commentId}", baseUrl, auth, body, ct);

        return response.IsSuccess
            ? SuccessOutput(new { success = true, comment = response.Data })
            : FailureOutput($"Update comment failed ({response.StatusCode}): {response.ErrorBody}");
    }

    private async Task<NodeOutput> RemoveAsync(
        NodeInput input, string issueKey, string baseUrl,
        System.Net.Http.Headers.AuthenticationHeaderValue auth, CancellationToken ct)
    {
        var commentId = GetRequiredConfigValue<string>(input, "commentId");
        var response = await SendJiraRequestAsync(
            HttpMethod.Delete, $"issue/{issueKey}/comment/{commentId}", baseUrl, auth, null, ct);

        return response.IsSuccess
            ? SuccessOutput(new { success = true, deleted = commentId })
            : FailureOutput($"Remove comment failed ({response.StatusCode}): {response.ErrorBody}");
    }

    private static object ToAdf(string text) => new
    {
        type = "doc",
        version = 1,
        content = new[]
        {
            new
            {
                type = "paragraph",
                content = new[]
                {
                    new { type = "text", text }
                }
            }
        }
    };
}
