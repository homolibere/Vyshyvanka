namespace Vyshyvanka.Designer.Models;

/// <summary>
/// Model for an installed package in the Designer.
/// </summary>
public record InstalledPackageModel
{
    /// <summary>The NuGet package identifier.</summary>
    public required string PackageId { get; init; }

    /// <summary>The installed version.</summary>
    public required string Version { get; init; }

    /// <summary>The source from which the package was installed.</summary>
    public required string SourceName { get; init; }

    /// <summary>When the package was installed.</summary>
    public required DateTime InstalledAt { get; init; }

    /// <summary>Node types provided by this package.</summary>
    public IReadOnlyList<string> NodeTypes { get; init; } = [];

    /// <summary>Whether the package is currently loaded.</summary>
    public bool IsLoaded { get; init; }

    /// <summary>Whether an update is available.</summary>
    public bool HasUpdate { get; init; }

    /// <summary>The latest available version, if an update exists.</summary>
    public string? LatestVersion { get; init; }
}

/// <summary>
/// Model for a package in search results.
/// </summary>
public record PackageSearchItemModel
{
    /// <summary>The NuGet package identifier.</summary>
    public required string PackageId { get; init; }

    /// <summary>The display title of the package.</summary>
    public required string Title { get; init; }

    /// <summary>The latest available version.</summary>
    public required string LatestVersion { get; init; }

    /// <summary>Package description.</summary>
    public string? Description { get; init; }

    /// <summary>Package authors.</summary>
    public string? Authors { get; init; }

    /// <summary>Total download count.</summary>
    public long DownloadCount { get; init; }

    /// <summary>URL to the package icon.</summary>
    public string? IconUrl { get; init; }

    /// <summary>Tags associated with the package.</summary>
    public IReadOnlyList<string> Tags { get; init; } = [];

    /// <summary>Whether this package is already installed.</summary>
    public bool IsInstalled { get; init; }

    /// <summary>The installed version, if the package is installed.</summary>
    public string? InstalledVersion { get; init; }
}

/// <summary>
/// Model for package search results.
/// </summary>
public record PackageSearchResultModel
{
    /// <summary>The list of packages matching the search.</summary>
    public IReadOnlyList<PackageSearchItemModel> Packages { get; init; } = [];

    /// <summary>Total count of matching packages.</summary>
    public int TotalCount { get; init; }

    /// <summary>Any errors that occurred during the search.</summary>
    public IReadOnlyList<string> Errors { get; init; } = [];
}

/// <summary>
/// Model for detailed package information.
/// </summary>
public record PackageDetailsModel
{
    /// <summary>The NuGet package identifier.</summary>
    public required string PackageId { get; init; }

    /// <summary>The package version.</summary>
    public required string Version { get; init; }

    /// <summary>The display title of the package.</summary>
    public string? Title { get; init; }

    /// <summary>Package description.</summary>
    public string? Description { get; init; }

    /// <summary>Package authors.</summary>
    public string? Authors { get; init; }

    /// <summary>License information.</summary>
    public string? License { get; init; }

    /// <summary>URL to the project page.</summary>
    public string? ProjectUrl { get; init; }

    /// <summary>URL to the package icon.</summary>
    public string? IconUrl { get; init; }

    /// <summary>Tags associated with the package.</summary>
    public IReadOnlyList<string> Tags { get; init; } = [];

    /// <summary>Package dependencies.</summary>
    public IReadOnlyList<string> Dependencies { get; init; } = [];

    /// <summary>All available versions of the package.</summary>
    public IReadOnlyList<string> AllVersions { get; init; } = [];

    /// <summary>Node types provided by this package (if installed).</summary>
    public IReadOnlyList<string> NodeTypes { get; init; } = [];

    /// <summary>Whether this package is installed.</summary>
    public bool IsInstalled { get; init; }

    /// <summary>The installed version, if the package is installed.</summary>
    public string? InstalledVersion { get; init; }
}

/// <summary>
/// Model for package installation result.
/// </summary>
public record PackageInstallResultModel
{
    /// <summary>Whether the installation succeeded.</summary>
    public bool Success { get; init; }

    /// <summary>The installed package details, if successful.</summary>
    public InstalledPackageModel? Package { get; init; }

    /// <summary>Any errors that occurred during installation.</summary>
    public IReadOnlyList<string> Errors { get; init; } = [];

