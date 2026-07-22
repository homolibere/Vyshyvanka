using System.Text.Json;
using Vyshyvanka.Core.Interfaces;
using Vyshyvanka.Engine.Expressions;
using EngineExecutionContext = Vyshyvanka.Engine.Execution.ExecutionContext;

namespace Vyshyvanka.Tests.Unit;

public class ExpressionEvaluatorTests
{
    private readonly ExpressionEvaluator _sut = new();

    private static IExecutionContext CreateContext(
        Dictionary<string, JsonElement>? nodeOutputs = null,
        Dictionary<string, object>? variables = null)
    {
        var credentialProvider = Substitute.For<ICredentialProvider>();
        var context = new EngineExecutionContext(
            Guid.NewGuid(),
            Guid.NewGuid(),
            credentialProvider);

        if (nodeOutputs is not null)
        {
            foreach (var (nodeId, output) in nodeOutputs)
            {
                context.NodeOutputs.Set(nodeId, output);
            }
        }

        if (variables is not null)
        {
            foreach (var (key, value) in variables)
            {
                context.Variables[key] = value;
            }
        }

        return context;
    }

    private static JsonElement ToJson(object value) =>
        JsonSerializer.SerializeToElement(value);

    // --- Plain text (no expressions) ---

    [Fact]
    public void WhenExpressionHasNoBracesThenReturnsOriginalString()
    {
        var context = CreateContext();

        var result = _sut.Evaluate("hello world", context);

        result.Should().Be("hello world");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void WhenExpressionIsEmptyOrWhitespaceThenReturnsAsIs(string expression)
    {
        var context = CreateContext();

        var result = _sut.Evaluate(expression, context);

        result.Should().Be(expression);
    }

    [Fact]
    public void WhenExpressionIsNullThenThrowsArgumentNullException()
    {
        var context = CreateContext();

        var act = () => _sut.Evaluate(null!, context);

        act.Should().Throw<ArgumentNullException>();
    }

    // --- Node output access ---

    [Fact]
    public void WhenAccessingNodeOutputThenReturnsValue()
    {
        var context = CreateContext(nodeOutputs: new()
        {
            ["httpNode"] = ToJson(new { statusCode = 200, body = "OK" })
        });

        var result = _sut.Evaluate("{{ nodes.httpNode.statusCode }}", context);

        result.Should().Be(200);
    }

    [Fact]
    public void WhenAccessingNestedNodeOutputThenReturnsValue()
    {
        var context = CreateContext(nodeOutputs: new()
        {
            ["apiCall"] = ToJson(new { response = new { data = new { name = "test" } } })
        });

        var result = _sut.Evaluate("{{ nodes.apiCall.response.data.name }}", context);

        result.Should().Be("test");
    }

    [Fact]
    public void WhenAccessingArrayIndexThenReturnsValue()
    {
        var context = CreateContext(nodeOutputs: new()
        {
            ["listNode"] = ToJson(new { items = new[] { "first", "second", "third" } })
        });

        var result = _sut.Evaluate("{{ nodes.listNode.items[1] }}", context);

        result.Should().Be("second");
    }

    [Fact]
    public void WhenNodeHasNoOutputThenThrowsExpressionEvaluationException()
    {
        var context = CreateContext();

        var act = () => _sut.Evaluate("{{ nodes.missing.data }}", context);

        act.Should().Throw<ExpressionEvaluationException>();
    }

    // --- Variable access ---

    [Fact]
    public void WhenAccessingVariableThenReturnsValue()
    {
        var context = CreateContext(variables: new()
        {
            ["input"] = ToJson(new { message = "hello" })
        });

        var result = _sut.Evaluate("{{ variables.input.message }}", context);

        result.Should().Be("hello");
    }

    [Fact]
    public void WhenVariableDoesNotExistThenThrowsExpressionEvaluationException()
    {
        var context = CreateContext();

        var act = () => _sut.Evaluate("{{ variables.missing }}", context);

        act.Should().Throw<ExpressionEvaluationException>();
    }

    // --- String interpolation ---

    [Fact]
    public void WhenMultipleExpressionsInStringThenInterpolatesAll()
    {
        var context = CreateContext(nodeOutputs: new()
        {
            ["node1"] = ToJson(new { name = "Alice" }),
            ["node2"] = ToJson(new { greeting = "Hello" })
        });

        var result = _sut.Evaluate("{{ nodes.node2.greeting }}, {{ nodes.node1.name }}!", context);

        result.Should().Be("Hello, Alice!");
    }

    [Fact]
    public void WhenSingleExpressionSpansEntireStringThenReturnsActualType()
    {
        var context = CreateContext(nodeOutputs: new()
        {
            ["node1"] = ToJson(new { count = 42 })
        });

        var result = _sut.Evaluate("{{ nodes.node1.count }}", context);

        result.Should().Be(42);
    }

    // --- Function calls ---

    [Fact]
    public void WhenCallingToUpperThenReturnsUppercaseString()
    {
        var context = CreateContext(nodeOutputs: new()
        {
            ["node1"] = ToJson(new { text = "hello" })
        });

        var result = _sut.Evaluate("{{ toUpper(nodes.node1.text) }}", context);

        result.Should().Be("HELLO");
    }

    [Fact]
    public void WhenCallingUnknownFunctionThenThrowsExpressionEvaluationException()
    {
        var context = CreateContext(nodeOutputs: new()
        {
            ["node1"] = ToJson(new { text = "hello" })
        });

        var act = () => _sut.Evaluate("{{ unknownFunc(nodes.node1.text) }}", context);

        act.Should().Throw<ExpressionEvaluationException>();
    }

    // --- Validation ---

    [Fact]
    public void WhenValidatingValidExpressionThenReturnsValid()
    {
        var result = _sut.Validate("{{ nodes.myNode.data }}");

        result.IsValid.Should().BeTrue();
        result.Errors.Should().BeEmpty();
    }

    [Fact]
    public void WhenValidatingExpressionWithUnknownRootThenReturnsInvalid()
    {
        var result = _sut.Validate("{{ unknown.path }}");

        result.IsValid.Should().BeFalse();
        result.Errors.Should().NotBeEmpty();
    }

    [Fact]
    public void WhenValidatingIncompleteExpressionThenReturnsInvalid()
    {
        var result = _sut.Validate("{{ nodes }}");

        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public void WhenValidatingPlainTextThenReturnsValid()
    {
        var result = _sut.Validate("just plain text");

        result.IsValid.Should().BeTrue();
    }

    // --- TryEvaluate ---

    [Fact]
    public void WhenTryEvaluateSucceedsThenReturnsTrue()
    {
        var context = CreateContext(nodeOutputs: new()
        {
            ["node1"] = ToJson(new { value = "test" })
        });

        var success = _sut.TryEvaluate("{{ nodes.node1.value }}", context, out var result, out var error);

        success.Should().BeTrue();
        result.Should().Be("test");
        error.Should().BeNull();
    }

    [Fact]
    public void WhenTryEvaluateFailsThenReturnsFalseWithError()
    {
        var context = CreateContext();

        var success = _sut.TryEvaluate("{{ nodes.missing.data }}", context, out var result, out var error);

        success.Should().BeFalse();
        error.Should().NotBeNullOrEmpty();
    }

    // --- Null-safe navigation ---

    [Fact]
    public void WhenAccessingPropertyOnNullValueThenReturnsNull()
    {
        var context = CreateContext(nodeOutputs: new()
        {
            ["webhook"] = ToJson(new { body = (object?)null })
        });

        var result = _sut.Evaluate("{{ nodes.webhook.body.object_kind }}", context);

        result.Should().BeNull();
    }

    [Fact]
    public void WhenAccessingNestedPropertyOnNullIntermediateThenReturnsNull()
    {
        var context = CreateContext(nodeOutputs: new()
        {
            ["api"] = ToJson(new { response = new { data = (object?)null } })
        });

        var result = _sut.Evaluate("{{ nodes.api.response.data.items.count }}", context);

        result.Should().BeNull();
    }

    [Fact]
    public void WhenAccessingMissingPropertyOnObjectThenReturnsNull()
    {
        var context = CreateContext(nodeOutputs: new()
        {
            ["node1"] = ToJson(new { name = "test" })
        });

        var result = _sut.Evaluate("{{ nodes.node1.nonExistentField }}", context);

        result.Should().BeNull();
    }

    [Fact]
    public void WhenAccessingPropertyOnEmptyObjectThenReturnsNull()
    {
        var context = CreateContext(nodeOutputs: new()
        {
            ["switch"] = ToJson(new { })
        });

        var result = _sut.Evaluate("{{ nodes.switch.body.object_kind }}", context);

        result.Should().BeNull();
    }

    [Fact]
    public void WhenArrayIndexOutOfBoundsThenReturnsNull()
    {
        var context = CreateContext(nodeOutputs: new()
        {
            ["node1"] = ToJson(new { items = new[] { "a", "b" } })
        });

        var result = _sut.Evaluate("{{ nodes.node1.items[5] }}", context);

        result.Should().BeNull();
    }

    [Fact]
    public void WhenNonNumericIndexOnArrayThenReturnsNull()
    {
        var context = CreateContext(nodeOutputs: new()
        {
            ["node1"] = ToJson(new { items = new[] { "a", "b" } })
        });

        var result = _sut.Evaluate("{{ nodes.node1.items.name }}", context);

        result.Should().BeNull();
    }

    [Fact]
    public void WhenNavigatingIntoScalarValueThenReturnsNull()
    {
        var context = CreateContext(nodeOutputs: new()
        {
            ["node1"] = ToJson(new { count = 42 })
        });

        var result = _sut.Evaluate("{{ nodes.node1.count.something }}", context);

        result.Should().BeNull();
    }

    [Fact]
    public void WhenTryEvaluateWithNullPathThenSucceedsWithNull()
    {
        var context = CreateContext(nodeOutputs: new()
        {
            ["webhook"] = ToJson(new { body = (object?)null })
        });

        var success = _sut.TryEvaluate("{{ nodes.webhook.body.field }}", context, out var result, out var error);

        success.Should().BeTrue();
        result.Should().BeNull();
        error.Should().BeNull();
    }

    [Fact]
    public void WhenNullPathInInterpolatedStringThenResolvesToEmpty()
    {
        var context = CreateContext(nodeOutputs: new()
        {
            ["node1"] = ToJson(new { name = "Alice" }),
            ["node2"] = ToJson(new { data = (object?)null })
        });

        var result = _sut.Evaluate("Hello {{ nodes.node1.name }}, status: {{ nodes.node2.data.status }}", context);

        result.Should().Be("Hello Alice, status: ");
    }
}
