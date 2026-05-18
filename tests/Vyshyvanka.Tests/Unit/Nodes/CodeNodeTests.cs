using System.Text.Json;
using Vyshyvanka.Core.Enums;
using Vyshyvanka.Core.Interfaces;
using Vyshyvanka.Engine.Credentials;
using ExecutionContext = Vyshyvanka.Engine.Execution.ExecutionContext;
using Vyshyvanka.Engine.Nodes.Actions;

namespace Vyshyvanka.Tests.Unit.Nodes;

public class CodeNodeTests
{
    private readonly CodeNode _sut = new();

    private static ExecutionContext CreateContext() =>
        new(Guid.NewGuid(), Guid.NewGuid(), NullCredentialProvider.Instance);

    [Fact]
    public void WhenCreatedThenHasCorrectMetadata()
    {
        _sut.Type.Should().Be("code");
        _sut.Category.Should().Be(NodeCategory.Action);
        _sut.Id.Should().NotBeNullOrWhiteSpace();
    }

    // --- JavaScript: runOnce ---

    [Fact]
    public async Task WhenJavaScriptReturnsValueThenOutputContainsResult()
    {
        var config = JsonSerializer.SerializeToElement(new
        {
            language = "javascript",
            code = "return 42;"
        });
        var input = new NodeInput
        {
            Data = default,
            Configuration = config
        };
        var context = CreateContext();

        var result = await _sut.ExecuteAsync(input, context);

        result.Success.Should().BeTrue();
        result.Data.GetProperty("result").GetDouble().Should().Be(42);
    }

    [Fact]
    public async Task WhenJavaScriptReturnsStringThenOutputContainsString()
    {
        var config = JsonSerializer.SerializeToElement(new
        {
            language = "javascript",
            code = """return "hello world";"""
        });
        var input = new NodeInput
        {
            Data = default,
            Configuration = config
        };
        var context = CreateContext();

        var result = await _sut.ExecuteAsync(input, context);

        result.Success.Should().BeTrue();
        result.Data.GetProperty("result").GetString().Should().Be("hello world");
    }

    [Fact]
    public async Task WhenJavaScriptReturnsObjectThenOutputContainsObject()
    {
        var config = JsonSerializer.SerializeToElement(new
        {
            language = "javascript",
            code = """return { name: "test", value: 123 };"""
        });
        var input = new NodeInput
        {
            Data = default,
            Configuration = config
        };
        var context = CreateContext();

        var result = await _sut.ExecuteAsync(input, context);

        result.Success.Should().BeTrue();
        result.Data.GetProperty("result").ValueKind.Should().Be(JsonValueKind.Object);
    }

    [Fact]
    public async Task WhenJavaScriptAccessesInputThenInputDataIsAvailable()
    {
        var config = JsonSerializer.SerializeToElement(new
        {
            language = "javascript",
            code = "return input.name + ' processed';"
        });
        var input = new NodeInput
        {
            Data = JsonSerializer.SerializeToElement(new { name = "test" }),
            Configuration = config
        };
        var context = CreateContext();

        var result = await _sut.ExecuteAsync(input, context);

        result.Success.Should().BeTrue();
        result.Data.GetProperty("result").GetString().Should().Be("test processed");
    }

    [Fact]
    public async Task WhenJavaScriptUsesLogThenLogsAreCaptured()
    {
        var config = JsonSerializer.SerializeToElement(new
        {
            language = "javascript",
            code = """
                log("first message");
                log("second message");
                return true;
                """
        });
        var input = new NodeInput
        {
            Data = default,
            Configuration = config
        };
        var context = CreateContext();

        var result = await _sut.ExecuteAsync(input, context);

        result.Success.Should().BeTrue();
        result.Data.GetProperty("logs").GetArrayLength().Should().Be(2);
    }