    /// <summary>Any warnings from the installation.</summary>
    public IReadOnlyList<string> Warnings { get; init; } = [];
}

/// <summary>
/// Model for package update result.
/// </summary>
public record PackageUpdateResultModel
{
    /// <summary>Whether the update succeeded.</summary>
    public bool Success { get; init; }

    /// <summary>The updated package details, if successful.</summary>
    public InstalledPackageModel? Package { get; init; }

    /// <summary>The version before the update.</summary>
    public string? PreviousVersion { get; init; }

    /// <summary>Any errors that occurred during the update.</summary>
    public IReadOnlyList<string> Errors { get; init; } = [];
}

/// <summary>
/// Model for package uninstall result.
/// </summary>
public record PackageUninstallResultModel
{
    /// <summary>Whether the uninstallation succeeded.</summary>
    public bool Success { get; init; }

    /// <summary>Workflows that reference nodes from the uninstalled package.</summary>
    public IReadOnlyList<string> AffectedWorkflows { get; init; } = [];

    /// <summary>Any errors that occurred during uninstallation.</summary>
    public IReadOnlyList<string> Errors { get; init; } = [];
}

/// <summary>
/// Model for package update information.
/// </summary>
public record PackageUpdateInfoModel
{
    /// <summary>The NuGet package identifier.</summary>
    public required string PackageId { get; init; }

    /// <summary>The currently installed version.</summary>
    public required string CurrentVersion { get; init; }

    /// <summary>The latest available version.</summary>
    public required string LatestVersion { get; init; }

    /// <summary>Release notes for the update.</summary>
    public string? ReleaseNotes { get; init; }
}

/// <summary>
/// Model for a package source.
/// </summary>
public record PackageSourceModel
{
    /// <summary>The source name.</summary>
    public required string Name { get; init; }

    /// <summary>The source URL.</summary>
    public required string Url { get; init; }

    /// <summary>Whether the source is enabled.</summary>
    public bool IsEnabled { get; init; } = true;

    /// <summary>Whether the source is trusted.</summary>
    public bool IsTrusted { get; init; }

    /// <summary>Whether the source has credentials configured.</summary>
    public bool HasCredentials { get; init; }

    /// <summary>Priority order for the source.</summary>
    public int Priority { get; init; }

    /// <summary>Username for authentication (optional).</summary>
    public string? Username { get; init; }

    /// <summary>Password for authentication (optional, write-only for security).</summary>
    public string? Password { get; init; }

    /// <summary>API key for authentication (optional, write-only for security).</summary>
    public string? ApiKey { get; init; }
}

/// <summary>
/// Model for source test result.
/// </summary>
public record SourceTestResultModel
{
    /// <summary>Whether the connection test succeeded.</summary>
    public bool Success { get; init; }

    /// <summary>The name of the source that was tested.</summary>
    public required string SourceName { get; init; }

    /// <summary>Response time in milliseconds.</summary>
    public long ResponseTimeMs { get; init; }

    /// <summary>Error message if the test failed.</summary>
    public string? ErrorMessage { get; init; }
}

/// <summary>
/// Variants for the confirmation dialog appearance.
/// </summary>
public enum ConfirmDialogVariant
{
    /// <summary>Default appearance with primary confirm button.</summary>
    Default,

    /// <summary>Warning appearance with yellow/orange styling.</summary>
    Warning,

    /// <summary>Danger appearance with red styling for destructive actions.</summary>
    Danger
}

/// <summary>
/// Types of toast notifications.
/// </summary>
public enum ToastType
{
    /// <summary>Success notification (green).</summary>
    Success,

    /// <summary>Error notification (red).</summary>
    Error,

    /// <summary>Warning notification (yellow/orange).</summary>
    Warning,

    /// <summary>Informational notification (blue).</summary>
    Info
}

/// <summary>
/// Model for a toast notification.
/// </summary>
public record ToastModel
{
    /// <summary>Unique identifier for the toast.</summary>
    public required string Id { get; init; }

    /// <summary>The toast type.</summary>
    public ToastType Type { get; init; } = ToastType.Info;

    /// <summary>Optional title for the toast.</summary>
    public string? Title { get; init; }

    /// <summary>The toast message.</summary>
    public required string Message { get; init; }

    /// <summary>Auto-dismiss timeout in milliseconds.</summary>
    public int DismissTimeout { get; init; } = 5000;
}
