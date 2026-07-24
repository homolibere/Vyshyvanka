using Vyshyvanka.Contracts.Packages;
using Vyshyvanka.Core.Interfaces;

namespace Vyshyvanka.Api.Models;

public static class PackageMappings
{
    public static PackageSearchResponse ToResponse(this PackageSearchResult result) => new()
    {
        Packages = result.Packages.Select(p => p.ToResponse()).ToList(),
        TotalCount = result.TotalCount,
        Errors = result.Errors
    };

    public static PackageSearchItemResponse ToResponse(this PackageSearchItem item) => new()
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

    public static PackageDetailsResponse ToResponse(this PackageDetails details) => new()
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

    public static InstalledPackageResponse ToResponse(this InstalledPackage package) => new()
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

    public static PackageInstallResponse ToResponse(this PackageInstallResult result) => new()
    {
        Success = result.Success,
        Package = result.Package is not null ? result.Package.ToResponse() : null,
        InstalledDependencies = result.InstalledDependencies.Select(d => d.ToResponse()).ToList(),
        Errors = result.Errors,
        Warnings = result.Warnings
    };

    public static PackageUpdateResponse ToResponse(this PackageUpdateResult result) => new()
    {
        Success = result.Success,
        Package = result.Package is not null ? result.Package.ToResponse() : null,
        PreviousVersion = result.PreviousVersion?.ToString(),
        Errors = result.Errors,
        Warnings = result.Warnings
    };

    public static PackageUninstallResponse ToResponse(this PackageUninstallResult result) => new()
    {
        Success = result.Success,
        PackageId = result.PackageId,
        RemovedDependencies = result.RemovedDependencies,
        AffectedWorkflows = result.AffectedWorkflows,
        Errors = result.Errors
    };

    public static PackageUpdateInfoResponse ToResponse(this PackageUpdateInfo info) => new()
    {
        PackageId = info.PackageId,
        CurrentVersion = info.CurrentVersion.ToString(),
        LatestVersion = info.LatestVersion.ToString(),
        ReleaseNotes = info.ReleaseNotes
    };

    public static PackageSourceResponse ToResponse(this PackageSource source) => new()
    {
        Name = source.Name,
        Url = source.Url,
        IsEnabled = source.IsEnabled,
        IsTrusted = source.IsTrusted,
        HasCredentials = source.Credentials is not null,
        Priority = source.Priority
    };

    public static SourceTestResponse ToResponse(this SourceTestResult result) => new()
    {
        Success = result.Success,
        SourceName = result.SourceName,
        ResponseTimeMs = result.ResponseTimeMs,
        ErrorMessage = result.ErrorMessage
    };

    public static PackageSourceConfig ToConfig(this PackageSourceRequest request) => new()
    {
        Name = request.Name,
        Url = request.Url,
        IsEnabled = request.IsEnabled,
        IsTrusted = request.IsTrusted,
        Priority = request.Priority,
        Credentials = HasCredentials(request)
            ? new PackageSourceCredentials
            {
                Username = request.Username,
                Password = request.Password,
                ApiKey = request.ApiKey
            }
            : null
    };

    private static bool HasCredentials(PackageSourceRequest request) =>
        !string.IsNullOrWhiteSpace(request.Username) ||
        !string.IsNullOrWhiteSpace(request.Password) ||
        !string.IsNullOrWhiteSpace(request.ApiKey);
}
