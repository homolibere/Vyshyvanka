using FlowForge.Designer.Models;
using FlowForge.Designer.Services;
using Microsoft.AspNetCore.Components;

namespace FlowForge.Designer.Components;

public partial class PluginManager : IDisposable
{
    [Inject]
    private PluginStateService PluginState { get; set; } = null!;

    private const string InstalledTab = "installed";
    private const string BrowseTab = "browse";
    private const string SourcesTab = "sources";

    private string _activeTab = InstalledTab;
    private bool _isPackageDetailsOpen;
    private string? _selectedPackageId;
    private bool _isSourceEditOpen;
    private PackageSourceModel? _editingSource;

    /// <summary>Whether the Plugin Manager is open.</summary>
    [Parameter]
    public bool IsOpen { get; set; }

    /// <summary>Callback when the Plugin Manager is closed.</summary>
    [Parameter]
    public EventCallback OnClose { get; set; }

    private bool IsInstalledTabActive => _activeTab == InstalledTab;
    private bool IsBrowseTabActive => _activeTab == BrowseTab;
    private bool IsSourcesTabActive => _activeTab == SourcesTab;

    protected override void OnInitialized()
    {
        PluginState.OnStateChanged += StateHasChanged;
    }

    protected override async Task OnParametersSetAsync()
    {
        if (IsOpen)
        {
            // Load initial data when opened (Requirement 1.4)
            await LoadInitialDataAsync();
        }
    }

    public void Dispose()
    {
        PluginState.OnStateChanged -= StateHasChanged;
    }

    private async Task LoadInitialDataAsync()
    {
        // Load installed packages if not already loaded
        if (!PluginState.InstalledPackages.Any())
        {
            await PluginState.LoadInstalledPackagesAsync();
        }
    }

    private void SelectInstalledTab() => _activeTab = InstalledTab;
    private void SelectBrowseTab() => _activeTab = BrowseTab;
    private void SelectSourcesTab() => _activeTab = SourcesTab;

    private void NavigateToBrowse()
    {
        _activeTab = BrowseTab;
    }

    private void ShowPackageDetails(string packageId)
    {
        _selectedPackageId = packageId;
        _isPackageDetailsOpen = true;
    }

    private void ClosePackageDetails()
    {
        _isPackageDetailsOpen = false;
        _selectedPackageId = null;
    }

    private void OnPackageInstalled(string packageId)
    {
        // Package was installed - UI will update via state change
        _ = packageId; // Suppress unused parameter warning
    }

    private void OnPackageUpdated(string packageId)
    {
        // Package was updated - UI will update via state change
        _ = packageId; // Suppress unused parameter warning
    }

    private void OnPackageUninstalled()
    {
        // Package was uninstalled, close details
        ClosePackageDetails();
    }

    private void ShowAddSourceModal()
    {
        _editingSource = null;
        _isSourceEditOpen = true;
    }

    private void ShowEditSourceModal(PackageSourceModel source)
    {
        _editingSource = source;
        _isSourceEditOpen = true;
    }

    private void CloseSourceEdit()
    {
        _isSourceEditOpen = false;
        _editingSource = null;
    }

    private void OnSourceSaved(PackageSourceModel source)
    {
        // Source was saved - UI will update via state change
        _ = source; // Suppress unused parameter warning
    }

    private async Task Close()
    {
        if (!PluginState.IsLoading)
        {
            await OnClose.InvokeAsync();
        }
    }

    private async Task HandleOverlayClick()
    {
        // Only close if no nested modals are open
        if (!_isPackageDetailsOpen && !_isSourceEditOpen)
        {
            await Close();
        }
    }
}
