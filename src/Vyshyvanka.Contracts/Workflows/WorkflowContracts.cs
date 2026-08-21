using System.Text.Json;
using Vyshyvanka.Core.Enums;

namespace Vyshyvanka.Contracts.Workflows;

/// <summary>
/// Request payload for creating a new workflow. Sent by clients to the create-workflow endpoint.
/// </summary>
public record CreateWorkflowRequest
{
    /// <summary>Display name of the workflow. Required and shown throughout the UI.</summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>Optional free-text description explaining the workflow's purpose.</summary>
    public string? Description { get; init; }

    /// <summary>Activation state. Only <see cref="WorkflowStatus.Active"/> workflows respond to automatic triggers (webhook, cron). Manual/API execution is always allowed.</summary>
    public WorkflowStatus Status { get; init; }

    /// <summary>The nodes that make up the workflow graph. Must contain exactly one trigger node.</summary>
    public List<WorkflowNodeDto> Nodes { get; init; } = [];

    /// <summary>The directed connections wiring node output ports to node input ports.</summary>
    public List<ConnectionDto> Connections { get; init; } = [];

    /// <summary>Optional execution settings (timeout, retries, error handling). Defaults are applied when omitted.</summary>
    public WorkflowSettingsDto? Settings { get; init; }

    /// <summary>Free-form tags used to categorize and filter workflows.</summary>
    public List<string> Tags { get; init; } = [];
}

/// <summary>
/// Request payload for updating an existing workflow. Replaces the workflow's editable state in full.
/// </summary>
public record UpdateWorkflowRequest
{
    /// <summary>Display name of the workflow. Required.</summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>Optional free-text description explaining the workflow's purpose.</summary>
    public string? Description { get; init; }

    /// <summary>Activation state. Only <see cref="WorkflowStatus.Active"/> workflows respond to automatic triggers (webhook, cron). Manual/API execution is always allowed.</summary>
    public WorkflowStatus Status { get; init; }

    /// <summary>The full replacement set of nodes for the workflow graph. Must contain exactly one trigger node.</summary>
    public List<WorkflowNodeDto> Nodes { get; init; } = [];

    /// <summary>The full replacement set of connections wiring node output ports to node input ports.</summary>
    public List<ConnectionDto> Connections { get; init; } = [];

    /// <summary>Optional execution settings (timeout, retries, error handling). Defaults are applied when omitted.</summary>
    public WorkflowSettingsDto? Settings { get; init; }

    /// <summary>Free-form tags used to categorize and filter workflows.</summary>
    public List<string> Tags { get; init; } = [];

    /// <summary>
    /// The version the client last loaded, used for optimistic concurrency control.
    /// The update is rejected if it does not match the current server-side version.
    /// </summary>
    public int Version { get; init; }
}

/// <summary>
/// Represents a single node within a workflow graph, transferred between client and server.
/// </summary>
public record WorkflowNodeDto
{
    /// <summary>Client-assigned unique identifier of the node within the workflow. Referenced by connections and expressions.</summary>
    public string Id { get; init; } = string.Empty;

    /// <summary>The node type key that identifies which registered node implementation to use (e.g. <c>http-request</c>).</summary>
    public string Type { get; init; } = string.Empty;

    /// <summary>User-facing display name of the node shown on the canvas.</summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>Node-specific configuration as a raw JSON element; its shape depends on <see cref="Type"/>. <c>null</c> when unconfigured.</summary>
    public JsonElement? Configuration { get; init; }

    /// <summary>The node's position on the designer canvas.</summary>
    public PositionDto Position { get; init; } = new();

    /// <summary>Optional identifier of the credential this node uses for authentication. <c>null</c> when no credential is needed.</summary>
    public Guid? CredentialId { get; init; }
}

/// <summary>
/// Represents a directed connection from one node's output port to another node's input port.
/// </summary>
public record ConnectionDto
{
    /// <summary>Identifier of the node the connection originates from.</summary>
    public string SourceNodeId { get; init; } = string.Empty;

    /// <summary>Name of the output port on the source node. Defaults to <c>output</c>.</summary>
    public string SourcePort { get; init; } = "output";

    /// <summary>Identifier of the node the connection terminates at.</summary>
    public string TargetNodeId { get; init; } = string.Empty;

    /// <summary>Name of the input port on the target node. Defaults to <c>input</c>.</summary>
    public string TargetPort { get; init; } = "input";
}

/// <summary>
/// A two-dimensional coordinate describing a node's placement on the designer canvas.
/// </summary>
/// <param name="X">Horizontal coordinate in canvas units. Defaults to 0.</param>
/// <param name="Y">Vertical coordinate in canvas units. Defaults to 0.</param>
public record PositionDto(double X = 0, double Y = 0);

/// <summary>
/// Execution-time settings that control how a workflow's run behaves.
/// </summary>
public record WorkflowSettingsDto
{
    /// <summary>Maximum time in seconds a single execution may run before it is cancelled. <c>null</c> means no timeout.</summary>
    public int? TimeoutSeconds { get; init; }

    /// <summary>Number of times a failed node is retried before the execution gives up.</summary>
    public int MaxRetries { get; init; }

    /// <summary>Strategy the engine applies when a node fails during execution.</summary>
    public ErrorHandlingMode ErrorHandling { get; init; }

    /// <summary>Maximum number of nodes the engine may execute concurrently.</summary>
    public int MaxDegreeOfParallelism { get; init; }
}

/// <summary>
/// Full representation of a workflow returned by the API, including server-managed metadata.
/// </summary>
public record WorkflowResponse
{
    /// <summary>Server-assigned unique identifier of the workflow.</summary>
    public Guid Id { get; init; }

    /// <summary>Display name of the workflow.</summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>Optional free-text description explaining the workflow's purpose.</summary>
    public string? Description { get; init; }

    /// <summary>Current version number, incremented on each successful update for optimistic concurrency.</summary>
    public int Version { get; init; }

    /// <summary>Activation state. Only <see cref="WorkflowStatus.Active"/> workflows respond to automatic triggers (webhook, cron). Manual/API execution is always allowed.</summary>
    public WorkflowStatus Status { get; init; }

    /// <summary>The nodes that make up the workflow graph.</summary>
    public List<WorkflowNodeDto> Nodes { get; init; } = [];

    /// <summary>The directed connections wiring node output ports to node input ports.</summary>
    public List<ConnectionDto> Connections { get; init; } = [];

    /// <summary>Execution settings for the workflow. <c>null</c> when engine defaults apply.</summary>
    public WorkflowSettingsDto? Settings { get; init; }

    /// <summary>Free-form tags used to categorize and filter workflows.</summary>
    public List<string> Tags { get; init; } = [];

    /// <summary>Identifier of the folder containing this workflow. <c>null</c> when the workflow lives at the root (unfiled).</summary>
    public Guid? FolderId { get; init; }

    /// <summary>UTC timestamp when the workflow was created.</summary>
    public DateTime CreatedAt { get; init; }

    /// <summary>UTC timestamp when the workflow was last updated.</summary>
    public DateTime UpdatedAt { get; init; }

    /// <summary>Identifier of the user who owns and created the workflow.</summary>
    public Guid CreatedBy { get; init; }
}
