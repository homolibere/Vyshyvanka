using System.Text.Json;
using Vyshyvanka.Core.Enums;

namespace Vyshyvanka.Contracts.Executions;

/// <summary>
/// Request payload to trigger a new execution of a workflow.
/// </summary>
public record TriggerExecutionRequest
{
    /// <summary>Identifier of the workflow to execute.</summary>
    public Guid WorkflowId { get; init; }

    /// <summary>Optional input data passed to the trigger node as raw JSON. <c>null</c> when the workflow needs no input.</summary>
    public JsonElement? InputData { get; init; }

    /// <summary>How the execution was initiated. Defaults to <see cref="ExecutionMode.Api"/>.</summary>
    public ExecutionMode Mode { get; init; } = ExecutionMode.Api;

    /// <summary>
    /// Optional node to run the workflow up to, used for partial or step-by-step execution.
    /// <c>null</c> runs the entire workflow.
    /// </summary>
    public string? TargetNodeId { get; init; }

    /// <summary>
    /// When <see cref="TargetNodeId"/> is set, whether the target node itself is executed
    /// (<c>true</c>) or only its upstream dependencies (<c>false</c>). Defaults to <c>true</c>.
    /// </summary>
    public bool IncludeTargetNode { get; init; } = true;
}

/// <summary>
/// Request payload to execute a single node in isolation with explicit input data.
/// Used by the designer to test one node without running the whole workflow.
/// </summary>
public record ExecuteNodeRequest
{
    /// <summary>Identifier of the workflow the node belongs to.</summary>
    public Guid WorkflowId { get; init; }

    /// <summary>Identifier of the node to execute.</summary>
    public string NodeId { get; init; } = string.Empty;

    /// <summary>Raw JSON input data supplied to the node for this test run.</summary>
    public JsonElement InputData { get; init; }
}

/// <summary>
/// Query filter and paging parameters for listing executions.
/// </summary>
public record ExecutionQueryRequest
{
    /// <summary>Filter to executions of a specific workflow. <c>null</c> returns executions across all workflows.</summary>
    public Guid? WorkflowId { get; init; }

    /// <summary>Filter by execution status. <c>null</c> returns all statuses.</summary>
    public ExecutionStatus? Status { get; init; }

    /// <summary>Filter by how the execution was triggered. <c>null</c> returns all modes.</summary>
    public ExecutionMode? Mode { get; init; }

    /// <summary>Inclusive lower bound on the execution start time (UTC). <c>null</c> for no lower bound.</summary>
    public DateTime? StartDateFrom { get; init; }

    /// <summary>Inclusive upper bound on the execution start time (UTC). <c>null</c> for no upper bound.</summary>
    public DateTime? StartDateTo { get; init; }

    /// <summary>Number of matching executions to skip before the returned page (zero-based offset).</summary>
    public int Skip { get; init; }

    /// <summary>Maximum number of executions to return in the page. Defaults to 50.</summary>
    public int Take { get; init; } = 50;
}

/// <summary>
/// Full detail of a single execution, including per-node results.
/// </summary>
public record ExecutionResponse
{
    /// <summary>Server-assigned unique identifier of the execution.</summary>
    public Guid Id { get; init; }

    /// <summary>Identifier of the workflow that was executed.</summary>
    public Guid WorkflowId { get; init; }

    /// <summary>Display name of the workflow at query time, provided for convenience. <c>null</c> when unavailable.</summary>
    public string? WorkflowName { get; init; }

    /// <summary>Version of the workflow that was executed.</summary>
    public int WorkflowVersion { get; init; }

    /// <summary>Current status of the execution. Terminal statuses are final and never change.</summary>
    public ExecutionStatus Status { get; init; }

    /// <summary>How the execution was triggered.</summary>
    public ExecutionMode Mode { get; init; }

    /// <summary>UTC timestamp when the execution started.</summary>
    public DateTime StartedAt { get; init; }

    /// <summary>UTC timestamp when the execution reached a terminal status. <c>null</c> while still pending or running.</summary>
    public DateTime? CompletedAt { get; init; }

    /// <summary>Total elapsed time of the execution. <c>null</c> until the execution completes.</summary>
    public TimeSpan? Duration { get; init; }

    /// <summary>Raw JSON input that triggered the execution. <c>null</c> when the trigger carried no data.</summary>
    public JsonElement? TriggerData { get; init; }

    /// <summary>Raw JSON output produced by the execution. <c>null</c> until output is available.</summary>
    public JsonElement? OutputData { get; init; }

    /// <summary>Error message when the execution failed. <c>null</c> when it did not fail.</summary>
    public string? ErrorMessage { get; init; }

    /// <summary>Per-node execution results, in the order the nodes were run.</summary>
    public List<NodeExecutionResponse> NodeExecutions { get; init; } = [];
}

/// <summary>
/// Result of executing a single node within an execution.
/// </summary>
public record NodeExecutionResponse
{
    /// <summary>Identifier of the node this result belongs to.</summary>
    public string NodeId { get; init; } = string.Empty;

    /// <summary>Status of this node's execution.</summary>
    public ExecutionStatus Status { get; init; }

    /// <summary>UTC timestamp when the node started executing.</summary>
    public DateTime StartedAt { get; init; }

    /// <summary>UTC timestamp when the node finished. <c>null</c> while still running.</summary>
    public DateTime? CompletedAt { get; init; }

    /// <summary>Elapsed time for this node. <c>null</c> until the node completes.</summary>
    public TimeSpan? Duration { get; init; }

    /// <summary>Raw JSON input the node received. <c>null</c> when it had none.</summary>
    public JsonElement? InputData { get; init; }

    /// <summary>Raw JSON output the node produced, available to downstream nodes via expressions. <c>null</c> when it produced none.</summary>
    public JsonElement? OutputData { get; init; }

    /// <summary>Error message when this node failed. <c>null</c> when it did not fail.</summary>
    public string? ErrorMessage { get; init; }
}

/// <summary>
/// Lightweight execution summary for list views, omitting per-node details and payloads.
/// </summary>
public record ExecutionSummaryResponse
{
    /// <summary>Server-assigned unique identifier of the execution.</summary>
    public Guid Id { get; init; }

    /// <summary>Identifier of the workflow that was executed.</summary>
    public Guid WorkflowId { get; init; }

    /// <summary>Display name of the workflow at query time, provided for convenience. <c>null</c> when unavailable.</summary>
    public string? WorkflowName { get; init; }

    /// <summary>Version of the workflow that was executed.</summary>
    public int WorkflowVersion { get; init; }

    /// <summary>Current status of the execution.</summary>
    public ExecutionStatus Status { get; init; }

    /// <summary>How the execution was triggered.</summary>
    public ExecutionMode Mode { get; init; }

    /// <summary>UTC timestamp when the execution started.</summary>
    public DateTime StartedAt { get; init; }

    /// <summary>UTC timestamp when the execution reached a terminal status. <c>null</c> while still pending or running.</summary>
    public DateTime? CompletedAt { get; init; }

    /// <summary>Total elapsed time of the execution. <c>null</c> until the execution completes.</summary>
    public TimeSpan? Duration { get; init; }

    /// <summary>Error message when the execution failed. <c>null</c> when it did not fail.</summary>
    public string? ErrorMessage { get; init; }
}
