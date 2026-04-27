using System.Text.Json;
using System.Text.Json.Serialization;
using FlowForge.Core.Interfaces;
using Microsoft.Extensions.Logging;
using NuGet.Versioning;

namespace FlowForge.Engine.Packages;

/// <summary>
/// Persists installed package information and source configuration to a JSON manifest on disk.
/// </summary>
public class ManifestManager : IManifestManager
{
    private readonly string _manifestPath;
    private readonly string _cacheDirectory;
    private readonly ILogger<ManifestManager>? _logger;
    private readonly Lock _lock = new();

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters = { new NuGetVersionJsonConverter() }
    };

    public ManifestManager(string manifestPath, string cacheDirectory, ILogger<ManifestManager>? logger = null)
    {
        _manifestPath = manifestPath ?? throw new ArgumentNullException(nameof(manifestPath));
        _cacheDirectory = cacheDirectory ?? throw new ArgumentNullException(nameof(cacheDirectory));
        _logger = logger;
    }

    public async Task<PackageManifest> LoadAsync()
    {
        lock (_lock)
        {
            if (!File.Exists(_manifestPath))
            {
                _logger?.LogDebug("Manifest file not found at {Path}, returning empty manifest", _manifestPath);
                return CreateDefaultManifest();
            }
        }

        try
        {
            var json = await File.ReadAllTextAsync(_manifestPath);
            var manifest = JsonSerializer.Deserialize<PackageManifest>(json, JsonOptions);
            return manifest ?? CreateDefaultManifest();
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Failed to load manifest from {Path}, returning empty manifest", _manifestPath);
            return CreateDefaultManifest();
        }
    }

    public async Task SaveAsync(PackageManifest manifest)
    {
        ArgumentNullException.ThrowIfNull(manifest);

        var directory = Path.GetDirectoryName(_manifestPath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var updated = manifest with { LastModified = DateTime.UtcNow };
        var json = JsonSerializer.Serialize(updated, JsonOptions);

        lock (_lock)
        {
            File.WriteAllText(_manifestPath, json);
        }

        _logger?.LogDebug("Saved manifest with {Count} packages to {Path}", manifest.Packages.Count, _manifestPath);
        await Task.CompletedTask;
    }

    public async Task AddPackageAsync(InstalledPackage package)
    {
        ArgumentNullException.ThrowIfNull(package);

        var manifest = await LoadAsync();
        var packages = manifest.Packages.ToList();
        packages.RemoveAll(p => string.Equals(p.PackageId, package.PackageId, StringComparison.OrdinalIgnoreCase));
        packages.Add(package);

        await SaveAsync(manifest with { Packages = packages });
        _logger?.LogInformation("Added package {PackageId} v{Version} to manifest", package.PackageId, package.Version);
    }

    public async Task RemovePackageAsync(string packageId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(packageId);

        var manifest = await LoadAsync();
        var packages = manifest.Packages.ToList();
        var removed = packages.RemoveAll(p => string.Equals(p.PackageId, packageId, StringComparison.OrdinalIgnoreCase));

        if (removed > 0)
        {
            await SaveAsync(manifest with { Packages = packages });
            _logger?.LogInformation("Removed package {PackageId} from manifest", packageId);
        }
    }

    public async Task UpdatePackageAsync(InstalledPackage package)
    {
        await AddPackageAsync(package);
    }

    private PackageManifest CreateDefaultManifest() => new()
    {
        Version = 1,
        LastModified = DateTime.UtcNow,
        Packages = [],
        Sources =
        [
            new PackageSource
            {
                Name = "nuget.org",
                Url = "https://api.nuget.org/v3/index.json",
                IsEnabled = true,
                IsTrusted = true
            }
        ]
    };
}
