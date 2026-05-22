using System.Net;

namespace Vyshyvanka.Tests.Unit.Helpers;

/// <summary>
/// Reusable mock HTTP message handler for API client tests.
/// Captures the request and returns a preconfigured response.
/// </summary>
internal sealed class MockHttpHandler : HttpMessageHandler
{
    private readonly Func<HttpRequestMessage, HttpResponseMessage> _handler;
    private readonly List<HttpRequestMessage> _requests = [];

    public IReadOnlyList<HttpRequestMessage> Requests => _requests;
    public HttpRequestMessage? LastRequest => _requests.Count > 0 ? _requests[^1] : null;

    public MockHttpHandler(HttpResponseMessage response)
    {
        _handler = _ => response;
    }

    public MockHttpHandler(Func<HttpRequestMessage, HttpResponseMessage> handler)
    {
        _handler = handler;
    }

    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        _requests.Add(request);
        return Task.FromResult(_handler(request));
    }

    public static HttpResponseMessage JsonResponse(string json, HttpStatusCode status = HttpStatusCode.OK) =>
        new(status)
        {
            Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json")
        };

    public static HttpResponseMessage EmptyResponse(HttpStatusCode status = HttpStatusCode.NoContent) =>
        new(status);

    public static HttpResponseMessage ErrorResponse(string code, string message, HttpStatusCode status = HttpStatusCode.BadRequest) =>
        JsonResponse($$"""{"code":"{{code}}","message":"{{message}}"}""", status);
}
