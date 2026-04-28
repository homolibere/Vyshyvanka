using System.Text.Json;
using System.Web;
using Vyshyvanka.Core.Attributes;
using Vyshyvanka.Core.Enums;
using Vyshyvanka.Core.Interfaces;

namespace Vyshyvanka.Plugin.Jira.Nodes;

/// <summary>
/// Manages Jira issues — create, read, update, delete, list (via JQL), and transition.
/// </summary>
[NodeDefinition(
    Name = "Jira Issue",
    Description = "Create, update, delete, get, list, and transition Jira issues",
    Icon = "fa-brands fa-jira")]
[NodeInput("input", DisplayName = "Input", Type = PortType.Object)]
[NodeOutput("output", DisplayName = "Output", Type = PortType.Object)]
[RequiresCredential(CredentialType.BasicAuth)]
[ConfigurationProperty("operation", "string", Description = "Operation: create, update, delete, get, getAll, transition", IsRequired = true)]
[ConfigurationProperty("issueIdOrKey", "string", Description = "Issue ID or key (e.g. PROJ-123). Required for get, update, delete, transition.")]
[ConfigurationProperty("projectKey", "string", Description = "Project key (e.g. PROJ). Required for create.")]
[ConfigurationProperty("summary", "string", Description = "Issue summary. Required for create.")]
[ConfigurationProperty("issueType", "string", Description = "Issue type name (e.g. Task, Bug, Story). Required for create.")]
[ConfigurationProperty("description", "string", Description = "Issue description (plain text, converted to ADF).")]
[ConfigurationProperty("assigneeAccountId", "string", Description = "Assignee account ID.")]
[ConfigurationProperty("labels", "array", Description = "Array of label strings.")]
[ConfigurationProperty("priority", "string", Description = "Priority name (e.g. High, Medium, Low).")]
[ConfigurationProperty("additionalFields", "object", Description = "Additional fields as key-value pairs merged into the request.")]
[ConfigurationProperty("jql", "string", Description = "JQL query for getAll operation.")]
[ConfigurationProperty("maxResults", "number", Description = "Max results for getAll (default 50).")]
[ConfigurationProperty("startAt", "number", Description = "Pagination offset for getAll (default 0).")]
[ConfigurationProperty("transitionId", "string", Description = "Transition ID for the transition operation.")]
public class JiraIssueNode : BaseJiraNode
{
    public override string Type => "jira-issue";
    public override NodeCategory Category => NodeCategory.Action;

    public JiraIssueNode() { }
    internal JiraIssueNode(HttpClient? httpClient) : base(httpClient) { }

    public override async Task<NodeOutput> ExecuteAsync(NodeInput input, IExecutionContext context)
    {
        try
        {
            var operation = GetRequiredConfigValue<string>(input, "operation").ToLowerInvariant();
            var (baseUrl, auth) = await ResolveCredentialsAsync(input, context);

            return operation switch
            {
                "create" => await CreateAsync(input, baseUrl, auth, context.CancellationToken),
                "update" => await UpdateAsync(input, baseUrl, auth, context.CancellationToken),
                "delete" => await DeleteAsync(input, baseUrl, auth, context.CancellationToken),
                "get" => await GetAsync(input, baseUrl, auth, context.CancellationToken),
                "getall" => await GetAllAsync(input, baseUrl, auth, context.CancellationToken),
                "transition" => await TransitionAsync(input, baseUrl, auth, context.CancellationToken),
                _ => FailureOutput($"Unsupported operation '{operation}'. Use: create, update, delete, get, getAll, transition.")
            };
        }
        catch (Exception ex)
        {
            return FailureOutput($"Jira Issue error: {ex.Message}");
        }
    }

    private async Task<NodeOutput> CreateAsync(
        NodeInput input, string baseUrl, System.Net.Http.Headers.AuthenticationHeaderValue auth, CancellationToken ct)
    {
        var projectKey = GetRequiredConfigValue<string>(input, "projectKey");
        var summary = GetRequiredConfigValue<string>(input, "summary");
        var issueType = GetRequiredConfigValue<string>(input, "issueType");
        var description = GetConfigValue<string>(input, "description");
        var assignee = GetConfigValue<string>(input, "assigneeAccountId");
        var labels = GetConfigValue<string[]>(input, "labels");
        var priority = GetConfigValue<string>(input, "priority");
        var additionalFields = GetConfigValue<Dictionary<string, object>>(input, "additionalFields");

        var fields = new Dictionary<string, object>
        {
            ["project"] = new { key = projectKey },
            ["summary"] = summary,
            ["issuetype"] = new { name = issueType }
        };

        if (!string.IsNullOrWhiteSpace(description))
            fields["description"] = ToAdf(description);

        if (!string.IsNullOrWhiteSpace(assignee))
            fields["assignee"] = new { accountId = assignee };

        if (labels is { Length: > 0 })
            fields["labels"] = labels;

        if (!string.IsNullOrWhiteSpace(priority))
            fields["priority"] = new { name = priority };

        if (additionalFields is not null)
        {
            foreach (var (key, value) in additionalFields)
                fields[key] = value;
        }

        var body = new { fields };
        var response = await SendJiraRequestAsync(HttpMethod.Post, "issue", baseUrl, auth, body, ct);

        return response.IsSuccess
            ? SuccessOutput(new { success = true, issue = response.Data })
            : FailureOutput($"Create failed ({response.StatusCode}): {response.ErrorBody}");
    }

