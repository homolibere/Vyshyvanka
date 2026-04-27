using FlowForge.Designer.Models;

namespace FlowForge.Designer.Services;

/// <summary>
/// Service for managing plugin state and operations in the Designer.
/// Provides state management for installed packages, search results, and package sources.
/// </summary>
public class PluginStateService : IDisposable
{
    private readonly FlowForgeApiClient _apiClient;
    private readonly WorkflowStateService _workflowStateService;

    private List<InstalledPackageModel> _installedPackages = [];
    private List<PackageUpdateInfoModel> _availableUpdates = [];
    private List<PackageSourceModel> _sources = [];
    private PackageSearchResultModel? _searchResults;
    private CancellationTokenSource? _searchCts;
    private Timer? _searchDebounceTimer;

    private bool _isLoading;
    private string? _statusMessage;
    private string? _errorMessage;
    private bool _disposed;

    // Operation-specific tracking to prevent duplicate requests
    private readonly HashSet<string> _packagesBeingInstalled = [];
    private readonly HashSet<string> _packagesBeingUpdated = [];
    private readonly HashSet<string> _packagesBeingUninstalled = [];
    private readonly HashSet<string> _sourcesBeingTested = [];
    private bool _isCheckingForUpdates;
    private bool _isSearching;

    /// <summary>Event raised when plugin state changes.</summary>
    public event Action? OnStateChanged;

    /// <summary>
    /// Creates a new instance of the PluginStateService.
    /// </summary>
    /// <param name="apiClient">The API client for communicating with the FlowForge API.</param>
    /// <param name="workflowStateService">The workflow state service for node refresh.</param>
    public PluginStateService(FlowForgeApiClient apiClient, WorkflowStateService workflowStateService)
    {
        _apiClient = apiClient;
        _workflowStateService = workflowStateService;
    }

    /// <summary>Currently installed packages.</summary>
    public IReadOnlyList<InstalledPackageModel> InstalledPackages => _installedPackages;

    /// <summary>Available updates for installed packages.</summary>
    public IReadOnlyList<PackageUpdateInfoModel> AvailableUpdates => _availableUpdates;

    /// <summary>Configured package sources.</summary>
    public IReadOnlyList<PackageSourceModel> Sources => _sources;

    /// <summary>Current search results.</summary>
    public PackageSearchResultModel? SearchResults => _searchResults;

    /// <summary>Whether an operation is in progress.</summary>
    public bool IsLoading => _isLoading;

    /// <summary>Current operation status message.</summary>
    public string? StatusMessage => _statusMessage;

    /// <summary>Current error message, if any.</summary>
    public string? ErrorMessage => _errorMessage;

    /// <summary>Whether any enabled package sources are untrusted.</summary>
    public bool HasUntrustedSources => _sources.Any(s => s.IsEnabled && !s.IsTrusted);

    /// <summary>Gets the names of untrusted enabled sources.</summary>
    public IReadOnlyList<string> UntrustedSourceNames =>
        _sources.Where(s => s.IsEnabled && !s.IsTrusted).Select(s => s.Name).ToList();

    /// <summary>Whether a search operation is in progress.</summary>
    public bool IsSearching => _isSearching;

    /// <summary>Whether an update check is in progress.</summary>
    public bool IsCheckingForUpdates => _isCheckingForUpdates;

    /// <summary>
    /// Checks if a specific package is currently being installed.
    /// </summary>
    /// <param name="packageId">The package identifier.</param>
    /// <returns>True if the package is being installed.</returns>
    public bool IsPackageBeingInstalled(string packageId) => _packagesBeingInstalled.Contains(packageId);

    /// <summary>
    /// Checks if a specific package is currently being updated.
    /// </summary>
    /// <param name="packageId">The package identifier.</param>
    /// <returns>True if the package is being updated.</returns>
    public bool IsPackageBeingUpdated(string packageId) => _packagesBeingUpdated.Contains(packageId);

    /// <summary>
    /// Checks if a specific package is currently being uninstalled.
    /// </summary>
    /// <param name="packageId">The package identifier.</param>
    /// <returns>True if the package is being uninstalled.</returns>
    public bool IsPackageBeingUninstalled(string packageId) => _packagesBeingUninstalled.Contains(packageId);

