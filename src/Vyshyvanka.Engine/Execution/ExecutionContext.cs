using System.Text.Json;
using Vyshyvanka.Core.Interfaces;

namespace Vyshyvanka.Engine.Execution;

/// <summary>
/// Default implementation of execution context.
/// </summary>
public class ExecutionContext : IExecutionContext
{
    /// <inheritdoc />
    public Guid ExecutionId { get; }
    
    /// <inheritdoc />
    public Guid WorkflowId { get; }
    
    /// <inheritdoc />
    public Dictionary<string, object> Variables { get; } = [];
    
    /// <inheritdoc />
    public INodeOutputStore NodeOutputs { get; }
    
    /// <inheritdoc />
    public ICredentialProvider Credentials { get; }
    
    /// <inheritdoc />
    public CancellationToken CancellationToken { get; }

    /// <summary>
    /// Creates a new execution context.
    /// </summary>
    public ExecutionContext(
        Guid executionId,
        Guid workflowId,
        ICredentialProvider credentialProvider,
        CancellationToken cancellationToken = default)
    {
        ExecutionId = executionId;
        WorkflowId = workflowId;
        Credentials = credentialProvider ?? throw new ArgumentNullException(nameof(credentialProvider));
        CancellationToken = cancellationToken;
        NodeOutputs = new NodeOutputStore();
    }
}

/// <summary>
/// Default implementation of node output store.
/// Supports port-specific outputs for nodes with multiple output ports.
/// </summary>
public class NodeOutputStore : INodeOutputStore
{
    private const string DefaultPort = "output";
    private readonly Dictionary<string, Dictionary<string, JsonElement>> _outputs = new Dictionary<string, Dictionary<string, JsonElement>>(StringComparer.OrdinalIgnoreCase);
    private readonly Lock _lock = new Lock();

    /// <inheritdoc />
    public void Set(string nodeId, JsonElement output)
    {
        Set(nodeId, DefaultPort, output);
    }

    /// <inheritdoc />
    public void Set(string nodeId, string portName, JsonElement output)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(nodeId);
        ArgumentException.ThrowIfNullOrWhiteSpace(portName);
        
        lock (_lock)
        {
            if (!_outputs.TryGetValue(nodeId, out var ports))
            {
                ports = new Dictionary<string, JsonElement>(StringComparer.OrdinalIgnoreCase);
                _outputs[nodeId] = ports;
            }
            ports[portName] = output;
        }
    }

    /// <inheritdoc />
    public JsonElement? Get(string nodeId)
    {
        return Get(nodeId, DefaultPort);
    }

    /// <inheritdoc />
    public JsonElement? Get(string nodeId, string portName)
    {
        lock (_lock)
        {
            if (_outputs.TryGetValue(nodeId, out var ports) && 
                ports.TryGetValue(portName, out var output))
            {
                return output;
            }
            return null;
        }
    }

    /// <inheritdoc />
    public bool HasOutput(string nodeId)
    {
        lock (_lock)
        {
            return _outputs.ContainsKey(nodeId);
        }
    }

    /// <inheritdoc />
    public bool HasOutput(string nodeId, string portName)
    {
        lock (_lock)
        {
            return _outputs.TryGetValue(nodeId, out var ports) && ports.ContainsKey(portName);
        }
    }

    /// <inheritdoc />
    public IReadOnlyDictionary<string, JsonElement> GetAllOutputs(string nodeId)
    {
        lock (_lock)
        {
            if (_outputs.TryGetValue(nodeId, out var ports))
            {
                return new Dictionary<string, JsonElement>(ports);
            }
            return new Dictionary<string, JsonElement>();
        }
    }
}
