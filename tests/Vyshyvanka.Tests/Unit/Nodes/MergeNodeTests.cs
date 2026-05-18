using System.Text.Json;
using Vyshyvanka.Core.Enums;
using Vyshyvanka.Core.Interfaces;
using Vyshyvanka.Engine.Credentials;
using ExecutionContext = Vyshyvanka.Engine.Execution.ExecutionContext;
using Vyshyvanka.Engine.Nodes.Logic;

namespace Vyshyvanka.Tests.Unit.Nodes;

public class MergeNodeTests
{
    private readonly MergeNode _sut = new();

    private static ExecutionContext CreateContext() =>
        new(Guid.NewGuid(), Guid.NewGuid(), NullCredentialProvider.Instance);

    [Fact]
    public void WhenCreatedThenHasCorrectMetadata()
    {
        _sut.Type.Should().Be("merge");
        _sut.Category.Should().Be(NodeCategory.Logic);
        _sut.Id.Should().NotBeNullOrWhiteSpace();
    }

    // --- PassThrough mode ---

    [Fact]
    public async Task WhenModeIsPassThroughThenForwardsInputAsIs()
    {
        var data = new { name = "test", value = 42 };
        var config = JsonSerializer.SerializeToElement(new { mode = "passThrough" });
        var input = new NodeInput
        {
            Data = JsonSerializer.SerializeToElement(data),
            Configuration = config
        };
        var context = CreateContext();

        var result = await _sut.ExecuteAsync(input, context);

        result.Success.Should().BeTrue();
        result.Data.GetProperty("name").GetString().Should().Be("test");
        result.Data.GetProperty("value").GetInt32().Should().Be(42);
    }

    [Fact]
    public async Task WhenModeIsDefaultThenUsesPassThrough()
    {
        var data = new { key = "value" };
        var input = new NodeInput
        {
            Data = JsonSerializer.SerializeToElement(data),
            Configuration = default
        };
        var context = CreateContext();

        var result = await _sut.ExecuteAsync(input, context);

        result.Success.Should().BeTrue();
        result.Data.GetProperty("key").GetString().Should().Be("value");
    }

    // --- WaitAll mode ---

    [Fact]
    public async Task WhenModeIsWaitAllAndNotAllInputsReceivedThenReturnsWaiting()
    {
        var node = new MergeNode();
        var config =
            JsonSerializer.SerializeToElement(new { mode = "waitAll", combineMode = "array", expectedInputs = 3 });
        var input = new NodeInput
        {
            Data = JsonSerializer.SerializeToElement(new { source = "branch1" }),
            Configuration = config
        };
        var context = CreateContext();

        var result = await node.ExecuteAsync(input, context);

        result.Success.Should().BeTrue();
        result.Data.GetProperty("status").GetString().Should().Be("waiting");
    }

    [Fact]
    public async Task WhenModeIsWaitAllAndAllInputsReceivedThenReturnsCombinedResult()
    {
        var node = new MergeNode();
        var config =
            JsonSerializer.SerializeToElement(new { mode = "waitAll", combineMode = "array", expectedInputs = 2 });
        var context = CreateContext();

        // Simulate engine-merged input with two source ports
        var mergedData = new { input1 = new { a = 1 }, input2 = new { b = 2 } };
        var input = new NodeInput
        {
            Data = JsonSerializer.SerializeToElement(mergedData),
            Configuration = config
        };

        var result = await node.ExecuteAsync(input, context);

        result.Success.Should().BeTrue();
        // When all inputs are received, the result is the combined array
        result.Data.ValueKind.Should().Be(JsonValueKind.Array);
        result.Data.GetArrayLength().Should().Be(2);
    }

    // --- Combine mode with array ---