    /// <summary>
    /// Checks if any operation is in progress for a specific package.
    /// </summary>
    /// <param name="packageId">The package identifier.</param>
    /// <returns>True if any operation is in progress for the package.</returns>
    public bool IsPackageOperationInProgress(string packageId) =>
        _packagesBeingInstalled.Contains(packageId) ||
        _packagesBeingUpdated.Contains(packageId) ||
        _packagesBeingUninstalled.Contains(packageId);

    /// <summary>
    /// Checks if a specific source is currently being tested.
    /// </summary>
    /// <param name="sourceName">The source name.</param>
    /// <returns>True if the source is being tested.</returns>
    public bool IsSourceBeingTested(string sourceName) => _sourcesBeingTested.Contains(sourceName);

    /// <summary>
    /// Loads installed packages from the API.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    public async Task LoadInstalledPackagesAsync(CancellationToken cancellationToken = default)
    {
        SetLoading(true, "Loading installed packages...");
        ClearError();

        try
        {
            _installedPackages = await _apiClient.GetInstalledPackagesAsync(cancellationToken);
            NotifyStateChanged();
        }
        catch (HttpRequestException ex)
        {
            SetError($"Failed to load installed packages: {ex.Message}");
        }
        catch (OperationCanceledException)
        {
            // Cancelled, no error
        }
        finally
        {
            SetLoading(false);
        }
    }

