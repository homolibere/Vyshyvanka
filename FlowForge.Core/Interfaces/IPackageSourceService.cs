using NuGet.Protocol.Core.Types;

namespace FlowForge.Core.Interfaces;

/// <summary>
/// Manages NuGet feed configuration and connectivity.
/// </summary>
public interface IPackageSourceService
{
    /// <summary>
    /// Gets all configured package sources.
    /// </summary>
    /// <returns>Read-only list of configured sources.</returns>
    IReadOnlyList<PackageSource> GetSources();

    /// <summary>
    /// Adds a new package source.
    /// </summary>
    /// <param name="config">Configuration for the new source.</param>
    /// <returns>The created package source.</returns>
    Task<PackageSource> AddSourceAsync(PackageSourceConfig config);

    /// <summary>
    /// Removes a package source.
    /// </summary>
    /// <param name="sourceName">Name of the source to remove.</param>
    Task RemoveSourceAsync(string sourceName);

    /// <summary>
    /// Updates a package source configuration.
    /// </summary>
    /// <param name="sourceName">Name of the source to update.</param>
    /// <param name="config">New configuration values.</param>
    Task UpdateSourceAsync(string sourceName, PackageSourceConfig config);

    /// <summary>
    /// Tests connectivity to a package source.
    /// </summary>
    /// <param name="sourceName">Name of the source to test.</param>
    /// <returns>Test result with connectivity status.</returns>
    Task<SourceTestResult> TestSourceAsync(string sourceName);

    /// <summary>
    /// Gets the NuGet source repository for a package source.
    /// </summary>
    /// <param name="source">The package source.</param>
    /// <returns>NuGet source repository for API operations.</returns>
    SourceRepository GetRepository(PackageSource source);
}

/// <summary>
/// Represents a configured NuGet package source.
/// </summary>
public record PackageSource
{
    /// <summary>Unique name for this source.</summary>
    public required string Name { get; init; }

    /// <summary>URL of the NuGet feed.</summary>
    public required string Url { get; init; }

    /// <summary>Whether this source is enabled for searches and installs.</summary>
    public bool IsEnabled { get; init; } = true;

    /// <summary>Whether this source is trusted (no confirmation required).</summary>
    public bool IsTrusted { get; init; }

    /// <summary>Credentials for authenticated sources.</summary>
    public PackageSourceCredentials? Credentials { get; init; }

    /// <summary>Priority for source ordering (lower = higher priority).</summary>
    public int Priority { get; init; }
}

/// <summary>
/// Credentials for authenticated package sources.
/// </summary>
public record PackageSourceCredentials
{
    /// <summary>Username for basic authentication.</summary>
    public string? Username { get; init; }

    /// <summary>Password for basic authentication.</summary>
    public string? Password { get; init; }

    /// <summary>API key for token-based authentication.</summary>
    public string? ApiKey { get; init; }
}

/// <summary>
/// Configuration for adding or updating a package source.
/// </summary>
public record PackageSourceConfig
{
    /// <summary>Unique name for this source.</summary>
    public required string Name { get; init; }

    /// <summary>URL of the NuGet feed.</summary>
    public required string Url { get; init; }

    /// <summary>Whether this source is enabled.</summary>
    public bool IsEnabled { get; init; } = true;

    /// <summary>Whether this source is trusted.</summary>
    public bool IsTrusted { get; init; }

    /// <summary>Credentials for authenticated sources.</summary>
    public PackageSourceCredentials? Credentials { get; init; }

    /// <summary>Priority for source ordering.</summary>
    public int Priority { get; init; }
}

/// <summary>
/// Result of testing connectivity to a package source.
/// </summary>
public record SourceTestResult
{
    /// <summary>Whether the connection test succeeded.</summary>
    public bool Success { get; init; }

    /// <summary>Name of the tested source.</summary>
    public required string SourceName { get; init; }

    /// <summary>Response time in milliseconds.</summary>
    public long ResponseTimeMs { get; init; }

    /// <summary>Error message if the test failed.</summary>
    public string? ErrorMessage { get; init; }
}
