using System.Text.Json;
using Vyshyvanka.Core.Enums;
using Vyshyvanka.Core.Interfaces;
using Vyshyvanka.Engine.Credentials;
using ExecutionContext = Vyshyvanka.Engine.Execution.ExecutionContext;
using Vyshyvanka.Engine.Nodes.Logic;

namespace Vyshyvanka.Tests.Unit.Nodes;

public class SwitchNodeTests
{
    private readonly SwitchNode _sut = new();

    private static ExecutionContext CreateContext() =>
        new(Guid.NewGuid(), Guid.NewGuid(), NullCredentialProvider.Instance);

    [Fact]
    public void WhenCreatedThenHasCorrectMetadata()
    {
        _sut.Type.Should().Be("switch");
        _sut.Category.Should().Be(NodeCategory.Logic);
        _sut.Id.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task WhenFieldMatchesCaseValueThenRoutesToMatchedOutput()
    {
        var data = new { status = "active" };
        var config = JsonSerializer.SerializeToElement(new
        {
            field = "status",
            cases = new[]
            {
                new { Value = "active", Output = "active-branch" },
                new { Value = "inactive", Output = "inactive-branch" }
            }
        });
        var input = new NodeInput
        {
            Data = JsonSerializer.SerializeToElement(data),
            Configuration = config
        };
        var context = CreateContext();

        var result = await _sut.ExecuteAsync(input, context);

        result.Success.Should().BeTrue();
        result.Data.GetProperty("matchedCase").GetString().Should().Be("active-branch");
        result.Data.GetProperty("outputPort").GetString().Should().Be("active-branch");
    }

    [Fact]
    public async Task WhenFieldMatchesSecondCaseThenRoutesToSecondOutput()
    {
        var data = new { status = "inactive" };
        var config = JsonSerializer.SerializeToElement(new
        {
            field = "status",
            cases = new[]
            {
                new { Value = "active", Output = "active-branch" },
                new { Value = "inactive", Output = "inactive-branch" }
            }
        });
        var input = new NodeInput
        {
            Data = JsonSerializer.SerializeToElement(data),
            Configuration = config
        };
        var context = CreateContext();

        var result = await _sut.ExecuteAsync(input, context);

        result.Success.Should().BeTrue();
        result.Data.GetProperty("matchedCase").GetString().Should().Be("inactive-branch");
        result.Data.GetProperty("outputPort").GetString().Should().Be("inactive-branch");
    }

    [Fact]
    public async Task WhenNoMatchThenRoutesToDefaultOutput()
    {
        var data = new { status = "unknown" };
        var config = JsonSerializer.SerializeToElement(new
        {
            field = "status",
            cases = new[]
            {
                new { Value = "active", Output = "active-branch" },
                new { Value = "inactive", Output = "inactive-branch" }
            }
        });
        var input = new NodeInput
        {
            Data = JsonSerializer.SerializeToElement(data),
            Configuration = config
        };
        var context = CreateContext();

        var result = await _sut.ExecuteAsync(input, context);

        result.Success.Should().BeTrue();
        result.Data.GetProperty("matchedCase").GetString().Should().Be("default");
        result.Data.GetProperty("outputPort").GetString().Should().Be("default");
    }

    [Fact]
    public async Task WhenNoCasesDefinedThenRoutesToDefault()
    {
        var data = new { status = "active" };
        var config = JsonSerializer.SerializeToElement(new
        {
            field = "status"
        });
        var input = new NodeInput
        {
            Data = JsonSerializer.SerializeToElement(data),
            Configuration = config
        };
        var context = CreateContext();

        var result = await _sut.ExecuteAsync(input, context);

        result.Success.Should().BeTrue();
        result.Data.GetProperty("matchedCase").GetString().Should().Be("default");
    }

    [Fact]
    public async Task WhenStringMatchIsCaseInsensitiveThenMatches()
    {
        var data = new { type = "ERROR" };
        var config = JsonSerializer.SerializeToElement(new
        {
            field = "type",
            cases = new[]
            {
                new { Value = "error", Output = "error-handler" }
            }
        });
        var input = new NodeInput
        {
            Data = JsonSerializer.SerializeToElement(data),
            Configuration = config
        };
        var context = CreateContext();

        var result = await _sut.ExecuteAsync(input, context);

        result.Success.Should().BeTrue();
        result.Data.GetProperty("matchedCase").GetString().Should().Be("error-handler");
    }

    [Fact]
    public async Task WhenNumericFieldMatchesNumericCaseThenRoutes()
    {
        var data = new { code = 200 };
        var config = JsonSerializer.SerializeToElement(new
        {
            field = "code",
            cases = new[]
            {
                new { Value = (object)200, Output = "success" },
                new { Value = (object)404, Output = "not-found" },
                new { Value = (object)500, Output = "error" }
            }
        });
        var input = new NodeInput
        {
            Data = JsonSerializer.SerializeToElement(data),
            Configuration = config
        };
        var context = CreateContext();

        var result = await _sut.ExecuteAsync(input, context);

        result.Success.Should().BeTrue();
        result.Data.GetProperty("matchedCase").GetString().Should().Be("success");
    }

    [Fact]
    public async Task WhenBooleanFieldMatchesTrueCaseThenRoutes()
    {
        var data = new { isAdmin = true };
        var config = JsonSerializer.SerializeToElement(new
        {
            field = "isAdmin",
            cases = new[]
            {
                new { Value = (object)true, Output = "admin-path" },
                new { Value = (object)false, Output = "user-path" }
            }
        });
        var input = new NodeInput
        {
            Data = JsonSerializer.SerializeToElement(data),
            Configuration = config
        };
        var context = CreateContext();

        var result = await _sut.ExecuteAsync(input, context);

        result.Success.Should().BeTrue();
        result.Data.GetProperty("matchedCase").GetString().Should().Be("admin-path");
    }

    [Fact]
    public async Task WhenBooleanFieldMatchesFalseCaseThenRoutes()
    {
        var data = new { isAdmin = false };
        var config = JsonSerializer.SerializeToElement(new
        {
            field = "isAdmin",
            cases = new[]
            {
                new { Value = (object)true, Output = "admin-path" },
                new { Value = (object)false, Output = "user-path" }
            }
        });
        var input = new NodeInput
        {
            Data = JsonSerializer.SerializeToElement(data),
            Configuration = config
        };
        var context = CreateContext();

        var result = await _sut.ExecuteAsync(input, context);

        result.Success.Should().BeTrue();
        result.Data.GetProperty("matchedCase").GetString().Should().Be("user-path");
    }

    [Fact]
    public async Task WhenNestedFieldPathThenEvaluatesCorrectly()
    {
        var data = new { response = new { status = "ok" } };
        var config = JsonSerializer.SerializeToElement(new
        {
            field = "response.status",
            cases = new[]
            {
                new { Value = "ok", Output = "success" },
                new { Value = "error", Output = "failure" }
            }
        });
        var input = new NodeInput
        {
            Data = JsonSerializer.SerializeToElement(data),
            Configuration = config
        };
        var context = CreateContext();

        var result = await _sut.ExecuteAsync(input, context);

        result.Success.Should().BeTrue();
        result.Data.GetProperty("matchedCase").GetString().Should().Be("success");
    }

    [Fact]
    public async Task WhenFieldDoesNotExistThenRoutesToDefault()
    {
        var data = new { other = "value" };
        var config = JsonSerializer.SerializeToElement(new
        {
            field = "missing",
            cases = new[]
            {
                new { Value = "something", Output = "branch" }
            }
        });
        var input = new NodeInput
        {
            Data = JsonSerializer.SerializeToElement(data),
            Configuration = config
        };
        var context = CreateContext();

        var result = await _sut.ExecuteAsync(input, context);

        result.Success.Should().BeTrue();
        result.Data.GetProperty("matchedCase").GetString().Should().Be("default");
    }

    [Fact]
    public async Task WhenNullFieldMatchesNullCaseThenRoutes()
    {
        var data = new { Value = (object?)null };
        var config = JsonSerializer.SerializeToElement(new
        {
            field = "value",
            cases = new object[]
            {
                new { Value = (object?)null, Output = "null-handler" }
            }
        });
        var input = new NodeInput
        {
            Data = JsonSerializer.SerializeToElement(data),
            Configuration = config
        };
        var context = CreateContext();

        var result = await _sut.ExecuteAsync(input, context);

        result.Success.Should().BeTrue();
        result.Data.GetProperty("matchedCase").GetString().Should().Be("null-handler");
    }

    [Fact]
    public async Task WhenFirstCaseMatchesThenStopsEvaluating()
    {
        var data = new { priority = "high" };
        var config = JsonSerializer.SerializeToElement(new
        {
            field = "priority",
            cases = new[]
            {
                new { Value = "high", Output = "first-match" },
                new { Value = "high", Output = "second-match" }
            }
        });
        var input = new NodeInput
        {
            Data = JsonSerializer.SerializeToElement(data),
            Configuration = config
        };
        var context = CreateContext();

        var result = await _sut.ExecuteAsync(input, context);

        result.Success.Should().BeTrue();
        result.Data.GetProperty("matchedCase").GetString().Should().Be("first-match");
    }

    [Fact]
    public async Task WhenExecutedThenOriginalDataIsPassedThrough()
    {
        var data = new { status = "active", id = 123, name = "test" };
        var config = JsonSerializer.SerializeToElement(new
        {
            field = "status",
            cases = new[]
            {
                new { Value = "active", Output = "active-branch" }
            }
        });
        var input = new NodeInput
        {
            Data = JsonSerializer.SerializeToElement(data),
            Configuration = config
        };
        var context = CreateContext();

        var result = await _sut.ExecuteAsync(input, context);

        result.Success.Should().BeTrue();
        result.Data.TryGetProperty("data", out _).Should().BeTrue();
    }
}
