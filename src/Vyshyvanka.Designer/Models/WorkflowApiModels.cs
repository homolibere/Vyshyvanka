using System.Text.Json;
using Vyshyvanka.Core.Enums;

namespace Vyshyvanka.Designer.Models;

/// <summary>
/// Request to create a new workflow.
/// </summary>
public record CreateWorkflowRequest
{
    public string Name { get; init; } = string.Empty;
    public string? Description { get; init; }
    public bool IsActive { get; init; }
    public List<WorkflowNodeDto> Nodes { get; init; } = [];
    public List<ConnectionDto> Connections { get; init; } = [];
    public WorkflowSettingsDto? Settings { get; init; }
    public List<string> Tags { get; init; } = [];
}

/// <summary>
/// Request to update an existing workflow.
/// </summary>
public record UpdateWorkflowRequest
{
    public string Name { get; init; } = string.Empty;
    public string? Description { get; init; }
    public bool IsActive { get; init; }
    public List<WorkflowNodeDto> Nodes { get; init; } = [];
    public List<ConnectionDto> Connections { get; init; } = [];
    public WorkflowSettingsDto? Settings { get; init; }
    public List<string> Tags { get; init; } = [];
    public int Version { get; init; }
}

/// <summary>
/// Workflow node DTO.
/// </summary>
public record WorkflowNodeDto
{
    public string Id { get; init; } = string.Empty;
    public string Type { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public JsonElement? Configuration { get; init; }
    public PositionDto Position { get; init; } = new();
    public Guid? CredentialId { get; init; }
}

/// <summary>
/// Connection DTO.
/// </summary>
public record ConnectionDto
{
    public string SourceNodeId { get; init; } = string.Empty;
    public string SourcePort { get; init; } = "output";
    public string TargetNodeId { get; init; } = string.Empty;
    public string TargetPort { get; init; } = "input";
}

/// <summary>
/// Position DTO.
/// </summary>
public record PositionDto(double X = 0, double Y = 0);

/// <summary>
/// Workflow settings DTO.
/// </summary>
public record WorkflowSettingsDto
{
    public int? TimeoutSeconds { get; init; }
    public int MaxRetries { get; init; }
    public ErrorHandlingMode ErrorHandling { get; init; }
    public int MaxDegreeOfParallelism { get; init; }
}

/// <summary>
/// Workflow response DTO.
/// </summary>
public record WorkflowResponse
{
    public Guid Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public string? Description { get; init; }
    public int Version { get; init; }
    public bool IsActive { get; init; }
    public List<WorkflowNodeDto> Nodes { get; init; } = [];
    public List<ConnectionDto> Connections { get; init; } = [];
    public WorkflowSettingsDto? Settings { get; init; }
    public List<string> Tags { get; init; } = [];
    public DateTime CreatedAt { get; init; }
    public DateTime UpdatedAt { get; init; }
    public Guid CreatedBy { get; init; }
}

/// <summary>
/// Paginated workflow response.
/// </summary>
public record PagedWorkflowResponse
{
    public List<WorkflowResponse> Items { get; init; } = [];
    public int Skip { get; init; }
    public int Take { get; init; }
    public int TotalCount { get; init; }
}
