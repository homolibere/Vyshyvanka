using System.Text.Json;
using FlowForge.Core.Enums;
using FlowForge.Core.Interfaces;
using FlowForge.Core.Models;
using FlowForge.Engine.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace FlowForge.Engine.Persistence;

/// <summary>
/// EF Core implementation of workflow repository.
/// </summary>
public class WorkflowRepository : IWorkflowRepository
{
    private readonly FlowForgeDbContext _context;
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public WorkflowRepository(FlowForgeDbContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    /// <inheritdoc />
    public async Task<Workflow> CreateAsync(Workflow workflow, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(workflow);

        var entity = ToEntity(workflow);
        _context.Workflows.Add(entity);
        await _context.SaveChangesAsync(cancellationToken);
        
        return ToModel(entity);
    }

    /// <inheritdoc />
    public async Task<Workflow?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await _context.Workflows
            .AsNoTracking()
            .FirstOrDefaultAsync(w => w.Id == id, cancellationToken);
        
        return entity is null ? null : ToModel(entity);
    }

    /// <inheritdoc />
    public async Task<Workflow> UpdateAsync(Workflow workflow, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(workflow);

        var entity = await _context.Workflows
            .FirstOrDefaultAsync(w => w.Id == workflow.Id, cancellationToken)
            ?? throw new InvalidOperationException($"Workflow {workflow.Id} not found");

        UpdateEntity(entity, workflow);
        await _context.SaveChangesAsync(cancellationToken);
        
        return ToModel(entity);
    }

    /// <inheritdoc />
    public async Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await _context.Workflows.FindAsync([id], cancellationToken);
        if (entity is null)
        {
            return false;
        }

