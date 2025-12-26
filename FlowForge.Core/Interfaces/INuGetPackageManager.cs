using NuGet.Versioning;

namespace FlowForge.Core.Interfaces;

/// <summary>
/// Primary interface for NuGet package operations including search, install, update, and uninstall.
/// </summary>
public interface INuGetPackageManager
{
    /// <summary>
    /// Searches for packages matching the query across configured sources.
    /// </summary>
    /// <param name="query">Search query string.</param>
    /// <param name="options">Optional search options.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Search results with matching packages.</returns>
    Task<PackageSearchResult> SearchPackagesAsync(
        string query,
        PackageSearchOptions? options = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets detailed information about a specific package.
    /// </summary>
    /// <param name="packageId">The package identifier.</param>
    /// <param name="version">Optional specific version to retrieve.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Package details, or null if not found.</returns>
    Task<PackageDetails?> GetPackageDetailsAsync(
        string packageId,
        NuGetVersion? version = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Installs a package and its dependencies.
    /// </summary>
    /// <param name="packageId">The package identifier to install.</param>
    /// <param name="version">Optional specific version to install.</param>
    /// <param name="prerelease">Whether to allow prerelease versions.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Installation result with status and details.</returns>
    Task<PackageInstallResult> InstallPackageAsync(
        string packageId,
        NuGetVersion? version = null,
        bool prerelease = false,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates an installed package to a newer version.
    /// </summary>
    /// <param name="packageId">The package identifier to update.</param>
    /// <param name="targetVersion">Optional target version, or latest if null.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Update result with status and details.</returns>
    Task<PackageUpdateResult> UpdatePackageAsync(
        string packageId,
        NuGetVersion? targetVersion = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Uninstalls a package and removes orphaned dependencies.
    /// </summary>
    /// <param name="packageId">The package identifier to uninstall.</param>
    /// <param name="force">Force uninstall even if workflows reference the package.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Uninstall result with status and details.</returns>
    Task<PackageUninstallResult> UninstallPackageAsync(
        string packageId,
        bool force = false,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all installed packages.
    /// </summary>
    /// <returns>Read-only list of installed packages.</returns>
    IReadOnlyList<InstalledPackage> GetInstalledPackages();

    /// <summary>
    /// Checks for available updates to installed packages.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>List of available updates.</returns>
    Task<IReadOnlyList<PackageUpdateInfo>> CheckForUpdatesAsync(
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Loads all installed packages from the manifest on startup.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task InitializeAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// Options for package search operations.
/// </summary>
public record PackageSearchOptions
{
    /// <summary>Number of results to skip for pagination.</summary>
    public int Skip { get; init; }

    /// <summary>Maximum number of results to return.</summary>
    public int Take { get; init; } = 20;

    /// <summary>Whether to include prerelease packages.</summary>
    public bool IncludePrerelease { get; init; }

    /// <summary>Filter by specific tags.</summary>
    public IReadOnlyList<string> Tags { get; init; } = [];

    /// <summary>Filter by author.</summary>
    public string? Author { get; init; }
}

/// <summary>
/// Result of a package search operation.
/// </summary>
public record PackageSearchResult
{
    /// <summary>Matching packages.</summary>
    public IReadOnlyList<PackageSearchItem> Packages { get; init; } = [];

    /// <summary>Total count of matching packages across all pages.</summary>
    public int TotalCount { get; init; }

    /// <summary>Errors encountered during search.</summary>
    public IReadOnlyList<string> Errors { get; init; } = [];
}

/// <summary>
/// A package in search results.
/// </summary>
public record PackageSearchItem
{
    /// <summary>Package identifier.</summary>
    public required string PackageId { get; init; }

    /// <summary>Display title.</summary>
    public required string Title { get; init; }

    /// <summary>Latest available version.</summary>
    public required NuGetVersion LatestVersion { get; init; }

    /// <summary>Package description.</summary>
    public string? Description { get; init; }

    /// <summary>Package authors.</summary>
    public string? Authors { get; init; }

    /// <summary>Total download count.</summary>
    public long DownloadCount { get; init; }

    /// <summary>URL to package icon.</summary>
    public string? IconUrl { get; init; }

    /// <summary>URL to project page.</summary>
    public string? ProjectUrl { get; init; }

    /// <summary>Package tags.</summary>
    public IReadOnlyList<string> Tags { get; init; } = [];

    /// <summary>Whether this package is already installed.</summary>
    public bool IsInstalled { get; init; }

    /// <summary>Currently installed version, if any.</summary>
    public NuGetVersion? InstalledVersion { get; init; }
}

/// <summary>
/// Detailed information about a package.
/// </summary>
public record PackageDetails
{
    /// <summary>Package identifier.</summary>
    public required string PackageId { get; init; }

    /// <summary>Package version.</summary>
    public required NuGetVersion Version { get; init; }

    /// <summary>Display title.</summary>
    public string? Title { get; init; }

    /// <summary>Package description.</summary>
    public string? Description { get; init; }

    /// <summary>Package authors.</summary>
    public string? Authors { get; init; }

    /// <summary>License URL or expression.</summary>
    public string? License { get; init; }

    /// <summary>URL to project page.</summary>
    public string? ProjectUrl { get; init; }

    /// <summary>URL to package icon.</summary>
    public string? IconUrl { get; init; }

    /// <summary>Package tags.</summary>
    public IReadOnlyList<string> Tags { get; init; } = [];

    /// <summary>Package dependencies.</summary>
    public IReadOnlyList<string> Dependencies { get; init; } = [];

    /// <summary>All available versions.</summary>
    public IReadOnlyList<NuGetVersion> AllVersions { get; init; } = [];

    /// <summary>Whether this package is already installed.</summary>
    public bool IsInstalled { get; init; }

    /// <summary>Currently installed version, if any.</summary>
    public NuGetVersion? InstalledVersion { get; init; }
}

/// <summary>
/// Information about an installed package.
/// </summary>
public record InstalledPackage
{
    /// <summary>Package identifier.</summary>
    public required string PackageId { get; init; }

    /// <summary>Installed version.</summary>
    public required NuGetVersion Version { get; init; }

    /// <summary>Name of the source the package was installed from.</summary>
    public required string SourceName { get; init; }

    /// <summary>Path where the package is extracted.</summary>
    public required string InstallPath { get; init; }

    /// <summary>When the package was installed.</summary>
    public required DateTime InstalledAt { get; init; }

    /// <summary>Node types provided by this package.</summary>
    public IReadOnlyList<string> NodeTypes { get; init; } = [];

    /// <summary>Package dependencies.</summary>
    public IReadOnlyList<string> Dependencies { get; init; } = [];

    /// <summary>Whether the package is currently loaded.</summary>
    public bool IsLoaded { get; init; }
}

/// <summary>
/// Result of a package installation.
/// </summary>
public record PackageInstallResult
{
    /// <summary>Whether the installation succeeded.</summary>
    public bool Success { get; init; }

    /// <summary>The installed package, if successful.</summary>
    public InstalledPackage? Package { get; init; }

    /// <summary>Dependencies that were installed.</summary>
    public IReadOnlyList<InstalledPackage> InstalledDependencies { get; init; } = [];

    /// <summary>Errors encountered during installation.</summary>
    public IReadOnlyList<string> Errors { get; init; } = [];

    /// <summary>Warnings encountered during installation.</summary>
    public IReadOnlyList<string> Warnings { get; init; } = [];
}

/// <summary>
/// Result of a package update.
/// </summary>
public record PackageUpdateResult
{
    /// <summary>Whether the update succeeded.</summary>
    public bool Success { get; init; }

    /// <summary>The updated package, if successful.</summary>
    public InstalledPackage? Package { get; init; }

    /// <summary>Previous version before update.</summary>
    public NuGetVersion? PreviousVersion { get; init; }

    /// <summary>Errors encountered during update.</summary>
    public IReadOnlyList<string> Errors { get; init; } = [];

    /// <summary>Warnings encountered during update.</summary>
    public IReadOnlyList<string> Warnings { get; init; } = [];
}

/// <summary>
/// Result of a package uninstallation.
/// </summary>
public record PackageUninstallResult
{
    /// <summary>Whether the uninstallation succeeded.</summary>
    public bool Success { get; init; }

    /// <summary>Package identifier that was uninstalled.</summary>
    public string? PackageId { get; init; }

    /// <summary>Dependencies that were removed.</summary>
    public IReadOnlyList<string> RemovedDependencies { get; init; } = [];

    /// <summary>Workflows that reference this package.</summary>
    public IReadOnlyList<string> AffectedWorkflows { get; init; } = [];

    /// <summary>Errors encountered during uninstallation.</summary>
    public IReadOnlyList<string> Errors { get; init; } = [];
}

/// <summary>
/// Information about an available package update.
/// </summary>
public record PackageUpdateInfo
{
    /// <summary>Package identifier.</summary>
    public required string PackageId { get; init; }

    /// <summary>Currently installed version.</summary>
    public required NuGetVersion CurrentVersion { get; init; }

    /// <summary>Latest available version.</summary>
    public required NuGetVersion LatestVersion { get; init; }

    /// <summary>Release notes or changelog.</summary>
    public string? ReleaseNotes { get; init; }
}
