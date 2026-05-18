using System.Text.Json;
using Vyshyvanka.Core.Enums;
using Vyshyvanka.Core.Interfaces;
using Vyshyvanka.Engine.Credentials;
using ExecutionContext = Vyshyvanka.Engine.Execution.ExecutionContext;
using Vyshyvanka.Engine.Nodes.Logic;

namespace Vyshyvanka.Tests.Unit.Nodes;

public class LoopNodeTests
{
    private readonly LoopNode _sut = new();

    private static ExecutionContext CreateContext() =>
        new(Guid.NewGuid(), Guid.NewGuid(), NullCredentialProvider.Instance);

    [Fact]
    public void WhenCreatedThenHasCorrectMetadata()
    {
        _sut.Type.Should().Be("loop");
        _sut.Category.Should().Be(NodeCategory.Logic);
        _sut.Id.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task WhenInputIsArrayThenReturnsLoopItemsWithCorrectCount()
    {
        var data = new[] { 1, 2, 3, 4, 5 };
        var input = new NodeInput
        {
            Data = JsonSerializer.SerializeToElement(data),
            Configuration = default
        };
        var context = CreateContext();

        var result = await _sut.ExecuteAsync(input, context);

        result.Success.Should().BeTrue();
        result.Data.GetProperty("totalCount").GetInt32().Should().Be(5);
        result.Data.GetProperty("outputPort").GetString().Should().Be("item");
        result.Data.TryGetProperty("__loopItems", out var loopItems).Should().BeTrue();
        loopItems.GetArrayLength().Should().Be(5);
    }

    [Fact]
    public async Task WhenInputIsEmptyArrayThenReturnsDoneWithZeroCount()
    {
        var data = Array.Empty<int>();
        var input = new NodeInput
        {
            Data = JsonSerializer.SerializeToElement(data),
            Configuration = default
        };
        var context = CreateContext();

        var result = await _sut.ExecuteAsync(input, context);

        result.Success.Should().BeTrue();
        result.Data.GetProperty("totalCount").GetInt32().Should().Be(0);
        result.Data.GetProperty("isComplete").GetBoolean().Should().BeTrue();
        result.Data.GetProperty("outputPort").GetString().Should().Be("done");
    }

    [Fact]
    public async Task WhenFieldPathSpecifiedThenIteratesNestedArray()
    {
        var data = new { results = new[] { "a", "b", "c" } };
        var config = JsonSerializer.SerializeToElement(new { field = "results" });
        var input = new NodeInput
        {
            Data = JsonSerializer.SerializeToElement(data),
            Configuration = config
        };
        var context = CreateContext();

        var result = await _sut.ExecuteAsync(input, context);

        result.Success.Should().BeTrue();
        result.Data.GetProperty("totalCount").GetInt32().Should().Be(3);
        result.Data.GetProperty("outputPort").GetString().Should().Be("item");
    }

    [Fact]
    public async Task WhenFieldPathPointsToNonArrayThenReturnsFailure()
    {
        var data = new { results = "not an array" };
        var config = JsonSerializer.SerializeToElement(new { field = "results" });
        var input = new NodeInput
        {
            Data = JsonSerializer.SerializeToElement(data),
            Configuration = config
        };
        var context = CreateContext();

        var result = await _sut.ExecuteAsync(input, context);

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("Expected array");
    }

    [Fact]
    public async Task WhenInputIsNotArrayAndNoFieldThenReturnsFailure()
    {
        var data = new { name = "not an array" };
        var input = new NodeInput
        {
            Data = JsonSerializer.SerializeToElement(data),
            Configuration = default
        };
        var context = CreateContext();

        var result = await _sut.ExecuteAsync(input, context);

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("Expected array");
    }

    [Fact]
    public async Task WhenInputIsArrayOfObjectsThenPreservesObjectStructure()
    {
        var data = new[]
        {
            new { id = 1, name = "first" },
            new { id = 2, name = "second" }
        };
        var input = new NodeInput
        {
            Data = JsonSerializer.SerializeToElement(data),
            Configuration = default
        };
        var context = CreateContext();

        var result = await _sut.ExecuteAsync(input, context);

        result.Success.Should().BeTrue();
        result.Data.GetProperty("totalCount").GetInt32().Should().Be(2);
        result.Data.GetProperty("__loopItems").GetArrayLength().Should().Be(2);
    }

    [Fact]
    public async Task WhenNestedFieldPathThenNavigatesCorrectly()
    {
        var data = new { response = new { data = new { items = new[] { 10, 20, 30 } } } };
        var config = JsonSerializer.SerializeToElement(new { field = "response.data.items" });
        var input = new NodeInput
        {
            Data = JsonSerializer.SerializeToElement(data),
            Configuration = config
        };
        var context = CreateContext();

        var result = await _sut.ExecuteAsync(input, context);

        result.Success.Should().BeTrue();
        result.Data.GetProperty("totalCount").GetInt32().Should().Be(3);
    }

    [Fact]
    public async Task WhenFieldPathDoesNotExistThenReturnsFailure()
    {
        var data = new { other = "value" };
        var config = JsonSerializer.SerializeToElement(new { field = "missing.path" });
        var input = new NodeInput
        {
            Data = JsonSerializer.SerializeToElement(data),
            Configuration = config
        };
        var context = CreateContext();

        var result = await _sut.ExecuteAsync(input, context);

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("Expected array");
    }

    [Fact]
    public async Task WhenInputIsSingleElementArrayThenReturnsOneItem()
    {
        var data = new[] { "only-one" };
        var input = new NodeInput
        {
            Data = JsonSerializer.SerializeToElement(data),
            Configuration = default
        };
        var context = CreateContext();

        var result = await _sut.ExecuteAsync(input, context);

        result.Success.Should().BeTrue();
        result.Data.GetProperty("totalCount").GetInt32().Should().Be(1);
        result.Data.GetProperty("__loopItems").GetArrayLength().Should().Be(1);
    }
}
