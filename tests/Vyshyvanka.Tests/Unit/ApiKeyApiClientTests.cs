using System.Net;
using Vyshyvanka.Designer.Models;
using Vyshyvanka.Designer.Services;
using Vyshyvanka.Tests.Unit.Helpers;

namespace Vyshyvanka.Tests.Unit;

public class ApiKeyApiClientTests
{
    private static ApiKeyApiClient CreateClient(MockHttpHandler handler)
    {
        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("http://localhost/") };
        return new ApiKeyApiClient(httpClient);
    }

    [Fact]
    public async Task WhenGetApiKeysAsyncThenCallsCorrectEndpoint()
    {
        var json = """[{"id":"00000000-0000-0000-0000-000000000001","name":"My Key","isActive":true}]""";
        var handler = new MockHttpHandler(MockHttpHandler.JsonResponse(json));
        var sut = CreateClient(handler);

        var result = await sut.GetApiKeysAsync();

        handler.LastRequest!.RequestUri!.PathAndQuery.Should().Be("/api/apikeys");
        result.Should().HaveCount(1);
    }

    [Fact]
    public async Task WhenGetApiKeysAsyncFailsThenReturnsEmptyList()
    {
        var handler = new MockHttpHandler(MockHttpHandler.EmptyResponse(HttpStatusCode.Unauthorized));
        var sut = CreateClient(handler);

        var result = await sut.GetApiKeysAsync();

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task WhenCreateApiKeyAsyncThenPostsToCorrectEndpoint()
    {
        var json = """{"id":"00000000-0000-0000-0000-000000000001","name":"New Key","key":"vk_abc123","isActive":true}""";
        var handler = new MockHttpHandler(MockHttpHandler.JsonResponse(json));
        var sut = CreateClient(handler);

        var model = new CreateApiKeyRequest { Name = "New Key" };
        var result = await sut.CreateApiKeyAsync(model);

        handler.LastRequest!.Method.Should().Be(HttpMethod.Post);
        handler.LastRequest.RequestUri!.PathAndQuery.Should().Be("/api/apikeys");
        result.Should().NotBeNull();
    }

    [Fact]
    public async Task WhenCreateApiKeyAsyncFailsThenThrowsApiException()
    {
        var handler = new MockHttpHandler(MockHttpHandler.ErrorResponse("VALIDATION", "Name required"));
        var sut = CreateClient(handler);

        var model = new CreateApiKeyRequest { Name = "" };

        var act = () => sut.CreateApiKeyAsync(model);

        await act.Should().ThrowAsync<ApiException>();
    }

    [Fact]
    public async Task WhenRevokeApiKeyAsyncThenPostsToCorrectEndpoint()
    {
        var id = Guid.NewGuid();
        var handler = new MockHttpHandler(MockHttpHandler.EmptyResponse(HttpStatusCode.OK));
        var sut = CreateClient(handler);

        await sut.RevokeApiKeyAsync(id);

        handler.LastRequest!.Method.Should().Be(HttpMethod.Post);
        handler.LastRequest.RequestUri!.PathAndQuery.Should().Be($"/api/apikeys/{id}/revoke");
    }

    [Fact]
    public async Task WhenRevokeApiKeyAsyncFailsThenThrowsApiException()
    {
        var handler = new MockHttpHandler(MockHttpHandler.ErrorResponse("NOT_FOUND", "Key not found", HttpStatusCode.NotFound));
        var sut = CreateClient(handler);

        var act = () => sut.RevokeApiKeyAsync(Guid.NewGuid());

        await act.Should().ThrowAsync<ApiException>();
    }

    [Fact]
    public async Task WhenDeleteApiKeyAsyncThenDeletesCorrectEndpoint()
    {
        var id = Guid.NewGuid();
        var handler = new MockHttpHandler(MockHttpHandler.EmptyResponse(HttpStatusCode.OK));
        var sut = CreateClient(handler);

        await sut.DeleteApiKeyAsync(id);

        handler.LastRequest!.Method.Should().Be(HttpMethod.Delete);
        handler.LastRequest.RequestUri!.PathAndQuery.Should().Be($"/api/apikeys/{id}");
    }

    [Fact]
    public async Task WhenDeleteApiKeyAsyncFailsThenThrowsApiException()
    {
        var handler = new MockHttpHandler(MockHttpHandler.ErrorResponse("NOT_FOUND", "Key not found", HttpStatusCode.NotFound));
        var sut = CreateClient(handler);

        var act = () => sut.DeleteApiKeyAsync(Guid.NewGuid());

        await act.Should().ThrowAsync<ApiException>();
    }
}
