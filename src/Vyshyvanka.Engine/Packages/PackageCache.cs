using Vyshyvanka.Core.Interfaces;
using Microsoft.Extensions.Logging;
using NuGet.Common;
using NuGet.Packaging;
using NuGet.Protocol.Core.Types;
using NuGet.Versioning;

namespace Vyshyvanka.Engine.Packages;

/// <summary>
/// Manages local package storage: downloading .nupkg files and extracting their contents.
/// </summary>
public class PackageCache : IPackageCache
{
    private readonly string _cacheDirectory;
    private readonly ILogger<PackageCache>? _logger;

    public PackageCache(string cacheDirectory, ILogger<PackageCache>? logger = null)
    {
        _cacheDirectory = cacheDirectory ?? throw new ArgumentNullException(nameof(cacheDirectory));
        _logger = logger;
        Directory.CreateDirectory(_cacheDirectory);
    }

    public async Task<string> GetPackagePathAsync(
        string packageId,
        NuGetVersion version,
        SourceRepository source,
        CancellationToken cancellationToken = default)
    {
        var nupkgPath = GetNupkgPath(packageId, version);

        if (File.Exists(nupkgPath))
        {
            _logger?.LogDebug("Package {PackageId} v{Version} found in cache", packageId, version);
            return nupkgPath;
        }

        _logger?.LogInformation("Downloading {PackageId} v{Version} from {Source}", packageId, version,
            source.PackageSource.Source);

        var findResource = await source.GetResourceAsync<FindPackageByIdResource>(cancellationToken);
        Directory.CreateDirectory(Path.GetDirectoryName(nupkgPath)!);

        await using var stream = File.Create(nupkgPath);
        var copied = await findResource.CopyNupkgToStreamAsync(
            packageId, version, stream, new SourceCacheContext(), NullLogger.Instance, cancellationToken);

        if (!copied)
        {
            File.Delete(nupkgPath);
            throw new InvalidOperationException($"Failed to download package {packageId} v{version}");
        }

        _logger?.LogDebug("Downloaded {PackageId} v{Version} to {Path}", packageId, version, nupkgPath);
        return nupkgPath;
    }

    public async Task<string> ExtractPackageAsync(
        string packagePath,
        string packageId,
        NuGetVersion version,
        CancellationToken cancellationToken = default)
    {
        var extractionPath = GetExtractionPath(packageId, version);

        if (Directory.Exists(extractionPath))
        {
            // Verify the directory actually contains DLL files; if empty or stale
            // (e.g. partial cleanup during uninstall), delete and re-extract.
            var hasFiles = Directory.EnumerateFiles(extractionPath, "*.dll").Any();
            if (hasFiles)
            {
                _logger?.LogDebug("Package {PackageId} v{Version} already extracted", packageId, version);
                return extractionPath;
            }

            _logger?.LogWarning(
                "Extraction directory for {PackageId} v{Version} exists but contains no DLLs, re-extracting", packageId,
                version);
            Directory.Delete(extractionPath, recursive: true);
        }

        Directory.CreateDirectory(extractionPath);

        _logger?.LogDebug("Extracting {PackageId} v{Version} to {Path}", packageId, version, extractionPath);

        using var packageReader = new PackageArchiveReader(packagePath);
        var libItems = (await packageReader.GetLibItemsAsync(cancellationToken)).ToList();

        // Find the best matching target framework group
        var targetGroup = libItems
            .OrderByDescending(g => g.TargetFramework.Version)
            .FirstOrDefault();

        if (targetGroup is not null)
        {
            foreach (var item in targetGroup.Items)
            {
                var targetPath = Path.Combine(extractionPath, Path.GetFileName(item));
                using var entryStream = await packageReader.GetStreamAsync(item, cancellationToken);
                await using var fileStream = File.Create(targetPath);
                await entryStream.CopyToAsync(fileStream, cancellationToken);
            }
        }

        return extractionPath;
    }

    public Task RemovePackageAsync(string packageId, NuGetVersion version)
    {
        var nupkgPath = GetNupkgPath(packageId, version);
        if (File.Exists(nupkgPath))
        {
            File.Delete(nupkgPath);
        }

        var extractionPath = GetExtractionPath(packageId, version);
        if (Directory.Exists(extractionPath))
        {
            Directory.Delete(extractionPath, recursive: true);
        }

        _logger?.LogDebug("Removed cached package {PackageId} v{Version}", packageId, version);
        return Task.CompletedTask;
    }

    public string GetExtractionPath(string packageId, NuGetVersion version)
    {
        return Path.Combine(_cacheDirectory, $"{packageId.ToLowerInvariant()}.{version.ToNormalizedString()}");
    }

    public Task CleanupAsync(IEnumerable<InstalledPackage> installedPackages)
    {
        var keepPaths = new HashSet<string>(
            installedPackages.Select(p => GetExtractionPath(p.PackageId, p.Version)),
            StringComparer.OrdinalIgnoreCase);

        if (!Directory.Exists(_cacheDirectory)) return Task.CompletedTask;

        foreach (var dir in Directory.GetDirectories(_cacheDirectory))
        {
            if (!keepPaths.Contains(dir))
            {
                try
                {
                    Directory.Delete(dir, recursive: true);
                    _logger?.LogDebug("Cleaned up orphaned cache entry: {Path}", dir);
                }
                catch (Exception ex)
                {
                    _logger?.LogWarning(ex, "Failed to clean up cache entry: {Path}", dir);
                }
            }
        }

        return Task.CompletedTask;
    }

    private string GetNupkgPath(string packageId, NuGetVersion version)
    {
        return Path.Combine(_cacheDirectory, $"{packageId.ToLowerInvariant()}.{version.ToNormalizedString()}.nupkg");
    }
}
