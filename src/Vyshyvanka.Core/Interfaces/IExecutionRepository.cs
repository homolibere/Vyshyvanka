using Vyshyvanka.Core.Enums;
using Vyshyvanka.Core.Models;

namespace Vyshyvanka.Core.Interfaces;

/// <summary>
/// Repository for persisting and querying workflow executions.
/// </summary>
public interface IExecutionRepository
{
    /// <summary>Creates a new execution record.</summary>
    Task<Execution> CreateAsync(Execution execution, CancellationToken cancellationToken = default);
    
    /// <summary>Gets an execution by ID.</summary>
    Task<Execution?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    
    /// <summary>Updates an existing execution.</summary>
    Task<Execution> UpdateAsync(Execution execution, CancellationToken cancellationToken = default);
    
    /// <summary>Deletes an execution by ID.</summary>
    Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default);
    
    /// <summary>Gets executions for a specific workflow.</summary>
    Task<IReadOnlyList<Execution>> GetByWorkflowIdAsync(
        Guid workflowId, 
        int skip = 0, 
        int take = 50,
        CancellationToken cancellationToken = default);
    
    /// <summary>Gets executions by status.</summary>
    Task<IReadOnlyList<Execution>> GetByStatusAsync(
        ExecutionStatus status, 
        int skip = 0, 
        int take = 50,
        CancellationToken cancellationToken = default);
    
    /// <summary>Gets executions within a date range.</summary>
    Task<IReadOnlyList<Execution>> GetByDateRangeAsync(
        DateTime startDate, 
        DateTime endDate, 
        int skip = 0, 
        int take = 50,
        CancellationToken cancellationToken = default);
    
    /// <summary>Queries executions with multiple filter criteria.</summary>
    Task<IReadOnlyList<Execution>> QueryAsync(
        ExecutionQuery query,
        CancellationToken cancellationToken = default);
    
    /// <summary>Adds a node execution record to an existing execution.</summary>
    Task AddNodeExecutionAsync(
        Guid executionId, 
        NodeExecution nodeExecution,
        CancellationToken cancellationToken = default);
    
    /// <summary>Updates a node execution record.</summary>
    Task UpdateNodeExecutionAsync(
        Guid executionId,
        NodeExecution nodeExecution,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Query parameters for filtering executions.
/// </summary>
public record ExecutionQuery
{
    /// <summary>Filter by workflow ID.</summary>
    public Guid? WorkflowId { get; init; }
    
    /// <summary>Filter by execution status.</summary>
    public ExecutionStatus? Status { get; init; }
    
    /// <summary>Filter by start date (inclusive).</summary>
    public DateTime? StartDateFrom { get; init; }
    
    /// <summary>Filter by start date (inclusive).</summary>
    public DateTime? StartDateTo { get; init; }
    
    /// <summary>Filter by execution mode.</summary>
    public ExecutionMode? Mode { get; init; }
    
    /// <summary>Number of records to skip.</summary>
    public int Skip { get; init; }
    
    /// <summary>Number of records to take.</summary>
    public int Take { get; init; } = 50;
}
