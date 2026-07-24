using System.Text.Json;
using Vyshyvanka.Core.Enums;

namespace Vyshyvanka.Contracts.Executions;

/// <summary>
/// Request to trigger a workflow execution.
/// </summary>
public record TriggerExecutionRequest
{
    public Guid WorkflowId { get; init; }
    public JsonElement? InputData { get; init; }
    public ExecutionMode Mode { get; init; } = ExecutionMode.Api;
    public string? TargetNodeId { get; init; }
    public bool IncludeTargetNode { get; init; } = true;
}

/// <summary>
/// Request to execute a single node with provided input data.
/// </summary>
public record ExecuteNodeRequest
{
    public Guid WorkflowId { get; init; }
    public string NodeId { get; init; } = string.Empty;
    public JsonElement InputData { get; init; }
}

/// <summary>
/// Query parameters for filtering executions.
/// </summary>
public record ExecutionQueryRequest
{
    public Guid? WorkflowId { get; init; }
    public ExecutionStatus? Status { get; init; }
    public ExecutionMode? Mode { get; init; }
    public DateTime? StartDateFrom { get; init; }
    public DateTime? StartDateTo { get; init; }
    public int Skip { get; init; }
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
}
