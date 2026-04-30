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

    private bool HasOutputData => CurrentPortData.HasValue
                                  && CurrentPortData.Value.ValueKind != JsonValueKind.Undefined;

    /// <summary>
    /// Extracts the data for the currently selected port.
    /// If the output data is an object with a key matching the port name, returns that value.
    /// Otherwise falls back to the full output data.
    /// </summary>
    private JsonElement? CurrentPortData
    {
        get
        {
            if (!OutputData.HasValue || OutputData.Value.ValueKind == JsonValueKind.Undefined)
                return null;

            if (!HasMultiplePorts || SelectedPort is null)
                return OutputData;

            if (OutputData.Value.ValueKind == JsonValueKind.Object &&
                OutputData.Value.TryGetProperty(SelectedPort, out var portData))
            {
                return portData;
            }

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
