using System.Text.Json;
using Vyshyvanka.Core.Interfaces;
using Microsoft.AspNetCore.Components;

namespace Vyshyvanka.Designer.Components;

/// <summary>
/// Panel component that displays output data from the last node execution.
/// Shows port tabs when the node has multiple output ports.
/// </summary>
public partial class NodeEditorOutputPanel : ComponentBase
{
    private static readonly JsonSerializerOptions IndentedOptions = new() { WriteIndented = true };

    /// <summary>
    /// Output data from the last execution. Null if no execution data exists.
    /// </summary>
    [Parameter]
    public JsonElement? OutputData { get; set; }

    /// <summary>
    /// Port definitions for this node's outputs. Used to render tabs when multiple ports exist.
    /// </summary>
    [Parameter]
    public List<PortDefinition>? Ports { get; set; }

    private string? SelectedPort { get; set; }

    private bool HasMultiplePorts => Ports is { Count: > 1 };

    protected override void OnParametersSet()
    {
        if (Ports is not { Count: > 0 })
            return;

        // Auto-select the active output port based on the execution result's outputPort field
        if (OutputData.HasValue &&
            OutputData.Value.ValueKind == JsonValueKind.Object &&
            OutputData.Value.TryGetProperty("outputPort", out var outputPortEl) &&
            outputPortEl.ValueKind == JsonValueKind.String)
        {
            var activePort = outputPortEl.GetString();
            if (activePort is not null && Ports.Any(p => p.Name == activePort))
            {
                SelectedPort = activePort;
                return;
            }
        }

        if (SelectedPort is null || Ports.All(p => p.Name != SelectedPort))
        {
            SelectedPort = Ports[0].Name;
        }
    }

    private void SelectPort(string portName)
    {
        SelectedPort = portName;
    }

    private bool HasOutputData => CurrentPortData.HasValue
                                  && CurrentPortData.Value.ValueKind != JsonValueKind.Undefined;

    /// <summary>
    /// Extracts the data for the currently selected port.
    /// For nodes with multiple output ports, only shows data on the port that was
    /// actually activated during execution (determined by the outputPort field).
    /// </summary>
    private JsonElement? CurrentPortData
    {
        get
        {
            if (!OutputData.HasValue || OutputData.Value.ValueKind == JsonValueKind.Undefined)
                return null;

            if (!HasMultiplePorts || SelectedPort is null)
                return OutputData;

            // If the output has port-keyed data (e.g. {"true": {...}, "false": {...}}), extract it
            if (OutputData.Value.ValueKind == JsonValueKind.Object &&
                OutputData.Value.TryGetProperty(SelectedPort, out var portData))
            {
                return portData;
            }

            // Check if the output declares which port was activated via outputPort field.
            // Only show data on the matching tab; other tabs get nothing.
            if (OutputData.Value.ValueKind == JsonValueKind.Object &&
                OutputData.Value.TryGetProperty("outputPort", out var outputPortEl) &&
                outputPortEl.ValueKind == JsonValueKind.String)
            {
                var activePort = outputPortEl.GetString();
                return string.Equals(activePort, SelectedPort, StringComparison.OrdinalIgnoreCase)
                    ? OutputData
                    : null;
            }

            // No routing info — show on the first port only
            return Ports?.FirstOrDefault()?.Name == SelectedPort ? OutputData : null;
        }
    }

    private string FormattedJson
    {
        get
        {
            if (!HasOutputData)
                return string.Empty;

            try
            {
                return JsonSerializer.Serialize(CurrentPortData!.Value, IndentedOptions);
            }
            catch
            {
                return CurrentPortData!.Value.GetRawText();
            }
        }
    }
}
