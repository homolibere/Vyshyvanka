namespace FlowForge.Core.Enums;

/// <summary>
/// Defines the data type of a node port.
/// </summary>
public enum PortType
{
    /// <summary>Accepts any data type.</summary>
    Any,
    
    /// <summary>JSON object type.</summary>
    Object,
    
    /// <summary>Array/collection type.</summary>
    Array,
    
    /// <summary>String type.</summary>
    String,
    
    /// <summary>Numeric type.</summary>
    Number,
    
    /// <summary>Boolean type.</summary>
    Boolean
}
