using Vyshyvanka.Designer.Models;
using Vyshyvanka.Designer.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;

namespace Vyshyvanka.Designer.Components;

public partial class BrowsePackages : IDisposable
{
    [Inject]
    private PluginStateService PluginState { get; set; } = null!;

    [Inject]
    private ToastService ToastService { get; set; } = null!;

    private string _searchQuery = string.Empty;
    private bool _showUntrustedConfirm;
    private string? _pendingInstallPackageId;
    private bool _isInstalling;

    /// <summary>The current search query.</summary>
    private string SearchQuery
    {
        get => _searchQuery;
        set
        {
            if (_searchQuery != value)
            {
                _searchQuery = value;
                PluginState.SearchPackagesDebounced(value);
            }
        }
    }

    /// <summary>Callback when a package is selected to show details.</summary>
    [Parameter]
    public EventCallback<string> OnShowDetails { get; set; }

    protected override void OnInitialized()
    {
        PluginState.OnStateChanged += StateHasChanged;
    }

    protected override async Task OnInitializedAsync()
    {
        // Ensure sources are loaded for trust checking
        if (!PluginState.Sources.Any())
        {
            await PluginState.LoadSourcesAsync();
        }
    }

    public void Dispose()
    {
        PluginState.OnStateChanged -= StateHasChanged;
    }

    private async Task OnSearchKeyUpAsync(KeyboardEventArgs e)
    {
        if (e.Key == "Enter")
        {
            await SearchAsync();
        }
    }

    private async Task SearchAsync()
    {
        if (!string.IsNullOrWhiteSpace(SearchQuery))
        {
            await PluginState.SearchPackagesAsync(SearchQuery);
        }
    }

    private async Task RetrySearchAsync()
    {
        PluginState.DismissError();
        await SearchAsync();
    }

    private async Task ShowDetailsAsync(string packageId)
    {
        await OnShowDetails.InvokeAsync(packageId);
    }

    private async Task InstallPackageAsync(string packageId)
    {
        // Check if there are untrusted sources (Requirement 4.6)
        if (PluginState.HasUntrustedSources)
        {
            _pendingInstallPackageId = packageId;
            _showUntrustedConfirm = true;
            return;
        }

        await ExecuteInstallAsync(packageId);
    }

    private async Task ConfirmUntrustedInstallAsync()
    {
        if (string.IsNullOrEmpty(_pendingInstallPackageId))
        {
            return;
        }

        _isInstalling = true;
        StateHasChanged();

        try
        {
            await ExecuteInstallAsync(_pendingInstallPackageId);
        }
        finally
        {
            _isInstalling = false;
            _showUntrustedConfirm = false;
            _pendingInstallPackageId = null;
            StateHasChanged();
        }
    }

    private void CancelUntrustedInstall()
    {
        _showUntrustedConfirm = false;
        _pendingInstallPackageId = null;
    }

    private async Task ExecuteInstallAsync(string packageId)
    {
        var success = await PluginState.InstallPackageAsync(packageId);

        if (success)
        {
            ToastService.ShowSuccess($"Successfully installed {packageId}", "Package Installed");
        }
        else
        {
            ToastService.ShowError(
                PluginState.ErrorMessage ?? $"Failed to install {packageId}",
                "Installation Failed");
        }
    }

    private void DismissError()
    {
        PluginState.DismissError();
    }
}
