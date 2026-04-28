using System.ComponentModel.DataAnnotations;
using Vyshyvanka.Core.Interfaces;

namespace Vyshyvanka.Api.Models;

/// <summary>
/// Response for package search results.
/// </summary>
public record PackageSearchResponse
{
    /// <summary>Matching packages.</summary>
    public IReadOnlyList<PackageSearchItemResponse> Packages { get; init; } = [];

    /// <summary>Total count of matching packages.</summary>
    public int TotalCount { get; init; }

    /// <summary>Errors encountered during search.</summary>
    public IReadOnlyList<string> Errors { get; init; } = [];

    public static PackageSearchResponse FromResult(PackageSearchResult result) => new()
    {
        Packages = result.Packages.Select(PackageSearchItemResponse.FromItem).ToList(),
        TotalCount = result.TotalCount,
        Errors = result.Errors
    };
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

    public static PackageSearchItemResponse FromItem(PackageSearchItem item) => new()
    {
        PackageId = item.PackageId,
        Title = item.Title,
        LatestVersion = item.LatestVersion.ToString(),
        Description = item.Description,
        Authors = item.Authors,
        DownloadCount = item.DownloadCount,
        IconUrl = item.IconUrl,
        ProjectUrl = item.ProjectUrl,
        Tags = item.Tags,
        IsInstalled = item.IsInstalled,
        InstalledVersion = item.InstalledVersion?.ToString()
    };
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

    public static PackageDetailsResponse FromDetails(PackageDetails details) => new()
    {
        PackageId = details.PackageId,
        Version = details.Version.ToString(),
        Title = details.Title,
        Description = details.Description,
        Authors = details.Authors,
        License = details.License,
        ProjectUrl = details.ProjectUrl,
        IconUrl = details.IconUrl,
        Tags = details.Tags,
        Dependencies = details.Dependencies,
        AllVersions = details.AllVersions.Select(v => v.ToString()).ToList(),
        IsInstalled = details.IsInstalled,
        InstalledVersion = details.InstalledVersion?.ToString()
    };
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

    public static InstalledPackageResponse FromPackage(InstalledPackage package) => new()
    {
        PackageId = package.PackageId,
        Version = package.Version.ToString(),
        SourceName = package.SourceName,
        InstallPath = package.InstallPath,
        InstalledAt = package.InstalledAt,
        NodeTypes = package.NodeTypes,
        Dependencies = package.Dependencies,
        IsLoaded = package.IsLoaded
    };
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

    public static PackageInstallResponse FromResult(PackageInstallResult result) => new()
    {
        Success = result.Success,
        Package = result.Package is not null ? InstalledPackageResponse.FromPackage(result.Package) : null,
        InstalledDependencies = result.InstalledDependencies.Select(InstalledPackageResponse.FromPackage).ToList(),
        Errors = result.Errors,
        Warnings = result.Warnings
    };
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

    public static PackageUpdateResponse FromResult(PackageUpdateResult result) => new()
    {
        Success = result.Success,
        Package = result.Package is not null ? InstalledPackageResponse.FromPackage(result.Package) : null,
        PreviousVersion = result.PreviousVersion?.ToString(),
        Errors = result.Errors,
        Warnings = result.Warnings
    };
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

    public static PackageUninstallResponse FromResult(PackageUninstallResult result) => new()
    {
        Success = result.Success,
        PackageId = result.PackageId,
        RemovedDependencies = result.RemovedDependencies,
        AffectedWorkflows = result.AffectedWorkflows,
        Errors = result.Errors
    };
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

    public static PackageUpdateInfoResponse FromInfo(PackageUpdateInfo info) => new()
    {
        PackageId = info.PackageId,
        CurrentVersion = info.CurrentVersion.ToString(),
        LatestVersion = info.LatestVersion.ToString(),
        ReleaseNotes = info.ReleaseNotes
    };
}

/// <summary>
/// Request to install a package.
/// </summary>
public record InstallPackageRequest
{
    /// <summary>Specific version to install (optional, latest if not specified).</summary>
    public string? Version { get; init; }

    /// <summary>Whether to allow prerelease versions.</summary>
    public bool Prerelease { get; init; }
}

/// <summary>
/// Request to update a package.
/// </summary>
public record UpdatePackageRequest
{
    /// <summary>Target version to update to (optional, latest if not specified).</summary>
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

    public static PackageSourceResponse FromSource(PackageSource source) => new()
    {
        Name = source.Name,
        Url = source.Url,
        IsEnabled = source.IsEnabled,
        IsTrusted = source.IsTrusted,
        HasCredentials = source.Credentials is not null,
        Priority = source.Priority
    };
}

/// <summary>
/// Request to add or update a package source.
/// </summary>
public record PackageSourceRequest
{
    /// <summary>Unique name for this source.</summary>
    [Required]
    [StringLength(100, MinimumLength = 1)]
    public required string Name { get; init; }

    /// <summary>URL of the NuGet feed.</summary>
    [Required]
    [Url]
    public required string Url { get; init; }

    /// <summary>Whether this source is enabled.</summary>
    public bool IsEnabled { get; init; } = true;

    /// <summary>Whether this source is trusted.</summary>
    public bool IsTrusted { get; init; }

    /// <summary>Username for authenticated sources.</summary>
    public string? Username { get; init; }

    /// <summary>Password for authenticated sources.</summary>
    public string? Password { get; init; }

    /// <summary>API key for authenticated sources.</summary>
    public string? ApiKey { get; init; }

    /// <summary>Priority for source ordering.</summary>
    public int Priority { get; init; }

    public PackageSourceConfig ToConfig() => new()
    {
        Name = Name,
        Url = Url,
        IsEnabled = IsEnabled,
        IsTrusted = IsTrusted,
        Priority = Priority,
        Credentials = HasCredentials()
            ? new PackageSourceCredentials
            {
                Username = Username,
                Password = Password,
                ApiKey = ApiKey
            }
            : null
    };

    private bool HasCredentials() =>
        !string.IsNullOrWhiteSpace(Username) ||
        !string.IsNullOrWhiteSpace(Password) ||
        !string.IsNullOrWhiteSpace(ApiKey);
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

    public static SourceTestResponse FromResult(SourceTestResult result) => new()
    {
        Success = result.Success,
        SourceName = result.SourceName,
        ResponseTimeMs = result.ResponseTimeMs,
        ErrorMessage = result.ErrorMessage
    };
}
