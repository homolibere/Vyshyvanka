using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using FlowForge.Core.Enums;
using FlowForge.Core.Interfaces;

namespace FlowForge.Plugin.AdvancedHttp.Nodes;

/// <summary>
/// HTTP request node with built-in retry and exponential backoff support.
/// </summary>
public class HttpRetryNode : BasePluginNode
{
    private readonly HttpClient? _httpClient;

    public override string Type => "http-retry-request";
    public override NodeCategory Category => NodeCategory.Action;

    public HttpRetryNode() : this(null)
    {
    }

    internal HttpRetryNode(HttpClient? httpClient)
    {
        _httpClient = httpClient;
    }

    public override async Task<NodeOutput> ExecuteAsync(NodeInput input, IExecutionContext context)
    {
        try
        {
            var url = GetRequiredConfigValue<string>(input, "url");
            var method = GetRequiredConfigValue<string>(input, "method").ToUpperInvariant();
            var headers = GetConfigValue<Dictionary<string, string>>(input, "headers");
            var body = GetConfigValue<JsonElement?>(input, "body");
            var timeoutSeconds = GetConfigValue<int?>(input, "timeout") ?? 30;
            var maxRetries = GetConfigValue<int?>(input, "maxRetries") ?? 3;
            var initialDelayMs = GetConfigValue<int?>(input, "initialDelayMs") ?? 1000;
            var maxDelayMs = GetConfigValue<int?>(input, "maxDelayMs") ?? 30000;
            var retryOnStatusCodes =
                GetConfigValue<int[]>(input, "retryOnStatusCodes") ?? [408, 429, 500, 502, 503, 504];

            using var client = _httpClient ?? new HttpClient { Timeout = TimeSpan.FromSeconds(timeoutSeconds) };

            var attempts = new List<AttemptResult>();
            HttpResponseMessage? response = null;
            var currentDelay = initialDelayMs;

            for (var attempt = 1; attempt <= maxRetries + 1; attempt++)
            {
                context.CancellationToken.ThrowIfCancellationRequested();

                var request = CreateRequest(method, url, headers, body);

                if (input.CredentialId.HasValue)
                    await ApplyCredentialsAsync(request, input.CredentialId.Value, context);

                var attemptStart = DateTime.UtcNow;
                try
                {
                    response = await client.SendAsync(request, context.CancellationToken);

                    attempts.Add(new AttemptResult(
                        attempt,
                        (int)response.StatusCode,
                        null,
                        (DateTime.UtcNow - attemptStart).TotalMilliseconds));

                    if (response.IsSuccessStatusCode || !retryOnStatusCodes.Contains((int)response.StatusCode))
                        break;

                    if (attempt <= maxRetries)
                    {
                        await Task.Delay(currentDelay, context.CancellationToken);
                        currentDelay = Math.Min(currentDelay * 2, maxDelayMs);
                    }
                }
                catch (HttpRequestException ex)
                {
                    attempts.Add(new AttemptResult(attempt, null, ex.Message,
                        (DateTime.UtcNow - attemptStart).TotalMilliseconds));

                    if (attempt > maxRetries)
                        return FailureOutput($"All {maxRetries + 1} attempts failed. Last error: {ex.Message}");

                    await Task.Delay(currentDelay, context.CancellationToken);
                    currentDelay = Math.Min(currentDelay * 2, maxDelayMs);
                }
            }

            if (response is null)
                return FailureOutput("No response received after all retry attempts");

            var responseBody = await response.Content.ReadAsStringAsync(context.CancellationToken);
            JsonElement? parsedBody = null;

            try
            {
                if (!string.IsNullOrWhiteSpace(responseBody))
                    parsedBody = JsonSerializer.Deserialize<JsonElement>(responseBody);
            }
            catch (JsonException)
            {
            }

            return SuccessOutput(new
            {
                statusCode = (int)response.StatusCode,
                statusText = response.ReasonPhrase,
                headers = response.Headers.ToDictionary(h => h.Key, h => string.Join(", ", h.Value)),
                body = parsedBody.HasValue
                    ? JsonSerializer.Deserialize<object>(parsedBody.Value.GetRawText())
                    : responseBody,
                isSuccess = response.IsSuccessStatusCode,
                attempts = attempts,
                totalAttempts = attempts.Count
            });
        }
        catch (OperationCanceledException)
        {
            return FailureOutput("Request was cancelled");
        }
        catch (Exception ex)
        {
            return FailureOutput($"HTTP request error: {ex.Message}");
        }
    }

    private static HttpRequestMessage CreateRequest(string method, string url, Dictionary<string, string>? headers,
        JsonElement? body)
    {
        var request = new HttpRequestMessage(GetHttpMethod(method), url);

        if (headers is not null)
        {
            foreach (var (key, value) in headers)
                request.Headers.TryAddWithoutValidation(key, value);
        }

        if (body.HasValue && method is "POST" or "PUT" or "PATCH")
            request.Content = new StringContent(body.Value.GetRawText(), Encoding.UTF8, "application/json");

        return request;
    }

    private static HttpMethod GetHttpMethod(string method) => method switch
    {
        "GET" => HttpMethod.Get,
        "POST" => HttpMethod.Post,
        "PUT" => HttpMethod.Put,
        "DELETE" => HttpMethod.Delete,
        "PATCH" => HttpMethod.Patch,
        _ => throw new ArgumentException($"Unsupported HTTP method: {method}")
    };

    private static async Task ApplyCredentialsAsync(HttpRequestMessage request, Guid credentialId,
        IExecutionContext context)
    {
        var credentials = await context.Credentials.GetCredentialAsync(credentialId, context.CancellationToken);
        if (credentials is null) return;

        if (credentials.TryGetValue("apiKey", out var apiKey))
        {
            credentials.TryGetValue("headerName", out var headerName);
            credentials.TryGetValue("prefix", out var prefix);
            headerName ??= "Authorization";
            prefix ??= "Bearer";
            request.Headers.TryAddWithoutValidation(headerName,
                string.IsNullOrWhiteSpace(prefix) ? apiKey : $"{prefix} {apiKey}");
        }
        else if (credentials.TryGetValue("username", out var username) &&
                 credentials.TryGetValue("password", out var password))
        {
            var authValue = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{username}:{password}"));
            request.Headers.Authorization = new AuthenticationHeaderValue("Basic", authValue);
        }
    }

    private record AttemptResult(int Attempt, int? StatusCode, string? Error, double DurationMs);
}
