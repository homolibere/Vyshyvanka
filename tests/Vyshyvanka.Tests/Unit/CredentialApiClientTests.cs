using System.Net;
using Vyshyvanka.Designer.Models;
using Vyshyvanka.Designer.Services;
using Vyshyvanka.Tests.Unit.Helpers;

namespace Vyshyvanka.Tests.Unit;

public class CredentialApiClientTests
{
    private static CredentialApiClient CreateClient(MockHttpHandler handler)
    {
        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("http://localhost/") };
        return new CredentialApiClient(httpClient);
    }

    [Fact]
    public async Task WhenGetCredentialsAsyncThenCallsCorrectEndpoint()
    {
        var json = """[{"id":"00000000-0000-0000-0000-000000000001","name":"My Cred","type":0}]""";
        var handler = new MockHttpHandler(MockHttpHandler.JsonResponse(json));
        var sut = CreateClient(handler);

        var result = await sut.GetCredentialsAsync();

        handler.LastRequest!.RequestUri!.PathAndQuery.Should().Be("/api/credentials");
        result.Should().HaveCount(1);
        result[0].Name.Should().Be("My Cred");
    }

    [Fact]
    public async Task WhenGetCredentialsAsyncFailsThenReturnsEmptyList()
    {
        var handler = new MockHttpHandler(MockHttpHandler.EmptyResponse(HttpStatusCode.Unauthorized));
        var sut = CreateClient(handler);

        var result = await sut.GetCredentialsAsync();

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task WhenGetCredentialAsyncThenCallsCorrectEndpoint()
    {
        var id = Guid.NewGuid();
        var json = $$"""{"id":"{{id}}","name":"Cred","type":0}""";
        var handler = new MockHttpHandler(MockHttpHandler.JsonResponse(json));
        var sut = CreateClient(handler);

        var result = await sut.GetCredentialAsync(id);

        handler.LastRequest!.RequestUri!.PathAndQuery.Should().Be($"/api/credentials/{id}");
        result.Should().NotBeNull();
        result!.Name.Should().Be("Cred");
    }

    [Fact]
    public async Task WhenGetCredentialAsyncNotFoundThenReturnsNull()
    {
        var handler = new MockHttpHandler(MockHttpHandler.EmptyResponse(HttpStatusCode.NotFound));
        var sut = CreateClient(handler);

        var result = await sut.GetCredentialAsync(Guid.NewGuid());

        result.Should().BeNull();
    }

    [Fact]
    public async Task WhenCreateCredentialAsyncThenPostsToCorrectEndpoint()
    {
        var json = """{"id":"00000000-0000-0000-0000-000000000001","name":"New Cred","type":0}""";
        var handler = new MockHttpHandler(MockHttpHandler.JsonResponse(json));
        var sut = CreateClient(handler);

        var model = new CreateCredentialRequest { Name = "New Cred", Type = Vyshyvanka.Core.Enums.CredentialType.ApiKey };
        var result = await sut.CreateCredentialAsync(model);

        handler.LastRequest!.Method.Should().Be(HttpMethod.Post);
        handler.LastRequest.RequestUri!.PathAndQuery.Should().Be("/api/credentials");
        result.Should().NotBeNull();
        result!.Name.Should().Be("New Cred");
    }

    [Fact]
    public async Task WhenCreateCredentialAsyncFailsThenThrowsApiException()
    {
        var handler = new MockHttpHandler(MockHttpHandler.ErrorResponse("VALIDATION", "Name required"));
        var sut = CreateClient(handler);

        var model = new CreateCredentialRequest { Name = "" };

        var act = () => sut.CreateCredentialAsync(model);

        await act.Should().ThrowAsync<ApiException>();
    }

    [Fact]
    public async Task WhenUpdateCredentialAsyncThenPutsToCorrectEndpoint()
    {
        var id = Guid.NewGuid();
        var json = $$"""{"id":"{{id}}","name":"Updated","type":0}""";
        var handler = new MockHttpHandler(MockHttpHandler.JsonResponse(json));
        var sut = CreateClient(handler);

        var model = new UpdateCredentialRequest { Name = "Updated" };
        var result = await sut.UpdateCredentialAsync(id, model);

        handler.LastRequest!.Method.Should().Be(HttpMethod.Put);
        handler.LastRequest.RequestUri!.PathAndQuery.Should().Be($"/api/credentials/{id}");
        result!.Name.Should().Be("Updated");
    }

    [Fact]
    public async Task WhenDeleteCredentialAsyncThenDeletesCorrectEndpoint()
    {
        var id = Guid.NewGuid();
        var handler = new MockHttpHandler(MockHttpHandler.EmptyResponse(HttpStatusCode.OK));
        var sut = CreateClient(handler);

        await sut.DeleteCredentialAsync(id);

        handler.LastRequest!.Method.Should().Be(HttpMethod.Delete);
        handler.LastRequest.RequestUri!.PathAndQuery.Should().Be($"/api/credentials/{id}");
    }

    [Fact]
    public async Task WhenDeleteCredentialAsyncFailsThenThrowsApiException()
    {
        var handler = new MockHttpHandler(MockHttpHandler.ErrorResponse("NOT_FOUND", "Credential not found", HttpStatusCode.NotFound));
        var sut = CreateClient(handler);

        var act = () => sut.DeleteCredentialAsync(Guid.NewGuid());

        await act.Should().ThrowAsync<ApiException>();
    }
}
