using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using FlowForge.Core.Enums;
using FlowForge.Core.Models;

namespace FlowForge.Api.Models;

/// <summary>
/// Request to trigger a workflow execution.
/// </summary>
public record TriggerExecutionRequest
{
    /// <summary>ID of the workflow to execute.</summary>
    [Required(ErrorMessage = "WorkflowId is required")]
    public Guid WorkflowId { get; init; }
    
    /// <summary>Input data for the execution.</summary>
    public JsonElement? InputData { get; init; }
    
    /// <summary>Execution mode.</summary>
    public ExecutionMode Mode { get; init; } = ExecutionMode.Api;
}

/// <summary>
/// Query parameters for filtering executions.
/// </summary>
public record ExecutionQueryRequest
{
    /// <summary>Filter by workflow ID.</summary>
    public Guid? WorkflowId { get; init; }
    
    /// <summary>Filter by status.</summary>
    public ExecutionStatus? Status { get; init; }
    
    /// <summary>Filter by mode.</summary>
    public ExecutionMode? Mode { get; init; }
    
    /// <summary>Filter by start date (from).</summary>
    public DateTime? StartDateFrom { get; init; }
    
    /// <summary>Filter by start date (to).</summary>
    public DateTime? StartDateTo { get; init; }
    
    /// <summary>Number of records to skip.</summary>
    [Range(0, int.MaxValue)]
    public int Skip { get; init; }
    
    /// <summary>Number of records to take.</summary>
    [Range(1, 100)]
    public int Take { get; init; } = 50;
}

/// <summary>
/// Execution response DTO.
/// </summary>
public record ExecutionResponse
{
    public Guid Id { get; init; }
    public Guid WorkflowId { get; init; }
    public int WorkflowVersion { get; init; }
    public ExecutionStatus Status { get; init; }
    public ExecutionMode Mode { get; init; }
    public DateTime StartedAt { get; init; }
    public DateTime? CompletedAt { get; init; }
    public TimeSpan? Duration { get; init; }
    public JsonElement? TriggerData { get; init; }
    public JsonElement? OutputData { get; init; }
    public string? ErrorMessage { get; init; }
    public List<NodeExecutionResponse> NodeExecutions { get; init; } = [];
    
    /// <summary>Creates a response from an execution model.</summary>
    public static ExecutionResponse FromModel(Execution execution)
    {
        return new ExecutionResponse
        {
            Id = execution.Id,
            WorkflowId = execution.WorkflowId,
            WorkflowVersion = execution.WorkflowVersion,
            Status = execution.Status,
            Mode = execution.Mode,
            StartedAt = execution.StartedAt,
            CompletedAt = execution.CompletedAt,
            Duration = execution.CompletedAt.HasValue 
                ? execution.CompletedAt.Value - execution.StartedAt 
                : null,
            TriggerData = execution.TriggerData,
            OutputData = execution.OutputData,
            ErrorMessage = execution.ErrorMessage,
            NodeExecutions = execution.NodeExecutions
                .Select(NodeExecutionResponse.FromModel)
                .ToList()
        };
    }
}


/// <summary>
/// Node execution response DTO.
/// </summary>
public record NodeExecutionResponse
{
    public string NodeId { get; init; } = string.Empty;
    public ExecutionStatus Status { get; init; }
    public DateTime StartedAt { get; init; }
    public DateTime? CompletedAt { get; init; }
    public TimeSpan? Duration { get; init; }
    public JsonElement? InputData { get; init; }
    public JsonElement? OutputData { get; init; }
    public string? ErrorMessage { get; init; }
    
    /// <summary>Creates a response from a node execution model.</summary>
    public static NodeExecutionResponse FromModel(NodeExecution nodeExecution)
    {
        return new NodeExecutionResponse
        {
            NodeId = nodeExecution.NodeId,
            Status = nodeExecution.Status,
            StartedAt = nodeExecution.StartedAt,
            CompletedAt = nodeExecution.CompletedAt,
            Duration = nodeExecution.CompletedAt.HasValue 
                ? nodeExecution.CompletedAt.Value - nodeExecution.StartedAt 
                : null,
            InputData = nodeExecution.InputData,
            OutputData = nodeExecution.OutputData,
            ErrorMessage = nodeExecution.ErrorMessage
        };
    }
}

/// <summary>
/// Summary response for execution list (without node details).
/// </summary>
public record ExecutionSummaryResponse
{
    public Guid Id { get; init; }
    public Guid WorkflowId { get; init; }
    public int WorkflowVersion { get; init; }
    public ExecutionStatus Status { get; init; }
    public ExecutionMode Mode { get; init; }
    public DateTime StartedAt { get; init; }
    public DateTime? CompletedAt { get; init; }
    public TimeSpan? Duration { get; init; }
    public string? ErrorMessage { get; init; }
    
    /// <summary>Creates a summary response from an execution model.</summary>
    public static ExecutionSummaryResponse FromModel(Execution execution)
    {
        return new ExecutionSummaryResponse
        {
            Id = execution.Id,
            WorkflowId = execution.WorkflowId,
            WorkflowVersion = execution.WorkflowVersion,
            Status = execution.Status,
            Mode = execution.Mode,
            StartedAt = execution.StartedAt,
            CompletedAt = execution.CompletedAt,
            Duration = execution.CompletedAt.HasValue 
                ? execution.CompletedAt.Value - execution.StartedAt 
                : null,
            ErrorMessage = execution.ErrorMessage
        };
    }
}
