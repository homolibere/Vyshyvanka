using System.Text.Json;
using Vyshyvanka.Core.Enums;
using Vyshyvanka.Core.Interfaces;
using Vyshyvanka.Engine.Credentials;
using ExecutionContext = Vyshyvanka.Engine.Execution.ExecutionContext;
using Vyshyvanka.Engine.Nodes.Logic;

namespace Vyshyvanka.Tests.Unit.Nodes;

public class IfNodeTests
{
    private readonly IfNode _sut = new();

    private static ExecutionContext CreateContext() =>
        new(Guid.NewGuid(), Guid.NewGuid(), NullCredentialProvider.Instance);

    private static NodeInput CreateInput(object data, string field, string @operator, object? value = null)
    {
        var config = new Dictionary<string, object?>
        {
            ["field"] = field,
            ["operator"] = @operator
        };
        if (value is not null)
        {
            config["value"] = value;
        }

        return new NodeInput
        {
            Data = JsonSerializer.SerializeToElement(data),
            Configuration = JsonSerializer.SerializeToElement(config)
        };
    }

    [Fact]
    public void WhenCreatedThenHasCorrectMetadata()
    {
        _sut.Type.Should().Be("if");
        _sut.Category.Should().Be(NodeCategory.Logic);
        _sut.Id.Should().NotBeNullOrWhiteSpace();
    }

    // --- Equals operator ---

    [Fact]
    public async Task WhenFieldEqualsValueThenRoutesToTruePort()
    {
        var input = CreateInput(new { status = "active" }, "status", "equals", "active");
        var context = CreateContext();

        var result = await _sut.ExecuteAsync(input, context);

        result.Success.Should().BeTrue();
        result.Data.GetProperty("conditionResult").GetBoolean().Should().BeTrue();
        result.Data.GetProperty("outputPort").GetString().Should().Be("true");
    }

    [Fact]
    public async Task WhenFieldDoesNotEqualValueThenRoutesToFalsePort()
    {
        var input = CreateInput(new { status = "inactive" }, "status", "equals", "active");
        var context = CreateContext();

        var result = await _sut.ExecuteAsync(input, context);

        result.Success.Should().BeTrue();
        result.Data.GetProperty("conditionResult").GetBoolean().Should().BeFalse();
        result.Data.GetProperty("outputPort").GetString().Should().Be("false");
    }

    // --- NotEquals operator ---

    [Fact]
    public async Task WhenFieldNotEqualsValueThenRoutesToTruePort()
    {
        var input = CreateInput(new { status = "inactive" }, "status", "notEquals", "active");
        var context = CreateContext();

        var result = await _sut.ExecuteAsync(input, context);

        result.Success.Should().BeTrue();
        result.Data.GetProperty("conditionResult").GetBoolean().Should().BeTrue();
    }

    [Fact]
    public async Task WhenFieldEqualsValueForNotEqualsThenRoutesToFalsePort()
    {
        var input = CreateInput(new { status = "active" }, "status", "notEquals", "active");
        var context = CreateContext();

        var result = await _sut.ExecuteAsync(input, context);

        result.Success.Should().BeTrue();
        result.Data.GetProperty("conditionResult").GetBoolean().Should().BeFalse();
    }

    // --- GreaterThan operator ---

    [Fact]
    public async Task WhenFieldIsGreaterThanValueThenRoutesToTruePort()
    {
        var input = CreateInput(new { count = 10 }, "count", "greaterThan", 5);
        var context = CreateContext();

        var result = await _sut.ExecuteAsync(input, context);

        result.Success.Should().BeTrue();
        result.Data.GetProperty("conditionResult").GetBoolean().Should().BeTrue();
    }

    [Fact]
    public async Task WhenFieldIsNotGreaterThanValueThenRoutesToFalsePort()
    {
        var input = CreateInput(new { count = 3 }, "count", "greaterThan", 5);
        var context = CreateContext();

        var result = await _sut.ExecuteAsync(input, context);

        result.Success.Should().BeTrue();
        result.Data.GetProperty("conditionResult").GetBoolean().Should().BeFalse();
    }

    // --- LessThan operator ---

    [Fact]
    public async Task WhenFieldIsLessThanValueThenRoutesToTruePort()
    {
        var input = CreateInput(new { count = 3 }, "count", "lessThan", 5);
        var context = CreateContext();

        var result = await _sut.ExecuteAsync(input, context);

        result.Success.Should().BeTrue();
        result.Data.GetProperty("conditionResult").GetBoolean().Should().BeTrue();
    }

    [Fact]
    public async Task WhenFieldIsNotLessThanValueThenRoutesToFalsePort()
    {
        var input = CreateInput(new { count = 10 }, "count", "lessThan", 5);
        var context = CreateContext();

        var result = await _sut.ExecuteAsync(input, context);

        result.Success.Should().BeTrue();
        result.Data.GetProperty("conditionResult").GetBoolean().Should().BeFalse();
    }

    // --- GreaterThanOrEqual operator ---

