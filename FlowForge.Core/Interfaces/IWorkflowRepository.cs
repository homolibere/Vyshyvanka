using FlowForge.Core.Models;

namespace FlowForge.Core.Interfaces;

/// <summary>
/// Repository for persisting and querying workflow definitions.
/// </summary>
public interface IWorkflowRepository
{
    /// <summary>Creates a new workflow.</summary>
    Task<Workflow> CreateAsync(Workflow workflow, CancellationToken cancellationToken = default);
    
    /// <summary>Gets a workflow by ID.</summary>
    Task<Workflow?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    
    /// <summary>Updates an existing workflow.</summary>
    Task<Workflow> UpdateAsync(Workflow workflow, CancellationToken cancellationToken = default);
    
    /// <summary>Deletes a workflow by ID.</summary>
    Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default);
    
    /// <summary>Gets all workflows with pagination.</summary>
    Task<IReadOnlyList<Workflow>> GetAllAsync(
        int skip = 0, 
        int take = 50,
        CancellationToken cancellationToken = default);
    
    /// <summary>Gets workflows by creator.</summary>
    Task<IReadOnlyList<Workflow>> GetByCreatorAsync(
        Guid createdBy, 
        int skip = 0, 
        int take = 50,
        CancellationToken cancellationToken = default);
    
    /// <summary>Gets active workflows.</summary>
    Task<IReadOnlyList<Workflow>> GetActiveAsync(
        int skip = 0, 
        int take = 50,
        CancellationToken cancellationToken = default);
    
    /// <summary>Searches workflows by name or tags.</summary>
    Task<IReadOnlyList<Workflow>> SearchAsync(
        string searchTerm, 
        int skip = 0, 
        int take = 50,
        CancellationToken cancellationToken = default);
    
    /// <summary>Checks if a workflow exists.</summary>
    Task<bool> ExistsAsync(Guid id, CancellationToken cancellationToken = default);
}
