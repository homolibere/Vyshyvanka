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

    // ── Lookup indexes (lazy, invalidated on mutation) ─────────────────
    private Dictionary<string, WorkflowNode>? _nodeIndex;
    private Dictionary<string, NodeDefinition>? _nodeDefinitionIndex;

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
        _nodeIndex = null;
    }

    /// <summary>Sets the available node definitions.</summary>
    public void SetNodeDefinitions(IEnumerable<NodeDefinition> definitions)
    {
        _nodeDefinitions.Clear();
        _nodeDefinitions.AddRange(definitions);
        _nodeDefinitionIndex = null;
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
        _nodeDefinitionIndex ??= _nodeDefinitions.ToDictionary(d => d.Type);
        return _nodeDefinitionIndex.GetValueOrDefault(nodeType);
    }

    /// <summary>Gets a node by ID.</summary>
    public WorkflowNode? GetNode(string nodeId)
    {
        _nodeIndex ??= _workflow.Nodes.ToDictionary(n => n.Id);
        return _nodeIndex.GetValueOrDefault(nodeId);
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

    // ── Notification Batching ──────────────────────────────────────────

    private int _suspendCount;
    private bool _notificationPending;

    /// <summary>
    /// Suspends state-change notifications for the lifetime of the returned scope.
    /// Calls to <see cref="NotifyStateChanged"/> while suspended are coalesced into
    /// a single notification fired when the outermost scope is disposed.
    /// Scopes are nestable (reference-counted).
    /// </summary>
    public IDisposable SuspendNotifications()
    {
        _suspendCount++;
        return new NotificationScope(this);
    }

    /// <summary>Notifies subscribers that state has changed.</summary>
    public void NotifyStateChanged()
    {
        if (_suspendCount > 0)
        {
            _notificationPending = true;
            return;
        }

        OnStateChanged?.Invoke();
    }

    private void ResumeNotifications()
    {
        if (--_suspendCount == 0 && _notificationPending)
        {
            _notificationPending = false;
            OnStateChanged?.Invoke();
        }
    }

    private sealed class NotificationScope(WorkflowStore store) : IDisposable
    {
        private bool _disposed;

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            store.ResumeNotifications();
        }
    }

    // ─────────────────────────────────────────────────────────────────────

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
