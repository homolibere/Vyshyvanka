using System.Text.Json;
using Microsoft.AspNetCore.Components;

namespace Vyshyvanka.Designer.Components;

/// <summary>
/// Panel component that displays output data from the last node execution.
/// Shows a placeholder when no execution data exists.
/// </summary>
public partial class NodeEditorOutputPanel : ComponentBase
{
    private static readonly JsonSerializerOptions IndentedOptions = new() { WriteIndented = true };

    /// <summary>
    /// Output data from the last execution. Null if no execution data exists.
    /// </summary>
    [Parameter]
    public JsonElement? OutputData { get; set; }

    private bool HasOutputData => OutputData.HasValue && OutputData.Value.ValueKind != JsonValueKind.Undefined;

    private string FormattedJson
    {
        get
        {
            if (!HasOutputData)
                return string.Empty;

            try
            {
                return JsonSerializer.Serialize(OutputData!.Value, IndentedOptions);
            }
            catch
            {
                return OutputData!.Value.GetRawText();
            }
        }
    }
}
