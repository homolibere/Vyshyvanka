using System.Reflection;
using System.Text.Json;
using Vyshyvanka.Core.Attributes;
using Vyshyvanka.Core.Enums;
using Vyshyvanka.Core.Interfaces;

namespace Vyshyvanka.Engine.Registry;

/// <summary>
/// Default implementation of the node registry.
/// </summary>
public class NodeRegistry : INodeRegistry
{
    private readonly Dictionary<string, Type> _nodeTypes = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, NodeDefinition> _definitions = new(StringComparer.OrdinalIgnoreCase);

    /// <inheritdoc />
    public void Register<TNode>() where TNode : INode
    {
        var nodeType = typeof(TNode);
        RegisterType(nodeType);
    }

    /// <inheritdoc />
    public void Register(Type nodeType)
    {
        ArgumentNullException.ThrowIfNull(nodeType);
        if (!typeof(INode).IsAssignableFrom(nodeType))
            throw new ArgumentException($"Type {nodeType.Name} does not implement INode", nameof(nodeType));
        RegisterType(nodeType);
    }

    /// <inheritdoc />
    public void RegisterFromAssembly(Assembly assembly)
    {
        ArgumentNullException.ThrowIfNull(assembly);

        var nodeTypes = assembly.GetTypes()
            .Where(t => typeof(INode).IsAssignableFrom(t) && !t.IsInterface && !t.IsAbstract);

        foreach (var nodeType in nodeTypes)
        {
            try
            {
                RegisterType(nodeType);
            }
            catch (Exception)
            {
                // Skip types that cannot be instantiated
            }
        }
    }

    /// <inheritdoc />
    public INode CreateNode(string nodeType, JsonElement configuration)
    {
        if (string.IsNullOrWhiteSpace(nodeType))
            throw new ArgumentException("Node type cannot be empty", nameof(nodeType));

        if (!_nodeTypes.TryGetValue(nodeType, out var type))
            throw new InvalidOperationException($"Node type '{nodeType}' is not registered");

        return Activator.CreateInstance(type) as INode
               ?? throw new InvalidOperationException($"Cannot create instance of node type '{nodeType}'");
    }

    /// <inheritdoc />
    public NodeDefinition? GetDefinition(string nodeType)
    {
        if (string.IsNullOrWhiteSpace(nodeType))
            return null;

        return _definitions.GetValueOrDefault(nodeType);
    }

    /// <inheritdoc />
    public IEnumerable<NodeDefinition> GetAllDefinitions()
    {
        return _definitions.Values;
    }

    /// <inheritdoc />
    public bool IsRegistered(string nodeType)
    {
        if (string.IsNullOrWhiteSpace(nodeType))
            return false;

        return _nodeTypes.ContainsKey(nodeType);
    }

    /// <inheritdoc />
    public bool Unregister(string nodeType)
    {
        if (string.IsNullOrWhiteSpace(nodeType))
            return false;

        var removedType = _nodeTypes.Remove(nodeType);
        var removedDef = _definitions.Remove(nodeType);

        return removedType || removedDef;
    }

    /// <inheritdoc />
    public void UnregisterFromAssembly(Assembly assembly)
    {
        ArgumentNullException.ThrowIfNull(assembly);

        // Find all node types from this assembly and unregister them
        var typesToRemove = _nodeTypes
            .Where(kvp => kvp.Value.Assembly == assembly)
            .Select(kvp => kvp.Key)
            .ToList();

        foreach (var nodeType in typesToRemove)
        {
            Unregister(nodeType);
        }
    }

    private void RegisterType(Type nodeType)
    {
        var instance = Activator.CreateInstance(nodeType) as INode
                       ?? throw new InvalidOperationException($"Cannot create instance of {nodeType.Name}");

        _nodeTypes[instance.Type] = nodeType;
        _definitions[instance.Type] = CreateDefinitionFromType(nodeType, instance);
    }

