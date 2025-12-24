using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using FlowForge.Core.Interfaces;
using FlowForge.Engine.Nodes.Base;
using FlowForge.Engine.Registry;

namespace FlowForge.Engine.Nodes.Actions;

/// <summary>
/// An action node that makes HTTP requests to external services.
/// Supports GET, POST, PUT, DELETE methods with headers, body, and query parameters.
/// </summary>
[NodeDefinition(
    Name = "HTTP Request",
    Description = "Make HTTP requests to external APIs and services",
    Icon = "globe")]
[NodeInput("input", DisplayName = "Input", IsRequired = false)]
[NodeOutput("output", DisplayName = "Response")]
[ConfigurationProperty("url", "string", Description = "The URL to send the request to", IsRequired = true)]
[ConfigurationProperty("method", "string", Description = "HTTP method (GET, POST, PUT, DELETE)", IsRequired = true)]
[ConfigurationProperty("headers", "object", Description = "HTTP headers to include")]
[ConfigurationProperty("body", "object", Description = "Request body (for POST/PUT)")]
[ConfigurationProperty("queryParameters", "object", Description = "Query string parameters")]
[ConfigurationProperty("timeout", "number", Description = "Request timeout in seconds")]
[ConfigurationProperty("contentType", "string", Description = "Content-Type header value")]
public class HttpRequestNode : BaseActionNode
{
    private readonly string _id = Guid.NewGuid().ToString();
    private readonly HttpClient? _httpClient;

    /// <inheritdoc />
    public override string Id => _id;

    /// <inheritdoc />
    public override string Type => "http-request";

    /// <summary>
    /// Creates a new HttpRequestNode with default HttpClient.
    /// </summary>
    public HttpRequestNode() : this(null)
    {
    }

    /// <summary>
    /// Creates a new HttpRequestNode with a custom HttpClient (for testing).
    /// </summary>
    internal HttpRequestNode(HttpClient? httpClient)
    {
        _httpClient = httpClient;
    }

    /// <inheritdoc />
    public override async Task<NodeOutput> ExecuteAsync(NodeInput input, IExecutionContext context)
    {
        try
        {
            var url = GetRequiredConfigValue<string>(input, "url");
            var method = GetRequiredConfigValue<string>(input, "method").ToUpperInvariant();
            var headers = GetConfigValue<Dictionary<string, string>>(input, "headers");
            var body = GetConfigValue<JsonElement?>(input, "body");
            var queryParams = GetConfigValue<Dictionary<string, string>>(input, "queryParameters");
            var timeoutSeconds = GetConfigValue<int?>(input, "timeout") ?? 30;
            var contentType = GetConfigValue<string>(input, "contentType") ?? "application/json";

            // Build URL with query parameters
            var requestUrl = BuildUrlWithQueryParams(url, queryParams);

            // Create HTTP client (use injected or create new)
            using var client = _httpClient ?? new HttpClient();
            client.Timeout = TimeSpan.FromSeconds(timeoutSeconds);

            // Create request
            var request = new HttpRequestMessage(GetHttpMethod(method), requestUrl);

            // Add headers
            if (headers is not null)
            {
                foreach (var (key, value) in headers)
                {
                    request.Headers.TryAddWithoutValidation(key, value);
                }
            }

            // Apply credentials if provided
            if (input.CredentialId.HasValue)
            {
                await ApplyCredentialsAsync(request, input.CredentialId.Value, context);
            }

            // Add body for POST/PUT/PATCH
            if (body.HasValue && method is "POST" or "PUT" or "PATCH")
            {
                var bodyContent = body.Value.GetRawText();
                request.Content = new StringContent(bodyContent, Encoding.UTF8, contentType);
            }

            // Send request
            var response = await client.SendAsync(request, context.CancellationToken);

            // Build response object
            var responseBody = await response.Content.ReadAsStringAsync(context.CancellationToken);
            JsonElement? parsedBody = null;

            try
            {
                if (!string.IsNullOrWhiteSpace(responseBody))
                {
                    parsedBody = JsonSerializer.Deserialize<JsonElement>(responseBody);
                }
            }
            catch (JsonException)
            {
                // Response is not JSON, keep as string
            }

            var result = new Dictionary<string, object?>
            {
                ["statusCode"] = (int)response.StatusCode,
                ["statusText"] = response.ReasonPhrase,
                ["headers"] = response.Headers.ToDictionary(h => h.Key, h => string.Join(", ", h.Value)),
                ["body"] = parsedBody.HasValue
                    ? JsonSerializer.Deserialize<object>(parsedBody.Value.GetRawText())
                    : responseBody,
                ["isSuccess"] = response.IsSuccessStatusCode
            };

            return SuccessOutput(result);
        }
        catch (HttpRequestException ex)
        {
            return FailureOutput($"HTTP request failed: {ex.Message}");
        }
        catch (TaskCanceledException ex) when (ex.InnerException is TimeoutException)
        {
            return FailureOutput("HTTP request timed out");
        }
        catch (TaskCanceledException)
        {
            return FailureOutput("HTTP request was cancelled");
        }
        catch (Exception ex)
        {
            return FailureOutput($"HTTP request error: {ex.Message}");
        }
    }

    private static string BuildUrlWithQueryParams(string url, Dictionary<string, string>? queryParams)
    {
        if (queryParams is null || queryParams.Count == 0)
            return url;

        var uriBuilder = new UriBuilder(url);
        var query = System.Web.HttpUtility.ParseQueryString(uriBuilder.Query);

        foreach (var (key, value) in queryParams)
        {
            query[key] = value;
        }

        uriBuilder.Query = query.ToString();
        return uriBuilder.ToString();
    }

    private static HttpMethod GetHttpMethod(string method)
    {
        return method.ToUpperInvariant() switch
        {
            "GET" => HttpMethod.Get,
            "POST" => HttpMethod.Post,
            "PUT" => HttpMethod.Put,
            "DELETE" => HttpMethod.Delete,
            "PATCH" => HttpMethod.Patch,
            "HEAD" => HttpMethod.Head,
            "OPTIONS" => HttpMethod.Options,
            _ => throw new ArgumentException($"Unsupported HTTP method: {method}")
        };
    }

    private static async Task ApplyCredentialsAsync(
        HttpRequestMessage request,
        Guid credentialId,
        IExecutionContext context)
    {
        var credentials = await context.Credentials.GetCredentialAsync(credentialId, context.CancellationToken);

        if (credentials is null)
            return;

        // Determine credential type and apply accordingly
        if (credentials.TryGetValue("apiKey", out var apiKey))
        {
            // API Key - check for header name or default to Authorization
            var headerName = credentials.TryGetValue("headerName", out var hn) ? hn : "Authorization";
            var prefix = credentials.TryGetValue("prefix", out var p) ? p : "Bearer";
            var headerValue = string.IsNullOrWhiteSpace(prefix) ? apiKey : $"{prefix} {apiKey}";
            request.Headers.TryAddWithoutValidation(headerName, headerValue);
        }
        else if (credentials.TryGetValue("username", out var username) &&
                 credentials.TryGetValue("password", out var password))
        {
            // Basic Auth
            var authValue = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{username}:{password}"));
            request.Headers.Authorization = new AuthenticationHeaderValue("Basic", authValue);
        }
        else
        {
            // Custom Headers - add all credential values as headers
            foreach (var (key, value) in credentials)
            {
                if (!string.IsNullOrWhiteSpace(key) && !string.IsNullOrWhiteSpace(value))
                {
                    request.Headers.TryAddWithoutValidation(key, value);
                }
            }
        }
    }
}
