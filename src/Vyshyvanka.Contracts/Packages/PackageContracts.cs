namespace Vyshyvanka.Contracts.Packages;

/// <summary>
/// Result of a package search query against the configured NuGet sources.
/// </summary>
public record PackageSearchResponse
{
    /// <summary>The packages matching the search on the current page.</summary>
    public IReadOnlyList<PackageSearchItemResponse> Packages { get; init; } = [];

    /// <summary>Total number of matching packages across all pages.</summary>
    public int TotalCount { get; init; }

    /// <summary>Non-fatal errors encountered while querying individual sources. Empty when all sources responded.</summary>
    public IReadOnlyList<string> Errors { get; init; } = [];
}

/// <summary>
/// A single package entry within search results.
/// </summary>
public record PackageSearchItemResponse
{
    /// <summary>Unique NuGet package identifier.</summary>
    public required string PackageId { get; init; }

    /// <summary>Display title of the package.</summary>
    public required string Title { get; init; }

    /// <summary>The most recent published version.</summary>
    public required string LatestVersion { get; init; }

    /// <summary>Package description. <c>null</c> when the source provides none.</summary>
    public string? Description { get; init; }

    /// <summary>Package authors. <c>null</c> when unspecified.</summary>
    public string? Authors { get; init; }

    /// <summary>Total download count reported by the source.</summary>
    public long DownloadCount { get; init; }

    /// <summary>URL of the package icon. <c>null</c> when none is provided.</summary>
    public string? IconUrl { get; init; }

    /// <summary>URL of the package project or homepage. <c>null</c> when none is provided.</summary>
    public string? ProjectUrl { get; init; }

    /// <summary>Tags associated with the package.</summary>
    public IReadOnlyList<string> Tags { get; init; } = [];

    /// <summary>Whether this package is already installed in the current instance.</summary>
    public bool IsInstalled { get; init; }

    /// <summary>The installed version when <see cref="IsInstalled"/> is <c>true</c>; otherwise <c>null</c>.</summary>
    public string? InstalledVersion { get; init; }
}

/// <summary>
/// Detailed metadata for a specific package, including versions and dependencies.
/// </summary>
public record PackageDetailsResponse
{
    /// <summary>Unique NuGet package identifier.</summary>
    public required string PackageId { get; init; }

    /// <summary>The specific version these details describe.</summary>
    public required string Version { get; init; }

    /// <summary>Display title. <c>null</c> when unspecified.</summary>
    public string? Title { get; init; }

    /// <summary>Package description. <c>null</c> when unspecified.</summary>
    public string? Description { get; init; }

    /// <summary>Package authors. <c>null</c> when unspecified.</summary>
    public string? Authors { get; init; }

    /// <summary>License identifier or text. <c>null</c> when unspecified.</summary>
    public string? License { get; init; }

    /// <summary>URL of the package project or homepage. <c>null</c> when none is provided.</summary>
    public string? ProjectUrl { get; init; }

    /// <summary>URL of the package icon. <c>null</c> when none is provided.</summary>
    public string? IconUrl { get; init; }

    /// <summary>Tags associated with the package.</summary>
    public IReadOnlyList<string> Tags { get; init; } = [];

    /// <summary>Package dependency identifiers.</summary>
    public IReadOnlyList<string> Dependencies { get; init; } = [];

    /// <summary>All published versions of the package.</summary>
    public IReadOnlyList<string> AllVersions { get; init; } = [];

    /// <summary>Whether the package is already installed in the current instance.</summary>
    public bool IsInstalled { get; init; }

    /// <summary>The installed version when <see cref="IsInstalled"/> is <c>true</c>; otherwise <c>null</c>.</summary>
    public string? InstalledVersion { get; init; }
}

/// <summary>
/// Represents a package currently installed in the instance.
/// </summary>
public record InstalledPackageResponse
{
    /// <summary>Unique NuGet package identifier.</summary>
    public required string PackageId { get; init; }

    /// <summary>The installed version.</summary>
    public required string Version { get; init; }

    /// <summary>Name of the source the package was installed from.</summary>
    public required string SourceName { get; init; }

    /// <summary>Filesystem path where the package assemblies are stored.</summary>
    public required string InstallPath { get; init; }

    /// <summary>UTC timestamp when the package was installed.</summary>
    public required DateTime InstalledAt { get; init; }

    /// <summary>Node type keys contributed by this package that become available in the designer.</summary>
    public IReadOnlyList<string> NodeTypes { get; init; } = [];

    /// <summary>Dependency package identifiers this package requires.</summary>
    public IReadOnlyList<string> Dependencies { get; init; } = [];

    /// <summary>Whether the package's assemblies are currently loaded into the engine.</summary>
    public bool IsLoaded { get; init; }
}

/// <summary>
/// Outcome of a package installation operation.
/// </summary>
public record PackageInstallResponse
{
    /// <summary>Whether the installation succeeded.</summary>
    public bool Success { get; init; }

    /// <summary>The installed package when successful; otherwise <c>null</c>.</summary>
    public InstalledPackageResponse? Package { get; init; }

    /// <summary>Dependency packages that were installed alongside the requested package.</summary>
    public IReadOnlyList<InstalledPackageResponse> InstalledDependencies { get; init; } = [];

    /// <summary>Fatal errors that caused the installation to fail. Empty on success.</summary>
    public IReadOnlyList<string> Errors { get; init; } = [];

