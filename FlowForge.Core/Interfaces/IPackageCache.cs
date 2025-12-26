using NuGet.Protocol.Core.Types;
using NuGet.Versioning;

namespace FlowForge.Core.Interfaces;

/// <summary>
/// Manages local package storage and extraction.
/// </summary>
public interface IPackageCache
{
    /// <summary>
    /// Gets the path to a cached package, downloading if necessary.
    /// </summary>
    /// <param name="packageId">The package identifier.</param>
    /// <param name="version">The package version.</param>
    /// <param name="source">The source repository to download from.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Path to the cached package file.</returns>
    Task<string> GetPackagePathAsync(
        string packageId,
        NuGetVersion version,
        SourceRepository source,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Extracts package contents to the plugins directory.
    /// </summary>
    /// <param name="packagePath">Path to the package file.</param>
    /// <param name="packageId">The package identifier.</param>
    /// <param name="version">The package version.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Path to the extracted package directory.</returns>
    Task<string> ExtractPackageAsync(
        string packagePath,
        string packageId,
        NuGetVersion version,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Removes a package from the cache.
    /// </summary>
    /// <param name="packageId">The package identifier.</param>
    /// <param name="version">The package version.</param>
    Task RemovePackageAsync(string packageId, NuGetVersion version);

    /// <summary>
    /// Gets the extraction path for a package.
    /// </summary>
    /// <param name="packageId">The package identifier.</param>
    /// <param name="version">The package version.</param>
    /// <returns>Path where the package would be extracted.</returns>
    string GetExtractionPath(string packageId, NuGetVersion version);

    /// <summary>
    /// Cleans up orphaned cache entries not in the installed packages list.
    /// </summary>
    /// <param name="installedPackages">Currently installed packages to preserve.</param>
    Task CleanupAsync(IEnumerable<InstalledPackage> installedPackages);
}
