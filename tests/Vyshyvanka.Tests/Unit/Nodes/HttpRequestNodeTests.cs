using System.Net;
using System.Text.Json;
using Vyshyvanka.Core.Enums;
using Vyshyvanka.Core.Interfaces;
using Vyshyvanka.Engine.Credentials;
using ExecutionContext = Vyshyvanka.Engine.Execution.ExecutionContext;
using Vyshyvanka.Engine.Nodes.Actions;

namespace Vyshyvanka.Tests.Unit.Nodes;

public class HttpRequestNodeTests
{
    private static ExecutionContext CreateContext() =>
        new(Guid.NewGuid(), Guid.NewGuid(), NullCredentialProvider.Instance);

    private static HttpRequestNode CreateNodeWithHandler(HttpMessageHandler handler)
    {
        var client = new HttpClient(handler);
        return new HttpRequestNode(client);
    }

    [Fact]
    public void WhenCreatedThenHasCorrectMetadata()
    {
        var sut = new HttpRequestNode();
        sut.Type.Should().Be("http-request");
        sut.Category.Should().Be(NodeCategory.Action);
        sut.Id.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task WhenGetRequestSucceedsThenReturnsSuccessWithResponseData()
    {
        var handler = new MockHttpHandler(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("""{"message":"hello"}""", System.Text.Encoding.UTF8, "application/json")
        });
        var sut = CreateNodeWithHandler(handler);
        var config = JsonSerializer.SerializeToElement(new
        {
            url = "https://api.example.com/data",
            method = "GET"
        });
        var input = new NodeInput { Data = default, Configuration = config };
        var context = CreateContext();

        var result = await sut.ExecuteAsync(input, context);

        result.Success.Should().BeTrue();
        result.Data.GetProperty("statusCode").GetInt32().Should().Be(200);
        result.Data.GetProperty("isSuccess").GetBoolean().Should().BeTrue();
    }

    [Fact]
    public async Task WhenPostRequestWithBodyThenSendsBody()
    {
        HttpRequestMessage? capturedRequest = null;
        var handler = new MockHttpHandler(new HttpResponseMessage(HttpStatusCode.Created)
        {
            Content = new StringContent("""{"id":1}""", System.Text.Encoding.UTF8, "application/json")
        }, req => capturedRequest = req);
        var sut = CreateNodeWithHandler(handler);
        var config = JsonSerializer.SerializeToElement(new
        {
            url = "https://api.example.com/items",
            method = "POST",
            body = new { name = "new item", price = 9.99 }
        });
        var input = new NodeInput { Data = default, Configuration = config };
        var context = CreateContext();

        var result = await sut.ExecuteAsync(input, context);

        result.Success.Should().BeTrue();
        result.Data.GetProperty("statusCode").GetInt32().Should().Be(201);
        capturedRequest.Should().NotBeNull();
        capturedRequest!.Method.Should().Be(HttpMethod.Post);
        capturedRequest.Content.Should().NotBeNull();
    }

    [Fact]
    public async Task WhenRequestIncludesHeadersThenHeadersAreSent()
    {
        HttpRequestMessage? capturedRequest = null;
        var handler = new MockHttpHandler(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("{}", System.Text.Encoding.UTF8, "application/json")
        }, req => capturedRequest = req);
        var sut = CreateNodeWithHandler(handler);
        var config = JsonSerializer.SerializeToElement(new
        {
            url = "https://api.example.com/data",
            method = "GET",
            headers = new { Authorization = "Bearer token123", Accept = "application/json" }
        });
        var input = new NodeInput { Data = default, Configuration = config };
        var context = CreateContext();

        await sut.ExecuteAsync(input, context);

