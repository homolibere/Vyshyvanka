using System.Text.Json;
using Vyshyvanka.Core.Enums;
using Vyshyvanka.Core.Interfaces;
using Vyshyvanka.Core.Models;

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
        return GetHeight(inputCount, outputCount);
    }

    /// <summary>
    /// Calculates the total height for a node based on explicit port counts.
    /// </summary>
    public static double GetHeight(int inputCount, int outputCount)
    {
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

    /// <summary>
    /// Resolves the effective output ports for a node, accounting for dynamic ports
    /// derived from node configuration (e.g., Switch node cases).
    /// </summary>
    public static List<PortDefinition> GetEffectiveOutputs(WorkflowNode node, NodeDefinition? definition)
    {
        if (definition is null) return [];

        // Switch nodes derive output ports from their cases configuration
        if (node.Type == "switch")
        {
            return ResolveSwitchOutputs(node, definition);
        }

        return definition.Outputs;
    }

    private static List<PortDefinition> ResolveSwitchOutputs(WorkflowNode node, NodeDefinition definition)
    {
        var outputs = new List<PortDefinition>();

        // Extract case output names from the node's configuration
        if (node.Configuration.ValueKind == JsonValueKind.Object &&
            node.Configuration.TryGetProperty("cases", out var casesElement) &&
            casesElement.ValueKind == JsonValueKind.Array)
        {
            foreach (var caseItem in casesElement.EnumerateArray())
            {
                if (caseItem.ValueKind != JsonValueKind.Object) continue;

                string? outputName = null;

                if (caseItem.TryGetProperty("output", out var outputProp) &&
                    outputProp.ValueKind == JsonValueKind.String)
                {
                    outputName = outputProp.GetString();
                }
                else if (caseItem.TryGetProperty("value", out var valueProp))
                {
                    outputName = valueProp.ValueKind switch
                    {
                        JsonValueKind.String => valueProp.GetString(),
                        JsonValueKind.Number => valueProp.GetRawText(),
                        JsonValueKind.True => "true",
                        JsonValueKind.False => "false",
                        _ => null
                    };
                }

                if (!string.IsNullOrWhiteSpace(outputName) &&
                    outputs.All(o => o.Name != outputName))
                {
                    outputs.Add(new PortDefinition
                    {
                        Name = outputName,
                        DisplayName = outputName,
                        Type = PortType.Any
                    });
                }
            }
        }

        // Always include the default port (from definition)
        var defaultPort = definition.Outputs.FirstOrDefault(o => o.Name == "default");
        if (defaultPort is not null && outputs.All(o => o.Name != "default"))
        {
            outputs.Add(defaultPort);
        }

        return outputs.Count > 0 ? outputs : definition.Outputs;
    }
}
