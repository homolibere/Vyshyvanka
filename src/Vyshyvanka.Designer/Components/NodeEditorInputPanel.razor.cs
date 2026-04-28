using System.Text.Json;
using Microsoft.AspNetCore.Components;

namespace Vyshyvanka.Designer.Components;

/// <summary>
/// Panel component that displays input data from the last node execution.
/// Shows a placeholder when no execution data exists.
/// </summary>
public partial class NodeEditorInputPanel : ComponentBase
{
    private static readonly JsonSerializerOptions IndentedOptions = new() { WriteIndented = true };

    /// <summary>
    /// Input data from the last execution. Null if no execution data exists.
    /// </summary>
    [Parameter]
    public JsonElement? InputData { get; set; }

    private bool HasInputData => InputData.HasValue && InputData.Value.ValueKind != JsonValueKind.Undefined;

    private string FormattedJson
    {
        get
        {
            if (!HasInputData)
                return string.Empty;

            try
            {
                return JsonSerializer.Serialize(InputData!.Value, IndentedOptions);
            }
            catch
            {
                return InputData!.Value.GetRawText();
            }
        }
    }
}
