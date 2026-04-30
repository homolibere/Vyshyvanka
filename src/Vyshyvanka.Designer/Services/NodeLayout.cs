using Vyshyvanka.Core.Interfaces;

namespace Vyshyvanka.Designer.Services;

/// <summary>
/// Calculates node dimensions based on the node definition (port count, name length).
/// Shared between <see cref="Components.CanvasNodeComponent"/> and <see cref="Components.WorkflowCanvas"/>
/// so rendering and connection coordinates stay in sync.
/// </summary>
public static class NodeLayout
{
    public const double MinWidth = 140;
    public const double MaxWidth = 260;
    public const double HeaderHeight = 28;
    public const double PortSpacing = 20;
    public const double PortVerticalPadding = 16;
    public const double MinBodyHeight = 40;
    public const double Radius = 8;

    /// <summary>Approximate width per character for the 12px font used in the header title.</summary>
    private const double CharWidth = 7.5;

    /// <summary>Horizontal padding: icon (36px left of text) + right margin (16px).</summary>
    private const double HeaderPadding = 52;

    /// <summary>
    /// Calculates the width for a node based on its name.
    /// </summary>
    public static double GetWidth(string? nodeName)
    {
        var textWidth = (nodeName?.Length ?? 0) * CharWidth + HeaderPadding;
        return Math.Clamp(textWidth, MinWidth, MaxWidth);
    }

    /// <summary>
    /// Calculates the total height for a node based on its port count.
    /// </summary>
    public static double GetHeight(NodeDefinition? definition)
    {
        var inputCount = definition?.Inputs?.Count ?? 0;
        var outputCount = definition?.Outputs?.Count ?? 0;
        var maxPorts = Math.Max(inputCount, outputCount);

        var bodyHeight = maxPorts <= 1
            ? MinBodyHeight
            : (maxPorts - 1) * PortSpacing + PortVerticalPadding * 2;

        return HeaderHeight + bodyHeight;
    }

    /// <summary>
    /// Returns both width and height for a node.
    /// </summary>
    public static (double Width, double Height) GetSize(string? nodeName, NodeDefinition? definition)
    {
        return (GetWidth(nodeName), GetHeight(definition));
    }
}
