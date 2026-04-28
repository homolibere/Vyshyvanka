using System.Web;
using Vyshyvanka.Core.Attributes;
using Vyshyvanka.Core.Enums;
using Vyshyvanka.Core.Interfaces;

namespace Vyshyvanka.Plugin.GitLab.Nodes;

/// <summary>
/// Manages GitLab pipelines — list, get, create, retry, cancel, delete, and list jobs.
/// </summary>
[NodeDefinition(
    Name = "GitLab Pipeline",
    Description = "List, get, trigger, retry, cancel, and inspect GitLab CI/CD pipelines and jobs",
    Icon = "fa-brands fa-gitlab")]
[NodeInput("input", DisplayName = "Input", Type = PortType.Object)]
[NodeOutput("output", DisplayName = "Output", Type = PortType.Object)]
[RequiresCredential(CredentialType.ApiKey)]
[ConfigurationProperty("operation", "string", Description = "Operation: get, getAll, create, retry, cancel, delete, getJobs", IsRequired = true)]
[ConfigurationProperty("projectId", "string", Description = "Project ID or URL-encoded path", IsRequired = true)]
[ConfigurationProperty("pipelineId", "number", Description = "Pipeline ID. Required for get, retry, cancel, delete, getJobs.")]
[ConfigurationProperty("ref", "string", Description = "Branch or tag to run the pipeline on. Required for create.")]
[ConfigurationProperty("variables", "array", Description = "Array of {key, value} objects for pipeline variables (create only).")]
[ConfigurationProperty("status", "string", Description = "Filter for getAll: running, pending, success, failed, canceled, skipped, manual, scheduled.")]
[ConfigurationProperty("source", "string", Description = "Filter for getAll: push, web, trigger, schedule, api, pipeline, merge_request_event.")]
[ConfigurationProperty("ref_filter", "string", Description = "Filter getAll by ref name.")]
[ConfigurationProperty("orderBy", "string", Description = "Order by: id, status, ref, updated_at, user_id (default: id).")]
[ConfigurationProperty("sort", "string", Description = "Sort direction: asc, desc (default: desc).")]
[ConfigurationProperty("perPage", "number", Description = "Results per page (default: 20, max 100).")]
[ConfigurationProperty("page", "number", Description = "Page number (default: 1).")]
[ConfigurationProperty("jobScope", "string", Description = "Filter jobs by scope: created, pending, running, failed, success, canceled, skipped, manual.")]
public class GitLabPipelineNode : BaseGitLabNode
{
    public override string Type => "gitlab-pipeline";
    public override NodeCategory Category => NodeCategory.Action;

    public GitLabPipelineNode() { }
    internal GitLabPipelineNode(HttpClient? httpClient) : base(httpClient) { }

    public override async Task<NodeOutput> ExecuteAsync(NodeInput input, IExecutionContext context)
    {
        try
        {
            var operation = GetRequiredConfigValue<string>(input, "operation").ToLowerInvariant();
            var projectId = EncodeProject(GetRequiredConfigValue<string>(input, "projectId"));
            var (apiBase, token) = await ResolveCredentialsAsync(input, context);

            return operation switch
            {
                "get" => await GetAsync(input, projectId, apiBase, token, context.CancellationToken),
                "getall" => await GetAllAsync(input, projectId, apiBase, token, context.CancellationToken),
                "create" => await CreateAsync(input, projectId, apiBase, token, context.CancellationToken),
                "retry" => await RetryAsync(input, projectId, apiBase, token, context.CancellationToken),
                "cancel" => await CancelAsync(input, projectId, apiBase, token, context.CancellationToken),
                "delete" => await DeleteAsync(input, projectId, apiBase, token, context.CancellationToken),
                "getjobs" => await GetJobsAsync(input, projectId, apiBase, token, context.CancellationToken),
                _ => FailureOutput($"Unsupported operation '{operation}'. Use: get, getAll, create, retry, cancel, delete, getJobs.")
            };
        }
        catch (Exception ex)
        {
            return FailureOutput($"GitLab Pipeline error: {ex.Message}");
        }
    }

    private async Task<NodeOutput> GetAsync(
        NodeInput input, string projectId, string apiBase, string token, CancellationToken ct)
    {
        var pipelineId = GetRequiredConfigValue<int>(input, "pipelineId");
        var response = await SendGitLabRequestAsync(
            HttpMethod.Get, $"projects/{projectId}/pipelines/{pipelineId}", apiBase, token, null, ct);

        return response.IsSuccess
            ? SuccessOutput(new { success = true, pipeline = response.Data })
            : FailureOutput($"Get failed ({response.StatusCode}): {response.ErrorBody}");
    }

