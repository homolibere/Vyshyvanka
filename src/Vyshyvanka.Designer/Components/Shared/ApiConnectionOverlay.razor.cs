using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Vyshyvanka.Designer.Services;

namespace Vyshyvanka.Designer.Components;

public partial class ApiConnectionOverlay : ComponentBase, IDisposable
{
    [Inject] private ApiConnectionService ConnectionService { get; set; } = null!;

    private string UrlInput { get; set; } = string.Empty;

    private bool IsVisible =>
        ConnectionService.State == ApiConnectionState.Connecting ||
        ConnectionService.State == ApiConnectionState.Disconnected;

    private bool IsConnecting => ConnectionService.State == ApiConnectionState.Connecting;

    protected override void OnInitialized()
    {
        UrlInput = ConnectionService.CurrentUrl;
        ConnectionService.StateChanged += OnStateChanged;
    }

    private void OnStateChanged()
    {
        InvokeAsync(() =>
        {
            if (string.IsNullOrEmpty(UrlInput) && !string.IsNullOrEmpty(ConnectionService.CurrentUrl))
            {
                UrlInput = ConnectionService.CurrentUrl;
            }

            StateHasChanged();
        });
    }

    private async Task HandleConnect()
    {
        if (string.IsNullOrWhiteSpace(UrlInput))
        {
            return;
        }

        await ConnectionService.ConnectAsync(UrlInput.Trim());
    }

    private async Task HandleRetry()
    {
        await ConnectionService.RetryAsync();
    }

    private async Task HandleKeyDown(KeyboardEventArgs e)
    {
        if (e.Key == "Enter")
        {
            await HandleConnect();
        }
    }

    public void Dispose()
    {
        ConnectionService.StateChanged -= OnStateChanged;
    }
}
