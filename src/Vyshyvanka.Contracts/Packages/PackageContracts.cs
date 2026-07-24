namespace Vyshyvanka.Contracts.Packages;

/// <summary>
/// Response for package search results.
/// </summary>
public record PackageSearchResponse
{
    public IReadOnlyList<PackageSearchItemResponse> Packages { get; init; } = [];
    public int TotalCount { get; init; }
    public IReadOnlyList<string> Errors { get; init; } = [];
}

/// <summary>
/// A package in search results.
/// </summary>
public record PackageSearchItemResponse
{
    public required string PackageId { get; init; }
    public required string Title { get; init; }
    public required string LatestVersion { get; init; }
    public string? Description { get; init; }
    public string? Authors { get; init; }
    public long DownloadCount { get; init; }
    public string? IconUrl { get; init; }
    public string? ProjectUrl { get; init; }
    public IReadOnlyList<string> Tags { get; init; } = [];
    public bool IsInstalled { get; init; }
    public string? InstalledVersion { get; init; }
}

/// <summary>
/// Detailed package information response.
/// </summary>
public record PackageDetailsResponse
{
    public required string PackageId { get; init; }
    public required string Version { get; init; }
    public string? Title { get; init; }
    public string? Description { get; init; }
    public string? Authors { get; init; }
    public string? License { get; init; }
    public string? ProjectUrl { get; init; }
    public string? IconUrl { get; init; }
    public IReadOnlyList<string> Tags { get; init; } = [];
    public IReadOnlyList<string> Dependencies { get; init; } = [];
    public IReadOnlyList<string> AllVersions { get; init; } = [];
    public bool IsInstalled { get; init; }
    public string? InstalledVersion { get; init; }
}

/// <summary>
/// Installed package response.
/// </summary>
public record InstalledPackageResponse
{
    public required string PackageId { get; init; }
    public required string Version { get; init; }
    public required string SourceName { get; init; }
    public required string InstallPath { get; init; }
    public required DateTime InstalledAt { get; init; }
    public IReadOnlyList<string> NodeTypes { get; init; } = [];
    public IReadOnlyList<string> Dependencies { get; init; } = [];
    public bool IsLoaded { get; init; }
}

/// <summary>
/// Package installation result response.
/// </summary>
public record PackageInstallResponse
{
    public bool Success { get; init; }
    public InstalledPackageResponse? Package { get; init; }
    public IReadOnlyList<InstalledPackageResponse> InstalledDependencies { get; init; } = [];
    public IReadOnlyList<string> Errors { get; init; } = [];
    public IReadOnlyList<string> Warnings { get; init; } = [];
}

/// <summary>
/// Package update result response.
/// </summary>
public record PackageUpdateResponse
{
    public bool Success { get; init; }
    public InstalledPackageResponse? Package { get; init; }
    public string? PreviousVersion { get; init; }
    public IReadOnlyList<string> Errors { get; init; } = [];
    public IReadOnlyList<string> Warnings { get; init; } = [];
}

/// <summary>
/// Package uninstall result response.
/// </summary>
public record PackageUninstallResponse
{
    public bool Success { get; init; }
    public string? PackageId { get; init; }
    public IReadOnlyList<string> RemovedDependencies { get; init; } = [];
    public IReadOnlyList<string> AffectedWorkflows { get; init; } = [];
    public IReadOnlyList<string> Errors { get; init; } = [];
}

/// <summary>
/// Package update info response.
/// </summary>
public record PackageUpdateInfoResponse
{
    public required string PackageId { get; init; }
    public required string CurrentVersion { get; init; }
    public required string LatestVersion { get; init; }
    public string? ReleaseNotes { get; init; }
}

/// <summary>
/// Request to install a package.
/// </summary>
public record InstallPackageRequest
{
    public string? Version { get; init; }
    public bool Prerelease { get; init; }
}

/// <summary>
/// Request to update a package.
/// </summary>
public record UpdatePackageRequest
{
    public string? TargetVersion { get; init; }
}

/// <summary>
/// Package source response.
/// </summary>
public record PackageSourceResponse
{
    public required string Name { get; init; }
    public required string Url { get; init; }
    public bool IsEnabled { get; init; }
    public bool IsTrusted { get; init; }
    public bool HasCredentials { get; init; }
    public int Priority { get; init; }
}

/// <summary>
/// Request to add or update a package source.
/// </summary>
public record PackageSourceRequest
{
    public required string Name { get; init; }
    public required string Url { get; init; }
    public bool IsEnabled { get; init; } = true;
    public bool IsTrusted { get; init; }
    public string? Username { get; init; }
    public string? Password { get; init; }
    public string? ApiKey { get; init; }
    public int Priority { get; init; }
}

/// <summary>
/// Source connectivity test result response.
/// </summary>
public record SourceTestResponse
{
    public bool Success { get; init; }
    public required string SourceName { get; init; }
    public long ResponseTimeMs { get; init; }
    public string? ErrorMessage { get; init; }
}
