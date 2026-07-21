using Vyshyvanka.Designer.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace Vyshyvanka.Designer.Components;

/// <summary>
/// Displays the resolved webhook URL(s) when a user configures the path
/// property on a webhook-trigger node. Shows both the path-based and
/// workflow-ID-based URLs with copy-to-clipboard buttons.
/// </summary>
public partial class WebhookUrlHint : ComponentBase, IDisposable
{
    [Inject] private HttpClient Http { get; set; } = null!;
    [Inject] private IJSRuntime JS { get; set; } = null!;
    [Inject] private WorkflowStore Store { get; set; } = null!;

    /// <summary>
    /// The current value of the webhook path property.
    /// </summary>
    [Parameter]
    public string? PathValue { get; set; }

    private bool _copiedPath;
    private bool _copiedId;
    private CancellationTokenSource? _resetCts;

    private string BaseUrl => Http.BaseAddress?.ToString().TrimEnd('/') ?? "";

    private string WorkflowId => Store.Workflow?.Id.ToString() ?? "";

    private string PathUrl
    {
        get
        {
            var path = (PathValue ?? "").TrimStart('/');
            return $"{BaseUrl}/api/webhook/path/{path}";
        }
    }

    private string IdUrl => $"{BaseUrl}/api/webhook/{WorkflowId}";

    private async Task CopyToClipboard(string url)
    {
        try
        {
            await JS.InvokeVoidAsync("navigator.clipboard.writeText", url);

            if (url == PathUrl)
                _copiedPath = true;
            else
                _copiedId = true;

            StateHasChanged();

            // Reset the checkmark after 2 seconds
            _resetCts?.Cancel();
            _resetCts = new CancellationTokenSource();
            var token = _resetCts.Token;

            _ = Task.Delay(2000, token).ContinueWith(_ =>
            {
                _copiedPath = false;
                _copiedId = false;
                InvokeAsync(StateHasChanged);
            }, token, TaskContinuationOptions.OnlyOnRanToCompletion, TaskScheduler.Default);
        }
        catch
        {
            // Clipboard API may not be available in all contexts
        }
    }

    public void Dispose()
    {
        _resetCts?.Cancel();
        _resetCts?.Dispose();
        GC.SuppressFinalize(this);
    }
}
