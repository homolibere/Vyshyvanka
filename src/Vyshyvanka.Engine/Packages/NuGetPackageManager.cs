using System.Collections.Concurrent;
using Vyshyvanka.Core.Interfaces;
using Microsoft.Extensions.Logging;
using NuGet.Common;
using NuGet.Protocol.Core.Types;
using NuGet.Versioning;

namespace Vyshyvanka.Engine.Packages;

/// <summary>
/// Primary implementation of NuGet package operations: search, install, update, uninstall.
/// Coordinates between the package cache, manifest, dependency resolver, plugin loader, and node registry.
/// </summary>
public class NuGetPackageManager : INuGetPackageManager
{
    private readonly IPackageSourceService _sourceService;
    private readonly IManifestManager _manifestManager;
    private readonly IDependencyResolver _dependencyResolver;
    private readonly IPackageCache _packageCache;
    private readonly IPluginLoader _pluginLoader;
    private readonly IPluginValidator _pluginValidator;
    private readonly INodeRegistry _nodeRegistry;
    private readonly IWorkflowRepository? _workflowRepository;
    private readonly PackageOptions _options;
    private readonly ILogger<NuGetPackageManager>? _logger;

    private readonly ConcurrentDictionary<string, InstalledPackage> _installedPackages = new(StringComparer.OrdinalIgnoreCase);

    public NuGetPackageManager(
        IPackageSourceService sourceService,
        IManifestManager manifestManager,
        IDependencyResolver dependencyResolver,
        IPackageCache packageCache,
        IPluginLoader pluginLoader,
        IPluginValidator pluginValidator,
        INodeRegistry nodeRegistry,
        IWorkflowRepository? workflowRepository,
        PackageOptions options,
        ILogger<NuGetPackageManager>? logger = null)
    {
        _sourceService = sourceService ?? throw new ArgumentNullException(nameof(sourceService));
        _manifestManager = manifestManager ?? throw new ArgumentNullException(nameof(manifestManager));
        _dependencyResolver = dependencyResolver ?? throw new ArgumentNullException(nameof(dependencyResolver));
        _packageCache = packageCache ?? throw new ArgumentNullException(nameof(packageCache));
        _pluginLoader = pluginLoader ?? throw new ArgumentNullException(nameof(pluginLoader));
        _pluginValidator = pluginValidator ?? throw new ArgumentNullException(nameof(pluginValidator));
        _nodeRegistry = nodeRegistry ?? throw new ArgumentNullException(nameof(nodeRegistry));
        _workflowRepository = workflowRepository;
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _logger = logger;
    }

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        _logger?.LogInformation("Initializing package manager...");

        var manifest = await _manifestManager.LoadAsync();
        var failedPackages = new List<string>();

        foreach (var package in manifest.Packages)
        {
            _installedPackages[package.PackageId] = package;

            if (Directory.Exists(package.InstallPath))
            {
                try
                {
                    var plugins = _pluginLoader.LoadPlugins(package.InstallPath);
                    var anyLoaded = false;
                    foreach (var plugin in plugins)
                    {
                        if (plugin.IsLoaded && plugin.Assembly is not null)
                        {
                            _nodeRegistry.RegisterFromAssembly(plugin.Assembly);
                            anyLoaded = true;
                        }
                    }

                    if (!anyLoaded)
                    {
                        _logger?.LogWarning("No loadable plugins found in {PackageId}, removing from installed", package.PackageId);
                        failedPackages.Add(package.PackageId);
                    }
                }
                catch (Exception ex)
                {
                    _logger?.LogWarning(ex, "Failed to load plugin from {PackageId}, removing from installed", package.PackageId);
                    failedPackages.Add(package.PackageId);
                }
            }
            else
            {
                _logger?.LogWarning("Install path missing for {PackageId}: {Path}, removing from installed", package.PackageId, package.InstallPath);
                failedPackages.Add(package.PackageId);
            }
        }

        // Remove failed packages from manifest and in-memory tracking
        foreach (var packageId in failedPackages)
        {
            _installedPackages.TryRemove(packageId, out _);
            await _manifestManager.RemovePackageAsync(packageId);
            _logger?.LogInformation("Removed failed package {PackageId} from manifest", packageId);
        }

