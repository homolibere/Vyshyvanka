using Vyshyvanka.Core.Interfaces;
using Microsoft.Extensions.Logging;
using NuGet.Common;
using NuGet.Protocol.Core.Types;
using NuGet.Versioning;

namespace Vyshyvanka.Engine.Packages;

/// <summary>
/// Provides package search and discovery operations against configured NuGet sources.
/// </summary>
public class PackageSearchService(
    IPackageSourceService sourceService,
    ILogger<PackageSearchService>? logger = null) : IPackageSearchService
{
    public async Task<PackageSearchResult> SearchPackagesAsync(
        string query,
        PackageSearchOptions? options = null,
        IReadOnlyDictionary<string, InstalledPackage>? installedPackages = null,
        CancellationToken cancellationToken = default)
    {
        options ??= new PackageSearchOptions();
        var allResults = new List<PackageSearchItem>();
        var errors = new List<string>();

        foreach (var source in sourceService.GetSources().Where(s => s.IsEnabled))
        {
            try
            {
                var repository = sourceService.GetRepository(source);
                var searchResource = await repository.GetResourceAsync<PackageSearchResource>(cancellationToken);

                var searchFilter = new SearchFilter(options.IncludePrerelease);
                var results = await searchResource.SearchAsync(
                    query, searchFilter, options.Skip, options.Take,
                    NullLogger.Instance, cancellationToken);

                foreach (var result in results)
                {
                    var versions = await result.GetVersionsAsync();
                    var latestVersion = versions
                        .Where(v => options.IncludePrerelease || !v.Version.IsPrerelease)
                        .OrderByDescending(v => v.Version)
                        .FirstOrDefault();

                    if (latestVersion is null) continue;

                    var installed = installedPackages?.GetValueOrDefault(result.Identity.Id);

                    allResults.Add(new PackageSearchItem
                    {
                        PackageId = result.Identity.Id,
                        Title = result.Title ?? result.Identity.Id,
                        LatestVersion = latestVersion.Version,
                        Description = result.Description,
                        Authors = result.Authors,
                        DownloadCount = result.DownloadCount ?? 0,
                        IconUrl = result.IconUrl?.ToString(),
                        ProjectUrl = result.ProjectUrl?.ToString(),
                        Tags = result.Tags?.Split(',',
                            StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList() ?? [],
                        IsInstalled = installed is not null,
                        InstalledVersion = installed?.Version
                    });
                }
            }
            catch (Exception ex)
            {
                logger?.LogWarning(ex, "Search failed on source {Source}", source.Name);
                errors.Add($"Search failed on {source.Name}: {ex.Message}");
            }
        }

        // Deduplicate by package ID, keeping the first occurrence
        var deduplicated = allResults
            .GroupBy(r => r.PackageId, StringComparer.OrdinalIgnoreCase)
            .Select(g => g.First())
            .ToList();

        return new PackageSearchResult
        {
            Packages = deduplicated,
            TotalCount = deduplicated.Count,
            Errors = errors
        };
    }

    public async Task<PackageDetails?> GetPackageDetailsAsync(
        string packageId,
        NuGetVersion? version = null,
        IReadOnlyDictionary<string, InstalledPackage>? installedPackages = null,
        CancellationToken cancellationToken = default)
    {
        foreach (var source in sourceService.GetSources().Where(s => s.IsEnabled))
        {
            try
            {
                var repository = sourceService.GetRepository(source);
                var metadataResource = await repository.GetResourceAsync<PackageMetadataResource>(cancellationToken);

                var metadata = await metadataResource.GetMetadataAsync(
                    packageId, includePrerelease: true, includeUnlisted: false,
                    new SourceCacheContext(), NullLogger.Instance, cancellationToken);

                var metadataList = metadata.ToList();
                if (metadataList.Count == 0) continue;

                var target = version is not null
                    ? metadataList.FirstOrDefault(m => m.Identity.Version == version)
                    : metadataList.OrderByDescending(m => m.Identity.Version).First();

                if (target is null) continue;

                var installed = installedPackages?.GetValueOrDefault(packageId);

                return new PackageDetails
                {
                    PackageId = target.Identity.Id,
                    Version = target.Identity.Version,
                    Title = target.Title,
                    Description = target.Description,
                    Authors = target.Authors,
                    License = target.LicenseUrl?.ToString(),
                    ProjectUrl = target.ProjectUrl?.ToString(),
                    IconUrl = target.IconUrl?.ToString(),
                    Tags = target.Tags
                               ?.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                               .ToList() ??
                           [],
                    Dependencies = target.DependencySets
                        .SelectMany(ds => ds.Packages)
                        .Select(d => $"{d.Id} {d.VersionRange}")
                        .ToList(),
                    AllVersions = metadataList.Select(m => m.Identity.Version).OrderByDescending(v => v).ToList(),
                    IsInstalled = installed is not null,
                    InstalledVersion = installed?.Version
                };
            }
            catch (Exception ex)
            {
                logger?.LogDebug(ex, "Failed to get details for {PackageId} from {Source}", packageId, source.Name);
            }
        }

        return null;
    }

    public async Task<IReadOnlyList<PackageUpdateInfo>> CheckForUpdatesAsync(
        IEnumerable<InstalledPackage> installedPackages,
        CancellationToken cancellationToken = default)
    {
        var updates = new List<PackageUpdateInfo>();

        foreach (var package in installedPackages)
        {
            try
            {
                var latest = await ResolveLatestVersionAsync(package.PackageId, prerelease: false, cancellationToken);
                if (latest is not null && latest > package.Version)
                {
                    updates.Add(new PackageUpdateInfo
                    {
                        PackageId = package.PackageId,
                        CurrentVersion = package.Version,
                        LatestVersion = latest
                    });
                }
            }
            catch (Exception ex)
            {
                logger?.LogDebug(ex, "Failed to check updates for {PackageId}", package.PackageId);
            }
        }

        return updates;
    }

    public async Task<NuGetVersion?> ResolveLatestVersionAsync(
        string packageId, bool prerelease, CancellationToken cancellationToken = default)
    {
        foreach (var source in sourceService.GetSources().Where(s => s.IsEnabled))
        {
            try
            {
                var repository = sourceService.GetRepository(source);
                var resource = await repository.GetResourceAsync<FindPackageByIdResource>(cancellationToken);
                var versions = await resource.GetAllVersionsAsync(
                    packageId, new SourceCacheContext(), NullLogger.Instance, cancellationToken);

                var latest = versions
                    .Where(v => prerelease || !v.IsPrerelease)
                    .OrderByDescending(v => v)
                    .FirstOrDefault();

                if (latest is not null) return latest;
            }
            catch (Exception ex)
            {
                logger?.LogDebug(ex, "Failed to resolve latest version for {PackageId} from {Source}", packageId,
                    source.Name);
            }
        }

        return null;
    }

    public async Task<(SourceRepository? Repository, PackageSource? Source)> FindPackageSourceAsync(
        string packageId, NuGetVersion version, CancellationToken cancellationToken = default)
    {
        foreach (var source in sourceService.GetSources().Where(s => s.IsEnabled))
        {
            try
            {
                var repository = sourceService.GetRepository(source);
                var resource = await repository.GetResourceAsync<FindPackageByIdResource>(cancellationToken);
                var exists = await resource.DoesPackageExistAsync(
                    packageId, version, new SourceCacheContext(), NullLogger.Instance, cancellationToken);

                if (exists) return (repository, source);
            }
            catch
            {
                // Try next source
            }
        }

        return (null, null);
    }
}
