using System.Text.Json;
using System.Text.Json.Serialization;
using FlowForge.Core.Enums;

namespace FlowForge.Core.Models;

/// <summary>
/// Represents a workflow execution record.
/// </summary>
public record Execution
{
    /// <summary>Unique identifier for this execution.</summary>
    [JsonPropertyName("id")]
    public Guid Id { get; init; }
    
    /// <summary>ID of the workflow that was executed.</summary>
    [JsonPropertyName("workflowId")]
    public Guid WorkflowId { get; init; }
    
    /// <summary>Version of the workflow at execution time.</summary>
    [JsonPropertyName("workflowVersion")]
    public int WorkflowVersion { get; init; }
    
    /// <summary>Current status of the execution.</summary>
    [JsonPropertyName("status")]
    public ExecutionStatus Status { get; init; }
    
    /// <summary>How the execution was triggered.</summary>
    [JsonPropertyName("mode")]
    public ExecutionMode Mode { get; init; }
    
    /// <summary>When execution started.</summary>
    [JsonPropertyName("startedAt")]
    public DateTime StartedAt { get; init; }
    
    /// <summary>When execution completed (if finished).</summary>
    [JsonPropertyName("completedAt")]
    public DateTime? CompletedAt { get; init; }
    
    /// <summary>Data that triggered the execution.</summary>
    [JsonPropertyName("triggerData")]
    public JsonElement? TriggerData { get; init; }
    
    /// <summary>Final output data from the execution.</summary>
    [JsonPropertyName("outputData")]
    public JsonElement? OutputData { get; init; }
    
    /// <summary>Error message if execution failed.</summary>
    [JsonPropertyName("errorMessage")]
    public string? ErrorMessage { get; init; }
    
    /// <summary>Individual node execution records.</summary>
    [JsonPropertyName("nodeExecutions")]
    public List<NodeExecution> NodeExecutions { get; init; } = [];
}

/// <summary>
/// Represents execution of a single node.
/// </summary>
public record NodeExecution
{
    /// <summary>ID of the node that was executed.</summary>
    [JsonPropertyName("nodeId")]
    public string NodeId { get; init; } = string.Empty;
    
    /// <summary>Status of this node's execution.</summary>
    [JsonPropertyName("status")]
    public ExecutionStatus Status { get; init; }
    
    /// <summary>When node execution started.</summary>
    [JsonPropertyName("startedAt")]
    public DateTime StartedAt { get; init; }
    
    /// <summary>When node execution completed.</summary>
    [JsonPropertyName("completedAt")]
    public DateTime? CompletedAt { get; init; }
    
    /// <summary>Input data received by the node.</summary>
    [JsonPropertyName("inputData")]
    public JsonElement? InputData { get; init; }
    
    /// <summary>Output data produced by the node.</summary>
    [JsonPropertyName("outputData")]
    public JsonElement? OutputData { get; init; }
    
    /// <summary>Error message if node failed.</summary>
    [JsonPropertyName("errorMessage")]
    public string? ErrorMessage { get; init; }
}
