using System.Text.Json;
using Microsoft.Extensions.Logging;
using Vyshyvanka.Core.Attributes;
using Vyshyvanka.Core.Enums;
using Vyshyvanka.Core.Interfaces;

namespace Vyshyvanka.Plugin.Jira.Nodes;

/// <summary>
/// Executes a JQL query against Jira and returns matching issues.
/// Uses the POST /rest/api/3/search/jql endpoint with nextPageToken pagination.
/// </summary>
[NodeDefinition(
    Name = "Jira Search",
    Description = "Search Jira issues using JQL (Jira Query Language)",
    Icon = "fa-brands fa-jira")]
[NodeInput("input", DisplayName = "Input", Type = PortType.Object)]
[NodeOutput("output", DisplayName = "Results", Type = PortType.Object)]
[RequiresCredential(CredentialType.BasicAuth)]
[ConfigurationProperty("jql", "string", Description = "JQL query (e.g. project = PROJ AND status = 'In Progress')",
    IsRequired = true)]
[ConfigurationProperty("fields", "string",
    Description = "Comma-separated field names to return (e.g. summary,status,assignee). Omit for all fields.")]
[ConfigurationProperty("expand", "string",
    Description = "Comma-separated expand options (e.g. changelog,renderedFields,transitions).")]
[ConfigurationProperty("maxResults", "number", Description = "Max results per page (default 50, max 100).")]
[ConfigurationProperty("returnAll", "boolean",
    Description = "When true, automatically paginates to fetch all matching issues.")]
public class JiraSearchNode : BaseJiraNode
{
    public override string Type => "jira-search";
    public override NodeCategory Category => NodeCategory.Action;

    public JiraSearchNode()
    {
    }

    internal JiraSearchNode(HttpClient? httpClient) : base(httpClient)
    {
    }

    public override async Task<NodeOutput> ExecuteAsync(NodeInput input, IExecutionContext context)
    {
        var logger = CreateLogger(context);

        try
        {
            var jql = GetRequiredConfigValue<string>(input, "jql");
            var fields = GetConfigValue<string>(input, "fields");
            var expand = GetConfigValue<string>(input, "expand");
            var maxResults = Math.Min(GetConfigValue<int?>(input, "maxResults") ?? 50, 100);
            var returnAll = GetConfigValue<bool?>(input, "returnAll") ?? false;

            logger.LogInformation("Jira search: {Jql} (returnAll={ReturnAll})", jql, returnAll);

            var (baseUrl, auth) = await ResolveCredentialsAsync(input, context);

            if (!returnAll)
            {
                var body = BuildSearchBody(jql, fields, expand, maxResults, nextPageToken: null);
                var response = await SendJiraRequestAsync(
                    HttpMethod.Post, "search/jql", baseUrl, auth, body, context.CancellationToken);

                return response.IsSuccess
                    ? SuccessOutput(new { success = true, results = response.Data })
                    : FailureOutputWithDebug(
                        $"Search failed ({response.StatusCode})",
                        response.Method, response.Url, response.RequestBody, response.ErrorBody);
            }

            // Auto-paginate using nextPageToken
            var allIssues = new List<JsonElement>();
            string? pageToken = null;

            do
            {
                context.CancellationToken.ThrowIfCancellationRequested();

                var body = BuildSearchBody(jql, fields, expand, maxResults, pageToken);
                var response = await SendJiraRequestAsync(
                    HttpMethod.Post, "search/jql", baseUrl, auth, body, context.CancellationToken);

                if (!response.IsSuccess)
                    return FailureOutputWithDebug(
                        $"Search failed ({response.StatusCode})",
                        response.Method, response.Url, response.RequestBody, response.ErrorBody);

                var data = response.Data!.Value;

                if (data.TryGetProperty("issues", out var issues) && issues.ValueKind == JsonValueKind.Array)
                {
                    foreach (var issue in issues.EnumerateArray())
                        allIssues.Add(issue.Clone());
                }

                pageToken = data.TryGetProperty("nextPageToken", out var tokenEl) &&
                            tokenEl.ValueKind == JsonValueKind.String
                    ? tokenEl.GetString()
                    : null;
            } while (pageToken is not null);

            return SuccessOutput(new
            {
                success = true,
                total = allIssues.Count,
                issues = allIssues
            });
        }
        catch (OperationCanceledException)
        {
            logger.LogWarning("Jira search was cancelled");
            return FailureOutput("Search was cancelled");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Jira search failed");
            return FailureOutput($"Jira Search error: {ex.Message}");
        }
    }

    private static Dictionary<string, object> BuildSearchBody(
        string jql, string? fields, string? expand, int maxResults, string? nextPageToken)
    {
        var body = new Dictionary<string, object>
        {
            ["jql"] = jql,
            ["maxResults"] = maxResults
        };

        if (nextPageToken is not null)
            body["nextPageToken"] = nextPageToken;

        if (!string.IsNullOrWhiteSpace(fields))
            body["fields"] = fields.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        if (!string.IsNullOrWhiteSpace(expand))
            body["expand"] = expand;

        return body;
    }
}
