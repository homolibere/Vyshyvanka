using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace Vyshyvanka.Core.Interfaces;

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

    /// <summary>Service provider for resolving runtime dependencies (e.g., IWorkflowEngine for sub-workflow execution).</summary>
    IServiceProvider? Services { get; }

    /// <summary>ID of the user who initiated the execution. Used for ownership checks in sub-workflow execution.</summary>
    Guid? UserId { get; }

    /// <summary>Logger for node execution diagnostics. Defaults to NullLogger when no logger is configured.</summary>
    ILogger Logger { get; }
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
    Task<IDictionary<string, string>?> GetCredentialAsync(Guid credentialId,
        CancellationToken cancellationToken = default);
}
