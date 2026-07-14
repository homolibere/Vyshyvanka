using Vyshyvanka.Engine.Execution;

namespace Vyshyvanka.Tests.Unit;

public class WebhookResponseWriterTests
{
    [Fact]
    public async Task WhenWriteAsyncCalledThenResponseIsAvailable()
    {
        var sut = new WebhookResponseWriter();
        var headers = new Dictionary<string, string> { ["Content-Type"] = "application/json" };

        await sut.WriteAsync(200, headers, """{"ok":true}""", CancellationToken.None);

        var result = await sut.WaitForResponseAsync(TimeSpan.FromSeconds(5), CancellationToken.None);
        result.Should().NotBeNull();
        result!.StatusCode.Should().Be(200);
        result.Body.Should().Be("""{"ok":true}""");
        result.Headers.Should().ContainKey("Content-Type");
    }

    [Fact]
    public async Task WhenWriteAsyncCalledThenIsResponseSentIsTrue()
    {
        var sut = new WebhookResponseWriter();

        sut.IsResponseSent.Should().BeFalse();

        await sut.WriteAsync(201, null, "created", CancellationToken.None);

        sut.IsResponseSent.Should().BeTrue();
    }

    [Fact]
    public async Task WhenWriteAsyncCalledTwiceThenThrowsInvalidOperationException()
    {
        var sut = new WebhookResponseWriter();
        await sut.WriteAsync(200, null, "first", CancellationToken.None);

        var act = () => sut.WriteAsync(200, null, "second", CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task WhenTimeoutElapsesWithoutWriteThenReturnsNull()
    {
        var sut = new WebhookResponseWriter();

        var result = await sut.WaitForResponseAsync(TimeSpan.FromMilliseconds(50), CancellationToken.None);

        result.Should().BeNull();
    }

    [Fact]
    public async Task WhenCancellationRequestedDuringWaitThenThrowsOperationCancelled()
    {
        var sut = new WebhookResponseWriter();
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var act = () => sut.WaitForResponseAsync(TimeSpan.FromSeconds(5), cts.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public async Task WhenSetFailedCalledThenResponseContainsErrorWith500()
    {
        var sut = new WebhookResponseWriter();

        sut.SetFailed("Something broke");

        var result = await sut.WaitForResponseAsync(TimeSpan.FromSeconds(5), CancellationToken.None);
        result.Should().NotBeNull();
        result!.StatusCode.Should().Be(500);
        result.Body.Should().Be("Something broke");
    }

    [Fact]
    public async Task WhenSetFailedCalledThenIsResponseSentIsTrue()
    {
        var sut = new WebhookResponseWriter();

        sut.SetFailed("error");

        sut.IsResponseSent.Should().BeTrue();
    }

    [Fact]
    public async Task WhenWriteCalledBeforeWaitThenWaitReturnsImmediately()
    {
        var sut = new WebhookResponseWriter();
        await sut.WriteAsync(204, null, null, CancellationToken.None);

        var result = await sut.WaitForResponseAsync(TimeSpan.FromSeconds(5), CancellationToken.None);

        result.Should().NotBeNull();
        result!.StatusCode.Should().Be(204);
        result.Body.Should().BeNull();
    }

    [Fact]
    public async Task WhenWriteCalledWithNullHeadersThenResponseHasNullHeaders()
    {
        var sut = new WebhookResponseWriter();

        await sut.WriteAsync(200, null, "body", CancellationToken.None);

        var result = await sut.WaitForResponseAsync(TimeSpan.FromSeconds(5), CancellationToken.None);
        result!.Headers.Should().BeNull();
    }

    [Fact]
    public async Task WhenCancellationRequestedOnWriteThenThrowsOperationCancelled()
    {
        var sut = new WebhookResponseWriter();
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var act = () => sut.WriteAsync(200, null, "body", cts.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
    }
}