        capturedRequest.Should().NotBeNull();
        capturedRequest!.Headers.GetValues("Authorization").Should().Contain("Bearer token123");
    }

    [Fact]
    public async Task WhenRequestIncludesQueryParametersThenUrlIsBuiltCorrectly()
    {
        HttpRequestMessage? capturedRequest = null;
        var handler = new MockHttpHandler(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("{}", System.Text.Encoding.UTF8, "application/json")
        }, req => capturedRequest = req);
        var sut = CreateNodeWithHandler(handler);
        var config = JsonSerializer.SerializeToElement(new
        {
            url = "https://api.example.com/search",
            method = "GET",
            queryParameters = new { q = "test", page = "1", limit = "10" }
        });
        var input = new NodeInput { Data = default, Configuration = config };
        var context = CreateContext();

        await sut.ExecuteAsync(input, context);

        capturedRequest.Should().NotBeNull();
        var requestUrl = capturedRequest!.RequestUri!.ToString();
        requestUrl.Should().Contain("q=test");
        requestUrl.Should().Contain("page=1");
        requestUrl.Should().Contain("limit=10");
    }

    [Fact]
    public async Task WhenResponseIsNotJsonThenReturnsBodyAsString()
    {
        var handler = new MockHttpHandler(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("plain text response", System.Text.Encoding.UTF8, "text/plain")
        });
        var sut = CreateNodeWithHandler(handler);
        var config = JsonSerializer.SerializeToElement(new
        {
            url = "https://api.example.com/text",
            method = "GET"
        });
        var input = new NodeInput { Data = default, Configuration = config };
        var context = CreateContext();

        var result = await sut.ExecuteAsync(input, context);

        result.Success.Should().BeTrue();
        result.Data.GetProperty("body").GetString().Should().Be("plain text response");
    }

    [Fact]
    public async Task WhenResponseIs404ThenReturnsSuccessWithErrorStatus()
    {
        var handler = new MockHttpHandler(new HttpResponseMessage(HttpStatusCode.NotFound)
        {
            Content = new StringContent("""{"error":"not found"}""", System.Text.Encoding.UTF8, "application/json")
        });
        var sut = CreateNodeWithHandler(handler);
        var config = JsonSerializer.SerializeToElement(new
        {
            url = "https://api.example.com/missing",
            method = "GET"
        });
        var input = new NodeInput { Data = default, Configuration = config };
        var context = CreateContext();

        var result = await sut.ExecuteAsync(input, context);

        result.Success.Should().BeTrue();
        result.Data.GetProperty("statusCode").GetInt32().Should().Be(404);
        result.Data.GetProperty("isSuccess").GetBoolean().Should().BeFalse();
    }

    [Fact]
    public async Task WhenRequestThrowsHttpExceptionThenReturnsFailure()
    {
        var handler = new MockHttpHandler(new HttpRequestException("Connection refused"));
        var sut = CreateNodeWithHandler(handler);
        var config = JsonSerializer.SerializeToElement(new
        {
            url = "https://api.example.com/data",
            method = "GET"
        });
        var input = new NodeInput { Data = default, Configuration = config };
        var context = CreateContext();

        var result = await sut.ExecuteAsync(input, context);

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("HTTP request failed");
    }

    [Fact]
    public async Task WhenPutRequestThenUsesCorrectMethod()
    {
        HttpRequestMessage? capturedRequest = null;
        var handler = new MockHttpHandler(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("{}", System.Text.Encoding.UTF8, "application/json")
        }, req => capturedRequest = req);
        var sut = CreateNodeWithHandler(handler);
        var config = JsonSerializer.SerializeToElement(new
        {
            url = "https://api.example.com/items/1",
            method = "PUT",
            body = new { name = "updated" }
        });
        var input = new NodeInput { Data = default, Configuration = config };
        var context = CreateContext();

        await sut.ExecuteAsync(input, context);

        capturedRequest!.Method.Should().Be(HttpMethod.Put);
    }

    [Fact]
    public async Task WhenDeleteRequestThenUsesCorrectMethod()
    {
        HttpRequestMessage? capturedRequest = null;
        var handler = new MockHttpHandler(new HttpResponseMessage(HttpStatusCode.NoContent)
        {
            Content = new StringContent("", System.Text.Encoding.UTF8, "application/json")
        }, req => capturedRequest = req);
        var sut = CreateNodeWithHandler(handler);
        var config = JsonSerializer.SerializeToElement(new
        {
            url = "https://api.example.com/items/1",
            method = "DELETE"
        });
        var input = new NodeInput { Data = default, Configuration = config };
        var context = CreateContext();

        await sut.ExecuteAsync(input, context);

        capturedRequest!.Method.Should().Be(HttpMethod.Delete);
    }

    [Fact]
    public async Task WhenUrlIsMissingThenReturnsFailure()
    {
        var handler = new MockHttpHandler(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("{}", System.Text.Encoding.UTF8, "application/json")
        });
        var sut = CreateNodeWithHandler(handler);
        var config = JsonSerializer.SerializeToElement(new { method = "GET" });
        var input = new NodeInput { Data = default, Configuration = config };
        var context = CreateContext();

        var result = await sut.ExecuteAsync(input, context);

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("url");
    }

    [Fact]
    public async Task WhenMethodIsMissingThenReturnsFailure()
    {
        var handler = new MockHttpHandler(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("{}", System.Text.Encoding.UTF8, "application/json")
        });
        var sut = CreateNodeWithHandler(handler);
        var config = JsonSerializer.SerializeToElement(new { url = "https://example.com" });
        var input = new NodeInput { Data = default, Configuration = config };
        var context = CreateContext();

        var result = await sut.ExecuteAsync(input, context);

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("method");
    }

    [Fact]
    public async Task WhenCredentialIdProvidedWithApiKeyThenAppliesAuthHeader()
    {
        HttpRequestMessage? capturedRequest = null;
        var handler = new MockHttpHandler(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("{}", System.Text.Encoding.UTF8, "application/json")
        }, req => capturedRequest = req);
        var sut = CreateNodeWithHandler(handler);

        var credentialId = Guid.NewGuid();
        var credentialProvider = Substitute.For<ICredentialProvider>();
        credentialProvider.GetCredentialAsync(credentialId, Arg.Any<CancellationToken>())
            .Returns(new Dictionary<string, string>
            {
                ["apiKey"] = "my-secret-key",
                ["prefix"] = "Bearer"
            });

        var context = new ExecutionContext(
            Guid.NewGuid(), Guid.NewGuid(), credentialProvider);

        var config = JsonSerializer.SerializeToElement(new
        {
            url = "https://api.example.com/data",
            method = "GET"
        });
        var input = new NodeInput
        {
            Data = default,
            Configuration = config,
            CredentialId = credentialId
        };

        await sut.ExecuteAsync(input, context);

        capturedRequest.Should().NotBeNull();
        capturedRequest!.Headers.GetValues("Authorization").Should().Contain("Bearer my-secret-key");
    }

    [Fact]
    public async Task WhenCredentialIdProvidedWithBasicAuthThenAppliesBasicHeader()
    {
        HttpRequestMessage? capturedRequest = null;
        var handler = new MockHttpHandler(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("{}", System.Text.Encoding.UTF8, "application/json")
        }, req => capturedRequest = req);
        var sut = CreateNodeWithHandler(handler);

        var credentialId = Guid.NewGuid();
        var credentialProvider = Substitute.For<ICredentialProvider>();
        credentialProvider.GetCredentialAsync(credentialId, Arg.Any<CancellationToken>())
            .Returns(new Dictionary<string, string>
            {
                ["username"] = "user",
                ["password"] = "pass"
            });

        var context = new ExecutionContext(
            Guid.NewGuid(), Guid.NewGuid(), credentialProvider);

        var config = JsonSerializer.SerializeToElement(new
        {
            url = "https://api.example.com/data",
            method = "GET"
        });
        var input = new NodeInput
        {
            Data = default,
            Configuration = config,
            CredentialId = credentialId
        };

        await sut.ExecuteAsync(input, context);

        capturedRequest.Should().NotBeNull();
        capturedRequest!.Headers.Authorization.Should().NotBeNull();
        capturedRequest.Headers.Authorization!.Scheme.Should().Be("Basic");
    }

    [Fact]
    public async Task WhenCustomContentTypeThenUsesIt()
    {
        HttpRequestMessage? capturedRequest = null;
        var handler = new MockHttpHandler(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("{}", System.Text.Encoding.UTF8, "application/json")
        }, req => capturedRequest = req);
        var sut = CreateNodeWithHandler(handler);
        var config = JsonSerializer.SerializeToElement(new
        {
            url = "https://api.example.com/data",
            method = "POST",
            body = new { data = "test" },
            contentType = "application/xml"
        });
        var input = new NodeInput { Data = default, Configuration = config };
        var context = CreateContext();

        await sut.ExecuteAsync(input, context);

        capturedRequest!.Content!.Headers.ContentType!.MediaType.Should().Be("application/xml");
    }

    [Fact]
    public async Task WhenResponseIsEmptyThenHandlesGracefully()
    {
        var handler = new MockHttpHandler(new HttpResponseMessage(HttpStatusCode.NoContent)
        {
            Content = new StringContent("", System.Text.Encoding.UTF8, "application/json")
        });
        var sut = CreateNodeWithHandler(handler);
        var config = JsonSerializer.SerializeToElement(new
        {
            url = "https://api.example.com/data",
            method = "DELETE"
        });
        var input = new NodeInput { Data = default, Configuration = config };
        var context = CreateContext();

        var result = await sut.ExecuteAsync(input, context);

        result.Success.Should().BeTrue();
        result.Data.GetProperty("statusCode").GetInt32().Should().Be(204);
    }

    // --- Helper classes ---

    private sealed class MockHttpHandler : HttpMessageHandler
    {
        private readonly HttpResponseMessage? _response;
        private readonly HttpRequestException? _exception;
        private readonly Action<HttpRequestMessage>? _onRequest;

        public MockHttpHandler(HttpResponseMessage response, Action<HttpRequestMessage>? onRequest = null)
        {
            _response = response;
            _onRequest = onRequest;
        }

        public MockHttpHandler(HttpRequestException exception)
        {
            _exception = exception;
        }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            _onRequest?.Invoke(request);

            if (_exception is not null)
                throw _exception;

            return Task.FromResult(_response!);
        }
    }
}
