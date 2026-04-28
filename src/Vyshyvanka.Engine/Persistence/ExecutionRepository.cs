using System.Text.Json;
using Vyshyvanka.Core.Enums;
using Vyshyvanka.Core.Interfaces;
using Vyshyvanka.Engine.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using ExecutionModel = Vyshyvanka.Core.Models.Execution;
using NodeExecutionModel = Vyshyvanka.Core.Models.NodeExecution;

namespace Vyshyvanka.Engine.Persistence;

/// <summary>
/// EF Core implementation of execution repository.
/// </summary>
public class ExecutionRepository : IExecutionRepository
{
    private readonly VyshyvankaDbContext _context;

    public ExecutionRepository(VyshyvankaDbContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    /// <inheritdoc />
    public async Task<ExecutionModel> CreateAsync(ExecutionModel execution, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(execution);

        var entity = ToEntity(execution);
        _context.Executions.Add(entity);
        await _context.SaveChangesAsync(cancellationToken);
        
        return ToModel(entity);
    }

    /// <inheritdoc />
    public async Task<ExecutionModel?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await _context.Executions
            .Include(e => e.NodeExecutions)
            .FirstOrDefaultAsync(e => e.Id == id, cancellationToken);
        
        return entity is null ? null : ToModel(entity);
    }

    /// <inheritdoc />
    public async Task<ExecutionModel> UpdateAsync(ExecutionModel execution, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(execution);

        var entity = await _context.Executions
            .Include(e => e.NodeExecutions)
            .FirstOrDefaultAsync(e => e.Id == execution.Id, cancellationToken)
            ?? throw new InvalidOperationException($"Execution {execution.Id} not found");

        UpdateEntity(entity, execution);
        await _context.SaveChangesAsync(cancellationToken);
        
        return ToModel(entity);
    }

    /// <inheritdoc />
    public async Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await _context.Executions.FindAsync([id], cancellationToken);
        if (entity is null)
        {
            return false;
        }

