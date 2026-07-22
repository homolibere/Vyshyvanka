using System.Text.Json;
using Vyshyvanka.Core.Enums;
using Vyshyvanka.Designer.Models;
using Vyshyvanka.Designer.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace Vyshyvanka.Designer.Components;

public partial class NodeExecutionInspector : IDisposable
{
    private static readonly JsonSerializerOptions PrettyPrintOptions = new()
    {
        WriteIndented = true
    };

    [Inject] private WorkflowStore Store { get; set; } = null!;
    [Inject] private CanvasStateService CanvasState { get; set; } = null!;
    [Inject] private ExecutionStateService ExecutionState { get; set; } = null!;
    [Inject] private IJSRuntime JS { get; set; } = null!;

    /// <summary>Raised when the inspector is closed.</summary>
    [Parameter] public EventCallback OnClose { get; set; }

    private NodeExecutionState? _state;
    private string? _selectedNodeId;
    private int _currentIteration;
    private bool _inputExpanded = true;
    private bool _outputExpanded = true;
    private bool _metaExpanded;
    private bool _inputCopied;
    private bool _outputCopied;

    protected override void OnInitialized()
    {
        Store.OnStateChanged += HandleStateChanged;
        ExecutionState.OnExecutionChanged += HandleExecutionChanged;
        UpdateState();
    }

    private void HandleStateChanged()
    {
        UpdateState();
        InvokeAsync(StateHasChanged);
    }

    private void HandleExecutionChanged(ExecutionResponse? execution)
    {
        UpdateState();
        InvokeAsync(StateHasChanged);
    }

    private void UpdateState()
    {
        var selectedNodeId = CanvasState.SelectedNodeId;

        if (selectedNodeId != _selectedNodeId)
        {
            _selectedNodeId = selectedNodeId;
            _currentIteration = 0;
            _inputCopied = false;
            _outputCopied = false;
        }

        _state = _selectedNodeId is not null
            ? ExecutionState.GetNodeExecutionState(_selectedNodeId)
            : null;
    }

    private string GetNodeName()
    {
        if (_selectedNodeId is null) return "";
        var node = Store.Workflow.Nodes.FirstOrDefault(n => n.Id == _selectedNodeId);
        return node?.Name ?? _selectedNodeId;
    }

    private JsonElement? GetCurrentInput()
    {
        if (_state is null) return null;
        if (_state.HasMultipleIterations && _currentIteration < _state.Iterations.Count)
            return _state.Iterations[_currentIteration].InputData;
        return _state.InputData;
    }

    private JsonElement? GetCurrentOutput()
    {
        if (_state is null) return null;
        if (_state.HasMultipleIterations && _currentIteration < _state.Iterations.Count)
            return _state.Iterations[_currentIteration].OutputData;
        return _state.OutputData;
    }

    private string? GetCurrentError()
    {
        if (_state is null) return null;
        if (_state.HasMultipleIterations && _currentIteration < _state.Iterations.Count)
            return _state.Iterations[_currentIteration].ErrorMessage;
        return _state.ErrorMessage;
    }

    private void PreviousIteration()
    {
        if (_currentIteration > 0)
        {
            _currentIteration--;
            _inputCopied = false;
            _outputCopied = false;
        }
    }

    private void NextIteration()
    {
        if (_state is not null && _currentIteration < _state.IterationCount - 1)
        {
            _currentIteration++;
            _inputCopied = false;
            _outputCopied = false;
        }
    }

    private async Task CopyInput()
    {
        if (GetCurrentInput() is { } input)
        {
            await CopyToClipboard(FormatJson(input));
            _inputCopied = true;
            StateHasChanged();
            await ResetCopyFlag(() => _inputCopied = false);
        }
    }

    private async Task CopyOutput()
    {
        if (GetCurrentOutput() is { } output)
        {
            await CopyToClipboard(FormatJson(output));
            _outputCopied = true;
            StateHasChanged();
            await ResetCopyFlag(() => _outputCopied = false);
        }
    }

    private async Task CopyToClipboard(string text)
    {
        try
        {
            await JS.InvokeVoidAsync("navigator.clipboard.writeText", text);
        }
        catch
        {
            // Clipboard API may not be available in all contexts
        }
    }

    private async Task ResetCopyFlag(Action reset)
    {
        await Task.Delay(2000);
        reset();
        await InvokeAsync(StateHasChanged);
    }

    private async Task Close()
    {
        await OnClose.InvokeAsync();
    }

    private static string FormatJson(JsonElement element)
    {
        try
        {
            return JsonSerializer.Serialize(element, PrettyPrintOptions);
        }
        catch
        {
            return element.GetRawText();
        }
    }

    private static string FormatDuration(double ms)
    {
        if (ms < 1000) return $"{ms:0}ms";
        if (ms < 60_000) return $"{ms / 1000:0.#}s";
        return $"{ms / 60_000:0.#}m";
    }

    private static string GetStatusClass(ExecutionStatus status) => status switch
    {
        ExecutionStatus.Completed => "status-completed",
        ExecutionStatus.Failed => "status-failed",
        ExecutionStatus.Running => "status-running",
        _ => ""
    };

    private static string GetStatusIcon(ExecutionStatus status) => status switch
    {
        ExecutionStatus.Completed => "✓",
        ExecutionStatus.Failed => "✗",
        ExecutionStatus.Running => "●",
        _ => "○"
    };

    public void Dispose()
    {
        Store.OnStateChanged -= HandleStateChanged;
        ExecutionState.OnExecutionChanged -= HandleExecutionChanged;
    }
}
