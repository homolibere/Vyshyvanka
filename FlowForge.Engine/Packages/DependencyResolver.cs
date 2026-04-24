using FlowForge.Core.Interfaces;
using Microsoft.Extensions.Logging;
using NuGet.Common;
using NuGet.Frameworks;
using NuGet.Packaging;
using NuGet.Packaging.Core;
using NuGet.Protocol.Core.Types;
using NuGet.Versioning;
using CorePackageDependency = FlowForge.Core.Interfaces.PackageDependency;

namespace FlowForge.Engine.Packages;

/// <summary>
/// Resolves transitive package dependencies and detects version conflicts with installed packages.
/// </summary>
public class DependencyResolver : IDependencyResolver
{
    private readonly IPackageSourceService _sourceService;
    private readonly ILogger<DependencyResolver>? _logger;

    private static readonly NuGetFramework TargetFramework =
        NuGetFramework.ParseFolder("net10.0");

    public DependencyResolver(
        IPackageSourceService sourceService,
        ILogger<DependencyResolver>? logger = null)
    {
        _sourceService = sourceService ?? throw new ArgumentNullException(nameof(sourceService));
        _logger = logger;
    }

    public async Task<DependencyResolutionResult> ResolveAsync(
        string packageId,
        NuGetVersion version,
        IEnumerable<InstalledPackage> installedPackages,
        CancellationToken cancellationToken = default)
    {
        var installed = installedPackages.ToList();
        var dependencies = new List<CorePackageDependency>();
        var conflicts = new List<DependencyConflict>();
        var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        try
        {
            await ResolveRecursiveAsync(
                packageId, version, packageId,
                installed, dependencies, conflicts, visited, cancellationToken);

            return new DependencyResolutionResult
            {
                Success = conflicts.Count == 0,
                Dependencies = dependencies,
                Conflicts = conflicts
            };
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Dependency resolution failed for {PackageId} v{Version}", packageId, version);
            return new DependencyResolutionResult
            {
                Success = false,
                Dependencies = dependencies,
                Conflicts = conflicts
            };
        }
    }

    public async Task<CompatibilityResult> CheckUpdateCompatibilityAsync(
        string packageId,
        NuGetVersion newVersion,
        IEnumerable<InstalledPackage> installedPackages,
        CancellationToken cancellationToken = default)
    {
        var installed = installedPackages.ToList();
        var warnings = new List<string>();
        var conflicts = new List<DependencyConflict>();

        try
        {
            var dependencies = new List<CorePackageDependency>();
            var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            await ResolveRecursiveAsync(
                packageId, newVersion, packageId,
                installed, dependencies, conflicts, visited, cancellationToken);

            if (conflicts.Count == 0)
            {
                var existing = installed.FirstOrDefault(p =>
                    string.Equals(p.PackageId, packageId, StringComparison.OrdinalIgnoreCase));

                if (existing is not null && newVersion < existing.Version)
                {
                    warnings.Add($"Downgrading {packageId} from {existing.Version} to {newVersion}");
                }
            }

            return new CompatibilityResult
            {
                IsCompatible = conflicts.Count == 0,
                Conflicts = conflicts,
                Warnings = warnings
            };
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Compatibility check failed for {PackageId} v{Version}", packageId, newVersion);
            return new CompatibilityResult
            {
                IsCompatible = false,
                Conflicts = conflicts,
                Warnings = [ex.Message]
            };
        }
    }

    private async Task ResolveRecursiveAsync(
        string packageId,
        NuGetVersion version,
        string requestedBy,
        List<InstalledPackage> installed,
        List<CorePackageDependency> dependencies,
        List<DependencyConflict> conflicts,
        HashSet<string> visited,
        CancellationToken cancellationToken)
    {
        if (!visited.Add(packageId.ToLowerInvariant()))
            return;

        // Check for conflicts with installed packages
        var existingInstalled = installed.FirstOrDefault(p =>
            string.Equals(p.PackageId, packageId, StringComparison.OrdinalIgnoreCase));

        if (existingInstalled is not null && existingInstalled.Version != version)
        {
            conflicts.Add(new DependencyConflict
            {
                PackageId = packageId,
                RequestedVersion = version,
                InstalledVersion = existingInstalled.Version,
                RequestedBy = requestedBy
            });
            return;
        }

        var isAlreadyInstalled = existingInstalled is not null;

        dependencies.Add(new CorePackageDependency
        {
            PackageId = packageId,
            Version = version,
            IsAlreadyInstalled = isAlreadyInstalled
        });

        // Resolve transitive dependencies from NuGet sources
        var depGroups = await GetDependencyGroupsAsync(packageId, version, cancellationToken);
        if (depGroups is null) return;

        // Find the best matching framework group
        var bestGroup = depGroups
            .Where(g => DefaultCompatibilityProvider.Instance.IsCompatible(TargetFramework, g.TargetFramework))
            .OrderByDescending(g => g.TargetFramework.Version)
            .FirstOrDefault();

        if (bestGroup is null) return;

        foreach (var dep in bestGroup.Packages)
        {
            var resolvedVersion = dep.VersionRange.MinVersion ?? new NuGetVersion(0, 0, 0);

            await ResolveRecursiveAsync(
                dep.Id, resolvedVersion, packageId,
                installed, dependencies, conflicts, visited, cancellationToken);
        }
    }

    private async Task<IEnumerable<PackageDependencyGroup>?> GetDependencyGroupsAsync(
        string packageId,
        NuGetVersion version,
        CancellationToken cancellationToken)
    {
        foreach (var source in _sourceService.GetSources().Where(s => s.IsEnabled))
        {
            try
            {
                var repository = _sourceService.GetRepository(source);
                var resource = await repository.GetResourceAsync<DependencyInfoResource>(cancellationToken);
                var identity = new PackageIdentity(packageId, version);
                var info = await resource.ResolvePackage(
                    identity, TargetFramework, new SourceCacheContext(), NullLogger.Instance, cancellationToken);

                if (info is not null)
                {
                    // SourcePackageDependencyInfo has a flat Dependencies list already resolved for the target framework
                    var group = new PackageDependencyGroup(TargetFramework, info.Dependencies);
                    return [group];
                }
            }
            catch (Exception ex)
            {
                _logger?.LogDebug(ex, "Failed to resolve deps for {PackageId} from {Source}", packageId, source.Name);
            }
        }

        return null;
    }
}
