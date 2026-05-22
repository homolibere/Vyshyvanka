using NuGet.Versioning;

namespace Vyshyvanka.Core.Interfaces;

/// <summary>
/// Provides package search and discovery operations against configured NuGet sources.
/// </summary>
public interface IPackageSearchService
{
    /// <summary>
    /// Searches for packages matching the query across configured sources.
    /// </summary>
    /// <param name="query">Search query string.</param>
    /// <param name="options">Optional search options.</param>
    /// <param name="installedPackages">Currently installed packages for enriching results.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Search results with matching packages.</returns>
    Task<PackageSearchResult> SearchPackagesAsync(
        string query,
        PackageSearchOptions? options = null,
        IReadOnlyDictionary<string, InstalledPackage>? installedPackages = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets detailed information about a specific package.
    /// </summary>
    /// <param name="packageId">The package identifier.</param>
    /// <param name="version">Optional specific version to retrieve.</param>
    /// <param name="installedPackages">Currently installed packages for enriching results.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Package details, or null if not found.</returns>
    Task<PackageDetails?> GetPackageDetailsAsync(
        string packageId,
        NuGetVersion? version = null,
        IReadOnlyDictionary<string, InstalledPackage>? installedPackages = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks for available updates to the given installed packages.
    /// </summary>
    /// <param name="installedPackages">Packages to check for updates.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>List of available updates.</returns>
    Task<IReadOnlyList<PackageUpdateInfo>> CheckForUpdatesAsync(
        IEnumerable<InstalledPackage> installedPackages,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Resolves the latest available version of a package.
    /// </summary>
    /// <param name="packageId">The package identifier.</param>
    /// <param name="prerelease">Whether to include prerelease versions.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The latest version, or null if not found.</returns>
    Task<NuGetVersion?> ResolveLatestVersionAsync(
        string packageId,
        bool prerelease,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Finds the source repository that contains a specific package version.
    /// </summary>
    /// <param name="packageId">The package identifier.</param>
    /// <param name="version">The package version.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The source repository and package source, or nulls if not found.</returns>
    Task<(NuGet.Protocol.Core.Types.SourceRepository? Repository, PackageSource? Source)> FindPackageSourceAsync(
        string packageId,
        NuGetVersion version,
        CancellationToken cancellationToken = default);
}
