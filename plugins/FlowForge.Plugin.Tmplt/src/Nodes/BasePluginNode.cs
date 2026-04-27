using System.Text.Json;
using FlowForge.Core.Enums;
using FlowForge.Core.Interfaces;

namespace FlowForge.Plugin.Template.Nodes;

/// <summary>
/// Base class for plugin nodes providing common helpers.
/// Inherit from this instead of implementing INode directly.
/// </summary>
public abstract class BasePluginNode : INode
{
    private readonly string _id = Guid.NewGuid().ToString();

    public string Id => _id;
    public abstract string Type { get; }
    public abstract NodeCategory Category { get; }

    public abstract Task<NodeOutput> ExecuteAsync(NodeInput input, IExecutionContext context);

    /// <summary>Creates a successful output from any serializable object.</summary>
    protected static NodeOutput SuccessOutput(object data) => new()
    {
        Data = JsonSerializer.SerializeToElement(data),
        Success = true
    };

    /// <summary>Creates a failure output with an error message.</summary>
    protected static NodeOutput FailureOutput(string errorMessage) => new()
    {
        Data = default,
        Success = false,
        ErrorMessage = errorMessage
    };

    /// <summary>Reads an optional configuration value.</summary>
    protected static T? GetConfigValue<T>(NodeInput input, string key)
    {
        if (input.Configuration.ValueKind is JsonValueKind.Undefined or JsonValueKind.Null)
            return default;

        return input.Configuration.TryGetProperty(key, out var value)
            ? JsonSerializer.Deserialize<T>(value.GetRawText())
            : default;
    }

    /// <summary>Reads a required configuration value or throws.</summary>
    protected static T GetRequiredConfigValue<T>(NodeInput input, string key)
    {
        return GetConfigValue<T>(input, key)
            ?? throw new InvalidOperationException($"Required configuration '{key}' is missing");
    }
}
