using System.Text.Json;
using FlowForge.Core.Interfaces;
using Microsoft.Extensions.Logging;
using NuGet.Protocol;
using NuGet.Protocol.Core.Types;
using SourceRepository = NuGet.Protocol.Core.Types.SourceRepository;

namespace FlowForge.Engine.Packages;

/// <summary>
/// Manages NuGet feed configuration and connectivity, persisted via the manifest.
/// </summary>
public class PackageSourceService : IPackageSourceService
{
    private readonly IManifestManager _manifestManager;
    private readonly ICredentialEncryption? _credentialEncryption;
    private readonly ILogger<PackageSourceService>? _logger;
    private List<PackageSource> _sources = [];

    public PackageSourceService(
        IManifestManager manifestManager,
        ICredentialEncryption? credentialEncryption = null,
        ILogger<PackageSourceService>? logger = null)
    {
        _manifestManager = manifestManager ?? throw new ArgumentNullException(nameof(manifestManager));
        _credentialEncryption = credentialEncryption;
        _logger = logger;

        // Load sources synchronously on construction
        var manifest = _manifestManager.LoadAsync().GetAwaiter().GetResult();
        _sources = manifest.Sources.ToList();

        if (_sources.Count == 0)
        {
            _sources.Add(new PackageSource
            {
                Name = "nuget.org",
                Url = "https://api.nuget.org/v3/index.json",
                IsEnabled = true,
                IsTrusted = true
            });
        }
    }

    public IReadOnlyList<PackageSource> GetSources() => _sources.AsReadOnly();

    public async Task<PackageSource> AddSourceAsync(PackageSourceConfig config)
    {
        ArgumentNullException.ThrowIfNull(config);
        ArgumentException.ThrowIfNullOrWhiteSpace(config.Name);
        ArgumentException.ThrowIfNullOrWhiteSpace(config.Url);

        if (!Uri.TryCreate(config.Url, UriKind.Absolute, out _))
        {
            throw new ArgumentException($"Invalid URL: '{config.Url}'", nameof(config));
        }

        var source = new PackageSource
        {
            Name = config.Name,
            Url = config.Url,
            IsEnabled = config.IsEnabled,
            IsTrusted = config.IsTrusted,
            Credentials = config.Credentials,
            Priority = config.Priority
        };

        _sources.Add(source);
        await PersistSourcesAsync();

        _logger?.LogInformation("Added package source: {Name} ({Url})", source.Name, source.Url);
        return source;
    }

    public async Task RemoveSourceAsync(string sourceName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceName);

        var removed = _sources.RemoveAll(s => string.Equals(s.Name, sourceName, StringComparison.OrdinalIgnoreCase));
        if (removed > 0)
        {
            await PersistSourcesAsync();
            _logger?.LogInformation("Removed package source: {Name}", sourceName);
        }
    }

    public async Task UpdateSourceAsync(string sourceName, PackageSourceConfig config)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceName);
        ArgumentNullException.ThrowIfNull(config);

        var index = _sources.FindIndex(s => string.Equals(s.Name, sourceName, StringComparison.OrdinalIgnoreCase));
        if (index < 0)
        {
            throw new KeyNotFoundException($"Package source '{sourceName}' not found");
        }

        _sources[index] = new PackageSource
        {
            Name = config.Name,
            Url = config.Url,
            IsEnabled = config.IsEnabled,
            IsTrusted = config.IsTrusted,
            Credentials = config.Credentials,
            Priority = config.Priority
        };

        await PersistSourcesAsync();
        _logger?.LogInformation("Updated package source: {Name}", config.Name);
    }

    public async Task<SourceTestResult> TestSourceAsync(string sourceName)
    {
        var source = _sources.FirstOrDefault(s => string.Equals(s.Name, sourceName, StringComparison.OrdinalIgnoreCase));
        if (source is null)
        {
            return new SourceTestResult
            {
                SourceName = sourceName,
                Success = false,
                ErrorMessage = $"Source '{sourceName}' not found"
            };
        }

        var sw = System.Diagnostics.Stopwatch.StartNew();
        try
        {
            var repository = GetRepository(source);
            var resource = await repository.GetResourceAsync<ServiceIndexResourceV3>(CancellationToken.None);
            sw.Stop();

            return new SourceTestResult
            {
                SourceName = sourceName,
                Success = resource is not null,
                ResponseTimeMs = sw.ElapsedMilliseconds,
                ErrorMessage = resource is null ? "Failed to retrieve service index" : null
            };
        }
        catch (Exception ex)
        {
            sw.Stop();
            _logger?.LogWarning(ex, "Source connectivity test failed for {Name}", sourceName);
            return new SourceTestResult
            {
                SourceName = sourceName,
                Success = false,
                ResponseTimeMs = sw.ElapsedMilliseconds,
                ErrorMessage = ex.Message
            };
        }
    }

    public SourceRepository GetRepository(PackageSource source)
    {
        var nugetSource = new NuGet.Configuration.PackageSource(source.Url, source.Name);

        if (source.Credentials is not null)
        {
            if (!string.IsNullOrEmpty(source.Credentials.Username))
            {
                nugetSource.Credentials = new NuGet.Configuration.PackageSourceCredential(
                    source.Name,
                    source.Credentials.Username,
                    source.Credentials.Password ?? string.Empty,
                    isPasswordClearText: true,
                    validAuthenticationTypesText: null);
            }
        }

        return Repository.Factory.GetCoreV3(nugetSource);
    }

    private async Task PersistSourcesAsync()
    {
        var manifest = await _manifestManager.LoadAsync();
        var updated = manifest with { Sources = _sources.OrderBy(s => s.Priority).ToList() };
        await _manifestManager.SaveAsync(updated);
    }
}
