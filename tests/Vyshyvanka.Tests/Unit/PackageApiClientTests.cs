using System.Net;
using Vyshyvanka.Designer.Services;
using Vyshyvanka.Tests.Unit.Helpers;

namespace Vyshyvanka.Tests.Unit;

public class PackageApiClientTests
{
    private static PackageApiClient CreateClient(MockHttpHandler handler)
    {
        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("http://localhost/") };
        return new PackageApiClient(httpClient);
    }

    [Fact]
    public async Task WhenGetInstalledPackagesAsyncThenCallsCorrectEndpoint()
    {
        var json = """[{"packageId":"Pkg.One","version":"1.0.0","name":"Pkg One","nodeTypes":[]}]""";
        var handler = new MockHttpHandler(MockHttpHandler.JsonResponse(json));
        var sut = CreateClient(handler);

        var result = await sut.GetInstalledPackagesAsync();

        handler.LastRequest!.RequestUri!.PathAndQuery.Should().Be("/api/packages");
        result.Should().HaveCount(1);
    }

    [Fact]
    public async Task WhenGetInstalledPackagesAsyncFailsThenReturnsEmptyList()
    {
        var handler = new MockHttpHandler(MockHttpHandler.EmptyResponse(HttpStatusCode.InternalServerError));
        var sut = CreateClient(handler);

        var result = await sut.GetInstalledPackagesAsync();

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task WhenSearchPackagesAsyncThenBuildsCorrectUrl()
    {
        var json = """{"packages":[],"totalCount":0}""";
        var handler = new MockHttpHandler(MockHttpHandler.JsonResponse(json));
        var sut = CreateClient(handler);

        await sut.SearchPackagesAsync("test query", skip: 10, take: 5);

        var uri = handler.LastRequest!.RequestUri!.PathAndQuery;
        uri.Should().Contain("/api/packages/search");
        uri.Should().Contain("query=test%20query");
        uri.Should().Contain("skip=10");
        uri.Should().Contain("take=5");
    }

    [Fact]
    public async Task WhenInstallPackageAsyncSucceedsThenReturnsSuccess()
    {
        var json = """{"success":true,"packageId":"Pkg.One","version":"1.0.0","nodeTypes":[]}""";
        var handler = new MockHttpHandler(MockHttpHandler.JsonResponse(json));
        var sut = CreateClient(handler);

        var result = await sut.InstallPackageAsync("Pkg.One", "1.0.0");

        handler.LastRequest!.Method.Should().Be(HttpMethod.Post);
        handler.LastRequest.RequestUri!.PathAndQuery.Should().Contain("/api/packages/Pkg.One/install");
        result.Success.Should().BeTrue();
    }

    [Fact]
    public async Task WhenInstallPackageAsyncFailsThenReturnsErrors()
    {
        var json = """{"message":"Package not found","code":"NOT_FOUND"}""";
        var handler = new MockHttpHandler(MockHttpHandler.JsonResponse(json, HttpStatusCode.NotFound));
        var sut = CreateClient(handler);

        var result = await sut.InstallPackageAsync("Nonexistent");

        result.Success.Should().BeFalse();
        result.Errors.Should().NotBeEmpty();
    }

    [Fact]
    public async Task WhenUninstallPackageAsyncThenDeletesCorrectEndpoint()
    {
        var json = """{"success":true}""";
        var handler = new MockHttpHandler(MockHttpHandler.JsonResponse(json));
        var sut = CreateClient(handler);

        var result = await sut.UninstallPackageAsync("Pkg.One", force: true);

        handler.LastRequest!.Method.Should().Be(HttpMethod.Delete);
        handler.LastRequest.RequestUri!.PathAndQuery.Should().Contain("/api/packages/Pkg.One");
        handler.LastRequest.RequestUri.PathAndQuery.Should().Contain("force=True");
    }

    [Fact]
    public async Task WhenCheckForUpdatesAsyncThenGetsCorrectEndpoint()
    {
        var json = """[]""";
        var handler = new MockHttpHandler(MockHttpHandler.JsonResponse(json));
        var sut = CreateClient(handler);

        var result = await sut.CheckForUpdatesAsync();

        handler.LastRequest!.RequestUri!.PathAndQuery.Should().Be("/api/packages/updates");
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task WhenGetPackageSourcesAsyncThenGetsCorrectEndpoint()
    {
        var json = """[{"name":"nuget.org","url":"https://api.nuget.org/v3/index.json","isEnabled":true,"isTrusted":true}]""";
        var handler = new MockHttpHandler(MockHttpHandler.JsonResponse(json));
        var sut = CreateClient(handler);

        var result = await sut.GetPackageSourcesAsync();

        handler.LastRequest!.RequestUri!.PathAndQuery.Should().Be("/api/packages/sources");
        result.Should().HaveCount(1);
    }

    [Fact]
    public async Task WhenTestPackageSourceAsyncThenPostsToCorrectEndpoint()
    {
        var json = """{"success":true,"sourceName":"nuget.org","responseTimeMs":150}""";
        var handler = new MockHttpHandler(MockHttpHandler.JsonResponse(json));
        var sut = CreateClient(handler);

        var result = await sut.TestPackageSourceAsync("nuget.org");

        handler.LastRequest!.Method.Should().Be(HttpMethod.Post);
        handler.LastRequest.RequestUri!.PathAndQuery.Should().Contain("/api/packages/sources/nuget.org/test");
        result.Success.Should().BeTrue();
    }

    [Fact]
    public async Task WhenTestPackageSourceAsyncFailsThenReturnsFailure()
    {
        var handler = new MockHttpHandler(MockHttpHandler.EmptyResponse(HttpStatusCode.ServiceUnavailable));
        var sut = CreateClient(handler);

        var result = await sut.TestPackageSourceAsync("bad-source");

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task WhenRemovePackageSourceAsyncThenDeletesCorrectEndpoint()
    {
        var handler = new MockHttpHandler(MockHttpHandler.EmptyResponse(HttpStatusCode.OK));
        var sut = CreateClient(handler);

        var result = await sut.RemovePackageSourceAsync("my-source");

        handler.LastRequest!.Method.Should().Be(HttpMethod.Delete);
        handler.LastRequest.RequestUri!.PathAndQuery.Should().Contain("/api/packages/sources/my-source");
        result.Should().BeTrue();
    }
}
