using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace Vyshyvanka.Engine.Nodes.Actions;

/// <summary>
/// Globals object exposed to user scripts in the Code node.
/// Provides access to input data, execution metadata, and utility methods.
/// </summary>
public sealed class CodeNodeGlobals
{
    private readonly ILogger _logger;
    private readonly List<string> _logs = [];

    /// <summary>
    /// The input data from upstream nodes.
    /// </summary>
    public JsonElement Input { get; }

    /// <summary>
    /// The current execution ID.
    /// </summary>
    public Guid ExecutionId { get; }

    /// <summary>
    /// The current workflow ID.
    /// </summary>
    public Guid WorkflowId { get; }

    /// <summary>
    /// The current item when running in "Run for Each Item" mode.
    /// In "Run Once" mode, this is the same as Input.
    /// </summary>
    public JsonElement CurrentItem { get; internal set; }

    /// <summary>
    /// The index of the current item when running in "Run for Each Item" mode.
    /// </summary>
    public int ItemIndex { get; internal set; }

    /// <summary>
    /// Collected log messages from the script execution.
    /// </summary>
    public IReadOnlyList<string> Logs => _logs;

    public CodeNodeGlobals(
        JsonElement input,
        Guid executionId,
        Guid workflowId,
        ILogger logger)
    {
        Input = input;
        CurrentItem = input;
        ExecutionId = executionId;
        WorkflowId = workflowId;
        _logger = logger;
    }

    /// <summary>
    /// Log a message. Messages are captured and available in execution output.
    /// </summary>
    public void Log(string message)
    {
        _logs.Add(message);
        _logger.LogInformation("[CodeNode] {Message}", message);
    }

    /// <summary>
    /// Log a formatted message.
    /// </summary>
    public void Log(string format, params object[] args)
    {
        var message = string.Format(format, args);
        _logs.Add(message);
        _logger.LogInformation("[CodeNode] {Message}", message);
    }

    /// <summary>
    /// Deserialize the input data to a specific type.
    /// </summary>
    public T? GetInput<T>() =>
        JsonSerializer.Deserialize<T>(Input.GetRawText());

    /// <summary>
    /// Deserialize the current item to a specific type.
    /// </summary>
    public T? GetCurrentItem<T>() =>
        JsonSerializer.Deserialize<T>(CurrentItem.GetRawText());

    /// <summary>
    /// Get a property from the input data by name.
    /// </summary>
    public JsonElement? GetProperty(string name)
    {
        if (Input.ValueKind == JsonValueKind.Object && Input.TryGetProperty(name, out var value))
            return value;
        return null;
    }

    /// <summary>
    /// Get the input items as an array. If input is not an array, wraps it in one.
    /// </summary>
    public JsonElement[] GetItems()
    {
        if (Input.ValueKind == JsonValueKind.Array)
        {
            return Input.EnumerateArray().ToArray();
        }

        return [Input];
    }

    /// <summary>
    /// Create a JSON object from a dictionary.
    /// </summary>
    public static JsonElement ToJson(object value) =>
        JsonSerializer.SerializeToElement(value);
}
