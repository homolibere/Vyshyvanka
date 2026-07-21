using System.Text.Json;
using Vyshyvanka.Core.Interfaces;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace Vyshyvanka.Designer.Components;

/// <summary>
/// Panel component that displays input data from the last node execution.
/// Supports an editable mode where users can paste JSON as mock input for isolated node testing.
/// Shows port tabs when the node has multiple input ports.
/// </summary>
public partial class NodeEditorInputPanel : ComponentBase
{
    private static readonly JsonSerializerOptions IndentedOptions = new() { WriteIndented = true };

    [Inject] private IJSRuntime Js { get; set; } = default!;

    private bool _copied;
    private bool _isEditMode;
    private string _editText = string.Empty;
    private string? _jsonError;

    /// <summary>
    /// Input data from the last execution. Null if no execution data exists.
    /// </summary>
    [Parameter]
    public JsonElement? InputData { get; set; }

    /// <summary>
    /// Mock input data currently pinned for this node. Null if no mock input.
    /// </summary>
    [Parameter]
    public JsonElement? MockInput { get; set; }

    /// <summary>
    /// Callback invoked when mock input JSON is committed (user clicks the pin button with valid JSON).
    /// </summary>
    [Parameter]
    public EventCallback<JsonElement> OnMockInputSet { get; set; }

    /// <summary>
    /// Callback invoked when mock input is cleared.
    /// </summary>
    [Parameter]
    public EventCallback OnMockInputCleared { get; set; }

    /// <summary>
    /// Port definitions for this node's inputs. Used to render tabs when multiple ports exist.
    /// </summary>
    [Parameter]
    public List<PortDefinition>? Ports { get; set; }

    private string? SelectedPort { get; set; }

    private bool HasMultiplePorts => Ports is { Count: > 1 };

    private bool HasMockInput => MockInput.HasValue &&
                                 MockInput.Value.ValueKind != JsonValueKind.Undefined;

    /// <summary>
    /// The effective input data to display: mock input takes priority over execution data.
    /// </summary>
    private JsonElement? EffectiveInputData => HasMockInput ? MockInput : InputData;

    protected override void OnParametersSet()
    {
        // Auto-select first port if none selected or selection is invalid
        if (Ports is { Count: > 0 } &&
            (SelectedPort is null || Ports.All(p => p.Name != SelectedPort)))
        {
            SelectedPort = Ports[0].Name;
        }

        // If entering edit mode with existing mock data, populate the textarea
        if (_isEditMode && HasMockInput && string.IsNullOrWhiteSpace(_editText))
        {
            _editText = FormatJsonElement(MockInput!.Value);
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
            var data = EffectiveInputData;

            if (!data.HasValue || data.Value.ValueKind == JsonValueKind.Undefined)
                return null;

            if (!HasMultiplePorts || SelectedPort is null)
                return data;

            // Try to extract per-port data from the input object
            if (data.Value.ValueKind == JsonValueKind.Object &&
                data.Value.TryGetProperty(SelectedPort, out var portData))
            {
                return portData;
            }

            // Fall back to full data for the first port, null for others
            return Ports?.FirstOrDefault()?.Name == SelectedPort ? data : null;
        }
    }

    private string FormattedJson
    {
        get
        {
            if (!HasInputData)
                return string.Empty;

            return FormatJsonElement(CurrentPortData!.Value);
        }
    }

    private static string FormatJsonElement(JsonElement element)
    {
        try
        {
            return JsonSerializer.Serialize(element, IndentedOptions);
        }
        catch
        {
            return element.GetRawText();
        }
    }

    // ── Edit mode ────────────────────────────────────────────────────────

    private void EnterEditMode()
    {
        _isEditMode = true;
        _jsonError = null;

        // Pre-populate with mock input if it exists, otherwise with execution data
        if (HasMockInput)
        {
            _editText = FormatJsonElement(MockInput!.Value);
        }
        else if (HasInputData)
        {
            _editText = FormattedJson;
        }
        else
        {
            _editText = "{\n  \n}";
        }
    }

    private void CancelEditMode()
    {
        _isEditMode = false;
        _jsonError = null;
        _editText = string.Empty;
    }

    private async Task PinMockInput()
    {
        _jsonError = null;

        if (string.IsNullOrWhiteSpace(_editText))
        {
            _jsonError = "JSON cannot be empty";
            return;
        }

        try
        {
            var parsed = JsonDocument.Parse(_editText);
            var element = parsed.RootElement.Clone();

            await OnMockInputSet.InvokeAsync(element);
            _isEditMode = false;
            _editText = string.Empty;
        }
        catch (JsonException ex)
        {
            _jsonError = $"Invalid JSON: {ex.Message}";
        }
    }

    private async Task ClearMockInput()
    {
        await OnMockInputCleared.InvokeAsync();
        _isEditMode = false;
        _editText = string.Empty;
        _jsonError = null;
    }

    private void OnEditTextChanged(ChangeEventArgs e)
    {
        _editText = e.Value?.ToString() ?? string.Empty;
        _jsonError = null; // Clear error on edit
    }

    // ── Clipboard ────────────────────────────────────────────────────────

    private async Task CopyToClipboardAsync()
    {
        try
        {
            await Js.InvokeVoidAsync("navigator.clipboard.writeText", FormattedJson);
            _copied = true;
            StateHasChanged();
            await Task.Delay(2000);
            _copied = false;
            StateHasChanged();
        }
        catch
        {
            // Clipboard API may not be available in all contexts
        }
    }
}
