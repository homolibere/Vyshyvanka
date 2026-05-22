using System.Text.Json;
using System.Text.Json.Serialization;
using Vyshyvanka.Core.Interfaces;
using Vyshyvanka.Core.Models;

namespace Vyshyvanka.Designer.Services;

/// <summary>
/// Shared state container for the workflow designer.
/// Holds the workflow data, node definitions, and dirty flag.
/// All services read from and coordinate through this store.
/// </summary>
public class WorkflowStore
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private Workflow _workflow = CreateEmptyWorkflow();
    private readonly List<NodeDefinition> _nodeDefinitions = [];
    private bool _isDirty;

    /// <summary>Event raised when any state changes (general re-render signal).</summary>
    public event Action? OnStateChanged;

    /// <summary>Gets the current workflow.</summary>
    public Workflow Workflow => _workflow;

    /// <summary>Gets available node definitions.</summary>
    public IReadOnlyList<NodeDefinition> NodeDefinitions => _nodeDefinitions;

    /// <summary>Gets whether the workflow has unsaved changes.</summary>
    public bool IsDirty => _isDirty;

    /// <summary>Sets the workflow (used by edit service and undo/redo).</summary>
    internal void SetWorkflow(Workflow workflow)
    {
        _workflow = workflow;
    }

    /// <summary>Sets the available node definitions.</summary>
    public void SetNodeDefinitions(IEnumerable<NodeDefinition> definitions)
    {
        _nodeDefinitions.Clear();
        _nodeDefinitions.AddRange(definitions);
        NotifyStateChanged();
    }

    /// <summary>Marks the workflow as having unsaved changes.</summary>
    internal void MarkDirty()
    {
        _isDirty = true;
    }

    /// <summary>Marks the workflow as saved (not dirty).</summary>
    public void MarkAsSaved()
    {
        _isDirty = false;
        NotifyStateChanged();
    }

    /// <summary>Gets a node definition by type.</summary>
    public NodeDefinition? GetNodeDefinition(string nodeType)
    {
        return _nodeDefinitions.FirstOrDefault(d => d.Type == nodeType);
    }

    /// <summary>Gets a node by ID.</summary>
    public WorkflowNode? GetNode(string nodeId)
    {
        return _workflow.Nodes.FirstOrDefault(n => n.Id == nodeId);
    }

    /// <summary>Serializes the current workflow to JSON.</summary>
    public string SerializeToJson()
    {
        return JsonSerializer.Serialize(_workflow, SerializerOptions);
    }

    /// <summary>Deserializes a workflow from JSON.</summary>
    public static Workflow? DeserializeFromJson(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return null;

        try
        {
            return JsonSerializer.Deserialize<Workflow>(json, SerializerOptions);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    /// <summary>Notifies subscribers that state has changed.</summary>
    public void NotifyStateChanged() => OnStateChanged?.Invoke();

    /// <summary>Creates a new empty workflow.</summary>
    internal static Workflow CreateEmptyWorkflow() => new()
    {
        Id = Guid.NewGuid(),
        Name = "New Workflow",
        Version = 1,
        IsActive = true,
        CreatedAt = DateTime.UtcNow,
        UpdatedAt = DateTime.UtcNow
    };
}
