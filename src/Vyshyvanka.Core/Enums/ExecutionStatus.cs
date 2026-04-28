namespace Vyshyvanka.Core.Enums;

/// <summary>
/// Represents the status of a workflow or node execution.
/// </summary>
public enum ExecutionStatus
{
    /// <summary>Execution is queued but not yet started.</summary>
    Pending,
    
    /// <summary>Execution is currently in progress.</summary>
    Running,
    
    /// <summary>Execution completed successfully.</summary>
    Completed,
    
    /// <summary>Execution failed with an error.</summary>
    Failed,
    
    /// <summary>Execution was cancelled by user or system.</summary>
    Cancelled
}