        _context.Workflows.Remove(entity);
        await _context.SaveChangesAsync(cancellationToken);
        return true;
    }


    /// <inheritdoc />
    public async Task<IReadOnlyList<Workflow>> GetAllAsync(
        int skip = 0, 
        int take = 50,
        CancellationToken cancellationToken = default)
    {
        var entities = await _context.Workflows
            .AsNoTracking()
            .OrderByDescending(w => w.UpdatedAt)
            .Skip(skip)
            .Take(take)
            .ToListAsync(cancellationToken);
        
        return entities.Select(ToModel).ToList();
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<Workflow>> GetByCreatorAsync(
        Guid createdBy, 
        int skip = 0, 
        int take = 50,
        CancellationToken cancellationToken = default)
    {
        var entities = await _context.Workflows
            .AsNoTracking()
            .Where(w => w.CreatedBy == createdBy)
            .OrderByDescending(w => w.UpdatedAt)
            .Skip(skip)
            .Take(take)
            .ToListAsync(cancellationToken);
        
        return entities.Select(ToModel).ToList();
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<Workflow>> GetActiveAsync(
        int skip = 0, 
        int take = 50,
        CancellationToken cancellationToken = default)
    {
        var entities = await _context.Workflows
            .AsNoTracking()
            .Where(w => w.IsActive)
            .OrderByDescending(w => w.UpdatedAt)
            .Skip(skip)
            .Take(take)
            .ToListAsync(cancellationToken);
        
        return entities.Select(ToModel).ToList();
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<Workflow>> SearchAsync(
        string searchTerm, 
        int skip = 0, 
        int take = 50,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(searchTerm))
        {
            return await GetAllAsync(skip, take, cancellationToken);
        }

        var lowerSearchTerm = searchTerm.ToLowerInvariant();
        
        var entities = await _context.Workflows
            .AsNoTracking()
            .Where(w => w.Name.ToLower().Contains(lowerSearchTerm) || 
                       (w.Tags != null && w.Tags.ToLower().Contains(lowerSearchTerm)))
            .OrderByDescending(w => w.UpdatedAt)
            .Skip(skip)
            .Take(take)
            .ToListAsync(cancellationToken);
        
        return entities.Select(ToModel).ToList();
    }

    /// <inheritdoc />
    public async Task<bool> ExistsAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _context.Workflows.AnyAsync(w => w.Id == id, cancellationToken);
    }


    private static WorkflowEntity ToEntity(Workflow workflow)
    {
        // Sanitize nodes to handle invalid JsonElement values
        var sanitizedNodes = workflow.Nodes.Select(SanitizeNode).ToList();
        
        return new WorkflowEntity
        {
            Id = workflow.Id,
            Name = workflow.Name,
            Description = workflow.Description,
            Version = workflow.Version,
            IsActive = workflow.IsActive,
            NodesJson = JsonSerializer.Serialize(sanitizedNodes, JsonOptions),
            ConnectionsJson = JsonSerializer.Serialize(workflow.Connections, JsonOptions),
            SettingsJson = JsonSerializer.Serialize(workflow.Settings, JsonOptions),
            Tags = workflow.Tags.Count > 0 ? string.Join(",", workflow.Tags) : null,
            CreatedAt = workflow.CreatedAt,
            UpdatedAt = workflow.UpdatedAt,
            CreatedBy = workflow.CreatedBy
        };
    }

    private static WorkflowNode SanitizeNode(WorkflowNode node)
    {
        // If Configuration has ValueKind.Undefined, replace with empty object
        if (node.Configuration.ValueKind == JsonValueKind.Undefined)
        {
            return node with { Configuration = JsonDocument.Parse("{}").RootElement };
        }
        return node;
    }

    private static Workflow ToModel(WorkflowEntity entity)
    {
        return new Workflow
        {
            Id = entity.Id,
            Name = entity.Name,
            Description = entity.Description,
            Version = entity.Version,
            IsActive = entity.IsActive,
            Nodes = DeserializeNodes(entity.NodesJson),
            Connections = DeserializeConnections(entity.ConnectionsJson),
            Settings = DeserializeSettings(entity.SettingsJson),
            Tags = ParseTags(entity.Tags),
            CreatedAt = entity.CreatedAt,
            UpdatedAt = entity.UpdatedAt,
            CreatedBy = entity.CreatedBy
        };
    }

    private static void UpdateEntity(WorkflowEntity entity, Workflow workflow)
    {
        // Sanitize nodes to handle invalid JsonElement values
        var sanitizedNodes = workflow.Nodes.Select(SanitizeNode).ToList();
        
        entity.Name = workflow.Name;
        entity.Description = workflow.Description;
        entity.Version = workflow.Version;
        entity.IsActive = workflow.IsActive;
        entity.NodesJson = JsonSerializer.Serialize(sanitizedNodes, JsonOptions);
        entity.ConnectionsJson = JsonSerializer.Serialize(workflow.Connections, JsonOptions);
        entity.SettingsJson = JsonSerializer.Serialize(workflow.Settings, JsonOptions);
        entity.Tags = workflow.Tags.Count > 0 ? string.Join(",", workflow.Tags) : null;
        entity.UpdatedAt = workflow.UpdatedAt;
    }

    private static List<WorkflowNode> DeserializeNodes(string json)
    {
        try
        {
            return JsonSerializer.Deserialize<List<WorkflowNode>>(json, JsonOptions) ?? [];
        }
        catch
        {
            return [];
        }
    }

    private static List<Connection> DeserializeConnections(string json)
    {
        try
        {
            return JsonSerializer.Deserialize<List<Connection>>(json, JsonOptions) ?? [];
        }
        catch
        {
            return [];
        }
    }

    private static WorkflowSettings DeserializeSettings(string? json)
    {
        if (string.IsNullOrEmpty(json))
        {
            return new WorkflowSettings();
        }

        try
        {
            return JsonSerializer.Deserialize<WorkflowSettings>(json, JsonOptions) ?? new WorkflowSettings();
        }
        catch
        {
            return new WorkflowSettings();
        }
    }

    private static List<string> ParseTags(string? tags)
    {
        if (string.IsNullOrWhiteSpace(tags))
        {
            return [];
        }

        return tags.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();
    }
}