    private async Task<NodeOutput> UpdateAsync(
        NodeInput input, string baseUrl, System.Net.Http.Headers.AuthenticationHeaderValue auth, CancellationToken ct)
    {
        var issueKey = GetRequiredConfigValue<string>(input, "issueIdOrKey");
        var summary = GetConfigValue<string>(input, "summary");
        var description = GetConfigValue<string>(input, "description");
        var assignee = GetConfigValue<string>(input, "assigneeAccountId");
        var labels = GetConfigValue<string[]>(input, "labels");
        var priority = GetConfigValue<string>(input, "priority");
        var additionalFields = GetConfigValue<Dictionary<string, object>>(input, "additionalFields");

        var fields = new Dictionary<string, object>();

        if (!string.IsNullOrWhiteSpace(summary))
            fields["summary"] = summary;

        if (!string.IsNullOrWhiteSpace(description))
            fields["description"] = ToAdf(description);

        if (!string.IsNullOrWhiteSpace(assignee))
            fields["assignee"] = new { accountId = assignee };

        if (labels is { Length: > 0 })
            fields["labels"] = labels;

        if (!string.IsNullOrWhiteSpace(priority))
            fields["priority"] = new { name = priority };

        if (additionalFields is not null)
        {
            foreach (var (key, value) in additionalFields)
                fields[key] = value;
        }

        var body = new { fields };
        var response = await SendJiraRequestAsync(HttpMethod.Put, $"issue/{issueKey}", baseUrl, auth, body, ct);

        return response.IsSuccess
            ? SuccessOutput(new { success = true, issueKey })
            : FailureOutput($"Update failed ({response.StatusCode}): {response.ErrorBody}");
    }

    private async Task<NodeOutput> DeleteAsync(
        NodeInput input, string baseUrl, System.Net.Http.Headers.AuthenticationHeaderValue auth, CancellationToken ct)
    {
        var issueKey = GetRequiredConfigValue<string>(input, "issueIdOrKey");
        var response = await SendJiraRequestAsync(HttpMethod.Delete, $"issue/{issueKey}", baseUrl, auth, null, ct);

        return response.IsSuccess
            ? SuccessOutput(new { success = true, deleted = issueKey })
            : FailureOutput($"Delete failed ({response.StatusCode}): {response.ErrorBody}");
    }

    private async Task<NodeOutput> GetAsync(
        NodeInput input, string baseUrl, System.Net.Http.Headers.AuthenticationHeaderValue auth, CancellationToken ct)
    {
        var issueKey = GetRequiredConfigValue<string>(input, "issueIdOrKey");
        var response = await SendJiraRequestAsync(HttpMethod.Get, $"issue/{issueKey}", baseUrl, auth, null, ct);

        return response.IsSuccess
            ? SuccessOutput(new { success = true, issue = response.Data })
            : FailureOutput($"Get failed ({response.StatusCode}): {response.ErrorBody}");
    }

    private async Task<NodeOutput> GetAllAsync(
        NodeInput input, string baseUrl, System.Net.Http.Headers.AuthenticationHeaderValue auth, CancellationToken ct)
    {
        var jql = GetConfigValue<string>(input, "jql") ?? "";
        var maxResults = GetConfigValue<int?>(input, "maxResults") ?? 50;
        var startAt = GetConfigValue<int?>(input, "startAt") ?? 0;

        var query = $"search?jql={HttpUtility.UrlEncode(jql)}&maxResults={maxResults}&startAt={startAt}";
        var response = await SendJiraRequestAsync(HttpMethod.Get, query, baseUrl, auth, null, ct);

        return response.IsSuccess
            ? SuccessOutput(new { success = true, results = response.Data })
            : FailureOutput($"Search failed ({response.StatusCode}): {response.ErrorBody}");
    }

    private async Task<NodeOutput> TransitionAsync(
        NodeInput input, string baseUrl, System.Net.Http.Headers.AuthenticationHeaderValue auth, CancellationToken ct)
    {
        var issueKey = GetRequiredConfigValue<string>(input, "issueIdOrKey");
        var transitionId = GetConfigValue<string>(input, "transitionId");

        // If no transitionId, return available transitions
        if (string.IsNullOrWhiteSpace(transitionId))
        {
            var listResponse = await SendJiraRequestAsync(
                HttpMethod.Get, $"issue/{issueKey}/transitions", baseUrl, auth, null, ct);

            return listResponse.IsSuccess
                ? SuccessOutput(new { success = true, transitions = listResponse.Data })
                : FailureOutput($"Get transitions failed ({listResponse.StatusCode}): {listResponse.ErrorBody}");
        }

        var body = new { transition = new { id = transitionId } };
        var response = await SendJiraRequestAsync(
            HttpMethod.Post, $"issue/{issueKey}/transitions", baseUrl, auth, body, ct);

        return response.IsSuccess
            ? SuccessOutput(new { success = true, issueKey, transitionId })
            : FailureOutput($"Transition failed ({response.StatusCode}): {response.ErrorBody}");
    }

    /// <summary>
    /// Converts plain text to Atlassian Document Format (ADF) paragraph.
    /// </summary>
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
