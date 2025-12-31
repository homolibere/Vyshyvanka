using FlowForge.Designer.Models;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace FlowForge.Designer.Components;

public partial class Toast : IDisposable
{
    [Inject]
    private IJSRuntime JS { get; set; } = null!;

    private bool _isVisible = true;
    private bool _copied;
    private Timer? _dismissTimer;

    /// <summary>The toast type (success, error, warning, info).</summary>
    [Parameter]
    public ToastType Type { get; set; } = ToastType.Info;

    /// <summary>Optional title for the toast.</summary>
    [Parameter]
    public string? Title { get; set; }

    /// <summary>The toast message.</summary>
    [Parameter]
    public string Message { get; set; } = string.Empty;

    /// <summary>Auto-dismiss timeout in milliseconds. Set to 0 to disable auto-dismiss.</summary>
    /// [Parameter]
    public int DismissTimeout { get; set; } = 5000;

    /// <summary>Callback when the toastismissed.</summary>
    [Parameter]
    public EventCallback OnDismiss { get; set; }

    protected override void OnInitialized()
    {
        if (DismissTimeout > 0)
        {
            _dismissTimer = new Timer(async _ => { await InvokeAsync(Dismiss); }, null, DismissTimeout, Timeout.Infinite);
        }
    }

    private string GetTypeClass() => Type switch
    {
        ToastType.Success => "toast-success",
        ToastType.Error => "toast-error",
        ToastType.Warning => "toast-warning",
        ToastType.Info => "toast-info",
        _ => "toast-info"
    };

    private string GetIcon() => Type switch
    {
        ToastType.Success => "✓",
        ToastType.Error => "✕",
        ToastType.Warning => "⚠",
        ToastType.Info => "ℹ",
        _ => "ℹ"
    };

    private async Task Dismiss()
    {
        _isVisible = false;
        StateHasChanged();

        // Allow animation to complete before removing
        await Task.Delay(200);
        await OnDismiss.InvokeAsync();
    }

    public void Dispose()
    {
        _dismissTimer?.Dispose();
    }

    private async Task CopyToClipboardAsync()
    {
        var textToCopy = string.IsNullOrEmpty(Title) ? Message : $"{Title}: {Message}";
        try
        {
            await JS.InvokeVoidAsync("navigator.clipboard.writeText", textToCopy);
            _copied = true;
            StateHasChanged();
            
            await Task.Delay(1500);
            _copied = false;
            StateHasChanged();
        }
        catch
        {
            // Clipboard API may not be available in all contexts
        }
    }
}