    private static NodeDefinition CreateDefinitionFromType(Type nodeType, INode instance)
    {
        var attr = nodeType.GetCustomAttribute<NodeDefinitionAttribute>();
        var inputAttrs = nodeType.GetCustomAttributes<NodeInputAttribute>().ToList();
        var outputAttrs = nodeType.GetCustomAttributes<NodeOutputAttribute>().ToList();
        var credentialAttr = nodeType.GetCustomAttribute<RequiresCredentialAttribute>();

        // Build input port definitions
        // Trigger nodes are entry points — they never receive input from other nodes
        var inputs = inputAttrs.Count > 0
            ? inputAttrs.Select(a => new PortDefinition
            {
                Name = a.Name,
                DisplayName = a.DisplayName ?? a.Name,
                Type = a.Type,
                IsRequired = a.IsRequired
            }).ToList()
            : instance.Category == NodeCategory.Trigger
                ? []
                :
                [
                    new PortDefinition
                        { Name = "input", DisplayName = "Input", Type = PortType.Any, IsRequired = false }
                ];

        // Build output port definitions
        var outputs = outputAttrs.Count > 0
            ? outputAttrs.Select(a => new PortDefinition
            {
                Name = a.Name,
                DisplayName = a.DisplayName ?? a.Name,
                Type = a.Type,
                IsRequired = false
            }).ToList()
            : [new PortDefinition { Name = "output", DisplayName = "Output", Type = PortType.Any, IsRequired = false }];

        // Build configuration schema from ConfigurationPropertyAttributes
        var configSchema = BuildConfigurationSchema(nodeType);

        return new NodeDefinition
        {
            Type = instance.Type,
            Name = attr?.Name ?? FormatTypeName(nodeType.Name),
            Description = attr?.Description ?? string.Empty,
            Category = instance.Category,
            Icon = string.IsNullOrWhiteSpace(attr?.Icon) ? GetDefaultIcon(instance.Category) : attr.Icon,
            Inputs = inputs,
            Outputs = outputs,
            ConfigurationSchema = configSchema,
            RequiredCredentialType = credentialAttr?.CredentialType
        };
    }

    private static JsonElement? BuildConfigurationSchema(Type nodeType)
    {
        var configAttrs = nodeType.GetCustomAttributes<ConfigurationPropertyAttribute>().ToList();

        if (configAttrs.Count == 0)
            return null;

        var properties = new Dictionary<string, object>();
        var required = new List<string>();

        foreach (var attr in configAttrs)
        {
            var propSchema = new Dictionary<string, object>
            {
                ["type"] = attr.PropertyType.ToLowerInvariant(),
                ["description"] = attr.Description ?? string.Empty
            };

            if (!string.IsNullOrEmpty(attr.DataSource))
                propSchema["dataSource"] = attr.DataSource;

            properties[attr.Name] = propSchema;

            if (attr.IsRequired)
                required.Add(attr.Name);
        }

        var schema = new Dictionary<string, object>
        {
            ["type"] = "object",
            ["properties"] = properties,
            ["required"] = required
        };

        return JsonSerializer.SerializeToElement(schema);
    }

    private static string FormatTypeName(string typeName)
    {
        // Remove "Node" suffix and add spaces before capitals
        if (typeName.EndsWith("Node", StringComparison.Ordinal))
            typeName = typeName[..^4];

        return string.Concat(typeName.Select((c, i) =>
            i > 0 && char.IsUpper(c) ? " " + c : c.ToString()));
    }

    private static string GetDefaultIcon(NodeCategory category)
    {
        return category switch
        {
            NodeCategory.Trigger => "play-circle",
            NodeCategory.Action => "fa-solid fa-bolt",
            NodeCategory.Logic => "fa-solid fa-code-branch",
            NodeCategory.Transform => "fa-solid fa-shuffle",
            _ => "fa-solid fa-cube"
        };
    }
}