    private async Task<NodeOutput> GetAllAsync(
        NodeInput input, string projectId, string apiBase, string token, CancellationToken ct)
    {
        var orderBy = GetConfigValue<string>(input, "orderBy") ?? "id";
        var sort = GetConfigValue<string>(input, "sort") ?? "desc";
        var perPage = Math.Min(GetConfigValue<int?>(input, "perPage") ?? 20, 100);
        var page = GetConfigValue<int?>(input, "page") ?? 1;

        var query = $"projects/{projectId}/pipelines" +
                    $"?order_by={HttpUtility.UrlEncode(orderBy)}" +
                    $"&sort={HttpUtility.UrlEncode(sort)}" +
                    $"&per_page={perPage}&page={page}";

        var status = GetConfigValue<string>(input, "status");
        if (!string.IsNullOrWhiteSpace(status))
            query += $"&status={HttpUtility.UrlEncode(status)}";

        var source = GetConfigValue<string>(input, "source");
        if (!string.IsNullOrWhiteSpace(source))
            query += $"&source={HttpUtility.UrlEncode(source)}";

        var refFilter = GetConfigValue<string>(input, "ref_filter");
        if (!string.IsNullOrWhiteSpace(refFilter))
            query += $"&ref={HttpUtility.UrlEncode(refFilter)}";

        var response = await SendGitLabRequestAsync(HttpMethod.Get, query, apiBase, token, null, ct);

        return response.IsSuccess
            ? SuccessOutput(new { success = true, pipelines = response.Data })
            : FailureOutput($"Get all failed ({response.StatusCode}): {response.ErrorBody}");
    }

    private async Task<NodeOutput> CreateAsync(
        NodeInput input, string projectId, string apiBase, string token, CancellationToken ct)
    {
        var body = new Dictionary<string, object>
        {
            ["ref"] = GetRequiredConfigValue<string>(input, "ref")
        };

        var variables = GetConfigValue<Dictionary<string, string>[]>(input, "variables");
        if (variables is { Length: > 0 })
        {
            body["variables"] = variables.Select(v => new
            {
                key = v.GetValueOrDefault("key") ?? "",
                value = v.GetValueOrDefault("value") ?? "",
                variable_type = "env_var"
            }).ToArray();
        }

        var response = await SendGitLabRequestAsync(
            HttpMethod.Post, $"projects/{projectId}/pipeline", apiBase, token, body, ct);

        return response.IsSuccess
            ? SuccessOutput(new { success = true, pipeline = response.Data })
            : FailureOutput($"Create failed ({response.StatusCode}): {response.ErrorBody}");
    }

    private async Task<NodeOutput> RetryAsync(
        NodeInput input, string projectId, string apiBase, string token, CancellationToken ct)
    {
        var pipelineId = GetRequiredConfigValue<int>(input, "pipelineId");
        var response = await SendGitLabRequestAsync(
            HttpMethod.Post, $"projects/{projectId}/pipelines/{pipelineId}/retry", apiBase, token, new { }, ct);

        return response.IsSuccess
            ? SuccessOutput(new { success = true, pipeline = response.Data })
            : FailureOutput($"Retry failed ({response.StatusCode}): {response.ErrorBody}");
    }

    private async Task<NodeOutput> CancelAsync(
        NodeInput input, string projectId, string apiBase, string token, CancellationToken ct)
    {
        var pipelineId = GetRequiredConfigValue<int>(input, "pipelineId");
        var response = await SendGitLabRequestAsync(
            HttpMethod.Post, $"projects/{projectId}/pipelines/{pipelineId}/cancel", apiBase, token, new { }, ct);

        return response.IsSuccess
            ? SuccessOutput(new { success = true, pipeline = response.Data })
            : FailureOutput($"Cancel failed ({response.StatusCode}): {response.ErrorBody}");
    }

    private async Task<NodeOutput> DeleteAsync(
        NodeInput input, string projectId, string apiBase, string token, CancellationToken ct)
    {
        var pipelineId = GetRequiredConfigValue<int>(input, "pipelineId");
        var response = await SendGitLabRequestAsync(
            HttpMethod.Delete, $"projects/{projectId}/pipelines/{pipelineId}", apiBase, token, null, ct);

        return response.IsSuccess
            ? SuccessOutput(new { success = true, deleted = pipelineId })
            : FailureOutput($"Delete failed ({response.StatusCode}): {response.ErrorBody}");
    }

    private async Task<NodeOutput> GetJobsAsync(
        NodeInput input, string projectId, string apiBase, string token, CancellationToken ct)
    {
        var pipelineId = GetRequiredConfigValue<int>(input, "pipelineId");
        var perPage = Math.Min(GetConfigValue<int?>(input, "perPage") ?? 20, 100);
        var page = GetConfigValue<int?>(input, "page") ?? 1;

        var query = $"projects/{projectId}/pipelines/{pipelineId}/jobs?per_page={perPage}&page={page}";

        var scope = GetConfigValue<string>(input, "jobScope");
        if (!string.IsNullOrWhiteSpace(scope))
            query += $"&scope[]={HttpUtility.UrlEncode(scope)}";

        var response = await SendGitLabRequestAsync(HttpMethod.Get, query, apiBase, token, null, ct);

        return response.IsSuccess
            ? SuccessOutput(new { success = true, jobs = response.Data })
            : FailureOutput($"Get jobs failed ({response.StatusCode}): {response.ErrorBody}");
    }
}
