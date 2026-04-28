using System.Text.Json;
using Vyshyvanka.Core.Enums;

namespace Vyshyvanka.Core.Interfaces;

/// <summary>
/// Represents a node that can be executed in a workflow.
/// </summary>
public interface INode
{
    /// <summary>Unique identifier for this node instance.</summary>
    string Id { get; }
    
    /// <summary>Type identifier for this node (e.g., "http-request", "if-condition").</summary>
    string Type { get; }
    
    /// <summary>Category of this node.</summary>
    NodeCategory Category { get; }
    
    /// <summary>Executes the node with the given input and context.</summary>
    Task<NodeOutput> ExecuteAsync(NodeInput input, IExecutionContext context);
}

/// <summary>
/// Represents a trigger node that initiates workflow execution.
/// </summary>
public interface ITriggerNode : INode
{
    /// <summary>Determines if this trigger should start a workflow execution.</summary>
    Task<bool> ShouldTriggerAsync(TriggerContext context);
}

/// <summary>
/// Input data for node execution.
/// </summary>
public record NodeInput
{
    /// <summary>Input data from upstream nodes.</summary>
    public JsonElement Data { get; init; }
    
    /// <summary>Node configuration.</summary>
    public JsonElement Configuration { get; init; }
    
    /// <summary>Optional credential ID for this node.</summary>
    public Guid? CredentialId { get; init; }
}

/// <summary>
/// Output data from node execution.
/// </summary>
public record NodeOutput
{
    /// <summary>Output data to pass to downstream nodes.</summary>
    public JsonElement Data { get; init; }
    
    /// <summary>Indicates if execution was successful.</summary>
    public bool Success { get; init; } = true;
    
    /// <summary>Error message if execution failed.</summary>
    public string? ErrorMessage { get; init; }
}

/// <summary>
/// Context for trigger evaluation.
/// </summary>
public record TriggerContext
{
    /// <summary>Trigger-specific data (e.g., webhook payload, schedule time).</summary>
    public JsonElement TriggerData { get; init; }
}
