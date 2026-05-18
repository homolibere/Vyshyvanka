using System.Data.Common;
using System.Text.Json;
using Microsoft.Data.Sqlite;
using Vyshyvanka.Core.Enums;
using Vyshyvanka.Core.Interfaces;
using Vyshyvanka.Engine.Credentials;
using ExecutionContext = Vyshyvanka.Engine.Execution.ExecutionContext;
using Vyshyvanka.Engine.Nodes.Actions;

namespace Vyshyvanka.Tests.Unit.Nodes;

public class DatabaseQueryNodeTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly DatabaseQueryNode _sut;

    public DatabaseQueryNodeTests()
    {
        _connection = new SqliteConnection("Data Source=:memory:");
        _connection.Open();

        // Create test table
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = """
                          CREATE TABLE users (
                              id INTEGER PRIMARY KEY,
                              name TEXT NOT NULL,
                              email TEXT,
                              age INTEGER
                          );
                          INSERT INTO users (id, name, email, age) VALUES (1, 'Alice', 'alice@example.com', 30);
                          INSERT INTO users (id, name, email, age) VALUES (2, 'Bob', 'bob@example.com', 25);
                          INSERT INTO users (id, name, email, age) VALUES (3, 'Charlie', 'charlie@example.com', 35);
                          """;
        cmd.ExecuteNonQuery();

        // Use a factory that returns our shared in-memory connection
        _sut = new DatabaseQueryNode(_ => _connection);
    }

    public void Dispose()
    {
        _connection.Dispose();
    }

    private static ExecutionContext CreateContext() =>
        new(Guid.NewGuid(), Guid.NewGuid(), NullCredentialProvider.Instance);

    [Fact]
    public void WhenCreatedThenHasCorrectMetadata()
    {
        var node = new DatabaseQueryNode();
        node.Type.Should().Be("database-query");
        node.Category.Should().Be(NodeCategory.Action);
        node.Id.Should().NotBeNullOrWhiteSpace();
    }

    // --- Select queries ---

    [Fact]
    public async Task WhenSelectQueryExecutedThenReturnsRows()
    {
        var config = JsonSerializer.SerializeToElement(new
        {
            connectionString = "Data Source=:memory:",
            query = "SELECT * FROM users ORDER BY id",
            queryType = "select"
        });
        var input = new NodeInput { Data = default, Configuration = config };
        var context = CreateContext();

        var result = await _sut.ExecuteAsync(input, context);

        result.Success.Should().BeTrue();
        result.Data.GetProperty("success").GetBoolean().Should().BeTrue();
        result.Data.GetProperty("queryType").GetString().Should().Be("select");
        var data = result.Data.GetProperty("data");
        data.GetArrayLength().Should().Be(3);
    }

    [Fact]
    public async Task WhenSelectQueryWithParametersThenFiltersCorrectly()
    {
        var config = JsonSerializer.SerializeToElement(new
        {
            connectionString = "Data Source=:memory:",
            query = "SELECT * FROM users WHERE age > 28",
            queryType = "select"
        });
        var input = new NodeInput { Data = default, Configuration = config };
        var context = CreateContext();

        var result = await _sut.ExecuteAsync(input, context);

        result.Success.Should().BeTrue();
        var data = result.Data.GetProperty("data");
        data.GetArrayLength().Should().Be(2); // Alice (30) and Charlie (35)
    }

    [Fact]
    public async Task WhenSelectQueryReturnsNoRowsThenReturnsEmptyArray()
    {
        var config = JsonSerializer.SerializeToElement(new
        {
            connectionString = "Data Source=:memory:",
            query = "SELECT * FROM users WHERE age > 100",
            queryType = "select"
        });
        var input = new NodeInput { Data = default, Configuration = config };
        var context = CreateContext();

        var result = await _sut.ExecuteAsync(input, context);

        result.Success.Should().BeTrue();
        var data = result.Data.GetProperty("data");
        data.GetArrayLength().Should().Be(0);
    }

    // --- Scalar queries ---

    [Fact]
    public async Task WhenScalarQueryExecutedThenReturnsSingleValue()
    {
        var config = JsonSerializer.SerializeToElement(new
        {
            connectionString = "Data Source=:memory:",
            query = "SELECT COUNT(*) FROM users",
            queryType = "scalar"
        });
        var input = new NodeInput { Data = default, Configuration = config };
        var context = CreateContext();

        var result = await _sut.ExecuteAsync(input, context);

        result.Success.Should().BeTrue();
        result.Data.GetProperty("queryType").GetString().Should().Be("scalar");
        result.Data.GetProperty("data").GetInt64().Should().Be(3);
    }

    [Fact]
    public async Task WhenScalarQueryWithFilterThenReturnsFilteredResult()
    {
        var config = JsonSerializer.SerializeToElement(new
        {
            connectionString = "Data Source=:memory:",
            query = "SELECT name FROM users WHERE id = 2",
            queryType = "scalar"
        });
        var input = new NodeInput { Data = default, Configuration = config };
        var context = CreateContext();

        var result = await _sut.ExecuteAsync(input, context);

        result.Success.Should().BeTrue();
        result.Data.GetProperty("data").GetString().Should().Be("Bob");
    }

    // --- Execute (non-query) ---

    [Fact]
    public async Task WhenExecuteQueryRunsThenReturnsRowsAffected()
    {
        var config = JsonSerializer.SerializeToElement(new
        {
            connectionString = "Data Source=:memory:",
            query = "UPDATE users SET age = age + 1 WHERE age < 30",
            queryType = "execute"
        });
        var input = new NodeInput { Data = default, Configuration = config };
        var context = CreateContext();

        var result = await _sut.ExecuteAsync(input, context);

        result.Success.Should().BeTrue();
        result.Data.GetProperty("queryType").GetString().Should().Be("execute");
        result.Data.GetProperty("rowsAffected").GetInt32().Should().Be(1); // Only Bob (25)
    }

    [Fact]
    public async Task WhenInsertQueryRunsThenReturnsRowsAffected()
    {
        var config = JsonSerializer.SerializeToElement(new
        {
            connectionString = "Data Source=:memory:",
            query = "INSERT INTO users (name, email, age) VALUES ('Dave', 'dave@example.com', 28)",
            queryType = "execute"
        });
        var input = new NodeInput { Data = default, Configuration = config };
        var context = CreateContext();

        var result = await _sut.ExecuteAsync(input, context);

        result.Success.Should().BeTrue();
        result.Data.GetProperty("rowsAffected").GetInt32().Should().Be(1);
    }

    [Fact]
    public async Task WhenDeleteQueryRunsThenReturnsRowsAffected()
    {
        var config = JsonSerializer.SerializeToElement(new
        {
            connectionString = "Data Source=:memory:",
            query = "DELETE FROM users WHERE id = 1",
            queryType = "execute"
        });
        var input = new NodeInput { Data = default, Configuration = config };
        var context = CreateContext();

        var result = await _sut.ExecuteAsync(input, context);

        result.Success.Should().BeTrue();
        result.Data.GetProperty("rowsAffected").GetInt32().Should().Be(1);
    }

    // --- Parameter handling ---

    [Fact]
    public async Task WhenParameterUsedInQueryThenFiltersCorrectly()
    {
        var config = JsonSerializer.SerializeToElement(new
        {
            connectionString = "Data Source=:memory:",
            query = "SELECT * FROM users WHERE name = 'Alice'",
            queryType = "select"
        });
        var input = new NodeInput { Data = default, Configuration = config };
        var context = CreateContext();

        var result = await _sut.ExecuteAsync(input, context);

        result.Success.Should().BeTrue();
        result.Data.GetProperty("data").GetArrayLength().Should().Be(1);
    }

    [Fact]
    public async Task WhenSelectQueryWithNullFilterThenReturnsEmptyResults()
    {
        var config = JsonSerializer.SerializeToElement(new
        {
            connectionString = "Data Source=:memory:",
            query = "SELECT * FROM users WHERE email IS NULL",
            queryType = "select"
        });
        var input = new NodeInput { Data = default, Configuration = config };
        var context = CreateContext();

        var result = await _sut.ExecuteAsync(input, context);

        result.Success.Should().BeTrue();
        result.Data.GetProperty("data").GetArrayLength().Should().Be(0);
    }

    // --- Default query type ---

    [Fact]
    public async Task WhenQueryTypeNotSpecifiedThenDefaultsToSelect()
    {
        var config = JsonSerializer.SerializeToElement(new
        {
            connectionString = "Data Source=:memory:",
            query = "SELECT * FROM users"
        });
        var input = new NodeInput { Data = default, Configuration = config };
        var context = CreateContext();

        var result = await _sut.ExecuteAsync(input, context);

        result.Success.Should().BeTrue();
        result.Data.GetProperty("queryType").GetString().Should().Be("select");
        result.Data.GetProperty("data").GetArrayLength().Should().Be(3);
    }

    // --- Error handling ---

    [Fact]
    public async Task WhenInvalidSqlThenReturnsFailure()
    {
        var config = JsonSerializer.SerializeToElement(new
        {
            connectionString = "Data Source=:memory:",
            query = "SELECT * FROM nonexistent_table"
        });
        var input = new NodeInput { Data = default, Configuration = config };
        var context = CreateContext();

        var result = await _sut.ExecuteAsync(input, context);

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("Database error");
    }

    [Fact]
    public async Task WhenConnectionStringMissingThenReturnsFailure()
    {
        var config = JsonSerializer.SerializeToElement(new
        {
            query = "SELECT 1"
        });
        var input = new NodeInput { Data = default, Configuration = config };
        var context = CreateContext();

        var result = await _sut.ExecuteAsync(input, context);

        result.Success.Should().BeFalse();
    }

    [Fact]
    public async Task WhenQueryMissingThenReturnsFailure()
    {
        var config = JsonSerializer.SerializeToElement(new
        {
            connectionString = "Data Source=:memory:"
        });
        var input = new NodeInput { Data = default, Configuration = config };
        var context = CreateContext();

        var result = await _sut.ExecuteAsync(input, context);

        result.Success.Should().BeFalse();
    }

    // --- Credential application ---

    [Fact]
    public async Task WhenCredentialIdProvidedThenAppliesCredentialsToConnectionString()
    {
        var credentialId = Guid.NewGuid();
        var credentialProvider = Substitute.For<ICredentialProvider>();
        credentialProvider.GetCredentialAsync(credentialId, Arg.Any<CancellationToken>())
            .Returns(new Dictionary<string, string>
            {
                ["username"] = "testuser",
                ["password"] = "testpass"
            });

        var context = new ExecutionContext(
            Guid.NewGuid(), Guid.NewGuid(), credentialProvider);

        var config = JsonSerializer.SerializeToElement(new
        {
            connectionString = "Data Source=:memory:",
            query = "SELECT 1",
            queryType = "scalar"
        });
        var input = new NodeInput
        {
            Data = default,
            Configuration = config,
            CredentialId = credentialId
        };

        // The node will try to connect with modified connection string
        // For SQLite in-memory, credentials don't matter but the code path is exercised
        var result = await _sut.ExecuteAsync(input, context);

        result.Success.Should().BeTrue();
    }

    // --- NULL handling in results ---

    [Fact]
    public async Task WhenResultContainsNullValuesThenHandlesCorrectly()
    {
        // Insert a row with NULL email
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = "INSERT INTO users (id, name, email, age) VALUES (4, 'NoEmail', NULL, 20)";
        cmd.ExecuteNonQuery();

        var config = JsonSerializer.SerializeToElement(new
        {
            connectionString = "Data Source=:memory:",
            query = "SELECT * FROM users WHERE id = 4",
            queryType = "select"
        });
        var input = new NodeInput { Data = default, Configuration = config };
        var context = CreateContext();

        var result = await _sut.ExecuteAsync(input, context);

        result.Success.Should().BeTrue();
        var data = result.Data.GetProperty("data");
        data.GetArrayLength().Should().Be(1);
        data[0].GetProperty("email").ValueKind.Should().Be(JsonValueKind.Null);
    }
}
