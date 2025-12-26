using System.Reflection;
using System.Text.Json;
using FlowForge.Core.Enums;

namespace FlowForge.Core.Interfaces;

/// <summary>
/// Registry for node types and their definitions.
/// </summary>
public interface INodeRegistry
{
    /// <summary>Registers a node type.</summary>
    void Register<TNode>() where TNode : INode;

    /// <summary>Registers a node type by Type instance.</summary>
    void Register(Type nodeType);

    /// <summary>Registers all node types from an assembly.</summary>
    void RegisterFromAssembly(Assembly assembly);

    /// <summary>Unregisters a node type by its type identifier.</summary>
    /// <param name="nodeType">The type identifier of the node to unregister.</param>
    /// <returns>True if the node was unregistered, false if it was not found.</returns>
    bool Unregister(string nodeType);

    /// <summary>Unregisters all node types from an assembly.</summary>
    /// <param name="assembly">The assembly whose node types should be unregistered.</param>
    void UnregisterFromAssembly(Assembly assembly);

    /// <summary>Creates a node instance from type and configuration.</summary>
    INode CreateNode(string nodeType, JsonElement configuration);

    /// <summary>Gets the definition for a node type.</summary>
    NodeDefinition? GetDefinition(string nodeType);

    /// <summary>Gets all registered node definitions.</summary>
    IEnumerable<NodeDefinition> GetAllDefinitions();

    /// <summary>Checks if a node type is registered.</summary>
    bool IsRegistered(string nodeType);
}

/// <summary>
/// Metadata definition for a node type.
/// </summary>
public record NodeDefinition
{
    /// <summary>Type identifier for this node.</summary>
    public string Type { get; init; } = string.Empty;

    /// <summary>Display name for the node.</summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>Description of what the node does.</summary>
    public string Description { get; init; } = string.Empty;

    /// <summary>Category of this node.</summary>
    public NodeCategory Category { get; init; }

    /// <summary>Icon identifier for UI display.</summary>
    public string Icon { get; init; } = string.Empty;

    /// <summary>Input port definitions.</summary>
    public List<PortDefinition> Inputs { get; init; } = [];

    /// <summary>Output port definitions.</summary>
    public List<PortDefinition> Outputs { get; init; } = [];

    /// <summary>JSON schema for node configuration.</summary>
    public JsonElement? ConfigurationSchema { get; init; }

    /// <summary>Required credential type, if any.</summary>
    public CredentialType? RequiredCredentialType { get; init; }

    /// <summary>
    /// Source package identifier for plugin nodes.
    /// Null or empty for built-in nodes.
    /// </summary>
    public string? SourcePackage { get; init; }

    /// <summary>
    /// Indicates whether this node is from a plugin package.
    /// </summary>
    public bool IsPluginNode => !string.IsNullOrEmpty(SourcePackage);
}

/// <summary>
/// Definition for a node input or output port.
/// </summary>
public record PortDefinition
{
    /// <summary>Internal name of the port.</summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>Display name for UI.</summary>
    public string DisplayName { get; init; } = string.Empty;

    /// <summary>Data type of the port.</summary>
    public PortType Type { get; init; }

    /// <summary>Whether this port is required.</summary>
    public bool IsRequired { get; init; }
}