        _context.Executions.Remove(entity);
        await _context.SaveChangesAsync(cancellationToken);
        return true;
    }


    /// <inheritdoc />
    public async Task<IReadOnlyList<ExecutionModel>> GetByWorkflowIdAsync(
        Guid workflowId, 
        int skip = 0, 
        int take = 50,
        CancellationToken cancellationToken = default)
    {
        var entities = await _context.Executions
            .Include(e => e.NodeExecutions)
            .Where(e => e.WorkflowId == workflowId)
            .OrderByDescending(e => e.StartedAt)
            .Skip(skip)
            .Take(take)
            .ToListAsync(cancellationToken);
        
        return entities.Select(ToModel).ToList();
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<ExecutionModel>> GetByStatusAsync(
        ExecutionStatus status, 
        int skip = 0, 
        int take = 50,
        CancellationToken cancellationToken = default)
    {
        var entities = await _context.Executions
            .Include(e => e.NodeExecutions)
            .Where(e => e.Status == status)
            .OrderByDescending(e => e.StartedAt)
            .Skip(skip)
            .Take(take)
            .ToListAsync(cancellationToken);
        
        return entities.Select(ToModel).ToList();
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<ExecutionModel>> GetByDateRangeAsync(
        DateTime startDate, 
        DateTime endDate, 
        int skip = 0, 
        int take = 50,
        CancellationToken cancellationToken = default)
    {
        var entities = await _context.Executions
            .Include(e => e.NodeExecutions)
            .Where(e => e.StartedAt >= startDate && e.StartedAt <= endDate)
            .OrderByDescending(e => e.StartedAt)
            .Skip(skip)
            .Take(take)
            .ToListAsync(cancellationToken);
        
        return entities.Select(ToModel).ToList();
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<ExecutionModel>> QueryAsync(
        ExecutionQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        var queryable = _context.Executions
            .Include(e => e.NodeExecutions)
            .AsQueryable();

        if (query.WorkflowId.HasValue)
        {
            queryable = queryable.Where(e => e.WorkflowId == query.WorkflowId.Value);
        }

        if (query.Status.HasValue)
        {
            queryable = queryable.Where(e => e.Status == query.Status.Value);
        }

        if (query.Mode.HasValue)
        {
            queryable = queryable.Where(e => e.Mode == query.Mode.Value);
        }

        if (query.StartDateFrom.HasValue)
        {
            queryable = queryable.Where(e => e.StartedAt >= query.StartDateFrom.Value);
        }

        if (query.StartDateTo.HasValue)
        {
            queryable = queryable.Where(e => e.StartedAt <= query.StartDateTo.Value);
        }

        var entities = await queryable
            .OrderByDescending(e => e.StartedAt)
            .Skip(query.Skip)
            .Take(query.Take)
            .ToListAsync(cancellationToken);
        
        return entities.Select(ToModel).ToList();
    }


    /// <inheritdoc />
    public async Task AddNodeExecutionAsync(
        Guid executionId, 
        NodeExecutionModel nodeExecution,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(nodeExecution);

        var entity = new NodeExecutionEntity
        {
            ExecutionId = executionId,
            NodeId = nodeExecution.NodeId,
            Status = nodeExecution.Status,
            StartedAt = nodeExecution.StartedAt,
            CompletedAt = nodeExecution.CompletedAt,
            InputDataJson = nodeExecution.InputData.HasValue 
                ? nodeExecution.InputData.Value.GetRawText() 
                : null,
            OutputDataJson = nodeExecution.OutputData.HasValue 
                ? nodeExecution.OutputData.Value.GetRawText() 
                : null,
            ErrorMessage = nodeExecution.ErrorMessage
        };

        _context.NodeExecutions.Add(entity);
        await _context.SaveChangesAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task UpdateNodeExecutionAsync(
        Guid executionId,
        NodeExecutionModel nodeExecution,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(nodeExecution);

        var entity = await _context.NodeExecutions
            .FirstOrDefaultAsync(
                ne => ne.ExecutionId == executionId && ne.NodeId == nodeExecution.NodeId, 
                cancellationToken)
            ?? throw new InvalidOperationException(
                $"Node execution for node '{nodeExecution.NodeId}' in execution {executionId} not found");

        entity.Status = nodeExecution.Status;
        entity.CompletedAt = nodeExecution.CompletedAt;
        entity.OutputDataJson = nodeExecution.OutputData.HasValue 
            ? nodeExecution.OutputData.Value.GetRawText() 
            : null;
        entity.ErrorMessage = nodeExecution.ErrorMessage;

        await _context.SaveChangesAsync(cancellationToken);
    }

    private static ExecutionEntity ToEntity(ExecutionModel execution)
    {
        return new ExecutionEntity
        {
            Id = execution.Id,
            WorkflowId = execution.WorkflowId,
            WorkflowVersion = execution.WorkflowVersion,
            Status = execution.Status,
            Mode = execution.Mode,
            StartedAt = execution.StartedAt,
            CompletedAt = execution.CompletedAt,
            TriggerDataJson = execution.TriggerData.HasValue 
                ? execution.TriggerData.Value.GetRawText() 
                : null,
            OutputDataJson = execution.OutputData.HasValue 
                ? execution.OutputData.Value.GetRawText() 
                : null,
            ErrorMessage = execution.ErrorMessage,
            NodeExecutions = execution.NodeExecutions.Select(ne => new NodeExecutionEntity
            {
                ExecutionId = execution.Id,
                NodeId = ne.NodeId,
                Status = ne.Status,
                StartedAt = ne.StartedAt,
                CompletedAt = ne.CompletedAt,
                InputDataJson = ne.InputData.HasValue ? ne.InputData.Value.GetRawText() : null,
                OutputDataJson = ne.OutputData.HasValue ? ne.OutputData.Value.GetRawText() : null,
                ErrorMessage = ne.ErrorMessage
            }).ToList()
        };
    }


    private static ExecutionModel ToModel(ExecutionEntity entity)
    {
        return new ExecutionModel
        {
            Id = entity.Id,
            WorkflowId = entity.WorkflowId,
            WorkflowVersion = entity.WorkflowVersion,
            Status = entity.Status,
            Mode = entity.Mode,
            StartedAt = entity.StartedAt,
            CompletedAt = entity.CompletedAt,
            TriggerData = ParseJsonElement(entity.TriggerDataJson),
            OutputData = ParseJsonElement(entity.OutputDataJson),
            ErrorMessage = entity.ErrorMessage,
            NodeExecutions = entity.NodeExecutions.Select(ne => new NodeExecutionModel
            {
                NodeId = ne.NodeId,
                Status = ne.Status,
                StartedAt = ne.StartedAt,
                CompletedAt = ne.CompletedAt,
                InputData = ParseJsonElement(ne.InputDataJson),
                OutputData = ParseJsonElement(ne.OutputDataJson),
                ErrorMessage = ne.ErrorMessage
            }).ToList()
        };
    }

    private static void UpdateEntity(ExecutionEntity entity, ExecutionModel execution)
    {
        entity.Status = execution.Status;
        entity.CompletedAt = execution.CompletedAt;
        entity.OutputDataJson = execution.OutputData.HasValue 
            ? execution.OutputData.Value.GetRawText() 
            : null;
        entity.ErrorMessage = execution.ErrorMessage;
    }

    private static JsonElement? ParseJsonElement(string? json)
    {
        if (string.IsNullOrEmpty(json))
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<JsonElement>(json);
        }
        catch
        {
            return null;
        }
    }
}