    [Fact]
    public async Task WhenJavaScriptAccessesExecutionIdThenItIsAvailable()
    {
        var config = JsonSerializer.SerializeToElement(new
        {
            language = "javascript",
            code = "return executionId;"
        });
        var input = new NodeInput
        {
            Data = default,
            Configuration = config
        };
        var context = CreateContext();

        var result = await _sut.ExecuteAsync(input, context);

        result.Success.Should().BeTrue();
        result.Data.GetProperty("result").GetString().Should().Be(context.ExecutionId.ToString());
    }

    [Fact]
    public async Task WhenJavaScriptAccessesWorkflowIdThenItIsAvailable()
    {
        var config = JsonSerializer.SerializeToElement(new
        {
            language = "javascript",
            code = "return workflowId;"
        });
        var input = new NodeInput
        {
            Data = default,
            Configuration = config
        };
        var context = CreateContext();

        var result = await _sut.ExecuteAsync(input, context);

        result.Success.Should().BeTrue();
        result.Data.GetProperty("result").GetString().Should().Be(context.WorkflowId.ToString());
    }

    [Fact]
    public async Task WhenJavaScriptUsesGetItemsWithArrayInputThenReturnsArray()
    {
        var config = JsonSerializer.SerializeToElement(new
        {
            language = "javascript",
            code = "var items = getItems(); return items.length;"
        });
        var input = new NodeInput
        {
            Data = JsonSerializer.SerializeToElement(new[] { 1, 2, 3 }),
            Configuration = config
        };
        var context = CreateContext();

        var result = await _sut.ExecuteAsync(input, context);

        result.Success.Should().BeTrue();
        result.Data.GetProperty("result").GetDouble().Should().Be(3);
    }

    [Fact]
    public async Task WhenJavaScriptUsesGetItemsWithNonArrayInputThenWrapsInArray()
    {
        var config = JsonSerializer.SerializeToElement(new
        {
            language = "javascript",
            code = "var items = getItems(); return items.length;"
        });
        var input = new NodeInput
        {
            Data = JsonSerializer.SerializeToElement(new { name = "single" }),
            Configuration = config
        };
        var context = CreateContext();

        var result = await _sut.ExecuteAsync(input, context);

        result.Success.Should().BeTrue();
        result.Data.GetProperty("result").GetDouble().Should().Be(1);
    }

    [Fact]
    public async Task WhenJavaScriptUsesToJsonThenSerializesCorrectly()
    {
        var config = JsonSerializer.SerializeToElement(new
        {
            language = "javascript",
            code = """return toJson({ key: "value" });"""
        });
        var input = new NodeInput
        {
            Data = default,
            Configuration = config
        };
        var context = CreateContext();

        var result = await _sut.ExecuteAsync(input, context);

        result.Success.Should().BeTrue();
        var jsonStr = result.Data.GetProperty("result").GetString();
        jsonStr.Should().Contain("key");
        jsonStr.Should().Contain("value");
    }

    [Fact]
    public async Task WhenJavaScriptReturnsNullThenResultIsNull()
    {
        var config = JsonSerializer.SerializeToElement(new
        {
            language = "javascript",
            code = "return null;"
        });
        var input = new NodeInput
        {
            Data = default,
            Configuration = config
        };
        var context = CreateContext();

        var result = await _sut.ExecuteAsync(input, context);

        result.Success.Should().BeTrue();
        result.Data.GetProperty("result").ValueKind.Should().Be(JsonValueKind.Null);
    }

    [Fact]
    public async Task WhenJavaScriptReturnsBooleanThenResultIsBoolean()
    {
        var config = JsonSerializer.SerializeToElement(new
        {
            language = "javascript",
            code = "return true;"
        });
        var input = new NodeInput
        {
            Data = default,
            Configuration = config
        };
        var context = CreateContext();

        var result = await _sut.ExecuteAsync(input, context);

        result.Success.Should().BeTrue();
        result.Data.GetProperty("result").GetBoolean().Should().BeTrue();
    }

    // --- JavaScript: runForEachItem ---