    /// <summary>Non-fatal warnings raised during installation.</summary>
    public IReadOnlyList<string> Warnings { get; init; } = [];
}

/// <summary>
/// Outcome of a package update operation.
/// </summary>
public record PackageUpdateResponse
{
    /// <summary>Whether the update succeeded.</summary>
    public bool Success { get; init; }

    /// <summary>The package at its new version when successful; otherwise <c>null</c>.</summary>
    public InstalledPackageResponse? Package { get; init; }

    /// <summary>The version that was installed before the update. <c>null</c> when unknown.</summary>
    public string? PreviousVersion { get; init; }

    /// <summary>Fatal errors that caused the update to fail. Empty on success.</summary>
    public IReadOnlyList<string> Errors { get; init; } = [];

    /// <summary>Non-fatal warnings raised during the update.</summary>
    public IReadOnlyList<string> Warnings { get; init; } = [];
}

/// <summary>
/// Outcome of a package uninstall operation.
/// </summary>
public record PackageUninstallResponse
{
    /// <summary>Whether the uninstall succeeded.</summary>
    public bool Success { get; init; }

    /// <summary>Identifier of the package that was uninstalled. <c>null</c> when the operation failed early.</summary>
    public string? PackageId { get; init; }

    /// <summary>Dependency packages that were removed because nothing else required them.</summary>
    public IReadOnlyList<string> RemovedDependencies { get; init; } = [];

    /// <summary>Workflows that reference node types from the removed package and may now be broken.</summary>
    public IReadOnlyList<string> AffectedWorkflows { get; init; } = [];

    /// <summary>Fatal errors that caused the uninstall to fail. Empty on success.</summary>
    public IReadOnlyList<string> Errors { get; init; } = [];
}

/// <summary>
/// Information about an available update for an installed package.
/// </summary>
public record PackageUpdateInfoResponse
{
    /// <summary>Unique NuGet package identifier.</summary>
    public required string PackageId { get; init; }

    /// <summary>The version currently installed.</summary>
    public required string CurrentVersion { get; init; }

    /// <summary>The latest available version.</summary>
    public required string LatestVersion { get; init; }

    /// <summary>Release notes for the latest version. <c>null</c> when none are published.</summary>
    public string? ReleaseNotes { get; init; }
}

/// <summary>
/// Request payload to install a package.
/// </summary>
public record InstallPackageRequest
{
    /// <summary>Specific version to install. <c>null</c> installs the latest stable version.</summary>
    public string? Version { get; init; }

    /// <summary>Whether prerelease versions are eligible for installation.</summary>
    public bool Prerelease { get; init; }
}

/// <summary>
/// Request payload to update an installed package.
/// </summary>
public record UpdatePackageRequest
{
    /// <summary>Version to update to. <c>null</c> updates to the latest available version.</summary>
    public string? TargetVersion { get; init; }
}

/// <summary>
/// Represents a configured NuGet source used for package discovery and installation.
/// </summary>
public record PackageSourceResponse
{
    /// <summary>Unique name identifying the source.</summary>
    public required string Name { get; init; }

    /// <summary>Base URL of the source feed.</summary>
    public required string Url { get; init; }

    /// <summary>Whether the source is enabled for searches and installs.</summary>
    public bool IsEnabled { get; init; }

    /// <summary>Whether packages from this source are trusted for automatic loading.</summary>
    public bool IsTrusted { get; init; }

    /// <summary>Whether authentication credentials are configured for the source. The credentials themselves are never returned.</summary>
    public bool HasCredentials { get; init; }

    /// <summary>Ordering priority when multiple sources are queried; lower values are consulted first.</summary>
    public int Priority { get; init; }
}

/// <summary>
/// Request payload to add or update a NuGet source.
/// </summary>
public record PackageSourceRequest
{
    /// <summary>Unique name identifying the source.</summary>
    public required string Name { get; init; }

    /// <summary>Base URL of the source feed.</summary>
    public required string Url { get; init; }

    /// <summary>Whether the source is enabled. Defaults to <c>true</c>.</summary>
    public bool IsEnabled { get; init; } = true;

    /// <summary>Whether packages from this source are trusted for automatic loading.</summary>
    public bool IsTrusted { get; init; }

    /// <summary>Optional username for basic-auth feeds. <c>null</c> when not required.</summary>
    public string? Username { get; init; }

    /// <summary>Optional password for basic-auth feeds. <c>null</c> when not required.</summary>
    public string? Password { get; init; }

    /// <summary>Optional API key for feeds that authenticate by key. <c>null</c> when not required.</summary>
    public string? ApiKey { get; init; }

    /// <summary>Ordering priority when multiple sources are queried; lower values are consulted first.</summary>
    public int Priority { get; init; }
}

/// <summary>
/// Result of testing connectivity to a package source.
/// </summary>
public record SourceTestResponse
{
    /// <summary>Whether the source responded successfully.</summary>
    public bool Success { get; init; }

    /// <summary>Name of the source that was tested.</summary>
    public required string SourceName { get; init; }

    /// <summary>Round-trip response time in milliseconds.</summary>
    public long ResponseTimeMs { get; init; }

    /// <summary>Error message when the test failed. <c>null</c> on success.</summary>
    public string? ErrorMessage { get; init; }
}