        _logger?.LogInformation("Package manager initialized with {Count} packages ({Failed} removed due to load failures)",
            _installedPackages.Count, failedPackages.Count);
    }

    public async Task<PackageSearchResult> SearchPackagesAsync(
        string query,
        PackageSearchOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        options ??= new PackageSearchOptions();
        var allResults = new List<PackageSearchItem>();
        var errors = new List<string>();

        foreach (var source in _sourceService.GetSources().Where(s => s.IsEnabled))
        {
            try
            {
                var repository = _sourceService.GetRepository(source);
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

                    var installed = _installedPackages.GetValueOrDefault(result.Identity.Id);

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
                        Tags = result.Tags?.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList() ?? [],
                        IsInstalled = installed is not null,
                        InstalledVersion = installed?.Version
                    });
                }
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "Search failed on source {Source}", source.Name);
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
        CancellationToken cancellationToken = default)
    {
        foreach (var source in _sourceService.GetSources().Where(s => s.IsEnabled))
        {
            try
            {
                var repository = _sourceService.GetRepository(source);
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

                var installed = _installedPackages.GetValueOrDefault(packageId);

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
                    Tags = target.Tags?.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList() ?? [],
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
                _logger?.LogDebug(ex, "Failed to get details for {PackageId} from {Source}", packageId, source.Name);
            }
        }

        return null;
    }

    public async Task<PackageInstallResult> InstallPackageAsync(
        string packageId,
        NuGetVersion? version = null,
        bool prerelease = false,
        CancellationToken cancellationToken = default)
    {
        _logger?.LogInformation("Installing package {PackageId} v{Version}", packageId, version?.ToString() ?? "latest");

        // Check block list
        if (_options.BlockedPackages.Any(b => string.Equals(b, packageId, StringComparison.OrdinalIgnoreCase)))
        {
            return new PackageInstallResult
            {
                Success = false,
                Errors = [$"Package '{packageId}' is in the block list and cannot be installed"]
            };
        }

        // Check allow list
        if (_options.AllowedPackages.Count > 0 &&
            !_options.AllowedPackages.Any(a => string.Equals(a, packageId, StringComparison.OrdinalIgnoreCase)))
        {
            return new PackageInstallResult
            {
                Success = false,
                Errors = [$"Package '{packageId}' is not in the allow list"]
            };
        }

        try
        {
            // Resolve version if not specified
            var resolvedVersion = version ?? await ResolveLatestVersionAsync(packageId, prerelease, cancellationToken);
            if (resolvedVersion is null)
            {
                return new PackageInstallResult
                {
                    Success = false,
                    Errors = [$"Package '{packageId}' was not found in any configured source"]
                };
            }

            // Find the source that has this package
            var (sourceRepository, source) = await FindPackageSourceAsync(packageId, resolvedVersion, cancellationToken);
            if (sourceRepository is null || source is null)
            {
                return new PackageInstallResult
                {
                    Success = false,
                    Errors = [$"Package '{packageId}' v{resolvedVersion} was not found in any configured source"]
                };
            }

            // Check untrusted source confirmation
            if (_options.RequireUntrustedSourceConfirmation && !source.IsTrusted)
            {
                if (_options.UntrustedSourceConfirmationCallback is not null)
                {
                    var confirmed = await _options.UntrustedSourceConfirmationCallback(packageId, source.Name);
                    if (!confirmed)
                    {
                        return new PackageInstallResult
                        {
                            Success = false,
                            Errors = [$"Installation from untrusted source '{source.Name}' was not confirmed"]
                        };
                    }
                }
            }

            // Resolve dependencies
            var depResult = await _dependencyResolver.ResolveAsync(
                packageId, resolvedVersion, _installedPackages.Values, cancellationToken);

            if (!depResult.Success)
            {
                return new PackageInstallResult
                {
                    Success = false,
                    Errors = depResult.Conflicts.Select(c =>
                        $"Dependency conflict: {c.PackageId} requires {c.RequestedVersion} but {c.InstalledVersion} is installed (requested by {c.RequestedBy})").ToList()
                };
            }

            // Download and extract the package
            var nupkgPath = await _packageCache.GetPackagePathAsync(
                packageId, resolvedVersion, sourceRepository, cancellationToken);
            var installPath = await _packageCache.ExtractPackageAsync(
                nupkgPath, packageId, resolvedVersion, cancellationToken);

            // Load and validate the plugin
            var nodeTypes = new List<string>();
            var warnings = new List<string>();

            try
            {
                var plugins = _pluginLoader.LoadPlugins(installPath);
                foreach (var plugin in plugins)
                {
                    if (plugin.Assembly is not null)
                    {
                        var validation = _pluginValidator.ValidatePlugin(plugin.Assembly);
                        if (!validation.IsValid)
                        {
                            await _packageCache.RemovePackageAsync(packageId, resolvedVersion);
                            return new PackageInstallResult
                            {
                                Success = false,
                                Errors = validation.Errors.Select(e => $"Plugin validation failed: {e.Message}").ToList()
                            };
                        }

                        _nodeRegistry.RegisterFromAssembly(plugin.Assembly);

                        // Track node type identifiers (INode.Type values, not class names)
                        foreach (var nodeType in plugin.NodeTypes)
                        {
                            try
                            {
                                if (Activator.CreateInstance(nodeType) is INode nodeInstance)
                                    nodeTypes.Add(nodeInstance.Type);
                            }
                            catch
                            {
                                // Skip types that can't be instantiated
                            }
                        }

                        warnings.AddRange(validation.Warnings.Select(w => w.Message));
                    }
                }
            }
            catch (Exception ex)
            {
                _logger?.LogDebug(ex, "No plugin assembly found in {PackageId}, treating as library dependency", packageId);
            }

            // Install dependencies
            var installedDeps = new List<InstalledPackage>();
            foreach (var dep in depResult.Dependencies.Where(d => !d.IsAlreadyInstalled && !string.Equals(d.PackageId, packageId, StringComparison.OrdinalIgnoreCase)))
            {
                try
                {
                    var depSource = await FindPackageSourceAsync(dep.PackageId, dep.Version, cancellationToken);
                    if (depSource.Repository is not null)
                    {
                        var depNupkg = await _packageCache.GetPackagePathAsync(
                            dep.PackageId, dep.Version, depSource.Repository, cancellationToken);
                        var depPath = await _packageCache.ExtractPackageAsync(
                            depNupkg, dep.PackageId, dep.Version, cancellationToken);

                        var depPackage = new InstalledPackage
                        {
                            PackageId = dep.PackageId,
                            Version = dep.Version,
                            SourceName = source.Name,
                            InstallPath = depPath,
                            InstalledAt = DateTime.UtcNow,
                            NodeTypes = [],
                            Dependencies = [],
                            IsLoaded = true
                        };

                        _installedPackages[dep.PackageId] = depPackage;
                        await _manifestManager.AddPackageAsync(depPackage);
                        installedDeps.Add(depPackage);
                    }
                }
                catch (Exception ex)
                {
                    _logger?.LogWarning(ex, "Failed to install dependency {DepId}", dep.PackageId);
                }
            }

            // Create the installed package record
            var installedPackage = new InstalledPackage
            {
                PackageId = packageId,
                Version = resolvedVersion,
                SourceName = source.Name,
                InstallPath = installPath,
                InstalledAt = DateTime.UtcNow,
                NodeTypes = nodeTypes,
                Dependencies = depResult.Dependencies
                    .Where(d => !string.Equals(d.PackageId, packageId, StringComparison.OrdinalIgnoreCase))
                    .Select(d => d.PackageId)
                    .ToList(),
                IsLoaded = true
            };

            _installedPackages[packageId] = installedPackage;
            await _manifestManager.AddPackageAsync(installedPackage);

            _logger?.LogInformation("Successfully installed {PackageId} v{Version}", packageId, resolvedVersion);

            return new PackageInstallResult
            {
                Success = true,
                Package = installedPackage,
                InstalledDependencies = installedDeps,
                Warnings = warnings
            };
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to install package {PackageId}", packageId);
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
        var newVersion = targetVersion ?? await ResolveLatestVersionAsync(packageId, prerelease: false, cancellationToken);

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
        var compat = await _dependencyResolver.CheckUpdateCompatibilityAsync(
            packageId, newVersion, _installedPackages.Values, cancellationToken);

        if (!compat.IsCompatible)
        {
            return new PackageUpdateResult
            {
                Success = false,
                PreviousVersion = previousVersion,
                Errors = compat.Conflicts.Select(c =>
                    $"Conflict: {c.PackageId} requires {c.RequestedVersion} but {c.InstalledVersion} is installed").ToList()
            };
        }

        // Unload old version — try both NuGet package ID and plugin attribute ID
        var oldPlugin = _pluginLoader.GetPlugin(packageId);
        _pluginLoader.UnloadPlugin(packageId);
        foreach (var loadedPlugin in _pluginLoader.GetLoadedPlugins())
        {
            if (string.Equals(loadedPlugin.FilePath, existing.InstallPath, StringComparison.OrdinalIgnoreCase) ||
                loadedPlugin.FilePath.StartsWith(existing.InstallPath, StringComparison.OrdinalIgnoreCase))
            {
                oldPlugin ??= loadedPlugin;
                _pluginLoader.UnloadPlugin(loadedPlugin.Id);
            }
        }
        if (oldPlugin?.Assembly is not null)
        {
            _nodeRegistry.UnregisterFromAssembly(oldPlugin.Assembly);
        }

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
        await _packageCache.RemovePackageAsync(packageId, previousVersion);

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
        var affectedWorkflows = new List<string>();
        if (_workflowRepository is not null && package.NodeTypes.Count > 0)
        {
            try
            {
                var workflows = await _workflowRepository.GetAllAsync(0, int.MaxValue, cancellationToken);
                foreach (var workflow in workflows)
                {
                    var usesPackageNode = workflow.Nodes.Any(n =>
                        package.NodeTypes.Any(nt => string.Equals(n.Type, nt, StringComparison.OrdinalIgnoreCase)));

                    if (usesPackageNode)
                    {
                        affectedWorkflows.Add(workflow.Name);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "Failed to check workflow references for {PackageId}", packageId);
            }
        }

        if (affectedWorkflows.Count > 0 && !force)
        {
            return new PackageUninstallResult
            {
                Success = false,
                PackageId = packageId,
                AffectedWorkflows = affectedWorkflows,
                Errors = [$"Package '{packageId}' is referenced by {affectedWorkflows.Count} workflow(s). Use force=true to uninstall anyway."]
            };
        }

        try
        {
            // Unregister nodes
            foreach (var nodeType in package.NodeTypes)
            {
                _nodeRegistry.Unregister(nodeType);
            }

            // Unload plugin — try both the NuGet package ID and scan loaded plugins by install path
            _pluginLoader.UnloadPlugin(packageId);
            foreach (var loadedPlugin in _pluginLoader.GetLoadedPlugins())
            {
                if (string.Equals(loadedPlugin.FilePath, package.InstallPath, StringComparison.OrdinalIgnoreCase) ||
                    loadedPlugin.FilePath.StartsWith(package.InstallPath, StringComparison.OrdinalIgnoreCase))
                {
                    _pluginLoader.UnloadPlugin(loadedPlugin.Id);
                }
            }

            // Remove from cache
            await _packageCache.RemovePackageAsync(packageId, package.Version);

            // Remove from manifest
            await _manifestManager.RemovePackageAsync(packageId);

            // Remove from in-memory tracking
            _installedPackages.TryRemove(packageId, out _);

            // Remove orphaned dependencies
            var removedDeps = new List<string>();
            foreach (var depId in package.Dependencies)
            {
                var stillNeeded = _installedPackages.Values.Any(p =>
                    p.Dependencies.Any(d => string.Equals(d, depId, StringComparison.OrdinalIgnoreCase)));

                if (!stillNeeded && _installedPackages.TryRemove(depId, out var depPackage))
                {
                    await _packageCache.RemovePackageAsync(depId, depPackage.Version);
                    await _manifestManager.RemovePackageAsync(depId);
                    removedDeps.Add(depId);
                }
            }

            _logger?.LogInformation("Uninstalled {PackageId} v{Version}", packageId, package.Version);

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
            _logger?.LogError(ex, "Failed to uninstall {PackageId}", packageId);
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
        var updates = new List<PackageUpdateInfo>();

        foreach (var package in _installedPackages.Values)
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
                _logger?.LogDebug(ex, "Failed to check updates for {PackageId}", package.PackageId);
            }
        }

        return updates;
    }

    public async Task<PackageInstallResult> InstallFromStreamAsync(
        Stream nupkgStream,
        string fileName,
        CancellationToken cancellationToken = default)
    {
        _logger?.LogInformation("Installing package from uploaded file: {FileName}", fileName);

        try
        {
            // Save the stream to a temp file
            var tempPath = Path.Combine(_options.CacheDirectory, $"upload-{Guid.NewGuid():N}.nupkg");
            Directory.CreateDirectory(_options.CacheDirectory);

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

                _logger?.LogInformation("Uploaded package: {PackageId} v{Version}", packageId, version);

                // Check block/allow lists
                if (_options.BlockedPackages.Any(b => string.Equals(b, packageId, StringComparison.OrdinalIgnoreCase)))
                {
                    return new PackageInstallResult
                    {
                        Success = false,
                        Errors = [$"Package '{packageId}' is in the block list"]
                    };
                }

                if (_options.AllowedPackages.Count > 0 &&
                    !_options.AllowedPackages.Any(a => string.Equals(a, packageId, StringComparison.OrdinalIgnoreCase)))
                {
                    return new PackageInstallResult
                    {
                        Success = false,
                        Errors = [$"Package '{packageId}' is not in the allow list"]
                    };
                }

                // Move to the proper cache location
                var nupkgPath = Path.Combine(
                    _options.CacheDirectory,
                    $"{packageId.ToLowerInvariant()}.{version.ToNormalizedString()}.nupkg");

                if (File.Exists(nupkgPath))
                    File.Delete(nupkgPath);

                File.Move(tempPath, nupkgPath);
                tempPath = nupkgPath; // update so cleanup targets the right file on error

                // Extract
                var installPath = await _packageCache.ExtractPackageAsync(
                    nupkgPath, packageId, version, cancellationToken);

                // Load and validate plugin
                var nodeTypes = new List<string>();
                var warnings = new List<string>();

                try
                {
                    var plugins = _pluginLoader.LoadPlugins(installPath);
                    foreach (var plugin in plugins)
                    {
                        if (plugin.Assembly is not null)
                        {
                            var validation = _pluginValidator.ValidatePlugin(plugin.Assembly);
                            if (!validation.IsValid)
                            {
                                await _packageCache.RemovePackageAsync(packageId, version);
                                return new PackageInstallResult
                                {
                                    Success = false,
                                    Errors = validation.Errors.Select(e => $"Plugin validation failed: {e.Message}").ToList()
                                };
                            }

                            _nodeRegistry.RegisterFromAssembly(plugin.Assembly);

                            foreach (var nodeType in plugin.NodeTypes)
                            {
                                try
                                {
                                    if (Activator.CreateInstance(nodeType) is INode nodeInstance)
                                        nodeTypes.Add(nodeInstance.Type);
                                }
                                catch
                                {
                                    // Skip types that can't be instantiated
                                }
                            }

                            warnings.AddRange(validation.Warnings.Select(w => w.Message));
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger?.LogDebug(ex, "No plugin assembly found in {PackageId}, treating as library", packageId);
                }

                var installedPackage = new InstalledPackage
                {
                    PackageId = packageId,
                    Version = version,
                    SourceName = "local-upload",
                    InstallPath = installPath,
                    InstalledAt = DateTime.UtcNow,
                    NodeTypes = nodeTypes,
                    Dependencies = [],
                    IsLoaded = true
                };

                _installedPackages[packageId] = installedPackage;
                await _manifestManager.AddPackageAsync(installedPackage);

                _logger?.LogInformation("Successfully installed uploaded package {PackageId} v{Version}", packageId, version);

                return new PackageInstallResult
                {
                    Success = true,
                    Package = installedPackage,
                    Warnings = warnings
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
            _logger?.LogError(ex, "Failed to install uploaded package {FileName}", fileName);
            return new PackageInstallResult
            {
                Success = false,
                Errors = [$"Failed to install uploaded package: {ex.Message}"]
            };
        }
    }

    private async Task<NuGetVersion?> ResolveLatestVersionAsync(
        string packageId, bool prerelease, CancellationToken cancellationToken)
    {
        foreach (var source in _sourceService.GetSources().Where(s => s.IsEnabled))
        {
            try
            {
                var repository = _sourceService.GetRepository(source);
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
                _logger?.LogDebug(ex, "Failed to resolve latest version for {PackageId} from {Source}", packageId, source.Name);
            }
        }

        return null;
    }

    private async Task<(SourceRepository? Repository, PackageSource? Source)> FindPackageSourceAsync(
        string packageId, NuGetVersion version, CancellationToken cancellationToken)
    {
        foreach (var source in _sourceService.GetSources().Where(s => s.IsEnabled))
        {
            try
            {
                var repository = _sourceService.GetRepository(source);
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
