using System.Text.Json;
using Vyshyvanka.Core.Models;

namespace Vyshyvanka.Core.Interfaces;

/// <summary>
/// Engine for executing workflows.
/// </summary>
public interface IWorkflowEngine
{
    /// <summary>Executes a workflow with the given context.</summary>
    Task<ExecutionResult> ExecuteAsync(
        Workflow workflow,
        IExecutionContext context,
        CancellationToken cancellationToken = default);

    /// <summary>Executes a single node with the given context.</summary>
    Task<ExecutionResult> ExecuteNodeAsync(
        WorkflowNode node,
        IExecutionContext context,
        CancellationToken cancellationToken = default);

    /// <summary>Cancels an in-progress execution.</summary>
    Task CancelExecutionAsync(Guid executionId);
}

/// <summary>
/// Result of a workflow or node execution.
/// </summary>
public record ExecutionResult
{
    /// <summary>Execution identifier.</summary>
    public Guid ExecutionId { get; init; }

    /// <summary>Whether execution completed successfully.</summary>
    public bool Success { get; init; }

    /// <summary>Output data from the execution.</summary>
    public object? OutputData { get; init; }

    /// <summary>Error message if execution failed.</summary>
    public string? ErrorMessage { get; init; }

    /// <summary>Duration of the execution.</summary>
    public TimeSpan Duration { get; init; }

    /// <summary>Results from individual node executions.</summary>
    public List<NodeExecutionResult> NodeResults { get; init; } = [];
}

/// <summary>
/// Result of a single node execution.
/// </summary>
public record NodeExecutionResult
{
    /// <summary>Node identifier.</summary>
    public string NodeId { get; init; } = string.Empty;

    /// <summary>Whether node execution succeeded.</summary>
    public bool Success { get; init; }

    /// <summary>Input data received by the node (gathered from upstream outputs).</summary>
    public JsonElement? InputData { get; init; }

    /// <summary>Output data from the node.</summary>
    public object? OutputData { get; init; }

    /// <summary>Error message if node failed.</summary>
    public string? ErrorMessage { get; init; }

    /// <summary>Duration of node execution.</summary>
    public TimeSpan Duration { get; init; }
}
