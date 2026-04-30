using System.Text.Json;
using Vyshyvanka.Core.Interfaces;
using Microsoft.AspNetCore.Components;

namespace Vyshyvanka.Designer.Components;

/// <summary>
/// Panel component that displays input data from the last node execution.
/// Shows port tabs when the node has multiple input ports.
/// </summary>
public partial class NodeEditorInputPanel : ComponentBase
{
    private static readonly JsonSerializerOptions IndentedOptions = new() { WriteIndented = true };

    /// <summary>
    /// Input data from the last execution. Null if no execution data exists.
    /// </summary>
    [Parameter]
    public JsonElement? InputData { get; set; }

    /// <summary>
    /// Port definitions for this node's inputs. Used to render tabs when multiple ports exist.
    /// </summary>
    [Parameter]
    public List<PortDefinition>? Ports { get; set; }

    private string? SelectedPort { get; set; }

    private bool HasMultiplePorts => Ports is { Count: > 1 };

    protected override void OnParametersSet()
    {
        // Auto-select first port if none selected or selection is invalid
        if (Ports is { Count: > 0 } &&
            (SelectedPort is null || Ports.All(p => p.Name != SelectedPort)))
        {
            SelectedPort = Ports[0].Name;
        }
    }

    private void SelectPort(string portName)
    {
        SelectedPort = portName;
    }

    private bool HasInputData => CurrentPortData.HasValue
                                 && CurrentPortData.Value.ValueKind != JsonValueKind.Undefined;

    /// <summary>
    /// Extracts the data for the currently selected port.
    /// If the input data is an object with a key matching the port name, returns that value.
    /// Otherwise falls back to the full input data.
    /// </summary>
    private JsonElement? CurrentPortData
    {
        get
        {
            if (!InputData.HasValue || InputData.Value.ValueKind == JsonValueKind.Undefined)
                return null;

            if (!HasMultiplePorts || SelectedPort is null)
                return InputData;

            // Try to extract per-port data from the input object
            if (InputData.Value.ValueKind == JsonValueKind.Object &&
                InputData.Value.TryGetProperty(SelectedPort, out var portData))
            {
                return portData;
            }

            // Fall back to full data for the first port, null for others
            return Ports?.FirstOrDefault()?.Name == SelectedPort ? InputData : null;
        }
    }

    private string FormattedJson
    {
        get
        {
            if (!HasInputData)
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
