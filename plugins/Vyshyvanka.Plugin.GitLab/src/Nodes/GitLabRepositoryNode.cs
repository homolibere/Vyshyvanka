using System.Web;
using Vyshyvanka.Core.Attributes;
using Vyshyvanka.Core.Enums;
using Vyshyvanka.Core.Interfaces;

namespace Vyshyvanka.Plugin.GitLab.Nodes;

/// <summary>
/// Retrieves GitLab repository and user project information.
/// </summary>
[NodeDefinition(
    Name = "GitLab Repository",
    Description = "Get repository details, list repository issues, or list a user's projects",
    Icon = "fa-brands fa-gitlab")]
[NodeInput("input", DisplayName = "Input", Type = PortType.Object)]
[NodeOutput("output", DisplayName = "Output", Type = PortType.Object)]
[RequiresCredential(CredentialType.ApiKey)]
[ConfigurationProperty("operation", "string", Description = "Operation: get, getIssues, getUserRepos", IsRequired = true)]
[ConfigurationProperty("projectId", "string", Description = "Project ID or URL-encoded path. Required for get and getIssues.")]
[ConfigurationProperty("userId", "string", Description = "User ID or username. Required for getUserRepos.")]
[ConfigurationProperty("state", "string", Description = "Issue state filter for getIssues: opened, closed, all (default: all).")]
[ConfigurationProperty("orderBy", "string", Description = "Order by field: created_at, updated_at (default: created_at).")]
[ConfigurationProperty("sort", "string", Description = "Sort direction: asc, desc (default: desc).")]
[ConfigurationProperty("perPage", "number", Description = "Results per page (default: 20, max 100).")]
[ConfigurationProperty("page", "number", Description = "Page number (default: 1).")]
public class GitLabRepositoryNode : BaseGitLabNode
{
    public override string Type => "gitlab-repository";
    public override NodeCategory Category => NodeCategory.Action;

    public GitLabRepositoryNode() { }
    internal GitLabRepositoryNode(HttpClient? httpClient) : base(httpClient) { }

    public override async Task<NodeOutput> ExecuteAsync(NodeInput input, IExecutionContext context)
    {
        try
        {
            var operation = GetRequiredConfigValue<string>(input, "operation").ToLowerInvariant();
            var (apiBase, token) = await ResolveCredentialsAsync(input, context);

            return operation switch
            {
                "get" => await GetAsync(input, apiBase, token, context.CancellationToken),
                "getissues" => await GetIssuesAsync(input, apiBase, token, context.CancellationToken),
                "getuserrepos" => await GetUserReposAsync(input, apiBase, token, context.CancellationToken),
                _ => FailureOutput($"Unsupported operation '{operation}'. Use: get, getIssues, getUserRepos.")
            };
        }
        catch (Exception ex)
        {
            return FailureOutput($"GitLab Repository error: {ex.Message}");
        }
    }

    private async Task<NodeOutput> GetAsync(
        NodeInput input, string apiBase, string token, CancellationToken ct)
    {
        var projectId = EncodeProject(GetRequiredConfigValue<string>(input, "projectId"));
        var response = await SendGitLabRequestAsync(
            HttpMethod.Get, $"projects/{projectId}", apiBase, token, null, ct);

        return response.IsSuccess
            ? SuccessOutput(new { success = true, project = response.Data })
            : FailureOutput($"Get failed ({response.StatusCode}): {response.ErrorBody}");
    }

    private async Task<NodeOutput> GetIssuesAsync(
        NodeInput input, string apiBase, string token, CancellationToken ct)
    {
        var projectId = EncodeProject(GetRequiredConfigValue<string>(input, "projectId"));
        var state = GetConfigValue<string>(input, "state") ?? "all";
        var orderBy = GetConfigValue<string>(input, "orderBy") ?? "created_at";
        var sort = GetConfigValue<string>(input, "sort") ?? "desc";
        var perPage = Math.Min(GetConfigValue<int?>(input, "perPage") ?? 20, 100);
        var page = GetConfigValue<int?>(input, "page") ?? 1;

        var query = $"projects/{projectId}/issues" +
                    $"?state={HttpUtility.UrlEncode(state)}" +
                    $"&order_by={HttpUtility.UrlEncode(orderBy)}" +
                    $"&sort={HttpUtility.UrlEncode(sort)}" +
                    $"&per_page={perPage}&page={page}";

        var response = await SendGitLabRequestAsync(HttpMethod.Get, query, apiBase, token, null, ct);

        return response.IsSuccess
            ? SuccessOutput(new { success = true, issues = response.Data })
            : FailureOutput($"Get issues failed ({response.StatusCode}): {response.ErrorBody}");
    }

    private async Task<NodeOutput> GetUserReposAsync(
        NodeInput input, string apiBase, string token, CancellationToken ct)
    {
        var userId = GetRequiredConfigValue<string>(input, "userId");
        var perPage = Math.Min(GetConfigValue<int?>(input, "perPage") ?? 20, 100);
        var page = GetConfigValue<int?>(input, "page") ?? 1;
        var orderBy = GetConfigValue<string>(input, "orderBy") ?? "created_at";
        var sort = GetConfigValue<string>(input, "sort") ?? "desc";

        var query = $"users/{HttpUtility.UrlEncode(userId)}/projects" +
                    $"?order_by={HttpUtility.UrlEncode(orderBy)}" +
                    $"&sort={HttpUtility.UrlEncode(sort)}" +
                    $"&per_page={perPage}&page={page}";

        var response = await SendGitLabRequestAsync(HttpMethod.Get, query, apiBase, token, null, ct);

        return response.IsSuccess
            ? SuccessOutput(new { success = true, projects = response.Data })
            : FailureOutput($"Get user repos failed ({response.StatusCode}): {response.ErrorBody}");
    }
}
