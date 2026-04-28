using System.Text.Json;
using Vyshyvanka.Core.Enums;
using Vyshyvanka.Core.Interfaces;

namespace Vyshyvanka.Plugin.AdvancedHttp.Nodes;

/// <summary>
/// Base class for plugin nodes providing common functionality.
/// </summary>
public abstract class BasePluginNode : INode
{
    private readonly string _id = Guid.NewGuid().ToString();

    public string Id => _id;
    public abstract string Type { get; }
    public abstract NodeCategory Category { get; }

    public abstract Task<NodeOutput> ExecuteAsync(NodeInput input, IExecutionContext context);

    protected static NodeOutput SuccessOutput(object data)
    {
        return new NodeOutput
        {
            Data = JsonSerializer.SerializeToElement(data),
            Success = true
        };
    }

    protected static NodeOutput FailureOutput(string errorMessage)
    {
        return new NodeOutput
        {
            Data = default,
            Success = false,
            ErrorMessage = errorMessage
        };
    }

    protected static T? GetConfigValue<T>(NodeInput input, string key)
    {
        if (input.Configuration.ValueKind is JsonValueKind.Undefined or JsonValueKind.Null)
            return default;

        if (input.Configuration.TryGetProperty(key, out var value))
            return JsonSerializer.Deserialize<T>(value.GetRawText());

        return default;
    }

    protected static T GetRequiredConfigValue<T>(NodeInput input, string key)
    {
        var value = GetConfigValue<T>(input, key);
        if (value is null)
            throw new InvalidOperationException($"Required configuration '{key}' is missing");
        return value;
    }
}
