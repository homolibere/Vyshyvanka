using System.Web;
using Microsoft.Extensions.Logging;
using Vyshyvanka.Core.Attributes;
using Vyshyvanka.Core.Enums;
using Vyshyvanka.Core.Interfaces;

namespace Vyshyvanka.Plugin.GitLab.Nodes;

/// <summary>
/// Manages GitLab releases — create, get, list, update, and delete.
/// </summary>
[NodeDefinition(
    Name = "GitLab Release",
    Description = "Create, get, list, update, and delete GitLab releases",
    Icon = "fa-brands fa-gitlab")]
[NodeInput("input", DisplayName = "Input", Type = PortType.Object)]
[NodeOutput("output", DisplayName = "Output", Type = PortType.Object)]
[RequiresCredential(CredentialType.ApiKey)]
[ConfigurationProperty("operation", "string", Description = "Operation: create, get, getAll, update, delete",
    IsRequired = true, Options = "create,get,getAll,update,delete")]
[ConfigurationProperty("projectId", "string", Description = "Project ID or URL-encoded path", IsRequired = true)]
[ConfigurationProperty("tagName", "string",
    Description = "Tag name for the release. Required for create, get, update, delete.")]
[ConfigurationProperty("name", "string", Description = "Release name.")]
[ConfigurationProperty("description", "string",
    Description = "Release description (Markdown). Also known as release notes.")]
[ConfigurationProperty("ref", "string",
    Description = "Branch or commit SHA to create the tag from. Required for create if tag does not exist.")]
[ConfigurationProperty("milestones", "array", Description = "Array of milestone titles to associate.")]
[ConfigurationProperty("releasedAt", "string", Description = "Release date (ISO 8601).")]
[ConfigurationProperty("perPage", "number", Description = "Results per page for getAll (default: 20, max 100).")]
[ConfigurationProperty("page", "number", Description = "Page number for getAll (default: 1).")]
public class GitLabReleaseNode : BaseGitLabNode
{
    public override string Type => "gitlab-release";
    public override NodeCategory Category => NodeCategory.Action;

    public GitLabReleaseNode()
    {
    }

    internal GitLabReleaseNode(HttpClient? httpClient) : base(httpClient)
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

            logger.LogInformation("GitLab release operation: {Operation} on project {ProjectId}", operation, projectId);

            return operation switch
            {
                "create" => await CreateAsync(input, projectId, apiBase, token, context.CancellationToken),
                "get" => await GetAsync(input, projectId, apiBase, token, context.CancellationToken),
                "getall" => await GetAllAsync(input, projectId, apiBase, token, context.CancellationToken),
                "update" => await UpdateAsync(input, projectId, apiBase, token, context.CancellationToken),
                "delete" => await DeleteAsync(input, projectId, apiBase, token, context.CancellationToken),
                _ => FailureOutput($"Unsupported operation '{operation}'. Use: create, get, getAll, update, delete.")
            };
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "GitLab release operation failed");
            return FailureOutput($"GitLab Release error: {ex.Message}");
        }
    }

    private async Task<NodeOutput> CreateAsync(
        NodeInput input, string projectId, string apiBase, string token, CancellationToken ct)
    {
        var tagName = GetRequiredConfigValue<string>(input, "tagName");
        var body = new Dictionary<string, object> { ["tag_name"] = tagName };

        AddOptional(body, input, "name");
        AddOptional(body, input, "description");
        AddOptional(body, input, "ref", "ref");
        AddOptional(body, input, "releasedAt", "released_at");

        var milestones = GetConfigValue<string[]>(input, "milestones");
        if (milestones is { Length: > 0 })
            body["milestones"] = milestones;

        var response = await SendGitLabRequestAsync(
            HttpMethod.Post, $"projects/{projectId}/releases", apiBase, token, body, ct);

        return response.IsSuccess
            ? SuccessOutput(new { success = true, release = response.Data })
            : FailureOutput($"Create failed ({response.StatusCode}): {response.ErrorBody}");
    }

    private async Task<NodeOutput> GetAsync(
        NodeInput input, string projectId, string apiBase, string token, CancellationToken ct)
    {
        var tagName = HttpUtility.UrlEncode(GetRequiredConfigValue<string>(input, "tagName"));
        var response = await SendGitLabRequestAsync(
            HttpMethod.Get, $"projects/{projectId}/releases/{tagName}", apiBase, token, null, ct);

        return response.IsSuccess
            ? SuccessOutput(new { success = true, release = response.Data })
            : FailureOutput($"Get failed ({response.StatusCode}): {response.ErrorBody}");
    }

    private async Task<NodeOutput> GetAllAsync(
        NodeInput input, string projectId, string apiBase, string token, CancellationToken ct)
    {
        var perPage = Math.Min(GetConfigValue<int?>(input, "perPage") ?? 20, 100);
        var page = GetConfigValue<int?>(input, "page") ?? 1;

        var response = await SendGitLabRequestAsync(
            HttpMethod.Get, $"projects/{projectId}/releases?per_page={perPage}&page={page}",
            apiBase, token, null, ct);

        return response.IsSuccess
            ? SuccessOutput(new { success = true, releases = response.Data })
            : FailureOutput($"Get all failed ({response.StatusCode}): {response.ErrorBody}");
    }

    private async Task<NodeOutput> UpdateAsync(
        NodeInput input, string projectId, string apiBase, string token, CancellationToken ct)
    {
        var tagName = HttpUtility.UrlEncode(GetRequiredConfigValue<string>(input, "tagName"));
        var body = new Dictionary<string, object>();

        AddOptional(body, input, "name");
        AddOptional(body, input, "description");
        AddOptional(body, input, "releasedAt", "released_at");

        var milestones = GetConfigValue<string[]>(input, "milestones");
        if (milestones is { Length: > 0 })
            body["milestones"] = milestones;

        var response = await SendGitLabRequestAsync(
            HttpMethod.Put, $"projects/{projectId}/releases/{tagName}", apiBase, token, body, ct);

        return response.IsSuccess
            ? SuccessOutput(new { success = true, release = response.Data })
            : FailureOutput($"Update failed ({response.StatusCode}): {response.ErrorBody}");
    }

    private async Task<NodeOutput> DeleteAsync(
        NodeInput input, string projectId, string apiBase, string token, CancellationToken ct)
    {
        var tagName = HttpUtility.UrlEncode(GetRequiredConfigValue<string>(input, "tagName"));
        var response = await SendGitLabRequestAsync(
            HttpMethod.Delete, $"projects/{projectId}/releases/{tagName}", apiBase, token, null, ct);

        return response.IsSuccess
            ? SuccessOutput(new { success = true, deleted = tagName })
            : FailureOutput($"Delete failed ({response.StatusCode}): {response.ErrorBody}");
    }

    private static void AddOptional(
        Dictionary<string, object> body, NodeInput input, string configKey, string? apiKey = null)
    {
        var value = GetConfigValue<object>(input, configKey);
        if (value is not null)
            body[apiKey ?? configKey] = value;
    }
}
