using Vyshyvanka.Designer.Models;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace Vyshyvanka.Designer.Components;

/// <summary>
/// Property editor for code type configuration properties.
/// Renders a CodeMirror editor with C# syntax highlighting, line numbers, and bracket matching.
/// </summary>
public partial class CodePropertyEditor : ComponentBase, IAsyncDisposable
{
    [Inject] private IJSRuntime JS { get; set; } = null!;

    [Parameter, EditorRequired] public ConfigurationProperty Property { get; set; } = null!;

    [Parameter] public object? Value { get; set; }

    [Parameter] public EventCallback<object?> ValueChanged { get; set; }

    [Parameter] public bool ShowValidationError { get; set; }

    private ElementReference _editorContainer;
    private DotNetObjectReference<CodePropertyEditor>? _dotNetRef;
    private string _editorId = $"code-editor-{Guid.NewGuid():N}";
    private bool _initialized;
    private string _currentValue = string.Empty;

    protected override void OnParametersSet()
    {
        _currentValue = Value?.ToString() ?? string.Empty;
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender)
        {
            _dotNetRef = DotNetObjectReference.Create(this);
            _initialized = await JS.InvokeAsync<bool>(
                "codeEditorInterop.initialize",
                _editorContainer,
                _editorId,
                _currentValue,
                _dotNetRef);
        }
    }

    /// <summary>
    /// Called from JavaScript when the editor content changes.
    /// </summary>
    [JSInvokable]
    public async Task OnCodeChanged(string newValue)
    {
        _currentValue = newValue;
        await ValueChanged.InvokeAsync(newValue);
    }

    /// <summary>
    /// Formats the code in the editor using CodeMirror's auto-indent.
    /// </summary>
    private async Task FormatCodeAsync()
    {
        if (!_initialized) return;
        await JS.InvokeVoidAsync("codeEditorInterop.autoFormat", _editorId);
    }

    public async ValueTask DisposeAsync()
    {
        if (_initialized)
        {
            try
            {
                await JS.InvokeVoidAsync("codeEditorInterop.dispose", _editorId);
            }
            catch (JSDisconnectedException)
            {
                // Circuit disconnected, nothing to clean up
            }
        }

        _dotNetRef?.Dispose();
    }
}
