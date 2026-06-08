namespace Vyshyvanka.Designer.Models;

/// <summary>
/// Represents a complete theme definition loaded from JSON.
/// </summary>
public record ThemeDefinition
{
    public required string Id { get; init; }
    public required string Name { get; init; }
    public string Description { get; init; } = string.Empty;
    public string Author { get; init; } = string.Empty;
    public required string BaseMode { get; init; } // "light" or "dark"
    public ThemePreview Preview { get; init; } = new();
    public Dictionary<string, string> Colors { get; init; } = [];
    public Dictionary<string, string> Icons { get; init; } = [];
    public ThemeCanvas Canvas { get; init; } = new();
    public bool IsBuiltIn { get; init; } = true;
}

/// <summary>
/// Color swatches used for theme preview in the selector dropdown.
/// </summary>
public record ThemePreview
{
    public string Bg { get; init; } = "#ffffff";
    public string Accent { get; init; } = "#c62828";
    public string Surface { get; init; } = "#ffffff";
}

/// <summary>
/// Canvas-specific theme settings.
/// </summary>
public record ThemeCanvas
{
    public string Pattern { get; init; } = "dots"; // "vyshyvanka", "dots", "none"
}
