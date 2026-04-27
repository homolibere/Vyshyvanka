using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using FlowForge.Core.Enums;
using FlowForge.Core.Interfaces;

namespace FlowForge.Plugin.Jira.Nodes;

/// <summary>
/// Base class for all Jira nodes. Provides shared helpers for authentication,
/// HTTP calls, and response handling against the Jira Cloud REST API v3.
/// </summary>
/// <remarks>
/// Credentials are expected as BasicAuth with keys: <c>email</c>, <c>apiToken</c>, <c>domain</c>.
/// <c>domain</c> is the Atlassian instance host, e.g. <c>yourcompany.atlassian.net</c>.
/// </remarks>
public abstract class BaseJiraNode : INode
{
    private readonly string _id = Guid.NewGuid().ToString();
    private readonly HttpClient? _httpClient;

    protected BaseJiraNode() : this(null) { }

    internal BaseJiraNode(HttpClient? httpClient)
    {
        _httpClient = httpClient;
    }

    public string Id => _id;
    public abstract string Type { get; }
    public abstract NodeCategory Category { get; }

    public abstract Task<NodeOutput> ExecuteAsync(NodeInput input, IExecutionContext context);

    // ── Helpers ──────────────────────────────────────────────────────────

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

    // ── Jira HTTP helpers ───────────────────────────────────────────────

    /// <summary>
    /// Resolves Jira credentials and returns (baseUrl, authHeader).
    /// </summary>
    protected static async Task<(string BaseUrl, AuthenticationHeaderValue Auth)> ResolveCredentialsAsync(
        NodeInput input, IExecutionContext context)
    {
        if (!input.CredentialId.HasValue)
            throw new InvalidOperationException("Jira credential is required. Attach a BasicAuth credential with email, apiToken, and domain.");

        var creds = await context.Credentials.GetCredentialAsync(input.CredentialId.Value, context.CancellationToken)
            ?? throw new InvalidOperationException("Credential not found.");

        var email = creds.TryGetValue("email", out var e) ? e
            : creds.TryGetValue("username", out var u) ? u
            : throw new InvalidOperationException("Credential must contain 'email' (or 'username').");

        var apiToken = creds.TryGetValue("apiToken", out var t) ? t
            : creds.TryGetValue("password", out var p) ? p
            : throw new InvalidOperationException("Credential must contain 'apiToken' (or 'password').");

        if (!creds.TryGetValue("domain", out var domain) || string.IsNullOrWhiteSpace(domain))
            throw new InvalidOperationException("Credential must contain 'domain' (e.g. yourcompany.atlassian.net).");

        var baseUrl = domain.StartsWith("http", StringComparison.OrdinalIgnoreCase)
            ? domain.TrimEnd('/')
            : $"https://{domain.TrimEnd('/')}";

        var authValue = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{email}:{apiToken}"));
        return (baseUrl, new AuthenticationHeaderValue("Basic", authValue));
    }

    /// <summary>
    /// Sends an authenticated request to the Jira REST API v3 and returns the parsed response.
    /// </summary>
    protected async Task<JiraApiResponse> SendJiraRequestAsync(
        HttpMethod method,
        string path,
        string baseUrl,
        AuthenticationHeaderValue auth,
        object? body,
        CancellationToken ct)
    {
        using var client = _httpClient ?? new HttpClient { Timeout = TimeSpan.FromSeconds(30) };

        var url = $"{baseUrl}/rest/api/3/{path.TrimStart('/')}";
        using var request = new HttpRequestMessage(method, url);
        request.Headers.Authorization = auth;
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

        return new JiraApiResponse(
            (int)response.StatusCode,
            response.IsSuccessStatusCode,
            parsed,
            response.IsSuccessStatusCode ? null : responseBody);
    }

    protected record JiraApiResponse(int StatusCode, bool IsSuccess, JsonElement? Data, string? ErrorBody);
}
