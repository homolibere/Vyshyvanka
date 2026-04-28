using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using Vyshyvanka.Core.Enums;

namespace Vyshyvanka.Core.Models;

/// <summary>
/// Represents a workflow definition.
/// </summary>
public record Workflow
{
    /// <summary>Unique identifier for the workflow.</summary>
    [JsonPropertyName("id")]
    public Guid Id { get; init; }
    
    /// <summary>Display name of the workflow.</summary>
    [JsonPropertyName("name")]
    [Required(ErrorMessage = "Workflow name is required")]
    [MinLength(1, ErrorMessage = "Workflow name cannot be empty")]
    [MaxLength(200, ErrorMessage = "Workflow name cannot exceed 200 characters")]
    public string Name { get; init; } = string.Empty;
    
    /// <summary>Optional description.</summary>
    [JsonPropertyName("description")]
    [MaxLength(2000, ErrorMessage = "Description cannot exceed 2000 characters")]
    public string? Description { get; init; }
    
    /// <summary>Version number for optimistic concurrency.</summary>
    [JsonPropertyName("version")]
    [Range(0, int.MaxValue, ErrorMessage = "Version must be non-negative")]
    public int Version { get; init; }
    
    /// <summary>Whether the workflow is active and can be triggered.</summary>
    [JsonPropertyName("isActive")]
    public bool IsActive { get; init; }
    
    /// <summary>Nodes in the workflow.</summary>
    [JsonPropertyName("nodes")]
    [Required(ErrorMessage = "Nodes collection is required")]
    public List<WorkflowNode> Nodes { get; init; } = [];
    
    /// <summary>Connections between nodes.</summary>
    [JsonPropertyName("connections")]
    [Required(ErrorMessage = "Connections collection is required")]
    public List<Connection> Connections { get; init; } = [];
    
    /// <summary>Workflow settings.</summary>
    [JsonPropertyName("settings")]
    public WorkflowSettings Settings { get; init; } = new();
    
    /// <summary>Tags for categorization and filtering.</summary>
    [JsonPropertyName("tags")]
    public List<string> Tags { get; init; } = [];
    
    /// <summary>When the workflow was created.</summary>
    [JsonPropertyName("createdAt")]
    public DateTime CreatedAt { get; init; }
    
    /// <summary>When the workflow was last updated.</summary>
    [JsonPropertyName("updatedAt")]
    public DateTime UpdatedAt { get; init; }
    
    /// <summary>User who created the workflow.</summary>
    [JsonPropertyName("createdBy")]
    public Guid CreatedBy { get; init; }
}

/// <summary>
/// Workflow execution settings.
/// </summary>
public record WorkflowSettings
{
    /// <summary>Maximum execution time.</summary>
    [JsonPropertyName("timeout")]
    public TimeSpan? Timeout { get; init; }
    
    /// <summary>Maximum retry attempts for failed nodes.</summary>
    [JsonPropertyName("maxRetries")]
    [Range(0, 10, ErrorMessage = "MaxRetries must be between 0 and 10")]
    public int MaxRetries { get; init; }
    
    /// <summary>How to handle node execution errors.</summary>
    [JsonPropertyName("errorHandling")]
    public ErrorHandlingMode ErrorHandling { get; init; }
    
    /// <summary>
    /// Maximum number of nodes to execute in parallel per level.
    /// 0 or negative means unlimited (default behavior).
    /// </summary>
    [JsonPropertyName("maxDegreeOfParallelism")]
    [Range(-1, 1000, ErrorMessage = "MaxDegreeOfParallelism must be between -1 and 1000")]
    public int MaxDegreeOfParallelism { get; init; }
}