    [Fact]
    public async Task WhenJavaScriptRunForEachItemWithArrayThenProcessesEachItem()
    {
        var config = JsonSerializer.SerializeToElement(new
        {
            language = "javascript",
            code = "return currentItem * 2;",
            mode = "runForEachItem"
        });
        var input = new NodeInput
        {
            Data = JsonSerializer.SerializeToElement(new[] { 1, 2, 3 }),
            Configuration = config
        };
        var context = CreateContext();

        var result = await _sut.ExecuteAsync(input, context);

        result.Success.Should().BeTrue();
        var results = result.Data.GetProperty("result");
        results.GetArrayLength().Should().Be(3);
    }

    [Fact]
    public async Task WhenJavaScriptRunForEachItemWithNonArrayThenProcessesSingleItem()
    {
        var config = JsonSerializer.SerializeToElement(new
        {
            language = "javascript",
            code = "return currentItem.name;",
            mode = "runForEachItem"
        });
        var input = new NodeInput
        {
            Data = JsonSerializer.SerializeToElement(new { name = "test" }),
            Configuration = config
        };
        var context = CreateContext();

        var result = await _sut.ExecuteAsync(input, context);

        result.Success.Should().BeTrue();
        result.Data.GetProperty("result").GetArrayLength().Should().Be(1);
    }

    [Fact]
    public async Task WhenJavaScriptRunForEachItemThenItemIndexIsAvailable()
    {
        var config = JsonSerializer.SerializeToElement(new
        {
            language = "javascript",
            code = "return itemIndex;",
            mode = "runForEachItem"
        });
        var input = new NodeInput
        {
            Data = JsonSerializer.SerializeToElement(new[] { "a", "b", "c" }),
            Configuration = config
        };
        var context = CreateContext();

        var result = await _sut.ExecuteAsync(input, context);

        result.Success.Should().BeTrue();
        var results = result.Data.GetProperty("result");
        results.GetArrayLength().Should().Be(3);
    }

    // --- JavaScript: error handling ---

    [Fact]
    public async Task WhenJavaScriptThrowsErrorThenReturnsFailure()
    {
        var config = JsonSerializer.SerializeToElement(new
        {
            language = "javascript",
            code = """throw new Error("something went wrong");"""
        });
        var input = new NodeInput
        {
            Data = default,
            Configuration = config
        };
        var context = CreateContext();

        var result = await _sut.ExecuteAsync(input, context);

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("JavaScript error");
    }

    [Fact]
    public async Task WhenJavaScriptHasSyntaxErrorThenReturnsFailure()
    {
        var config = JsonSerializer.SerializeToElement(new
        {
            language = "javascript",
            code = "return {{{invalid"
        });
        var input = new NodeInput
        {
            Data = default,
            Configuration = config
        };
        var context = CreateContext();

        var result = await _sut.ExecuteAsync(input, context);

        result.Success.Should().BeFalse();
    }

    // --- JSONata: runOnce ---

    [Fact]
    public async Task WhenJsonataExpressionEvaluatesThenReturnsResult()
    {
        var config = JsonSerializer.SerializeToElement(new
        {
            language = "jsonata",
            code = "$.name"
        });
        var input = new NodeInput
        {
            Data = JsonSerializer.SerializeToElement(new { name = "John", age = 30 }),
            Configuration = config
        };
        var context = CreateContext();

        var result = await _sut.ExecuteAsync(input, context);

        result.Success.Should().BeTrue();
        result.Data.GetProperty("result").GetString().Should().Be("John");
    }

    [Fact]
    public async Task WhenJsonataTransformsDataThenReturnsTransformedResult()
    {
        var config = JsonSerializer.SerializeToElement(new
        {
            language = "jsonata",
            code = "$.price * $.quantity"
        });
        var input = new NodeInput
        {
            Data = JsonSerializer.SerializeToElement(new { price = 10, quantity = 5 }),
            Configuration = config
        };
        var context = CreateContext();

        var result = await _sut.ExecuteAsync(input, context);

        result.Success.Should().BeTrue();
        result.Data.GetProperty("result").GetInt32().Should().Be(50);
    }