    [Fact]
    public async Task WhenModeIsCombineWithArrayThenCollectsInputsIntoArray()
    {
        var node = new MergeNode();
        var config = JsonSerializer.SerializeToElement(new { mode = "combine", combineMode = "array" });
        var context = CreateContext();

        var input1 = new NodeInput
        {
            Data = JsonSerializer.SerializeToElement(new { value = 1 }),
            Configuration = config
        };

        var result1 = await node.ExecuteAsync(input1, context);
        result1.Success.Should().BeTrue();
        result1.Data.ValueKind.Should().Be(JsonValueKind.Array);
        result1.Data.GetArrayLength().Should().Be(1);

        var input2 = new NodeInput
        {
            Data = JsonSerializer.SerializeToElement(new { value = 2 }),
            Configuration = config
        };

        var result2 = await node.ExecuteAsync(input2, context);
        result2.Success.Should().BeTrue();
        result2.Data.GetArrayLength().Should().Be(2);
    }

    // --- Combine mode with object ---

    [Fact]
    public async Task WhenModeIsCombineWithObjectThenMergesProperties()
    {
        var node = new MergeNode();
        var config = JsonSerializer.SerializeToElement(new { mode = "combine", combineMode = "object" });
        var context = CreateContext();

        var input1 = new NodeInput
        {
            Data = JsonSerializer.SerializeToElement(new { name = "John" }),
            Configuration = config
        };
        await node.ExecuteAsync(input1, context);

        var input2 = new NodeInput
        {
            Data = JsonSerializer.SerializeToElement(new { age = 30 }),
            Configuration = config
        };

        var result = await node.ExecuteAsync(input2, context);

        result.Success.Should().BeTrue();
        result.Data.GetProperty("name").GetString().Should().Be("John");
        result.Data.GetProperty("age").GetInt32().Should().Be(30);
    }

    [Fact]
    public async Task WhenModeIsCombineWithObjectAndNonObjectInputThenUsesIndexedKey()
    {
        var node = new MergeNode();
        var config = JsonSerializer.SerializeToElement(new { mode = "combine", combineMode = "object" });
        var context = CreateContext();

        var input1 = new NodeInput
        {
            Data = JsonSerializer.SerializeToElement("just a string"),
            Configuration = config
        };

        var result = await node.ExecuteAsync(input1, context);

        result.Success.Should().BeTrue();
        result.Data.TryGetProperty("input1", out _).Should().BeTrue();
    }

    // --- Combine mode with append ---

    [Fact]
    public async Task WhenModeIsCombineWithAppendThenFlattensArrays()
    {
        var node = new MergeNode();
        var config = JsonSerializer.SerializeToElement(new { mode = "combine", combineMode = "append" });
        var context = CreateContext();

        var input1 = new NodeInput
        {
            Data = JsonSerializer.SerializeToElement(new[] { 1, 2, 3 }),
            Configuration = config
        };
        await node.ExecuteAsync(input1, context);

        var input2 = new NodeInput
        {
            Data = JsonSerializer.SerializeToElement(new[] { 4, 5 }),
            Configuration = config
        };

        var result = await node.ExecuteAsync(input2, context);

        result.Success.Should().BeTrue();
        result.Data.GetArrayLength().Should().Be(5);
    }

    [Fact]
    public async Task WhenModeIsCombineWithAppendAndNonArrayInputThenAddsAsElement()
    {
        var node = new MergeNode();
        var config = JsonSerializer.SerializeToElement(new { mode = "combine", combineMode = "append" });
        var context = CreateContext();

        var input1 = new NodeInput
        {
            Data = JsonSerializer.SerializeToElement(new[] { 1, 2 }),
            Configuration = config
        };
        await node.ExecuteAsync(input1, context);

        var input2 = new NodeInput
        {
            Data = JsonSerializer.SerializeToElement(3),
            Configuration = config
        };

        var result = await node.ExecuteAsync(input2, context);

        result.Success.Should().BeTrue();
        result.Data.GetArrayLength().Should().Be(3);
    }

    // --- Edge cases ---

    [Fact]
    public async Task WhenPassThroughWithArrayDataThenForwardsArray()
    {
        var config = JsonSerializer.SerializeToElement(new { mode = "passThrough" });
        var input = new NodeInput
        {
            Data = JsonSerializer.SerializeToElement(new[] { 1, 2, 3 }),
            Configuration = config
        };
        var context = CreateContext();

        var result = await _sut.ExecuteAsync(input, context);

        result.Success.Should().BeTrue();
        result.Data.ValueKind.Should().Be(JsonValueKind.Array);
        result.Data.GetArrayLength().Should().Be(3);
    }
}
