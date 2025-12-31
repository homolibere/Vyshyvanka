using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using FlowForge.Core.Enums;
using FlowForge.Core.Models;

namespace FlowForge.Api.Models;

/// <summary>
/// Request to create a new workflow.
/// </summary>
public record CreateWorkflowRequest
{
    /// <summary>Display name of the workflow.</summary>
    [Required(ErrorMessage = "Name is required")]
    [MinLength(1, ErrorMessage = "Name cannot be empty")]
    [MaxLength(200, ErrorMessage = "Name cannot exceed 200 characters")]
    public string Name { get; init; } = string.Empty;
    
    /// <summary>Optional description.</summary>
    [MaxLength(2000, ErrorMessage = "Description cannot exceed 2000 characters")]
    public string? Description { get; init; }
    
    /// <summary>Whether the workflow is active.</summary>
    public bool IsActive { get; init; }
    
    /// <summary>Nodes in the workflow.</summary>
    [Required(ErrorMessage = "Nodes are required")]
    public List<WorkflowNodeDto> Nodes { get; init; } = [];
    
    /// <summary>Connections between nodes.</summary>
    public List<ConnectionDto> Connections { get; init; } = [];
    
    /// <summary>Workflow settings.</summary>
    public WorkflowSettingsDto? Settings { get; init; }
    
    /// <summary>Tags for categorization.</summary>
    public List<string> Tags { get; init; } = [];
}

/// <summary>
/// Request to update an existing workflow.
/// </summary>
public record UpdateWorkflowRequest
{
    /// <summary>Display name of the workflow.</summary>
    [Required(ErrorMessage = "Name is required")]
    [MinLength(1, ErrorMessage = "Name cannot be empty")]
    [MaxLength(200, ErrorMessage = "Name cannot exceed 200 characters")]
    public string Name { get; init; } = string.Empty;
    
    /// <summary>Optional description.</summary>
    [MaxLength(2000, ErrorMessage = "Description cannot exceed 2000 characters")]
    public string? Description { get; init; }
    
    /// <summary>Whether the workflow is active.</summary>
    public bool IsActive { get; init; }
    
    /// <summary>Nodes in the workflow.</summary>
    [Required(ErrorMessage = "Nodes are required")]
    public List<WorkflowNodeDto> Nodes { get; init; } = [];
    
    /// <summary>Connections between nodes.</summary>
    public List<ConnectionDto> Connections { get; init; } = [];
    
    /// <summary>Workflow settings.</summary>
    public WorkflowSettingsDto? Settings { get; init; }
    
    /// <summary>Tags for categorization.</summary>
    public List<string> Tags { get; init; } = [];
    
    /// <summary>Expected version for optimistic concurrency.</summary>
    public int Version { get; init; }
}


/// <summary>
/// Workflow node DTO.
/// </summary>
public record WorkflowNodeDto
{
    /// <summary>Unique identifier within the workflow.</summary>
    [Required(ErrorMessage = "Node ID is required")]
    public string Id { get; init; } = string.Empty;
    
    /// <summary>Node type identifier.</summary>
    [Required(ErrorMessage = "Node type is required")]
    public string Type { get; init; } = string.Empty;
    
    /// <summary>Display name.</summary>
    [Required(ErrorMessage = "Node name is required")]
    [MaxLength(200, ErrorMessage = "Node name cannot exceed 200 characters")]
    public string Name { get; init; } = string.Empty;
    
    /// <summary>Node configuration.</summary>
    public JsonElement? Configuration { get; init; }
    
    /// <summary>Position on canvas.</summary>
    public PositionDto Position { get; init; } = new();
    
    /// <summary>Associated credential ID.</summary>
    public Guid? CredentialId { get; init; }
}

/// <summary>
/// Connection DTO.
/// </summary>
public record ConnectionDto
{
    /// <summary>Source node ID.</summary>
    [Required(ErrorMessage = "Source node ID is required")]
    public string SourceNodeId { get; init; } = string.Empty;
    
    /// <summary>Source port name.</summary>
    public string SourcePort { get; init; } = "output";
    
    /// <summary>Target node ID.</summary>
    [Required(ErrorMessage = "Target node ID is required")]
    public string TargetNodeId { get; init; } = string.Empty;
    
    /// <summary>Target port name.</summary>
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
    /// <summary>Maximum execution time in seconds.</summary>
    public int? TimeoutSeconds { get; init; }
    
    /// <summary>Maximum retry attempts.</summary>
    [Range(0, 10, ErrorMessage = "MaxRetries must be between 0 and 10")]
    public int MaxRetries { get; init; }
    
    /// <summary>Error handling mode.</summary>
    public ErrorHandlingMode ErrorHandling { get; init; }
    
    /// <summary>Maximum parallel node executions per level. 0 or negative means use default.</summary>
    [Range(-1, 1000, ErrorMessage = "MaxDegreeOfParallelism must be between -1 and 1000")]
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
    
    /// <summary>Creates a response from a workflow model.</summary>
    public static WorkflowResponse FromModel(Workflow workflow)
    {
        return new WorkflowResponse
        {
            Id = workflow.Id,
            Name = workflow.Name,
            Description = workflow.Description,
            Version = workflow.Version,
            IsActive = workflow.IsActive,
            Nodes = workflow.Nodes.Select(n => new WorkflowNodeDto
            {
                Id = n.Id,
                Type = n.Type,
                Name = n.Name,
                Configuration = n.Configuration,
                Position = new PositionDto(n.Position.X, n.Position.Y),
                CredentialId = n.CredentialId
            }).ToList(),
            Connections = workflow.Connections.Select(c => new ConnectionDto
            {
                SourceNodeId = c.SourceNodeId,
                SourcePort = c.SourcePort,
                TargetNodeId = c.TargetNodeId,
                TargetPort = c.TargetPort
            }).ToList(),
            Settings = workflow.Settings is not null ? new WorkflowSettingsDto
            {
                TimeoutSeconds = workflow.Settings.Timeout.HasValue 
                    ? (int)workflow.Settings.Timeout.Value.TotalSeconds 
                    : null,
                MaxRetries = workflow.Settings.MaxRetries,
                ErrorHandling = workflow.Settings.ErrorHandling,
                MaxDegreeOfParallelism = workflow.Settings.MaxDegreeOfParallelism
            } : null,
            Tags = workflow.Tags,
            CreatedAt = workflow.CreatedAt,
            UpdatedAt = workflow.UpdatedAt,
            CreatedBy = workflow.CreatedBy
        };
    }
}

/// <summary>
/// Paginated list response.
/// </summary>
public record PagedResponse<T>
{
    public List<T> Items { get; init; } = [];
    public int Skip { get; init; }
    public int Take { get; init; }
    public int TotalCount { get; init; }
}
