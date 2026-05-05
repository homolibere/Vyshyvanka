using Vyshyvanka.Core.Enums;

namespace Vyshyvanka.Core.Attributes;

/// <summary>
/// Attribute for providing node metadata (name, description, icon).
/// </summary>
[AttributeUsage(AttributeTargets.Class)]
public class NodeDefinitionAttribute : Attribute
{
    /// <summary>Display name for the node.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Description of what the node does.</summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>Icon identifier (Font Awesome class, emoji, or URL).</summary>
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
/// Attribute for defining a configuration property on a node.
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

    /// <summary>
    /// Data source identifier for dynamic options (e.g., "workflows").
    /// When set, the Designer fetches options from the API instead of using static values.
    /// </summary>
    public string? DataSource { get; set; }

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
