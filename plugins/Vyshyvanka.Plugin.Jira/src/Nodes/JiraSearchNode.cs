using System.Text.Json;
using System.Web;
using Vyshyvanka.Core.Attributes;
using Vyshyvanka.Core.Enums;
using Vyshyvanka.Core.Interfaces;

namespace Vyshyvanka.Plugin.Jira.Nodes;

/// <summary>
/// Executes a JQL query against Jira and returns matching issues.
/// Supports field selection, expand options, and automatic pagination.
/// </summary>
[NodeDefinition(
    Name = "Jira Search",
    Description = "Search Jira issues using JQL (Jira Query Language)",
    Icon = "fa-brands fa-jira")]
[NodeInput("input", DisplayName = "Input", Type = PortType.Object)]
[NodeOutput("output", DisplayName = "Results", Type = PortType.Object)]
[RequiresCredential(CredentialType.BasicAuth)]
[ConfigurationProperty("jql", "string", Description = "JQL query (e.g. project = PROJ AND status = 'In Progress')", IsRequired = true)]
[ConfigurationProperty("fields", "string", Description = "Comma-separated field names to return (e.g. summary,status,assignee). Omit for all fields.")]
[ConfigurationProperty("expand", "string", Description = "Comma-separated expand options (e.g. changelog,renderedFields,transitions).")]
[ConfigurationProperty("maxResults", "number", Description = "Max results per page (default 50, max 100).")]
[ConfigurationProperty("startAt", "number", Description = "Pagination offset (default 0).")]
[ConfigurationProperty("returnAll", "boolean", Description = "When true, automatically paginates to fetch all matching issues.")]
public class JiraSearchNode : BaseJiraNode
{
    public override string Type => "jira-search";
    public override NodeCategory Category => NodeCategory.Action;

    public JiraSearchNode() { }
    internal JiraSearchNode(HttpClient? httpClient) : base(httpClient) { }

    public override async Task<NodeOutput> ExecuteAsync(NodeInput input, IExecutionContext context)
    {
        try
        {
            var jql = GetRequiredConfigValue<string>(input, "jql");
            var fields = GetConfigValue<string>(input, "fields");
            var expand = GetConfigValue<string>(input, "expand");
            var maxResults = Math.Min(GetConfigValue<int?>(input, "maxResults") ?? 50, 100);
            var startAt = GetConfigValue<int?>(input, "startAt") ?? 0;
            var returnAll = GetConfigValue<bool?>(input, "returnAll") ?? false;

            var (baseUrl, auth) = await ResolveCredentialsAsync(input, context);

            if (!returnAll)
            {
                var path = BuildSearchPath(jql, fields, expand, maxResults, startAt);
                var response = await SendJiraRequestAsync(HttpMethod.Get, path, baseUrl, auth, null, context.CancellationToken);

                return response.IsSuccess
                    ? SuccessOutput(new { success = true, results = response.Data })
                    : FailureOutput($"Search failed ({response.StatusCode}): {response.ErrorBody}");
            }

            // Auto-paginate
            var allIssues = new List<JsonElement>();
            var currentStart = startAt;
            int total;

            do
            {
                context.CancellationToken.ThrowIfCancellationRequested();

                var path = BuildSearchPath(jql, fields, expand, maxResults, currentStart);
                var response = await SendJiraRequestAsync(HttpMethod.Get, path, baseUrl, auth, null, context.CancellationToken);

                if (!response.IsSuccess)
                    return FailureOutput($"Search failed at offset {currentStart} ({response.StatusCode}): {response.ErrorBody}");

                var data = response.Data!.Value;
                total = data.TryGetProperty("total", out var t) ? t.GetInt32() : 0;

                if (data.TryGetProperty("issues", out var issues) && issues.ValueKind == JsonValueKind.Array)
                {
                    foreach (var issue in issues.EnumerateArray())
                        allIssues.Add(issue.Clone());
                }

                currentStart += maxResults;
            }
            while (currentStart < total);

            return SuccessOutput(new
            {
                success = true,
                total = allIssues.Count,
                issues = allIssues
            });
        }
        catch (OperationCanceledException)
        {
            return FailureOutput("Search was cancelled");
        }
        catch (Exception ex)
        {
            return FailureOutput($"Jira Search error: {ex.Message}");
        }
    }

    private static string BuildSearchPath(string jql, string? fields, string? expand, int maxResults, int startAt)
    {
        var path = $"search?jql={HttpUtility.UrlEncode(jql)}&maxResults={maxResults}&startAt={startAt}";

        if (!string.IsNullOrWhiteSpace(fields))
            path += $"&fields={HttpUtility.UrlEncode(fields)}";

        if (!string.IsNullOrWhiteSpace(expand))
            path += $"&expand={HttpUtility.UrlEncode(expand)}";

        return path;
    }
}
