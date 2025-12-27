using System.Net;
using System.Text;
using System.Text.Json;

namespace FlowForge.Tests.Integration.Designer;

/// <summary>
/// Mock HTTP message handler for testing FlowForgeApiClient.
/// Provides configurable response handling for different HTTP methods and status codes.
/// </summary>
public class MockHttpMessageHandler : HttpMessageHandler
{
    private readonly List<MockResponse> _responses = [];
    private readonly List<HttpRequestMessage> _requests = [];
    private Func<HttpRequestMessage, HttpResponseMessage>? _defaultHandler;

    /// <summary>
    /// Gets all requests that were sent through this handler.
    /// </summary>
    public IReadOnlyList<HttpRequestMessage> Requests => _requests.AsReadOnly();

    /// <summary>
    /// Gets the last request that was sent through this handler.
    /// </summary>
    public HttpRequestMessage? LastRequest => _requests.Count > 0 ? _requests[^1] : null;

    /// <summary>
    /// Creates a new MockHttpMessageHandler with a default handler function.
    /// </summary>
    /// <param name="handler">Function that handles requests and returns responses.</param>
    public MockHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> handler)
    {
        _defaultHandler = handler;
    }

    /// <summary>
    /// Creates a new MockHttpMessageHandler with no default handler.
    /// Use SetupResponse methods to configure responses.
    /// </summary>
    public MockHttpMessageHandler()
    {
    }

    /// <summary>
    /// Sets up a response for a specific HTTP method and URL pattern.
    /// </summary>
    /// <param name="method">HTTP method to match.</param>
    /// <param name="urlPattern">URL pattern to match (supports * wildcard).</param>
    /// <param name="statusCode">HTTP status code to return.</param>
    /// <param name="content">Response content.</param>
    /// <param name="contentType">Content type header value.</param>
    public MockHttpMessageHandler SetupResponse(
        HttpMethod method,
        string urlPattern,
        HttpStatusCode statusCode,
        string content,
        string contentType = "application/json")
    {
        _responses.Add(new MockResponse
        {
            Method = method,
            UrlPattern = urlPattern,
            StatusCode = statusCode,
            Content = content,
            ContentType = contentType
        });
        return this;
    }

    /// <summary>
    /// Sets up a JSON response for a specific HTTP method and URL pattern.
    /// </summary>
    /// <typeparam name="T">Type of the response object.</typeparam>
    /// <param name="method">HTTP method to match.</param>
    /// <param name="urlPattern">URL pattern to match (supports * wildcard).</param>
    /// <param name="responseObject">Object to serialize as JSON response.</param>
    /// <param name="statusCode">HTTP status code to return.</param>
    public MockHttpMessageHandler SetupJsonResponse<T>(
        HttpMethod method,
        string urlPattern,
        T responseObject,
        HttpStatusCode statusCode = HttpStatusCode.OK)
    {
        var json = JsonSerializer.Serialize(responseObject, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });
        return SetupResponse(method, urlPattern, statusCode, json, "application/json");
    }

    /// <summary>
    /// Sets up a GET response for a URL pattern.
    /// </summary>
    public MockHttpMessageHandler SetupGet(string urlPattern, HttpStatusCode statusCode, string content) =>
        SetupResponse(HttpMethod.Get, urlPattern, statusCode, content);

    /// <summary>
    /// Sets up a GET JSON response for a URL pattern.
    /// </summary>
    public MockHttpMessageHandler SetupGetJson<T>(string urlPattern, T responseObject, HttpStatusCode statusCode = HttpStatusCode.OK) =>
        SetupJsonResponse(HttpMethod.Get, urlPattern, responseObject, statusCode);

    /// <summary>
    /// Sets up a POST response for a URL pattern.
    /// </summary>
    public MockHttpMessageHandler SetupPost(string urlPattern, HttpStatusCode statusCode, string content) =>
        SetupResponse(HttpMethod.Post, urlPattern, statusCode, content);

    /// <summary>
    /// Sets up a POST JSON response for a URL pattern.
    /// </summary>
    public MockHttpMessageHandler SetupPostJson<T>(string urlPattern, T responseObject, HttpStatusCode statusCode = HttpStatusCode.OK) =>
        SetupJsonResponse(HttpMethod.Post, urlPattern, responseObject, statusCode);

    /// <summary>
    /// Sets up a PUT response for a URL pattern.
    /// </summary>
    public MockHttpMessageHandler SetupPut(string urlPattern, HttpStatusCode statusCode, string content) =>
        SetupResponse(HttpMethod.Put, urlPattern, statusCode, content);

    /// <summary>
    /// Sets up a PUT JSON response for a URL pattern.
    /// </summary>
    public MockHttpMessageHandler SetupPutJson<T>(string urlPattern, T responseObject, HttpStatusCode statusCode = HttpStatusCode.OK) =>
        SetupJsonResponse(HttpMethod.Put, urlPattern, responseObject, statusCode);

    /// <summary>
    /// Sets up a DELETE response for a URL pattern.
    /// </summary>
    public MockHttpMessageHandler SetupDelete(string urlPattern, HttpStatusCode statusCode, string content = "") =>
        SetupResponse(HttpMethod.Delete, urlPattern, statusCode, content);

    /// <summary>
    /// Sets up an error response for a URL pattern.
    /// </summary>
    public MockHttpMessageHandler SetupError(HttpMethod method, string urlPattern, HttpStatusCode statusCode, string errorMessage)
    {
        var errorContent = JsonSerializer.Serialize(new { error = errorMessage });
        return SetupResponse(method, urlPattern, statusCode, errorContent);
    }

    /// <summary>
    /// Sets up a default handler for unmatched requests.
    /// </summary>
    public MockHttpMessageHandler SetupDefault(Func<HttpRequestMessage, HttpResponseMessage> handler)
    {
        _defaultHandler = handler;
        return this;
    }

    /// <summary>
    /// Clears all configured responses and recorded requests.
    /// </summary>
    public void Clear()
    {
        _responses.Clear();
        _requests.Clear();
    }

    /// <inheritdoc />
    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        _requests.Add(request);

        // Find matching response
        var matchingResponse = _responses.FirstOrDefault(r =>
            r.Method == request.Method &&
            MatchesUrlPattern(request.RequestUri?.PathAndQuery ?? "", r.UrlPattern));

        if (matchingResponse != null)
        {
            return Task.FromResult(CreateResponse(matchingResponse));
        }

        // Use default handler if available
        if (_defaultHandler != null)
        {
            return Task.FromResult(_defaultHandler(request));
        }

        // Return 404 for unmatched requests
        return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound)
        {
            Content = new StringContent(
                JsonSerializer.Serialize(new { error = "No mock response configured" }),
                Encoding.UTF8,
                "application/json")
        });
    }

    private static bool MatchesUrlPattern(string url, string pattern)
    {
        if (pattern == "*")
            return true;

        if (pattern.Contains('*'))
        {
            var parts = pattern.Split('*');
            var index = 0;
            foreach (var part in parts)
            {
                if (string.IsNullOrEmpty(part))
                    continue;

                var foundIndex = url.IndexOf(part, index, StringComparison.OrdinalIgnoreCase);
                if (foundIndex < 0)
                    return false;
                index = foundIndex + part.Length;
            }
            return true;
        }

        return url.Equals(pattern, StringComparison.OrdinalIgnoreCase) ||
               url.StartsWith(pattern, StringComparison.OrdinalIgnoreCase);
    }

    private static HttpResponseMessage CreateResponse(MockResponse mockResponse)
    {
        return new HttpResponseMessage(mockResponse.StatusCode)
        {
            Content = new StringContent(
                mockResponse.Content,
                Encoding.UTF8,
                mockResponse.ContentType)
        };
    }

    private sealed record MockResponse
    {
        public required HttpMethod Method { get; init; }
        public required string UrlPattern { get; init; }
        public required HttpStatusCode StatusCode { get; init; }
        public required string Content { get; init; }
        public required string ContentType { get; init; }
    }
}
