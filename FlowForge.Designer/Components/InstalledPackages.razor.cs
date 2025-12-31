using FlowForge.Designer.Models;
using FlowForge.Designer.Services;
using Microsoft.AspNetCore.Components;

namespace FlowForge.Designer.Components;

public partial class InstalledPackages : IDisposable
{
    [Inject]
    private PluginStateService PluginState { get; set; } = null!;

    [Inject]
    private ToastService ToastService { get; set; } = null!;

    private bool _showUninstallConfirm;
    private string? _pendingUninstallPackageId;
    private string _uninstallMessage = string.Empty;
    private List<string> _affectedWorkflows = [];
    private ConfirmDialogVariant _uninstallVariant = ConfirmDialogVariant.Danger;
    private bool _isUninstalling;

    /// <summary>Callback to switch to the Browse tab.</summary>
    [Parameter]
    public EventCallback OnNavigateToBrowse { get; set; }

    /// <summary>Callback when a package is selected to show details.</summary>
    [Parameter]
    public EventCallback<string> OnShowDetails { get; set; }

    protected override void OnInitialized()
    {
        PluginState.OnStateChanged += StateHasChanged;
    }

    protected override async Task OnInitializedAsync()
    {
        if (!PluginState.InstalledPackages.Any())
        {
            await PluginState.LoadInstalledPackagesAsync();
        }
    }

    public void Dispose()
    {
        PluginState.OnStateChanged -= StateHasChanged;
    }

    private async Task CheckForUpdatesAsync()
    {
        await PluginState.CheckForUpdatesAsync();
    }

    private async Task GoToBrowse()
    {
        await OnNavigateToBrowse.InvokeAsync();
    }

    private async Task ShowDetailsAsync(string packageId)
    {
        await OnShowDetails.InvokeAsync(packageId);
    }

    private async Task UpdatePackageAsync(string packageId)
    {
        var success = await PluginState.UpdatePackageAsync(packageId);

        if (success)
        {
            ToastService.ShowSuccess($"Successfully updated {packageId}", "Package Updated");
        }
        else
        {
            ToastService.ShowError(
                PluginState.ErrorMessage ?? $"Failed to update {packageId}",
                "Update Failed");
        }
    }

    private async Task UninstallPackageAsync(string packageId)
    {
        // First, try to uninstall without force to check for affected workflows
        var result = await PluginState.UninstallPackageAsync(packageId, force: false);

        if (result.Success)
        {
            // Uninstall succeeded without issues
            ToastService.ShowSuccess($"Successfully uninstalled {packageId}", "Package Uninstalled");
            return;
        }

        if (result.AffectedWorkflows.Count > 0)
        {
            // Show confirmation dialog with affected workflows warning
            _pendingUninstallPackageId = packageId;
            _affectedWorkflows = result.AffectedWorkflows.ToList();
            _uninstallMessage = $"The package \"{packageId}\" is used by workflows. Uninstalling it may break these workflows.";
            _uninstallVariant = ConfirmDialogVariant.Warning;
            _showUninstallConfirm = true;
        }
        else
        {
            // Show simple confirmation dialog
            _pendingUninstallPackageId = packageId;
            _affectedWorkflows = [];
            _uninstallMessage = $"Are you sure you want to uninstall \"{packageId}\"? This action cannot be undone.";
            _uninstallVariant = ConfirmDialogVariant.Danger;
            _showUninstallConfirm = true;
        }
    }

    private async Task ConfirmUninstallAsync()
    {
        if (string.IsNullOrEmpty(_pendingUninstallPackageId))
        {
            return;
        }

        _isUninstalling = true;
        StateHasChanged();

        try
        {
            // Force uninstall since user confirmed
            var result = await PluginState.UninstallPackageAsync(_pendingUninstallPackageId, force: true);

            if (result.Success)
            {
                ToastService.ShowSuccess($"Successfully uninstalled {_pendingUninstallPackageId}", "Package Uninstalled");
            }
            else
            {
                ToastService.ShowError(
                    result.Errors.FirstOrDefault() ?? $"Failed to uninstall {_pendingUninstallPackageId}",
                    "Uninstall Failed");
            }
        }
        finally
        {
            _isUninstalling = false;
            _showUninstallConfirm = false;
            _pendingUninstallPackageId = null;
            _affectedWorkflows = [];
            StateHasChanged();
        }
    }

    private void CancelUninstall()
    {
        _showUninstallConfirm = false;
        _pendingUninstallPackageId = null;
        _affectedWorkflows = [];
    }

    private void DismissError()
    {
        PluginState.DismissError();
    }

    private async Task RetryLoadPackagesAsync()
    {
        PluginState.DismissError();
        await PluginState.LoadInstalledPackagesAsync();
    }
}
