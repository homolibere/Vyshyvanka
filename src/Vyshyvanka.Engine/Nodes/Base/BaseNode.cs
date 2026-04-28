using System.Text.Json;
using Vyshyvanka.Core.Enums;
using Vyshyvanka.Core.Interfaces;

namespace Vyshyvanka.Engine.Nodes.Base;

/// <summary>
/// Abstract base class for all workflow nodes.
/// Provides common functionality and enforces the node contract.
/// </summary>
public abstract class BaseNode : INode
{
    /// <inheritdoc />
    public abstract string Id { get; }

    /// <inheritdoc />
    public abstract string Type { get; }

    /// <inheritdoc />
    public abstract NodeCategory Category { get; }

    /// <inheritdoc />
    public abstract Task<NodeOutput> ExecuteAsync(NodeInput input, IExecutionContext context);

    /// <summary>
    /// Creates a successful output with the given data.
    /// </summary>
    protected static NodeOutput SuccessOutput(JsonElement data)
    {
        return new NodeOutput
        {
            Data = data,
            Success = true
        };
    }

    /// <summary>
    /// Creates a successful output with the given object serialized to JSON.
    /// </summary>
    protected static NodeOutput SuccessOutput(object data)
    {
        var json = JsonSerializer.SerializeToElement(data);
        return new NodeOutput
        {
            Data = json,
            Success = true
        };
    }

    /// <summary>
    /// Creates a failed output with the given error message.
    /// </summary>
    protected static NodeOutput FailureOutput(string errorMessage)
    {
        return new NodeOutput
        {
            Data = default,
            Success = false,
            ErrorMessage = errorMessage
        };
    }

    /// <summary>
    /// Gets a configuration value from the node input.
    /// </summary>
    protected static T? GetConfigValue<T>(NodeInput input, string key)
    {
        if (input.Configuration.ValueKind == JsonValueKind.Undefined ||
            input.Configuration.ValueKind == JsonValueKind.Null)
        {
            return default;
        }

        if (input.Configuration.TryGetProperty(key, out var value))
        {
            return JsonSerializer.Deserialize<T>(value.GetRawText());
        }

        return default;
    }

    /// <summary>
    /// Gets a required configuration value, throwing if not present.
    /// </summary>
    protected static T GetRequiredConfigValue<T>(NodeInput input, string key)
    {
        var value = GetConfigValue<T>(input, key);
        if (value is null)
        {
            throw new InvalidOperationException($"Required configuration '{key}' is missing");
        }
        return value;
    }
}
