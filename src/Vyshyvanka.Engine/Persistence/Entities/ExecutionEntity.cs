using System.ComponentModel.DataAnnotations;
using Vyshyvanka.Core.Enums;

namespace Vyshyvanka.Engine.Persistence.Entities;

/// <summary>
/// EF Core entity for workflow execution persistence.
/// </summary>
public class ExecutionEntity
{
    [Key]
    public Guid Id { get; set; }
    
    public Guid WorkflowId { get; set; }
    
    public int WorkflowVersion { get; set; }
    
    public ExecutionStatus Status { get; set; }
    
    public ExecutionMode Mode { get; set; }
    
    public DateTime StartedAt { get; set; }
    
    public DateTime? CompletedAt { get; set; }
    
    /// <summary>JSON-serialized trigger data.</summary>
    public string? TriggerDataJson { get; set; }
    
    /// <summary>JSON-serialized output data.</summary>
    public string? OutputDataJson { get; set; }
    
    public string? ErrorMessage { get; set; }
    
    /// <summary>Navigation property for node executions.</summary>
    public List<NodeExecutionEntity> NodeExecutions { get; set; } = [];
}
