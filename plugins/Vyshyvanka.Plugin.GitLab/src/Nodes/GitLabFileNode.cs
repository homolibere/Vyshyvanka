using System.Web;
using Vyshyvanka.Core.Attributes;
using Vyshyvanka.Core.Enums;
using Vyshyvanka.Core.Interfaces;

namespace Vyshyvanka.Plugin.GitLab.Nodes;

/// <summary>
/// Manages repository files in GitLab — create, get, list, edit, and delete.
/// </summary>
[NodeDefinition(
    Name = "GitLab File",
    Description = "Create, get, list, edit, and delete files in a GitLab repository",
    Icon = "fa-brands fa-gitlab")]
[NodeInput("input", DisplayName = "Input", Type = PortType.Object)]
[NodeOutput("output", DisplayName = "Output", Type = PortType.Object)]
[RequiresCredential(CredentialType.ApiKey)]
[ConfigurationProperty("operation", "string", Description = "Operation: create, get, list, edit, delete", IsRequired = true)]
[ConfigurationProperty("projectId", "string", Description = "Project ID or URL-encoded path", IsRequired = true)]
[ConfigurationProperty("filePath", "string", Description = "Path to the file in the repository. Required for create, get, edit, delete.")]
[ConfigurationProperty("branch", "string", Description = "Branch name (default: main)")]
[ConfigurationProperty("content", "string", Description = "File content. Required for create and edit.")]
[ConfigurationProperty("commitMessage", "string", Description = "Commit message. Required for create, edit, delete.")]
[ConfigurationProperty("encoding", "string", Description = "File encoding: text or base64 (default: text).")]
[ConfigurationProperty("path", "string", Description = "Directory path for list operation (default: root).")]
[ConfigurationProperty("recursive", "boolean", Description = "List files recursively (default: false).")]
[ConfigurationProperty("perPage", "number", Description = "Results per page for list (default: 20, max 100).")]
[ConfigurationProperty("page", "number", Description = "Page number for list (default: 1).")]
public class GitLabFileNode : BaseGitLabNode
{
    public override string Type => "gitlab-file";
    public override NodeCategory Category => NodeCategory.Action;

    public GitLabFileNode() { }
    internal GitLabFileNode(HttpClient? httpClient) : base(httpClient) { }

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
                "list" => await ListAsync(input, projectId, apiBase, token, context.CancellationToken),
                "edit" => await EditAsync(input, projectId, apiBase, token, context.CancellationToken),
                "delete" => await DeleteAsync(input, projectId, apiBase, token, context.CancellationToken),
                _ => FailureOutput($"Unsupported operation '{operation}'. Use: create, get, list, edit, delete.")
            };
        }
        catch (Exception ex)
        {
            return FailureOutput($"GitLab File error: {ex.Message}");
        }
    }

    private async Task<NodeOutput> CreateAsync(
        NodeInput input, string projectId, string apiBase, string token, CancellationToken ct)
    {
        var filePath = HttpUtility.UrlEncode(GetRequiredConfigValue<string>(input, "filePath"));
        var body = new Dictionary<string, object>
        {
            ["branch"] = GetConfigValue<string>(input, "branch") ?? "main",
            ["content"] = GetRequiredConfigValue<string>(input, "content"),
            ["commit_message"] = GetConfigValue<string>(input, "commitMessage") ?? "Create file",
            ["encoding"] = GetConfigValue<string>(input, "encoding") ?? "text"
        };

        var response = await SendGitLabRequestAsync(
            HttpMethod.Post, $"projects/{projectId}/repository/files/{filePath}", apiBase, token, body, ct);

        return response.IsSuccess
            ? SuccessOutput(new { success = true, file = response.Data })
            : FailureOutput($"Create failed ({response.StatusCode}): {response.ErrorBody}");
    }

    private async Task<NodeOutput> GetAsync(
        NodeInput input, string projectId, string apiBase, string token, CancellationToken ct)
    {
        var filePath = HttpUtility.UrlEncode(GetRequiredConfigValue<string>(input, "filePath"));
        var branch = GetConfigValue<string>(input, "branch") ?? "main";

        var response = await SendGitLabRequestAsync(
            HttpMethod.Get, $"projects/{projectId}/repository/files/{filePath}?ref={HttpUtility.UrlEncode(branch)}",
            apiBase, token, null, ct);

        return response.IsSuccess
            ? SuccessOutput(new { success = true, file = response.Data })
            : FailureOutput($"Get failed ({response.StatusCode}): {response.ErrorBody}");
    }

    private async Task<NodeOutput> ListAsync(
        NodeInput input, string projectId, string apiBase, string token, CancellationToken ct)
    {
        var path = GetConfigValue<string>(input, "path") ?? "";
        var branch = GetConfigValue<string>(input, "branch") ?? "main";
        var recursive = GetConfigValue<bool?>(input, "recursive") ?? false;
        var perPage = Math.Min(GetConfigValue<int?>(input, "perPage") ?? 20, 100);
        var page = GetConfigValue<int?>(input, "page") ?? 1;

        var query = $"projects/{projectId}/repository/tree" +
                    $"?ref={HttpUtility.UrlEncode(branch)}" +
                    $"&path={HttpUtility.UrlEncode(path)}" +
                    $"&recursive={recursive.ToString().ToLowerInvariant()}" +
                    $"&per_page={perPage}&page={page}";

        var response = await SendGitLabRequestAsync(HttpMethod.Get, query, apiBase, token, null, ct);

        return response.IsSuccess
            ? SuccessOutput(new { success = true, files = response.Data })
            : FailureOutput($"List failed ({response.StatusCode}): {response.ErrorBody}");
    }

    private async Task<NodeOutput> EditAsync(
        NodeInput input, string projectId, string apiBase, string token, CancellationToken ct)
    {
        var filePath = HttpUtility.UrlEncode(GetRequiredConfigValue<string>(input, "filePath"));
        var body = new Dictionary<string, object>
        {
            ["branch"] = GetConfigValue<string>(input, "branch") ?? "main",
            ["content"] = GetRequiredConfigValue<string>(input, "content"),
            ["commit_message"] = GetConfigValue<string>(input, "commitMessage") ?? "Update file",
            ["encoding"] = GetConfigValue<string>(input, "encoding") ?? "text"
        };

        var response = await SendGitLabRequestAsync(
            HttpMethod.Put, $"projects/{projectId}/repository/files/{filePath}", apiBase, token, body, ct);

        return response.IsSuccess
            ? SuccessOutput(new { success = true, file = response.Data })
            : FailureOutput($"Edit failed ({response.StatusCode}): {response.ErrorBody}");
    }

    private async Task<NodeOutput> DeleteAsync(
        NodeInput input, string projectId, string apiBase, string token, CancellationToken ct)
    {
        var filePath = HttpUtility.UrlEncode(GetRequiredConfigValue<string>(input, "filePath"));
        var body = new Dictionary<string, object>
        {
            ["branch"] = GetConfigValue<string>(input, "branch") ?? "main",
            ["commit_message"] = GetConfigValue<string>(input, "commitMessage") ?? "Delete file"
        };

        var response = await SendGitLabRequestAsync(
            HttpMethod.Delete, $"projects/{projectId}/repository/files/{filePath}", apiBase, token, body, ct);

        return response.IsSuccess
            ? SuccessOutput(new { success = true, deleted = filePath })
            : FailureOutput($"Delete failed ({response.StatusCode}): {response.ErrorBody}");
    }
}