    [Fact]
    public async Task WhenFieldIsGreaterThanOrEqualThenRoutesToTruePort()
    {
        var input = CreateInput(new { count = 5 }, "count", "greaterThanOrEqual", 5);
        var context = CreateContext();

        var result = await _sut.ExecuteAsync(input, context);

        result.Success.Should().BeTrue();
        result.Data.GetProperty("conditionResult").GetBoolean().Should().BeTrue();
    }

    // --- LessThanOrEqual operator ---

    [Fact]
    public async Task WhenFieldIsLessThanOrEqualThenRoutesToTruePort()
    {
        var input = CreateInput(new { count = 5 }, "count", "lessThanOrEqual", 5);
        var context = CreateContext();

        var result = await _sut.ExecuteAsync(input, context);

        result.Success.Should().BeTrue();
        result.Data.GetProperty("conditionResult").GetBoolean().Should().BeTrue();
    }

    // --- Contains operator ---

    [Fact]
    public async Task WhenFieldContainsValueThenRoutesToTruePort()
    {
        var input = CreateInput(new { message = "Hello World" }, "message", "contains", "World");
        var context = CreateContext();

        var result = await _sut.ExecuteAsync(input, context);

        result.Success.Should().BeTrue();
        result.Data.GetProperty("conditionResult").GetBoolean().Should().BeTrue();
    }

    [Fact]
    public async Task WhenFieldDoesNotContainValueThenRoutesToFalsePort()
    {
        var input = CreateInput(new { message = "Hello World" }, "message", "contains", "Goodbye");
        var context = CreateContext();

        var result = await _sut.ExecuteAsync(input, context);

        result.Success.Should().BeTrue();
        result.Data.GetProperty("conditionResult").GetBoolean().Should().BeFalse();
    }

    [Fact]
    public async Task WhenContainsCheckIsCaseInsensitiveThenMatches()
    {
        var input = CreateInput(new { message = "Hello World" }, "message", "contains", "world");
        var context = CreateContext();

        var result = await _sut.ExecuteAsync(input, context);

        result.Success.Should().BeTrue();
        result.Data.GetProperty("conditionResult").GetBoolean().Should().BeTrue();
    }

    // --- StartsWith operator ---

    [Fact]
    public async Task WhenFieldStartsWithValueThenRoutesToTruePort()
    {
        var input = CreateInput(new { url = "https://example.com" }, "url", "startsWith", "https");
        var context = CreateContext();

        var result = await _sut.ExecuteAsync(input, context);

        result.Success.Should().BeTrue();
        result.Data.GetProperty("conditionResult").GetBoolean().Should().BeTrue();
    }

    [Fact]
    public async Task WhenFieldDoesNotStartWithValueThenRoutesToFalsePort()
    {
        var input = CreateInput(new { url = "http://example.com" }, "url", "startsWith", "https");
        var context = CreateContext();

        var result = await _sut.ExecuteAsync(input, context);

        result.Success.Should().BeTrue();
        result.Data.GetProperty("conditionResult").GetBoolean().Should().BeFalse();
    }

    // --- EndsWith operator ---

    [Fact]
    public async Task WhenFieldEndsWithValueThenRoutesToTruePort()
    {
        var input = CreateInput(new { file = "document.pdf" }, "file", "endsWith", ".pdf");
        var context = CreateContext();

        var result = await _sut.ExecuteAsync(input, context);

        result.Success.Should().BeTrue();
        result.Data.GetProperty("conditionResult").GetBoolean().Should().BeTrue();
    }

    // --- IsEmpty operator ---

    [Fact]
    public async Task WhenFieldIsEmptyStringThenIsEmptyReturnsTrue()
    {
        var input = CreateInput(new { name = "" }, "name", "isEmpty");
        var context = CreateContext();

        var result = await _sut.ExecuteAsync(input, context);

        result.Success.Should().BeTrue();
        result.Data.GetProperty("conditionResult").GetBoolean().Should().BeTrue();
    }

    [Fact]
    public async Task WhenFieldIsNonEmptyStringThenIsEmptyReturnsFalse()
    {
        var input = CreateInput(new { name = "John" }, "name", "isEmpty");
        var context = CreateContext();

        var result = await _sut.ExecuteAsync(input, context);

        result.Success.Should().BeTrue();
        result.Data.GetProperty("conditionResult").GetBoolean().Should().BeFalse();
    }

    // --- IsNotEmpty operator ---

    [Fact]
    public async Task WhenFieldIsNonEmptyThenIsNotEmptyReturnsTrue()
    {
        var input = CreateInput(new { name = "John" }, "name", "isNotEmpty");
        var context = CreateContext();

        var result = await _sut.ExecuteAsync(input, context);

        result.Success.Should().BeTrue();
        result.Data.GetProperty("conditionResult").GetBoolean().Should().BeTrue();
    }

    // --- IsTrue / IsFalse operators ---