    /// <summary>
    /// Searches for packages matching the query with debouncing.
    /// </summary>
    /// <param name="query">Search query string.</param>
    /// <param name="debounceMs">Debounce delay in milliseconds (default 300ms).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public void SearchPackagesDebounced(string query, int debounceMs = 300,
        CancellationToken cancellationToken = default)
    {
        // Cancel any pending search
        _searchDebounceTimer?.Dispose();

        if (string.IsNullOrWhiteSpace(query))
        {
            _searchResults = null;
            NotifyStateChanged();
            return;
        }

        // Set up debounce timer - fire and forget with error handling
        _searchDebounceTimer = new Timer(
            _ => ExecuteSearchWithErrorHandlingAsync(query, cancellationToken),
            null,
            debounceMs,
            Timeout.Infinite);
    }

    /// <summary>
    /// Executes search with error handling for timer callback.
    /// </summary>
    private async void ExecuteSearchWithErrorHandlingAsync(string query, CancellationToken cancellationToken)
    {
        try
        {
            await SearchPackagesAsync(query, 0, 20, cancellationToken);
        }
        catch (Exception)
        {
            // Errors are handled within SearchPackagesAsync
        }
    }

    /// <summary>
    /// Searches for packages matching the query.
    /// </summary>
    /// <param name="query">Search query string.</param>
    /// <param name="skip">Number of results to skip for pagination.</param>
    /// <param name="take">Maximum number of results to return.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public async Task SearchPackagesAsync(string query, int skip = 0, int take = 20,
        CancellationToken cancellationToken = default)
    {
        // Cancel any previous search
        await CancelPendingSearchAsync();

        if (string.IsNullOrWhiteSpace(query))
        {
            _searchResults = null;
            NotifyStateChanged();
            return;
        }

        _searchCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var linkedToken = _searchCts.Token;

        _isSearching = true;
        SetLoading(true, "Searching packages...");
        ClearError();

        try
        {
            _searchResults = await _apiClient.SearchPackagesAsync(query, skip, take, false, linkedToken);

            // Mark installed packages in search results
            if (_searchResults is not null)
            {
                var installedIds = _installedPackages.ToDictionary(p => p.PackageId, p => p.Version);
                var updatedPackages = _searchResults.Packages.Select(p =>
                {
                    if (installedIds.TryGetValue(p.PackageId, out var installedVersion))
                    {
                        return p with { IsInstalled = true, InstalledVersion = installedVersion };
                    }

                    return p;
                }).ToList();

                _searchResults = _searchResults with { Packages = updatedPackages };
            }

            NotifyStateChanged();
        }
        catch (HttpRequestException ex)
        {
            SetError($"Search failed: {ex.Message}");
            _searchResults = new PackageSearchResultModel
            {
                Errors = [$"Search failed: {ex.Message}"]
            };
            NotifyStateChanged();
        }
        catch (OperationCanceledException)
        {
            // Cancelled, no error
        }
        finally
        {
            _isSearching = false;
            SetLoading(false);
        }
    }

    /// <summary>
    /// Cancels any pending search operation.
    /// </summary>
    private async Task CancelPendingSearchAsync()
    {
        if (_searchCts is not null)
        {
            await _searchCts.CancelAsync();
            _searchCts.Dispose();
            _searchCts = null;
        }
    }

    /// <summary>
    /// Gets detailed information about a package.
    /// </summary>
    /// <param name="packageId">Package identifier.</param>
    /// <param name="version">Optional specific version.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Package details or null if not found.</returns>
    public async Task<PackageDetailsModel?> GetPackageDetailsAsync(
        string packageId,
        string? version = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(packageId);

        SetLoading(true, "Loading package details...");
        ClearError();

        try
        {
            var details = await _apiClient.GetPackageDetailsAsync(packageId, version, cancellationToken);

            // Enrich with installation status
            if (details is not null)
            {
                var installed = _installedPackages.FirstOrDefault(p => p.PackageId == packageId);
                if (installed is not null)
                {
                    details = details with
                    {
                        IsInstalled = true,
                        InstalledVersion = installed.Version,
                        NodeTypes = installed.NodeTypes
                    };
                }
            }

            return details;
        }
        catch (HttpRequestException ex)
        {
            SetError($"Failed to load package details: {ex.Message}");
            return null;
        }
        catch (OperationCanceledException)
        {
            return null;
        }
        finally
        {
            SetLoading(false);
        }
    }


    /// <summary>
    /// Installs a package.
    /// </summary>
    /// <param name="packageId">Package identifier to install.</param>
    /// <param name="version">Optional specific version to install.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if installation succeeded, false otherwise.</returns>
    public async Task<bool> InstallPackageAsync(
        string packageId,
        string? version = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(packageId);

        // Prevent duplicate requests
        if (_packagesBeingInstalled.Contains(packageId))
        {
            return false;
        }

        _packagesBeingInstalled.Add(packageId);
        SetLoading(true, $"Installing {packageId}...");
        ClearError();
        NotifyStateChanged();

        try
        {
            var result = await _apiClient.InstallPackageAsync(packageId, version, false, cancellationToken);

            if (result.Success && result.Package is not null)
            {
                // Add to installed packages list
                _installedPackages = [.. _installedPackages, result.Package];

                // Refresh node definitions
                await RefreshNodeDefinitionsAsync(cancellationToken);

                SetStatus($"Successfully installed {packageId}");
                NotifyStateChanged();
                return true;
            }

            SetError(result.Errors.FirstOrDefault() ?? "Installation failed");
            return false;
        }
        catch (HttpRequestException ex)
        {
            SetError($"Installation failed: {ex.Message}");
            return false;
        }
        catch (OperationCanceledException)
        {
            return false;
        }
        finally
        {
            _packagesBeingInstalled.Remove(packageId);
            SetLoading(false);
        }
    }

    /// <summary>
    /// Updates a package to the latest or specified version.
    /// </summary>
    /// <param name="packageId">Package identifier to update.</param>
    /// <param name="targetVersion">Optional target version to update to.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if update succeeded, false otherwise.</returns>
    public async Task<bool> UpdatePackageAsync(
        string packageId,
        string? targetVersion = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(packageId);

        // Prevent duplicate requests
        if (_packagesBeingUpdated.Contains(packageId))
        {
            return false;
        }

        _packagesBeingUpdated.Add(packageId);
        SetLoading(true, $"Updating {packageId}...");
        ClearError();
        NotifyStateChanged();

        try
        {
            var result = await _apiClient.UpdatePackageAsync(packageId, targetVersion, cancellationToken);

            if (result.Success && result.Package is not null)
            {
                // Update the package in the installed list
                _installedPackages = _installedPackages
                    .Select(p => p.PackageId == packageId ? result.Package : p)
                    .ToList();

                // Remove from available updates
                _availableUpdates = _availableUpdates
                    .Where(u => u.PackageId != packageId)
                    .ToList();

                // Refresh node definitions
                await RefreshNodeDefinitionsAsync(cancellationToken);

                SetStatus(
                    $"Successfully updated {packageId} from {result.PreviousVersion} to {result.Package.Version}");
                NotifyStateChanged();
                return true;
            }

            SetError(result.Errors.FirstOrDefault() ?? "Update failed");
            return false;
        }
        catch (HttpRequestException ex)
        {
            SetError($"Update failed: {ex.Message}");
            return false;
        }
        catch (OperationCanceledException)
        {
            return false;
        }
        finally
        {
            _packagesBeingUpdated.Remove(packageId);
            SetLoading(false);
        }
    }

    /// <summary>
    /// Uninstalls a package.
    /// </summary>
    /// <param name="packageId">Package identifier to uninstall.</param>
    /// <param name="force">Force uninstall even if workflows reference the package.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Uninstall result with affected workflows information.</returns>
    public async Task<PackageUninstallResultModel> UninstallPackageAsync(
        string packageId,
        bool force = false,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(packageId);

        // Prevent duplicate requests
        if (_packagesBeingUninstalled.Contains(packageId))
        {
            return new PackageUninstallResultModel
            {
                Success = false,
                Errors = ["Uninstall operation already in progress"]
            };
        }

        _packagesBeingUninstalled.Add(packageId);
        SetLoading(true, $"Uninstalling {packageId}...");
        ClearError();
        NotifyStateChanged();

        try
        {
            var result = await _apiClient.UninstallPackageAsync(packageId, force, cancellationToken);

            if (result.Success)
            {
                // Remove from installed packages list
                _installedPackages = _installedPackages
                    .Where(p => p.PackageId != packageId)
                    .ToList();

                // Remove from available updates
                _availableUpdates = _availableUpdates
                    .Where(u => u.PackageId != packageId)
                    .ToList();

                // Refresh node definitions
                await RefreshNodeDefinitionsAsync(cancellationToken);

                SetStatus($"Successfully uninstalled {packageId}");
                NotifyStateChanged();
            }
            else if (result.AffectedWorkflows.Count > 0 && !force)
            {
                // Return result with affected workflows for UI to handle
                SetStatus($"Package {packageId} is used by {result.AffectedWorkflows.Count} workflow(s)");
            }
            else
            {
                SetError(result.Errors.FirstOrDefault() ?? "Uninstall failed");
            }

            return result;
        }
        catch (HttpRequestException ex)
        {
            SetError($"Uninstall failed: {ex.Message}");
            return new PackageUninstallResultModel
            {
                Success = false,
                Errors = [$"Uninstall failed: {ex.Message}"]
            };
        }
        catch (OperationCanceledException)
        {
            return new PackageUninstallResultModel
            {
                Success = false,
                Errors = ["Operation cancelled"]
            };
        }
        finally
        {
            _packagesBeingUninstalled.Remove(packageId);
            SetLoading(false);
        }
    }

    /// <summary>
    /// Checks for available updates to installed packages.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    public async Task CheckForUpdatesAsync(CancellationToken cancellationToken = default)
    {
        // Prevent duplicate requests
        if (_isCheckingForUpdates)
        {
            return;
        }

        _isCheckingForUpdates = true;
        SetLoading(true, "Checking for updates...");
        ClearError();
        NotifyStateChanged();

        try
        {
            _availableUpdates = await _apiClient.CheckForUpdatesAsync(cancellationToken);

            // Update HasUpdate flag on installed packages
            var updateLookup = _availableUpdates.ToDictionary(u => u.PackageId);
            _installedPackages = _installedPackages
                .Select(p =>
                {
                    if (updateLookup.TryGetValue(p.PackageId, out var update))
                    {
                        return p with { HasUpdate = true, LatestVersion = update.LatestVersion };
                    }

                    return p with { HasUpdate = false, LatestVersion = null };
                })
                .ToList();

            var updateCount = _availableUpdates.Count;
            SetStatus(updateCount > 0
                ? $"{updateCount} update(s) available"
                : "All packages are up to date");

            NotifyStateChanged();
        }
        catch (HttpRequestException ex)
        {
            SetError($"Failed to check for updates: {ex.Message}");
        }
        catch (OperationCanceledException)
        {
            // Cancelled, no error
        }
        finally
        {
            _isCheckingForUpdates = false;
            SetLoading(false);
        }
    }

    /// <summary>
    /// <summary>
    /// Refreshes node definitions from the API and updates the workflow state service.
    /// </summary>
    public async Task RefreshNodeDefinitionsAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var definitions = await _apiClient.GetNodeDefinitionsAsync(cancellationToken);
            _workflowStateService.SetNodeDefinitions(definitions);
        }
        catch (HttpRequestException)
        {
            // Log but don't fail the operation
        }
    }


    /// <summary>
    /// Loads package sources from the API.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    public async Task LoadSourcesAsync(CancellationToken cancellationToken = default)
    {
        SetLoading(true, "Loading package sources...");
        ClearError();

        try
        {
            _sources = await _apiClient.GetPackageSourcesAsync(cancellationToken);
            NotifyStateChanged();
        }
        catch (HttpRequestException ex)
        {
            SetError($"Failed to load package sources: {ex.Message}");
        }
        catch (OperationCanceledException)
        {
            // Cancelled, no error
        }
        finally
        {
            SetLoading(false);
        }
    }

    /// <summary>
    /// Adds a new package source.
    /// </summary>
    /// <param name="source">Source configuration.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if the source was added successfully, false otherwise.</returns>
    public async Task<bool> AddSourceAsync(PackageSourceModel source, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(source);

        // Validate source
        if (string.IsNullOrWhiteSpace(source.Name))
        {
            SetError("Source name is required");
            return false;
        }

        if (string.IsNullOrWhiteSpace(source.Url))
        {
            SetError("Source URL is required");
            return false;
        }

        if (!Uri.TryCreate(source.Url, UriKind.Absolute, out var uri) ||
            (uri.Scheme != "http" && uri.Scheme != "https"))
        {
            SetError("Source URL must be a valid HTTP or HTTPS URL");
            return false;
        }

        // Check for duplicate name
        if (_sources.Any(s => s.Name.Equals(source.Name, StringComparison.OrdinalIgnoreCase)))
        {
            SetError($"A source with name '{source.Name}' already exists");
            return false;
        }

        SetLoading(true, $"Adding source {source.Name}...");
        ClearError();

        try
        {
            var result = await _apiClient.AddPackageSourceAsync(source, cancellationToken);

            if (result is not null)
            {
                _sources = [.. _sources, result];
                SetStatus($"Successfully added source {source.Name}");
                NotifyStateChanged();
                return true;
            }

            SetError("Failed to add package source");
            return false;
        }
        catch (HttpRequestException ex)
        {
            SetError($"Failed to add source: {ex.Message}");
            return false;
        }
        catch (OperationCanceledException)
        {
            return false;
        }
        finally
        {
            SetLoading(false);
        }
    }

    /// <summary>
    /// Updates an existing package source.
    /// </summary>
    /// <param name="name">Name of the source to update.</param>
    /// <param name="source">Updated source configuration.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if the source was updated successfully, false otherwise.</returns>
    public async Task<bool> UpdateSourceAsync(
        string name,
        PackageSourceModel source,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(source);

        // Validate source
        if (string.IsNullOrWhiteSpace(source.Url))
        {
            SetError("Source URL is required");
            return false;
        }

        if (!Uri.TryCreate(source.Url, UriKind.Absolute, out var uri) ||
            (uri.Scheme != "http" && uri.Scheme != "https"))
        {
            SetError("Source URL must be a valid HTTP or HTTPS URL");
            return false;
        }

        SetLoading(true, $"Updating source {name}...");
        ClearError();

        try
        {
            var success = await _apiClient.UpdatePackageSourceAsync(name, source, cancellationToken);

            if (success)
            {
                // Update the source in the local list
                _sources = _sources
                    .Select(s => s.Name == name ? source : s)
                    .ToList();

                SetStatus($"Successfully updated source {name}");
                NotifyStateChanged();
                return true;
            }

            SetError("Failed to update package source");
            return false;
        }
        catch (HttpRequestException ex)
        {
            SetError($"Failed to update source: {ex.Message}");
            return false;
        }
        catch (OperationCanceledException)
        {
            return false;
        }
        finally
        {
            SetLoading(false);
        }
    }

    /// <summary>
    /// Removes a package source.
    /// </summary>
    /// <param name="name">Name of the source to remove.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if the source was removed successfully, false otherwise.</returns>
    public async Task<bool> RemoveSourceAsync(string name, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        SetLoading(true, $"Removing source {name}...");
        ClearError();

        try
        {
            var success = await _apiClient.RemovePackageSourceAsync(name, cancellationToken);

            if (success)
            {
                _sources = _sources.Where(s => s.Name != name).ToList();
                SetStatus($"Successfully removed source {name}");
                NotifyStateChanged();
                return true;
            }

            SetError("Failed to remove package source");
            return false;
        }
        catch (HttpRequestException ex)
        {
            SetError($"Failed to remove source: {ex.Message}");
            return false;
        }
        catch (OperationCanceledException)
        {
            return false;
        }
        finally
        {
            SetLoading(false);
        }
    }

    /// <summary>
    /// Tests connectivity to a package source.
    /// </summary>
    /// <param name="name">Name of the source to test.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Test result with success status and response time.</returns>
    public async Task<SourceTestResultModel> TestSourceAsync(string name, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        // Prevent duplicate requests
        if (_sourcesBeingTested.Contains(name))
        {
            return new SourceTestResultModel
            {
                Success = false,
                SourceName = name,
                ResponseTimeMs = 0,
                ErrorMessage = "Test already in progress"
            };
        }

        _sourcesBeingTested.Add(name);
        SetLoading(true, $"Testing source {name}...");
        ClearError();
        NotifyStateChanged();

        try
        {
            var result = await _apiClient.TestPackageSourceAsync(name, cancellationToken);

            if (result.Success)
            {
                SetStatus($"Source {name} is reachable ({result.ResponseTimeMs}ms)");
            }
            else
            {
                SetError($"Source {name} test failed: {result.ErrorMessage}");
            }

            NotifyStateChanged();
            return result;
        }
        catch (HttpRequestException ex)
        {
            var errorResult = new SourceTestResultModel
            {
                Success = false,
                SourceName = name,
                ResponseTimeMs = 0,
                ErrorMessage = ex.Message
            };
            SetError($"Failed to test source: {ex.Message}");
            return errorResult;
        }
        catch (OperationCanceledException)
        {
            return new SourceTestResultModel
            {
                Success = false,
                SourceName = name,
                ResponseTimeMs = 0,
                ErrorMessage = "Operation cancelled"
            };
        }
        finally
        {
            _sourcesBeingTested.Remove(name);
            SetLoading(false);
        }
    }

    #region State Management Helpers

    private void SetLoading(bool isLoading, string? message = null)
    {
        _isLoading = isLoading;
        _statusMessage = message;
        if (!isLoading)
        {
            _statusMessage = null;
        }

        NotifyStateChanged();
    }

    private void SetStatus(string message)
    {
        _statusMessage = message;
        NotifyStateChanged();
    }

    private void SetError(string message)
    {
        _errorMessage = message;
        NotifyStateChanged();
    }

    private void ClearError()
    {
        _errorMessage = null;
    }

    /// <summary>
    /// Clears the current error message.
    /// </summary>
    public void DismissError()
    {
        _errorMessage = null;
        NotifyStateChanged();
    }

    /// <summary>
    /// Clears the current status message.
    /// </summary>
    public void DismissStatus()
    {
        _statusMessage = null;
        NotifyStateChanged();
    }

    private void NotifyStateChanged() => OnStateChanged?.Invoke();

    #endregion

    #region IDisposable

    /// <summary>
    /// Disposes of the service and releases resources.
    /// </summary>
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Disposes of managed resources.
    /// </summary>
    /// <param name="disposing">True if disposing managed resources.</param>
    protected virtual void Dispose(bool disposing)
    {
        if (_disposed) return;

        if (disposing)
        {
            _searchDebounceTimer?.Dispose();
            _searchCts?.Dispose();
        }

        _disposed = true;
    }

    #endregion
}
