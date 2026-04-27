using System.Web;
using FlowForge.Core.Attributes;
using FlowForge.Core.Enums;
using FlowForge.Core.Interfaces;

namespace FlowForge.Plugin.Jira.Nodes;

/// <summary>
/// Retrieves Jira user information by account ID or searches users by query.
/// </summary>
[NodeDefinition(
    Name = "Jira User",
    Description = "Get or search Jira users",
    Icon = "fa-brands fa-jira")]
[NodeInput("input", DisplayName = "Input", Type = PortType.Object)]
[NodeOutput("output", DisplayName = "Output", Type = PortType.Object)]
[RequiresCredential(CredentialType.BasicAuth)]
[ConfigurationProperty("operation", "string", Description = "Operation: get, search", IsRequired = true)]
[ConfigurationProperty("accountId", "string", Description = "User account ID. Required for get.")]
[ConfigurationProperty("query", "string", Description = "Search query string. Required for search.")]
[ConfigurationProperty("maxResults", "number", Description = "Max results for search (default 50).")]
[ConfigurationProperty("startAt", "number", Description = "Pagination offset for search (default 0).")]
public class JiraUserNode : BaseJiraNode
{
    public override string Type => "jira-user";
    public override NodeCategory Category => NodeCategory.Action;

    public JiraUserNode() { }
    internal JiraUserNode(HttpClient? httpClient) : base(httpClient) { }

    public override async Task<NodeOutput> ExecuteAsync(NodeInput input, IExecutionContext context)
    {
        try
        {
            var operation = GetRequiredConfigValue<string>(input, "operation").ToLowerInvariant();
            var (baseUrl, auth) = await ResolveCredentialsAsync(input, context);

            return operation switch
            {
                "get" => await GetAsync(input, baseUrl, auth, context.CancellationToken),
                "search" => await SearchAsync(input, baseUrl, auth, context.CancellationToken),
                _ => FailureOutput($"Unsupported operation '{operation}'. Use: get, search.")
            };
        }
        catch (Exception ex)
        {
            return FailureOutput($"Jira User error: {ex.Message}");
        }
    }

    private async Task<NodeOutput> GetAsync(
        NodeInput input, string baseUrl,
        System.Net.Http.Headers.AuthenticationHeaderValue auth, CancellationToken ct)
    {
        var accountId = GetRequiredConfigValue<string>(input, "accountId");
        var response = await SendJiraRequestAsync(
            HttpMethod.Get, $"user?accountId={HttpUtility.UrlEncode(accountId)}", baseUrl, auth, null, ct);

        return response.IsSuccess
            ? SuccessOutput(new { success = true, user = response.Data })
            : FailureOutput($"Get user failed ({response.StatusCode}): {response.ErrorBody}");
    }

    private async Task<NodeOutput> SearchAsync(
        NodeInput input, string baseUrl,
        System.Net.Http.Headers.AuthenticationHeaderValue auth, CancellationToken ct)
    {
        var query = GetRequiredConfigValue<string>(input, "query");
        var maxResults = GetConfigValue<int?>(input, "maxResults") ?? 50;
        var startAt = GetConfigValue<int?>(input, "startAt") ?? 0;

        var path = $"user/search?query={HttpUtility.UrlEncode(query)}&maxResults={maxResults}&startAt={startAt}";
        var response = await SendJiraRequestAsync(HttpMethod.Get, path, baseUrl, auth, null, ct);

        return response.IsSuccess
            ? SuccessOutput(new { success = true, users = response.Data })
            : FailureOutput($"Search users failed ({response.StatusCode}): {response.ErrorBody}");
    }
}