    [Fact]
    public async Task WhenFieldIsTrueThenIsTrueReturnsTrue()
    {
        var input = CreateInput(new { active = true }, "active", "isTrue");
        var context = CreateContext();

        var result = await _sut.ExecuteAsync(input, context);

        result.Success.Should().BeTrue();
        result.Data.GetProperty("conditionResult").GetBoolean().Should().BeTrue();
    }

    [Fact]
    public async Task WhenFieldIsFalseThenIsTrueReturnsFalse()
    {
        var input = CreateInput(new { active = false }, "active", "isTrue");
        var context = CreateContext();

        var result = await _sut.ExecuteAsync(input, context);

        result.Success.Should().BeTrue();
        result.Data.GetProperty("conditionResult").GetBoolean().Should().BeFalse();
    }

    [Fact]
    public async Task WhenFieldIsFalseThenIsFalseReturnsTrue()
    {
        var input = CreateInput(new { active = false }, "active", "isFalse");
        var context = CreateContext();

        var result = await _sut.ExecuteAsync(input, context);

        result.Success.Should().BeTrue();
        result.Data.GetProperty("conditionResult").GetBoolean().Should().BeTrue();
    }

    // --- IsNull / IsNotNull operators ---

    [Fact]
    public async Task WhenFieldIsNullThenIsNullReturnsTrue()
    {
        var input = CreateInput(new { value = (object?)null }, "value", "isNull");
        var context = CreateContext();

        var result = await _sut.ExecuteAsync(input, context);

        result.Success.Should().BeTrue();
        result.Data.GetProperty("conditionResult").GetBoolean().Should().BeTrue();
    }

    [Fact]
    public async Task WhenFieldIsNotNullThenIsNotNullReturnsTrue()
    {
        var input = CreateInput(new { value = "something" }, "value", "isNotNull");
        var context = CreateContext();

        var result = await _sut.ExecuteAsync(input, context);

        result.Success.Should().BeTrue();
        result.Data.GetProperty("conditionResult").GetBoolean().Should().BeTrue();
    }

    [Fact]
    public async Task WhenFieldDoesNotExistThenIsNullReturnsTrue()
    {
        var input = CreateInput(new { other = "value" }, "missing", "isNull");
        var context = CreateContext();

        var result = await _sut.ExecuteAsync(input, context);

        result.Success.Should().BeTrue();
        result.Data.GetProperty("conditionResult").GetBoolean().Should().BeTrue();
    }

    // --- Nested field path ---

    [Fact]
    public async Task WhenNestedFieldPathThenEvaluatesCorrectly()
    {
        var data = new { user = new { profile = new { age = 25 } } };
        var input = CreateInput(data, "user.profile.age", "greaterThan", 18);
        var context = CreateContext();

        var result = await _sut.ExecuteAsync(input, context);

        result.Success.Should().BeTrue();
        result.Data.GetProperty("conditionResult").GetBoolean().Should().BeTrue();
    }

    [Fact]
    public async Task WhenNestedFieldPathDoesNotExistThenHandlesGracefully()
    {
        var data = new { user = new { name = "John" } };
        var input = CreateInput(data, "user.profile.age", "isNull");
        var context = CreateContext();

        var result = await _sut.ExecuteAsync(input, context);

        result.Success.Should().BeTrue();
        result.Data.GetProperty("conditionResult").GetBoolean().Should().BeTrue();
    }

    // --- Error handling ---

    [Fact]
    public async Task WhenUnknownOperatorThenReturnsFailure()
    {
        var input = CreateInput(new { value = 1 }, "value", "unknownOp", 2);
        var context = CreateContext();

        var result = await _sut.ExecuteAsync(input, context);

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("Condition evaluation failed");
    }

    [Fact]
    public async Task WhenFieldConfigIsMissingThenReturnsFailure()
    {
        var config = JsonSerializer.SerializeToElement(new { @operator = "equals", value = "test" });
        var input = new NodeInput
        {
            Data = JsonSerializer.SerializeToElement(new { name = "test" }),
            Configuration = config
        };
        var context = CreateContext();

        var act = () => _sut.ExecuteAsync(input, context);

        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    // --- Data passthrough ---

    [Fact]
    public async Task WhenConditionEvaluatedThenOriginalDataIsPassedThrough()
    {
        var originalData = new { id = 1, name = "test", nested = new { key = "value" } };
        var input = CreateInput(originalData, "id", "equals", 1);
        var context = CreateContext();

        var result = await _sut.ExecuteAsync(input, context);

        result.Success.Should().BeTrue();
        result.Data.TryGetProperty("data", out var data).Should().BeTrue();
    }

    // --- Numeric equality ---

    [Fact]
    public async Task WhenNumericFieldEqualsNumericValueThenRoutesToTruePort()
    {
        var input = CreateInput(new { count = 42 }, "count", "equals", 42);
        var context = CreateContext();

        var result = await _sut.ExecuteAsync(input, context);

        result.Success.Should().BeTrue();
        result.Data.GetProperty("conditionResult").GetBoolean().Should().BeTrue();
    }
}
