using System.Web;
using FlowForge.Core.Attributes;
using FlowForge.Core.Enums;
using FlowForge.Core.Interfaces;

namespace FlowForge.Plugin.GitLab.Nodes;

/// <summary>
/// Manages GitLab repository tags — create, get, list, and delete.
/// </summary>
[NodeDefinition(
    Name = "GitLab Tag",
    Description = "Create, get, list, and delete tags in a GitLab repository",
    Icon = "fa-brands fa-gitlab")]
[NodeInput("input", DisplayName = "Input", Type = PortType.Object)]
[NodeOutput("output", DisplayName = "Output", Type = PortType.Object)]
[RequiresCredential(CredentialType.ApiKey)]
[ConfigurationProperty("operation", "string", Description = "Operation: create, get, getAll, delete", IsRequired = true)]
[ConfigurationProperty("projectId", "string", Description = "Project ID or URL-encoded path", IsRequired = true)]
[ConfigurationProperty("tagName", "string", Description = "Tag name. Required for create, get, delete.")]
[ConfigurationProperty("ref", "string", Description = "Branch or commit SHA to create the tag from. Required for create.")]
[ConfigurationProperty("message", "string", Description = "Annotation message for the tag (creates an annotated tag).")]
[ConfigurationProperty("releaseDescription", "string", Description = "Release notes for the tag (Markdown).")]
[ConfigurationProperty("search", "string", Description = "Search tags by name pattern for getAll.")]
[ConfigurationProperty("orderBy", "string", Description = "Order by: name, updated, version (default: updated).")]
[ConfigurationProperty("sort", "string", Description = "Sort direction: asc, desc (default: desc).")]
[ConfigurationProperty("perPage", "number", Description = "Results per page for getAll (default: 20, max 100).")]
[ConfigurationProperty("page", "number", Description = "Page number for getAll (default: 1).")]
public class GitLabTagNode : BaseGitLabNode
{
    public override string Type => "gitlab-tag";
    public override NodeCategory Category => NodeCategory.Action;

    public GitLabTagNode() { }
    internal GitLabTagNode(HttpClient? httpClient) : base(httpClient) { }

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
                "getall" => await GetAllAsync(input, projectId, apiBase, token, context.CancellationToken),
                "delete" => await DeleteAsync(input, projectId, apiBase, token, context.CancellationToken),
                _ => FailureOutput($"Unsupported operation '{operation}'. Use: create, get, getAll, delete.")
            };
        }
        catch (Exception ex)
        {
            return FailureOutput($"GitLab Tag error: {ex.Message}");
        }
    }

    private async Task<NodeOutput> CreateAsync(
        NodeInput input, string projectId, string apiBase, string token, CancellationToken ct)
    {
        var body = new Dictionary<string, object>
        {
            ["tag_name"] = GetRequiredConfigValue<string>(input, "tagName"),
            ["ref"] = GetRequiredConfigValue<string>(input, "ref")
        };

        var message = GetConfigValue<string>(input, "message");
        if (!string.IsNullOrWhiteSpace(message))
            body["message"] = message;

        var releaseDesc = GetConfigValue<string>(input, "releaseDescription");
        if (!string.IsNullOrWhiteSpace(releaseDesc))
            body["release_description"] = releaseDesc;

        var response = await SendGitLabRequestAsync(
            HttpMethod.Post, $"projects/{projectId}/repository/tags", apiBase, token, body, ct);

        return response.IsSuccess
            ? SuccessOutput(new { success = true, tag = response.Data })
            : FailureOutput($"Create failed ({response.StatusCode}): {response.ErrorBody}");
    }

    private async Task<NodeOutput> GetAsync(
        NodeInput input, string projectId, string apiBase, string token, CancellationToken ct)
    {
        var tagName = HttpUtility.UrlEncode(GetRequiredConfigValue<string>(input, "tagName"));
        var response = await SendGitLabRequestAsync(
            HttpMethod.Get, $"projects/{projectId}/repository/tags/{tagName}", apiBase, token, null, ct);

        return response.IsSuccess
            ? SuccessOutput(new { success = true, tag = response.Data })
            : FailureOutput($"Get failed ({response.StatusCode}): {response.ErrorBody}");
    }

    private async Task<NodeOutput> GetAllAsync(
        NodeInput input, string projectId, string apiBase, string token, CancellationToken ct)
    {
        var orderBy = GetConfigValue<string>(input, "orderBy") ?? "updated";
        var sort = GetConfigValue<string>(input, "sort") ?? "desc";
        var perPage = Math.Min(GetConfigValue<int?>(input, "perPage") ?? 20, 100);
        var page = GetConfigValue<int?>(input, "page") ?? 1;

        var query = $"projects/{projectId}/repository/tags" +
                    $"?order_by={HttpUtility.UrlEncode(orderBy)}" +
                    $"&sort={HttpUtility.UrlEncode(sort)}" +
                    $"&per_page={perPage}&page={page}";

        var search = GetConfigValue<string>(input, "search");
        if (!string.IsNullOrWhiteSpace(search))
            query += $"&search={HttpUtility.UrlEncode(search)}";

        var response = await SendGitLabRequestAsync(HttpMethod.Get, query, apiBase, token, null, ct);

        return response.IsSuccess
            ? SuccessOutput(new { success = true, tags = response.Data })
            : FailureOutput($"Get all failed ({response.StatusCode}): {response.ErrorBody}");
    }

    private async Task<NodeOutput> DeleteAsync(
        NodeInput input, string projectId, string apiBase, string token, CancellationToken ct)
    {
        var tagName = HttpUtility.UrlEncode(GetRequiredConfigValue<string>(input, "tagName"));
        var response = await SendGitLabRequestAsync(
            HttpMethod.Delete, $"projects/{projectId}/repository/tags/{tagName}", apiBase, token, null, ct);

        return response.IsSuccess
            ? SuccessOutput(new { success = true, deleted = tagName })
            : FailureOutput($"Delete failed ({response.StatusCode}): {response.ErrorBody}");
    }
}
