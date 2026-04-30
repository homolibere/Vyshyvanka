using System.Net;
using System.Text.Json;
using Vyshyvanka.Api.Middleware;
using Vyshyvanka.Api.Models;
using Vyshyvanka.Core.Exceptions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace Vyshyvanka.Tests.Unit;

public class ErrorHandlingMiddlewareTests
{
    private readonly ILogger<ErrorHandlingMiddleware> _logger =
        Substitute.For<ILogger<ErrorHandlingMiddleware>>();

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private async Task<(int StatusCode, ApiError Error)> InvokeMiddlewareWithException(Exception exception)
    {
        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();

        var middleware = new ErrorHandlingMiddleware(
            _ => throw exception,
            _logger);

        await middleware.InvokeAsync(context);

        context.Response.Body.Seek(0, SeekOrigin.Begin);
        var body = await new StreamReader(context.Response.Body).ReadToEndAsync();
        var error = JsonSerializer.Deserialize<ApiError>(body, JsonOptions)!;

        return (context.Response.StatusCode, error);
    }

    [Fact]
    public async Task WhenWorkflowNotFoundExceptionThenReturns404()
    {
        var (statusCode, error) = await InvokeMiddlewareWithException(
            new WorkflowNotFoundException(Guid.NewGuid()));

        statusCode.Should().Be((int)HttpStatusCode.NotFound);
        error.Code.Should().Be("WORKFLOW_NOT_FOUND");
    }

    [Fact]
    public async Task WhenExecutionNotFoundExceptionThenReturns404()
    {
        var (statusCode, error) = await InvokeMiddlewareWithException(
            new ExecutionNotFoundException(Guid.NewGuid()));

        statusCode.Should().Be((int)HttpStatusCode.NotFound);
        error.Code.Should().Be("EXECUTION_NOT_FOUND");
    }

    [Fact]
    public async Task WhenCredentialNotFoundExceptionThenReturns404()
    {
        var (statusCode, error) = await InvokeMiddlewareWithException(
            new CredentialNotFoundException(Guid.NewGuid()));

        statusCode.Should().Be((int)HttpStatusCode.NotFound);
        error.Code.Should().Be("CREDENTIAL_NOT_FOUND");
    }

    [Fact]
    public async Task WhenWorkflowValidationExceptionThenReturns400()
    {
        var (statusCode, error) = await InvokeMiddlewareWithException(
            new WorkflowValidationException(["Error 1", "Error 2"]));

        statusCode.Should().Be((int)HttpStatusCode.BadRequest);
        error.Code.Should().Be("WORKFLOW_VALIDATION_FAILED");
        error.Details.Should().NotBeNull();
    }

    [Fact]
    public async Task WhenVersionConflictExceptionThenReturns409()
    {
        var (statusCode, error) = await InvokeMiddlewareWithException(
            new VersionConflictException(1, 2));

        statusCode.Should().Be((int)HttpStatusCode.Conflict);
        error.Code.Should().Be("VERSION_CONFLICT");
    }

    [Fact]
    public async Task WhenWorkflowExecutionExceptionThenReturns500()
    {
        var (statusCode, error) = await InvokeMiddlewareWithException(
            new WorkflowExecutionException(Guid.NewGuid(), "Execution failed"));

        statusCode.Should().Be((int)HttpStatusCode.InternalServerError);
        error.Code.Should().Be("EXECUTION_FAILED");
    }

    [Fact]
    public async Task WhenArgumentNullExceptionThenReturns400()
    {
        var (statusCode, error) = await InvokeMiddlewareWithException(
            new ArgumentNullException("param"));

        statusCode.Should().Be((int)HttpStatusCode.BadRequest);
        error.Code.Should().Be("INVALID_ARGUMENT");
    }

    [Fact]
    public async Task WhenArgumentExceptionThenReturns400()
    {
        var (statusCode, error) = await InvokeMiddlewareWithException(
            new ArgumentException("Bad argument"));

        statusCode.Should().Be((int)HttpStatusCode.BadRequest);
        error.Code.Should().Be("INVALID_ARGUMENT");
    }

    [Fact]
    public async Task WhenInvalidOperationExceptionThenReturns409()
    {
        var (statusCode, error) = await InvokeMiddlewareWithException(
            new InvalidOperationException("Invalid state"));

        statusCode.Should().Be((int)HttpStatusCode.Conflict);
        error.Code.Should().Be("INVALID_OPERATION");
    }

    [Fact]
    public async Task WhenKeyNotFoundExceptionThenReturns404()
    {
        var (statusCode, error) = await InvokeMiddlewareWithException(
            new KeyNotFoundException());

        statusCode.Should().Be((int)HttpStatusCode.NotFound);
        error.Code.Should().Be("RESOURCE_NOT_FOUND");
    }

    [Fact]
    public async Task WhenUnauthorizedAccessExceptionThenReturns401()
    {
        var (statusCode, error) = await InvokeMiddlewareWithException(
            new UnauthorizedAccessException());

        statusCode.Should().Be((int)HttpStatusCode.Unauthorized);
        error.Code.Should().Be("UNAUTHORIZED");
    }

    [Fact]
    public async Task WhenTimeoutExceptionThenReturns504()
    {
        var (statusCode, error) = await InvokeMiddlewareWithException(
            new TimeoutException());

        statusCode.Should().Be((int)HttpStatusCode.GatewayTimeout);
        error.Code.Should().Be("TIMEOUT");
    }

    [Fact]
    public async Task WhenJsonExceptionThenReturns400()
    {
        var (statusCode, error) = await InvokeMiddlewareWithException(
            new JsonException("Bad JSON"));

        statusCode.Should().Be((int)HttpStatusCode.BadRequest);
        error.Code.Should().Be("INVALID_JSON");
    }

    [Fact]
    public async Task WhenUnhandledExceptionThenReturns500()
    {
        var (statusCode, error) = await InvokeMiddlewareWithException(
            new Exception("Something unexpected"));

        statusCode.Should().Be((int)HttpStatusCode.InternalServerError);
        error.Code.Should().Be("INTERNAL_ERROR");
    }

    [Fact]
    public async Task WhenNoExceptionThenPassesThrough()
    {
        var context = new DefaultHttpContext();
        var middleware = new ErrorHandlingMiddleware(
            ctx =>
            {
                ctx.Response.StatusCode = 200;
                return Task.CompletedTask;
            },
            _logger);

        await middleware.InvokeAsync(context);

        context.Response.StatusCode.Should().Be(200);
    }

    [Fact]
    public async Task WhenExceptionOccursThenTraceIdIsIncluded()
    {
        var context = new DefaultHttpContext();
        context.TraceIdentifier = "test-trace-123";
        context.Response.Body = new MemoryStream();

        var middleware = new ErrorHandlingMiddleware(
            _ => throw new Exception("fail"),
            _logger);

        await middleware.InvokeAsync(context);

        context.Response.Body.Seek(0, SeekOrigin.Begin);
        var body = await new StreamReader(context.Response.Body).ReadToEndAsync();
        var error = JsonSerializer.Deserialize<ApiError>(body, JsonOptions)!;

        error.TraceId.Should().Be("test-trace-123");
    }
}