    [Fact]
    public async Task WhenJsonataWithNullInputThenHandlesGracefully()
    {
        var config = JsonSerializer.SerializeToElement(new
        {
            language = "jsonata",
            code = "$"
        });
        var input = new NodeInput
        {
            Data = default,
            Configuration = config
        };
        var context = CreateContext();

        var result = await _sut.ExecuteAsync(input, context);

        result.Success.Should().BeTrue();
    }

    // --- JSONata: runForEachItem ---

    [Fact]
    public async Task WhenJsonataRunForEachItemWithArrayThenProcessesEachItem()
    {
        var config = JsonSerializer.SerializeToElement(new
        {
            language = "jsonata",
            code = "$ * 2",
            mode = "runForEachItem"
        });
        var input = new NodeInput
        {
            Data = JsonSerializer.SerializeToElement(new[] { 1, 2, 3 }),
            Configuration = config
        };
        var context = CreateContext();

        var result = await _sut.ExecuteAsync(input, context);

        result.Success.Should().BeTrue();
        result.Data.GetProperty("result").GetArrayLength().Should().Be(3);
    }

    [Fact]
    public async Task WhenJsonataRunForEachItemWithNonArrayThenProcessesSingleItem()
    {
        var config = JsonSerializer.SerializeToElement(new
        {
            language = "jsonata",
            code = "$.name",
            mode = "runForEachItem"
        });
        var input = new NodeInput
        {
            Data = JsonSerializer.SerializeToElement(new { name = "test" }),
            Configuration = config
        };
        var context = CreateContext();

        var result = await _sut.ExecuteAsync(input, context);

        result.Success.Should().BeTrue();
        result.Data.GetProperty("result").GetArrayLength().Should().Be(1);
    }

    // --- JSONata: error handling ---

    [Fact]
    public async Task WhenJsonataHasInvalidExpressionThenReturnsFailure()
    {
        var config = JsonSerializer.SerializeToElement(new
        {
            language = "jsonata",
            code = "$$$invalid((("
        });
        var input = new NodeInput
        {
            Data = JsonSerializer.SerializeToElement(new { name = "test" }),
            Configuration = config
        };
        var context = CreateContext();

        var result = await _sut.ExecuteAsync(input, context);

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("JSONata error");
    }

    // --- General ---

    [Fact]
    public async Task WhenCodeIsEmptyThenReturnsFailure()
    {
        var config = JsonSerializer.SerializeToElement(new
        {
            language = "javascript",
            code = "   "
        });
        var input = new NodeInput
        {
            Data = default,
            Configuration = config
        };
        var context = CreateContext();

        var result = await _sut.ExecuteAsync(input, context);

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("Code cannot be empty");
    }

    [Fact]
    public async Task WhenLanguageNotSpecifiedThenDefaultsToJavaScript()
    {
        var config = JsonSerializer.SerializeToElement(new
        {
            code = "return 'js default';"
        });
        var input = new NodeInput
        {
            Data = default,
            Configuration = config
        };
        var context = CreateContext();

        var result = await _sut.ExecuteAsync(input, context);

        result.Success.Should().BeTrue();
        result.Data.GetProperty("result").GetString().Should().Be("js default");
    }

    [Fact]
    public async Task WhenCancellationRequestedThenReturnsFailure()
    {
        var config = JsonSerializer.SerializeToElement(new
        {
            language = "javascript",
            code = "while(true) {}"  // infinite loop
        });
        var input = new NodeInput
        {
            Data = default,
            Configuration = config
        };

        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(100));
        var context = new ExecutionContext(
            Guid.NewGuid(), Guid.NewGuid(), NullCredentialProvider.Instance, cts.Token);

        var result = await _sut.ExecuteAsync(input, context);

        result.Success.Should().BeFalse();
    }
}
