using System.Reflection;
using System.Text.Json;
using FlowForge.Core.Enums;
using FlowForge.Core.Interfaces;

namespace FlowForge.Engine.Registry;

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
        var inputs = inputAttrs.Count > 0
            ? inputAttrs.Select(a => new PortDefinition
            {
                Name = a.Name,
                DisplayName = a.DisplayName ?? a.Name,
                Type = a.Type,
                IsRequired = a.IsRequired
            }).ToList()
            : [new PortDefinition { Name = "input", DisplayName = "Input", Type = PortType.Any, IsRequired = false }];
        
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

    private static JsonElement BuildConfigurationSchema(Type nodeType)
    {
        var configAttrs = nodeType.GetCustomAttributes<ConfigurationPropertyAttribute>().ToList();
        
        if (configAttrs.Count == 0)
            return default;
        
        var properties = new Dictionary<string, object>();
        var required = new List<string>();
        
        foreach (var attr in configAttrs)
        {
            properties[attr.Name] = new Dictionary<string, object>
            {
                ["type"] = attr.PropertyType.ToLowerInvariant(),
                ["description"] = attr.Description ?? string.Empty
            };
            
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
            NodeCategory.Action => "bolt",
            NodeCategory.Logic => "git-branch",
            NodeCategory.Transform => "shuffle",
            _ => "cube"
        };
    }
}

/// <summary>
/// Attribute for providing node metadata.
/// </summary>
[AttributeUsage(AttributeTargets.Class)]
public class NodeDefinitionAttribute : Attribute
{
    /// <summary>Display name for the node.</summary>
    public string Name { get; set; } = string.Empty;
    
    /// <summary>Description of what the node does.</summary>
    public string Description { get; set; } = string.Empty;
    
    /// <summary>Icon identifier.</summary>
    public string Icon { get; set; } = string.Empty;
}

/// <summary>
/// Attribute for defining a node input port.
/// </summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
public class NodeInputAttribute : Attribute
{
    /// <summary>Internal name of the port.</summary>
    public string Name { get; }
    
    /// <summary>Display name for UI.</summary>
    public string? DisplayName { get; set; }
    
    /// <summary>Data type of the port.</summary>
    public PortType Type { get; set; } = PortType.Any;
    
    /// <summary>Whether this input is required.</summary>
    public bool IsRequired { get; set; }

    public NodeInputAttribute(string name)
    {
        Name = name;
    }
}

/// <summary>
/// Attribute for defining a node output port.
/// </summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
public class NodeOutputAttribute : Attribute
{
    /// <summary>Internal name of the port.</summary>
    public string Name { get; }
    
    /// <summary>Display name for UI.</summary>
    public string? DisplayName { get; set; }
    
    /// <summary>Data type of the port.</summary>
    public PortType Type { get; set; } = PortType.Any;

    public NodeOutputAttribute(string name)
    {
        Name = name;
    }
}

/// <summary>
/// Attribute for defining a configuration property.
/// </summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
public class ConfigurationPropertyAttribute : Attribute
{
    /// <summary>Property name.</summary>
    public string Name { get; }
    
    /// <summary>Property type (string, number, boolean, object, array).</summary>
    public string PropertyType { get; }
    
    /// <summary>Description of the property.</summary>
    public string? Description { get; set; }
    
    /// <summary>Whether this property is required.</summary>
    public bool IsRequired { get; set; }

    public ConfigurationPropertyAttribute(string name, string propertyType)
    {
        Name = name;
        PropertyType = propertyType;
    }
}

/// <summary>
/// Attribute indicating a node requires credentials.
/// </summary>
[AttributeUsage(AttributeTargets.Class)]
public class RequiresCredentialAttribute : Attribute
{
    /// <summary>Type of credential required.</summary>
    public CredentialType CredentialType { get; }

    public RequiresCredentialAttribute(CredentialType credentialType)
    {
        CredentialType = credentialType;
    }
}
