using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Web;
using Vyshyvanka.Core.Enums;
using Vyshyvanka.Core.Interfaces;

namespace Vyshyvanka.Plugin.GitLab.Nodes;

/// <summary>
/// Base class for all GitLab nodes. Provides shared helpers for authentication,
/// HTTP calls, and response handling against the GitLab REST API v4.
/// </summary>
/// <remarks>
/// Credentials are expected as ApiKey with keys:
/// <c>accessToken</c> — personal access token or OAuth token,
/// <c>baseUrl</c> — GitLab instance URL (defaults to <c>https://gitlab.com</c>).
/// </remarks>
public abstract class BaseGitLabNode : INode
{
    private readonly string _id = Guid.NewGuid().ToString();
    private readonly HttpClient? _httpClient;

    protected BaseGitLabNode() : this(null) { }
    internal BaseGitLabNode(HttpClient? httpClient) { _httpClient = httpClient; }

    public string Id => _id;
    public abstract string Type { get; }
    public abstract NodeCategory Category { get; }
    public abstract Task<NodeOutput> ExecuteAsync(NodeInput input, IExecutionContext context);

    // ── Output helpers ──────────────────────────────────────────────────

    protected static NodeOutput SuccessOutput(object data) => new()
    {
        Data = JsonSerializer.SerializeToElement(data),
        Success = true
    };

    protected static NodeOutput FailureOutput(string errorMessage) => new()
    {
        Data = default,
        Success = false,
        ErrorMessage = errorMessage
    };

    // ── Config helpers ──────────────────────────────────────────────────

    protected static T? GetConfigValue<T>(NodeInput input, string key)
    {
        if (input.Configuration.ValueKind is JsonValueKind.Undefined or JsonValueKind.Null)
            return default;

        return input.Configuration.TryGetProperty(key, out var value)
            ? JsonSerializer.Deserialize<T>(value.GetRawText())
            : default;
    }

    protected static T GetRequiredConfigValue<T>(NodeInput input, string key) =>
        GetConfigValue<T>(input, key)
        ?? throw new InvalidOperationException($"Required configuration '{key}' is missing");

    // ── GitLab credential resolution ────────────────────────────────────

    /// <summary>
    /// Resolves GitLab credentials and returns (apiBaseUrl, accessToken).
    /// </summary>
    protected static async Task<(string ApiBaseUrl, string AccessToken)> ResolveCredentialsAsync(
        NodeInput input, IExecutionContext context)
    {
        if (!input.CredentialId.HasValue)
            throw new InvalidOperationException(
                "GitLab credential is required. Attach an ApiKey credential with accessToken and optional baseUrl.");

        var creds = await context.Credentials.GetCredentialAsync(input.CredentialId.Value, context.CancellationToken)
            ?? throw new InvalidOperationException("Credential not found.");

        var token = creds.TryGetValue("accessToken", out var t) ? t
            : creds.TryGetValue("apiKey", out var k) ? k
            : throw new InvalidOperationException("Credential must contain 'accessToken' (or 'apiKey').");

        var host = creds.TryGetValue("baseUrl", out var b) && !string.IsNullOrWhiteSpace(b)
            ? b.TrimEnd('/')
            : "https://gitlab.com";

        return ($"{host}/api/v4", token);
    }

    // ── GitLab HTTP helpers ─────────────────────────────────────────────

    protected async Task<GitLabApiResponse> SendGitLabRequestAsync(
        HttpMethod method,
        string path,
        string apiBaseUrl,
        string accessToken,
        object? body,
        CancellationToken ct)
    {
        using var client = _httpClient ?? new HttpClient { Timeout = TimeSpan.FromSeconds(30) };

        var url = $"{apiBaseUrl}/{path.TrimStart('/')}";
        using var request = new HttpRequestMessage(method, url);
        request.Headers.TryAddWithoutValidation("PRIVATE-TOKEN", accessToken);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

        if (body is not null && method != HttpMethod.Get)
        {
            request.Content = new StringContent(
                JsonSerializer.Serialize(body),
                Encoding.UTF8,
                "application/json");
        }

        var response = await client.SendAsync(request, ct);
        var responseBody = await response.Content.ReadAsStringAsync(ct);

        JsonElement? parsed = null;
        if (!string.IsNullOrWhiteSpace(responseBody))
        {
            try { parsed = JsonSerializer.Deserialize<JsonElement>(responseBody); }
            catch (JsonException) { /* non-JSON response */ }
        }

        return new GitLabApiResponse(
            (int)response.StatusCode,
            response.IsSuccessStatusCode,
            parsed,
            response.IsSuccessStatusCode ? null : responseBody);
    }

    /// <summary>URL-encodes a project path (e.g. <c>namespace/project</c> → <c>namespace%2Fproject</c>).</summary>
    protected static string EncodeProject(string projectIdOrPath) =>
        int.TryParse(projectIdOrPath, out _)
            ? projectIdOrPath
            : HttpUtility.UrlEncode(projectIdOrPath).Replace("+", "%20");

    protected record GitLabApiResponse(int StatusCode, bool IsSuccess, JsonElement? Data, string? ErrorBody);
}
