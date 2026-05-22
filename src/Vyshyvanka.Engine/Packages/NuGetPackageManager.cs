using System.Collections.Concurrent;
using Vyshyvanka.Core.Interfaces;
using Microsoft.Extensions.Logging;
using NuGet.Protocol.Core.Types;
using NuGet.Versioning;

namespace Vyshyvanka.Engine.Packages;

/// <summary>
/// Thrown when a package cannot be found in any configured source during installation.
/// </summary>
internal sealed class PackageNotFoundException(string message) : Exception(message);

/// <summary>
/// Primary implementation of NuGet package operations: install, update, uninstall.
/// Orchestrates between the package search service, plugin loading service, cache, manifest,
/// and dependency resolver.
/// </summary>
public class NuGetPackageManager(
    IPackageSearchService searchService,
    IPluginLoadingService pluginLoadingService,
    IManifestManager manifestManager,
    IDependencyResolver dependencyResolver,
    IPackageCache packageCache,
    IWorkflowRepository? workflowRepository,
    PackageOptions options,
    ILogger<NuGetPackageManager>? logger = null) : INuGetPackageManager
{
    private readonly ConcurrentDictionary<string, InstalledPackage> _installedPackages =
        new(StringComparer.OrdinalIgnoreCase);

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        logger?.LogInformation("Initializing package manager...");

        var manifest = await manifestManager.LoadAsync();
        var failedPackages = new List<string>();

        foreach (var package in manifest.Packages)
        {
            _installedPackages[package.PackageId] = package;

            if (Directory.Exists(package.InstallPath))
            {
                if (!pluginLoadingService.TryLoadPluginsForInitialization(package.PackageId, package.InstallPath))
                {
                    failedPackages.Add(package.PackageId);
                }
            }
            else
            {
                logger?.LogWarning("Install path missing for {PackageId}: {Path}, removing from installed",
                    package.PackageId, package.InstallPath);
                failedPackages.Add(package.PackageId);
            }
        }

        // Remove failed packages from manifest and in-memory tracking
        foreach (var packageId in failedPackages)
        {
            _installedPackages.TryRemove(packageId, out _);
            await manifestManager.RemovePackageAsync(packageId);
            logger?.LogInformation("Removed failed package {PackageId} from manifest", packageId);
        }

        logger?.LogInformation(
            "Package manager initialized with {Count} packages ({Failed} removed due to load failures)",
            _installedPackages.Count, failedPackages.Count);
    }

    public async Task<PackageSearchResult> SearchPackagesAsync(
        string query,
        PackageSearchOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        return await searchService.SearchPackagesAsync(
            query, options, _installedPackages, cancellationToken);
    }

    public async Task<PackageDetails?> GetPackageDetailsAsync(
        string packageId,
        NuGetVersion? version = null,
        CancellationToken cancellationToken = default)
    {
        return await searchService.GetPackageDetailsAsync(
            packageId, version, _installedPackages, cancellationToken);
    }

    public async Task<PackageInstallResult> InstallPackageAsync(
        string packageId,
        NuGetVersion? version = null,
        bool prerelease = false,
        CancellationToken cancellationToken = default)
    {
        logger?.LogInformation("Installing package {PackageId} v{Version}", packageId,
            version?.ToString() ?? "latest");

        var listValidation = ValidatePackageLists(packageId);
        if (listValidation is not null) return listValidation;

        try
        {
            var (resolvedVersion, sourceRepository, source) =
                await ResolveVersionAndSourceAsync(packageId, version, prerelease, cancellationToken);

            var sourceConfirmation = await ConfirmUntrustedSourceAsync(packageId, source);
            if (sourceConfirmation is not null) return sourceConfirmation;

            var depResult = await ResolveDependenciesAsync(packageId, resolvedVersion, cancellationToken);
            if (!depResult.Success)
            {
                return new PackageInstallResult
                {
                    Success = false,
                    Errors = depResult.Conflicts.Select(c =>
                            $"Dependency conflict: {c.PackageId} requires {c.RequestedVersion} but {c.InstalledVersion} is installed (requested by {c.RequestedBy})")
                        .ToList()
                };
            }

            var installPath = await DownloadAndExtractPackageAsync(
                packageId, resolvedVersion, sourceRepository, cancellationToken);

            pluginLoadingService.UnloadPlugins(packageId, installPath);

            var pluginResult = await pluginLoadingService.LoadAndValidatePluginsAsync(
                packageId, resolvedVersion, installPath, cancellationToken);
            if (pluginResult.Failure is not null) return pluginResult.Failure;

            var installedDeps = await InstallDependenciesAsync(
                depResult, packageId, source.Name, cancellationToken);

            var installedPackage = CreateInstalledPackageRecord(
                packageId, resolvedVersion, source.Name, installPath, pluginResult.NodeTypes, depResult);

            _installedPackages[packageId] = installedPackage;
            await manifestManager.AddPackageAsync(installedPackage);

            logger?.LogInformation("Successfully installed {PackageId} v{Version}", packageId, resolvedVersion);

            return new PackageInstallResult
            {
                Success = true,
                Package = installedPackage,
                InstalledDependencies = installedDeps,
                Warnings = pluginResult.Warnings
            };
        }
        catch (PackageNotFoundException ex)
        {
            return new PackageInstallResult
            {
                Success = false,
                Errors = [ex.Message]
            };
        }
        catch (Exception ex)
        {
            logger?.LogError(ex, "Failed to install package {PackageId}", packageId);
            return new PackageInstallResult
            {
                Success = false,
                Errors = [$"Installation failed: {ex.Message}"]
            };
        }
    }

    public async Task<PackageUpdateResult> UpdatePackageAsync(
        string packageId,
        NuGetVersion? targetVersion = null,
        CancellationToken cancellationToken = default)
    {
        if (!_installedPackages.TryGetValue(packageId, out var existing))
        {
            return new PackageUpdateResult
            {
                Success = false,
                Errors = [$"Package '{packageId}' is not installed"]
            };
        }

        var previousVersion = existing.Version;
        var newVersion = targetVersion ??
                         await searchService.ResolveLatestVersionAsync(packageId, prerelease: false, cancellationToken);

        if (newVersion is null)
        {
            return new PackageUpdateResult
            {
                Success = false,
                Errors = [$"No newer version found for '{packageId}'"]
            };
        }

        if (newVersion == previousVersion)
        {
            return new PackageUpdateResult
            {
                Success = true,
                Package = existing,
                PreviousVersion = previousVersion,
                Warnings = [$"Package '{packageId}' is already at version {previousVersion}"]
            };
        }

        // Check compatibility
        var compat = await dependencyResolver.CheckUpdateCompatibilityAsync(
            packageId, newVersion, _installedPackages.Values, cancellationToken);

        if (!compat.IsCompatible)
        {
            return new PackageUpdateResult
            {
                Success = false,
                PreviousVersion = previousVersion,
                Errors = compat.Conflicts.Select(c =>
                        $"Conflict: {c.PackageId} requires {c.RequestedVersion} but {c.InstalledVersion} is installed")
                    .ToList()
            };
        }

        // Unload old version
        pluginLoadingService.UnloadAndUnregisterPlugins(packageId, existing.InstallPath);

        // Install new version
        var installResult = await InstallPackageAsync(packageId, newVersion, cancellationToken: cancellationToken);

        if (!installResult.Success)
        {
            return new PackageUpdateResult
            {
                Success = false,
                PreviousVersion = previousVersion,
                Errors = installResult.Errors
            };
        }

        // Clean up old version from cache
        await packageCache.RemovePackageAsync(packageId, previousVersion);

        return new PackageUpdateResult
        {
            Success = true,
            Package = installResult.Package,
            PreviousVersion = previousVersion,
            Warnings = compat.Warnings.ToList()
        };
    }

    public async Task<PackageUninstallResult> UninstallPackageAsync(
        string packageId,
        bool force = false,
        CancellationToken cancellationToken = default)
    {
        if (!_installedPackages.TryGetValue(packageId, out var package))
        {
            return new PackageUninstallResult
            {
                Success = false,
                PackageId = packageId,
                Errors = [$"Package '{packageId}' is not installed"]
            };
        }

        // Check for workflow references
        var affectedWorkflows = await GetAffectedWorkflowsAsync(package, cancellationToken);

        if (affectedWorkflows.Count > 0 && !force)
        {
            return new PackageUninstallResult
            {
                Success = false,
                PackageId = packageId,
                AffectedWorkflows = affectedWorkflows,
                Errors =
                [
                    $"Package '{packageId}' is referenced by {affectedWorkflows.Count} workflow(s). Use force=true to uninstall anyway."
                ]
            };
        }

        try
        {
            // Unload plugin and unregister nodes
            pluginLoadingService.UnloadPlugins(packageId, package.InstallPath);

            // Remove from cache
            await packageCache.RemovePackageAsync(packageId, package.Version);

            // Remove from manifest
            await manifestManager.RemovePackageAsync(packageId);

            // Remove from in-memory tracking
            _installedPackages.TryRemove(packageId, out _);

            // Remove orphaned dependencies
            var removedDeps = await RemoveOrphanedDependenciesAsync(package.Dependencies);

            logger?.LogInformation("Uninstalled {PackageId} v{Version}", packageId, package.Version);

            return new PackageUninstallResult
            {
                Success = true,
                PackageId = packageId,
                RemovedDependencies = removedDeps,
                AffectedWorkflows = affectedWorkflows
            };
        }
        catch (Exception ex)
        {
            logger?.LogError(ex, "Failed to uninstall {PackageId}", packageId);
            return new PackageUninstallResult
            {
                Success = false,
                PackageId = packageId,
                AffectedWorkflows = affectedWorkflows,
                Errors = [$"Uninstallation failed: {ex.Message}"]
            };
        }
    }

    public IReadOnlyList<InstalledPackage> GetInstalledPackages()
    {
        return _installedPackages.Values.ToList().AsReadOnly();
    }

    public async Task<IReadOnlyList<PackageUpdateInfo>> CheckForUpdatesAsync(
        CancellationToken cancellationToken = default)
    {
        return await searchService.CheckForUpdatesAsync(_installedPackages.Values, cancellationToken);
    }

    public async Task<PackageInstallResult> InstallFromStreamAsync(
        Stream nupkgStream,
        string fileName,
        CancellationToken cancellationToken = default)
    {
        logger?.LogInformation("Installing package from uploaded file: {FileName}", fileName);

        try
        {
            // Save the stream to a temp file
            var tempPath = Path.Combine(options.CacheDirectory, $"upload-{Guid.NewGuid():N}.nupkg");
            Directory.CreateDirectory(options.CacheDirectory);

            await using (var fileStream = File.Create(tempPath))
            {
                await nupkgStream.CopyToAsync(fileStream, cancellationToken);
            }

            try
            {
                // Read package identity from the .nupkg
                using var packageReader = new NuGet.Packaging.PackageArchiveReader(tempPath);
                var identity = await packageReader.GetIdentityAsync(cancellationToken);
                var packageId = identity.Id;
                var version = identity.Version;

                logger?.LogInformation("Uploaded package: {PackageId} v{Version}", packageId, version);

                // Check block/allow lists
                var listValidation = ValidatePackageLists(packageId);
                if (listValidation is not null) return listValidation;

                // Move to the proper cache location
                var nupkgPath = Path.Combine(
                    options.CacheDirectory,
                    $"{packageId.ToLowerInvariant()}.{version.ToNormalizedString()}.nupkg");

                if (File.Exists(nupkgPath))
                    File.Delete(nupkgPath);

                File.Move(tempPath, nupkgPath);
                tempPath = nupkgPath; // update so cleanup targets the right file on error

                // Extract
                var installPath = await packageCache.ExtractPackageAsync(
                    nupkgPath, packageId, version, cancellationToken);

                // Load and validate plugin
                var pluginResult = await pluginLoadingService.LoadAndValidatePluginsAsync(
                    packageId, version, installPath, cancellationToken);
                if (pluginResult.Failure is not null)
                    return pluginResult.Failure;

                var installedPackage = new InstalledPackage
                {
                    PackageId = packageId,
                    Version = version,
                    SourceName = "local-upload",
                    InstallPath = installPath,
                    InstalledAt = DateTime.UtcNow,
                    NodeTypes = pluginResult.NodeTypes,
                    Dependencies = [],
                    IsLoaded = true
                };

                _installedPackages[packageId] = installedPackage;
                await manifestManager.AddPackageAsync(installedPackage);

                logger?.LogInformation("Successfully installed uploaded package {PackageId} v{Version}", packageId,
                    version);

                return new PackageInstallResult
                {
                    Success = true,
                    Package = installedPackage,
                    Warnings = pluginResult.Warnings
                };
            }
            finally
            {
                // Clean up temp file if it still exists (error path)
                if (tempPath.Contains("upload-") && File.Exists(tempPath))
                    File.Delete(tempPath);
            }
        }
        catch (Exception ex)
        {
            logger?.LogError(ex, "Failed to install uploaded package {FileName}", fileName);
            return new PackageInstallResult
            {
                Success = false,
                Errors = [$"Failed to install uploaded package: {ex.Message}"]
            };
        }
    }

    private PackageInstallResult? ValidatePackageLists(string packageId)
    {
        if (options.BlockedPackages.Any(b => string.Equals(b, packageId, StringComparison.OrdinalIgnoreCase)))
        {
            return new PackageInstallResult
            {
                Success = false,
                Errors = [$"Package '{packageId}' is in the block list and cannot be installed"]
            };
        }

        if (options.AllowedPackages.Count > 0 &&
            !options.AllowedPackages.Any(a => string.Equals(a, packageId, StringComparison.OrdinalIgnoreCase)))
        {
            return new PackageInstallResult
            {
                Success = false,
                Errors = [$"Package '{packageId}' is not in the allow list"]
            };
        }

        return null;
    }

    private async Task<(NuGetVersion Version, SourceRepository Repository, PackageSource Source)>
        ResolveVersionAndSourceAsync(
            string packageId,
            NuGetVersion? version,
            bool prerelease,
            CancellationToken cancellationToken)
    {
        var resolvedVersion = version ??
                              await searchService.ResolveLatestVersionAsync(packageId, prerelease, cancellationToken);
        if (resolvedVersion is null)
        {
            throw new PackageNotFoundException(
                $"Package '{packageId}' was not found in any configured source");
        }

        var (sourceRepository, source) =
            await searchService.FindPackageSourceAsync(packageId, resolvedVersion, cancellationToken);
        if (sourceRepository is null || source is null)
        {
            throw new PackageNotFoundException(
                $"Package '{packageId}' v{resolvedVersion} was not found in any configured source");
        }

        return (resolvedVersion, sourceRepository, source);
    }

    private async Task<PackageInstallResult?> ConfirmUntrustedSourceAsync(
        string packageId, PackageSource source)
    {
        if (!options.RequireUntrustedSourceConfirmation || source.IsTrusted)
            return null;

        if (options.UntrustedSourceConfirmationCallback is null)
            return null;

        var confirmed = await options.UntrustedSourceConfirmationCallback(packageId, source.Name);
        if (confirmed) return null;

        return new PackageInstallResult
        {
            Success = false,
            Errors = [$"Installation from untrusted source '{source.Name}' was not confirmed"]
        };
    }

    private async Task<DependencyResolutionResult> ResolveDependenciesAsync(
        string packageId, NuGetVersion resolvedVersion, CancellationToken cancellationToken)
    {
        return await dependencyResolver.ResolveAsync(
            packageId, resolvedVersion, _installedPackages.Values, cancellationToken);
    }

    private async Task<string> DownloadAndExtractPackageAsync(
        string packageId,
        NuGetVersion resolvedVersion,
        SourceRepository sourceRepository,
        CancellationToken cancellationToken)
    {
        var nupkgPath = await packageCache.GetPackagePathAsync(
            packageId, resolvedVersion, sourceRepository, cancellationToken);
        return await packageCache.ExtractPackageAsync(
            nupkgPath, packageId, resolvedVersion, cancellationToken);
    }

    private async Task<List<InstalledPackage>> InstallDependenciesAsync(
        DependencyResolutionResult depResult,
        string packageId,
        string sourceName,
        CancellationToken cancellationToken)
    {
        var installedDeps = new List<InstalledPackage>();

        foreach (var dep in depResult.Dependencies.Where(d =>
                     !d.IsAlreadyInstalled &&
                     !string.Equals(d.PackageId, packageId, StringComparison.OrdinalIgnoreCase)))
        {
            try
            {
                var depSource = await searchService.FindPackageSourceAsync(dep.PackageId, dep.Version, cancellationToken);
                if (depSource.Repository is null) continue;

                var depNupkg = await packageCache.GetPackagePathAsync(
                    dep.PackageId, dep.Version, depSource.Repository, cancellationToken);
                var depPath = await packageCache.ExtractPackageAsync(
                    depNupkg, dep.PackageId, dep.Version, cancellationToken);

                var depPackage = new InstalledPackage
                {
                    PackageId = dep.PackageId,
                    Version = dep.Version,
                    SourceName = sourceName,
                    InstallPath = depPath,
                    InstalledAt = DateTime.UtcNow,
                    NodeTypes = [],
                    Dependencies = [],
                    IsLoaded = true
                };

                _installedPackages[dep.PackageId] = depPackage;
                await manifestManager.AddPackageAsync(depPackage);
                installedDeps.Add(depPackage);
            }
            catch (Exception ex)
            {
                logger?.LogWarning(ex, "Failed to install dependency {DepId}", dep.PackageId);
            }
        }

        return installedDeps;
    }

    private static InstalledPackage CreateInstalledPackageRecord(
        string packageId,
        NuGetVersion resolvedVersion,
        string sourceName,
        string installPath,
        List<string> nodeTypes,
        DependencyResolutionResult depResult)
    {
        return new InstalledPackage
        {
            PackageId = packageId,
            Version = resolvedVersion,
            SourceName = sourceName,
            InstallPath = installPath,
            InstalledAt = DateTime.UtcNow,
            NodeTypes = nodeTypes,
            Dependencies = depResult.Dependencies
                .Where(d => !string.Equals(d.PackageId, packageId, StringComparison.OrdinalIgnoreCase))
                .Select(d => d.PackageId)
                .ToList(),
            IsLoaded = true
        };
    }

    private async Task<List<string>> GetAffectedWorkflowsAsync(
        InstalledPackage package, CancellationToken cancellationToken)
    {
        var affectedWorkflows = new List<string>();

        if (workflowRepository is not null && package.NodeTypes.Count > 0)
        {
            try
            {
                var workflows = await workflowRepository.GetAllAsync(0, int.MaxValue, cancellationToken);
                foreach (var workflow in workflows)
                {
                    var usesPackageNode = workflow.Nodes.Any(n =>
                        package.NodeTypes.Any(nt =>
                            string.Equals(n.Type, nt, StringComparison.OrdinalIgnoreCase)));

                    if (usesPackageNode)
                    {
                        affectedWorkflows.Add(workflow.Name);
                    }
                }
            }
            catch (Exception ex)
            {
                logger?.LogWarning(ex, "Failed to check workflow references for {PackageId}", package.PackageId);
            }
        }

        return affectedWorkflows;
    }

    private async Task<List<string>> RemoveOrphanedDependenciesAsync(IReadOnlyList<string> dependencies)
    {
        var removedDeps = new List<string>();

        foreach (var depId in dependencies)
        {
            var stillNeeded = _installedPackages.Values.Any(p =>
                p.Dependencies.Any(d => string.Equals(d, depId, StringComparison.OrdinalIgnoreCase)));

            if (!stillNeeded && _installedPackages.TryRemove(depId, out var depPackage))
            {
                await packageCache.RemovePackageAsync(depId, depPackage.Version);
                await manifestManager.RemovePackageAsync(depId);
                removedDeps.Add(depId);
            }
        }

        return removedDeps;
    }
}
