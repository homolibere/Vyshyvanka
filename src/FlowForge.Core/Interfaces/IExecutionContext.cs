using System.Text.Json;

namespace FlowForge.Core.Interfaces;

/// <summary>
/// Provides runtime context during workflow execution.
/// </summary>
public interface IExecutionContext
{
    /// <summary>Unique identifier for this execution.</summary>
    Guid ExecutionId { get; }
    
    /// <summary>Identifier of the workflow being executed.</summary>
    Guid WorkflowId { get; }
    
    /// <summary>Variables available during execution.</summary>
    Dictionary<string, object> Variables { get; }
    
    /// <summary>Store for node output data.</summary>
    INodeOutputStore NodeOutputs { get; }
    
    /// <summary>Provider for credential retrieval.</summary>
    ICredentialProvider Credentials { get; }
    
    /// <summary>Token for cancellation support.</summary>
    CancellationToken CancellationToken { get; }
}

/// <summary>
/// Stores and retrieves node output data during execution.
/// </summary>
public interface INodeOutputStore
{
    /// <summary>Stores output data for a node's default output port.</summary>
    void Set(string nodeId, JsonElement output);
    
    /// <summary>Stores output data for a specific port of a node.</summary>
    void Set(string nodeId, string portName, JsonElement output);
    
    /// <summary>Retrieves output data for a node's default output port.</summary>
    JsonElement? Get(string nodeId);
    
    /// <summary>Retrieves output data for a specific port of a node.</summary>
    JsonElement? Get(string nodeId, string portName);
    
    /// <summary>Checks if output exists for a node.</summary>
    bool HasOutput(string nodeId);
    
    /// <summary>Checks if output exists for a specific port of a node.</summary>
    bool HasOutput(string nodeId, string portName);
    
    /// <summary>Gets all outputs for a node (all ports).</summary>
    IReadOnlyDictionary<string, JsonElement> GetAllOutputs(string nodeId);
}

/// <summary>
/// Provides credential retrieval during execution.
/// </summary>
public interface ICredentialProvider
{
    /// <summary>Gets decrypted credential by ID.</summary>
    Task<IDictionary<string, string>?> GetCredentialAsync(Guid credentialId, CancellationToken cancellationToken = default);
}
