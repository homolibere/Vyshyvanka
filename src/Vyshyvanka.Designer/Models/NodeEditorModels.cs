namespace Vyshyvanka.Designer.Models;

/// <summary>
/// Represents a single configuration property parsed from the node's schema.
/// </summary>
public record ConfigurationProperty
{
    /// <summary>Property name (key in configuration JSON).</summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>Display label for the property.</summary>
    public string DisplayName { get; init; } = string.Empty;

    /// <summary>Property type: string, number, boolean, object, array.</summary>
    public string Type { get; init; } = "string";

    /// <summary>Description shown as helper text.</summary>
    public string? Description { get; init; }

    /// <summary>Whether this property is required.</summary>
    public bool IsRequired { get; init; }

    /// <summary>Predefined options for dropdown (null for free-form input).</summary>
    public List<string>? Options { get; init; }

    /// <summary>
    /// Data source identifier for dynamic options (e.g., "workflows").
    /// When set, the editor fetches options from the API at runtime.
    /// </summary>
    public string? DataSource { get; init; }
}

/// <summary>
/// Tracks the state of the node editor modal.
/// </summary>
public record NodeEditorState
{
    /// <summary>ID of the node being edited.</summary>
    public string NodeId { get; init; } = string.Empty;

    /// <summary>Current configuration values by property name.</summary>
    public Dictionary<string, object?> Values { get; init; } = new();

    /// <summary>Whether the editor is in JSON mode.</summary>
    public bool IsJsonMode { get; init; }

    /// <summary>Raw JSON string when in JSON mode.</summary>
    public string? RawJson { get; init; }

    /// <summary>Validation errors by property name.</summary>
    public Dictionary<string, string> ValidationErrors { get; init; } = new();

    /// <summary>Whether there are unsaved changes.</summary>
    public bool IsDirty { get; init; }
}
