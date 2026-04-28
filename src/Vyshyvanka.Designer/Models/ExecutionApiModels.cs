using System.Text.Json;
using Vyshyvanka.Core.Enums;

namespace Vyshyvanka.Designer.Models;

/// <summary>
/// Request to trigger a workflow execution.
/// </summary>
public record TriggerExecutionRequest
{
    public Guid WorkflowId { get; init; }
    public JsonElement? InputData { get; init; }
    public ExecutionMode Mode { get; init; } = ExecutionMode.Api;
    public string? TargetNodeId { get; init; }
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
