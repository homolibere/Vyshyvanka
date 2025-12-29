using System.Data.Common;
using FlowForge.Core.Interfaces;
using FlowForge.Engine.Nodes.Base;
using FlowForge.Engine.Registry;
using Microsoft.Data.Sqlite;

namespace FlowForge.Engine.Nodes.Actions;

/// <summary>
/// An action node that executes SQL queries against a database.
/// Supports parameterized queries and returns results as JSON.
/// </summary>
[NodeDefinition(
    Name = "Database Query",
    Description = "Execute SQL queries against a database and return results as JSON",
    Icon = "fa-solid fa-database")]
[NodeInput("input", DisplayName = "Input", IsRequired = false)]
[NodeOutput("output", DisplayName = "Results")]
[ConfigurationProperty("connectionString", "string", Description = "Database connection string", IsRequired = true)]
[ConfigurationProperty("query", "string", Description = "SQL query to execute", IsRequired = true)]
[ConfigurationProperty("parameters", "object", Description = "Query parameters as key-value pairs")]
[ConfigurationProperty("queryType", "string", Description = "Query type: select, execute, scalar")]
[ConfigurationProperty("timeout", "number", Description = "Command timeout in seconds")]
public class DatabaseQueryNode : BaseActionNode
{
    private readonly string _id = Guid.NewGuid().ToString();
    private readonly Func<string, DbConnection>? _connectionFactory;

    /// <inheritdoc />
    public override string Id => _id;

    /// <inheritdoc />
    public override string Type => "database-query";

    /// <summary>
    /// Creates a new DatabaseQueryNode with default SQLite connection factory.
    /// </summary>
    public DatabaseQueryNode() : this(null)
    {
    }

    /// <summary>
    /// Creates a new DatabaseQueryNode with a custom connection factory (for testing).
    /// </summary>
    internal DatabaseQueryNode(Func<string, DbConnection>? connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    /// <inheritdoc />
    public override async Task<NodeOutput> ExecuteAsync(NodeInput input, IExecutionContext context)
    {
        try
        {
            var connectionString = GetRequiredConfigValue<string>(input, "connectionString");
            var query = GetRequiredConfigValue<string>(input, "query");
            var parameters = GetConfigValue<Dictionary<string, object?>>(input, "parameters");
            var queryType = GetConfigValue<string>(input, "queryType")?.ToLowerInvariant() ?? "select";
            var timeoutSeconds = GetConfigValue<int?>(input, "timeout") ?? 30;

            // Apply credentials to connection string if provided
            if (input.CredentialId.HasValue)
            {
                connectionString = await ApplyCredentialsToConnectionStringAsync(
                    connectionString, input.CredentialId.Value, context);
            }

            // Create connection using factory or default to SQLite
            await using var connection = _connectionFactory?.Invoke(connectionString)
                                         ?? new SqliteConnection(connectionString);

            await connection.OpenAsync(context.CancellationToken);

            await using var command = connection.CreateCommand();
            command.CommandText = query;
            command.CommandTimeout = timeoutSeconds;

            // Add parameters
            if (parameters is not null)
            {
                foreach (var (key, value) in parameters)
                {
                    var parameter = command.CreateParameter();
                    parameter.ParameterName = key.StartsWith('@') ? key : $"@{key}";
                    parameter.Value = value ?? DBNull.Value;
                    command.Parameters.Add(parameter);
                }
            }

            object? result;
            int rowsAffected = 0;

            switch (queryType)
            {
                case "scalar":
                    result = await command.ExecuteScalarAsync(context.CancellationToken);
                    break;

                case "execute":
                    rowsAffected = await command.ExecuteNonQueryAsync(context.CancellationToken);
                    result = null;
                    break;

                default:
                    result = await ExecuteSelectAsync(command, context.CancellationToken);
                    break;
            }

            var output = new Dictionary<string, object?>
            {
                ["success"] = true,
                ["queryType"] = queryType,
                ["rowsAffected"] = rowsAffected,
                ["data"] = result
            };

            return SuccessOutput(output);
        }
        catch (DbException ex)
        {
            return FailureOutput($"Database error: {ex.Message}");
        }
        catch (Exception ex)
        {
            return FailureOutput($"Query execution error: {ex.Message}");
        }
    }

    private static async Task<List<Dictionary<string, object?>>> ExecuteSelectAsync(
        DbCommand command,
        CancellationToken cancellationToken)
    {
        var results = new List<Dictionary<string, object?>>();

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);

        while (await reader.ReadAsync(cancellationToken))
        {
            var row = new Dictionary<string, object?>();

            for (var i = 0; i < reader.FieldCount; i++)
            {
                var columnName = reader.GetName(i);
                var value = reader.IsDBNull(i) ? null : reader.GetValue(i);

                // Convert certain types to JSON-friendly formats
                row[columnName] = value switch
                {
                    DateTime dt => dt.ToString("O"),
                    DateTimeOffset dto => dto.ToString("O"),
                    byte[] bytes => Convert.ToBase64String(bytes),
                    not null => value,
                    _ => null
                };
            }

            results.Add(row);
        }

        return results;
    }

    private static async Task<string> ApplyCredentialsToConnectionStringAsync(
        string connectionString,
        Guid credentialId,
        IExecutionContext context)
    {
        var credentials = await context.Credentials.GetCredentialAsync(credentialId, context.CancellationToken);

        if (credentials is null)
            return connectionString;

        // Build connection string with credentials
        var builder = new DbConnectionStringBuilder { ConnectionString = connectionString };

        if (credentials.TryGetValue("username", out var username))
        {
            builder["User ID"] = username;
        }

        if (credentials.TryGetValue("password", out var password))
        {
            builder["Password"] = password;
        }

        // Support additional connection properties from credentials
        if (credentials.TryGetValue("server", out var server))
        {
            builder["Server"] = server;
        }

        if (credentials.TryGetValue("database", out var database))
        {
            builder["Database"] = database;
        }

        return builder.ConnectionString;
    }
}
