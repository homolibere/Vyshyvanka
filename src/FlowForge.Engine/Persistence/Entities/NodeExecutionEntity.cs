using System.ComponentModel.DataAnnotations;
using FlowForge.Core.Enums;

namespace FlowForge.Engine.Persistence.Entities;

/// <summary>
/// EF Core entity for node execution persistence.
/// </summary>
public class NodeExecutionEntity
{
    [Key]
    public int Id { get; set; }
    
    public Guid ExecutionId { get; set; }
    
    public string NodeId { get; set; } = string.Empty;
    
    public ExecutionStatus Status { get; set; }
    
    public DateTime StartedAt { get; set; }
    
    public DateTime? CompletedAt { get; set; }
    
    /// <summary>JSON-serialized input data.</summary>
    public string? InputDataJson { get; set; }
    
    /// <summary>JSON-serialized output data.</summary>
    public string? OutputDataJson { get; set; }
    
    public string? ErrorMessage { get; set; }
    
    /// <summary>Navigation property to parent execution.</summary>
    public ExecutionEntity Execution { get; set; } = null!;
}
