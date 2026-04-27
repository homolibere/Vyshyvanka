namespace FlowForge.Core.Interfaces;

/// <summary>
/// Persists installed package information to disk.
/// </summary>
public interface IManifestManager
{
    /// <summary>
    /// Loads the package manifest from disk.
    /// </summary>
    /// <returns>The loaded manifest, or a new empty manifest if none exists.</returns>
    Task<PackageManifest> LoadAsync();

    /// <summary>
    /// Saves the package manifest to disk.
    /// </summary>
    /// <param name="manifest">The manifest to save.</param>
    Task SaveAsync(PackageManifest manifest);

    /// <summary>
    /// Adds a package to the manifest.
    /// </summary>
    /// <param name="package">The package to add.</param>
    Task AddPackageAsync(InstalledPackage package);

    /// <summary>
    /// Removes a package from the manifest.
    /// </summary>
    /// <param name="packageId">The package identifier to remove.</param>
    Task RemovePackageAsync(string packageId);

    /// <summary>
    /// Updates a package in the manifest.
    /// </summary>
    /// <param name="package">The updated package information.</param>
    Task UpdatePackageAsync(InstalledPackage package);
}

/// <summary>
/// The persisted manifest of installed packages and sources.
/// </summary>
public record PackageManifest
{
    /// <summary>Manifest format version.</summary>
    public int Version { get; init; } = 1;

    /// <summary>When the manifest was last modified.</summary>
    public DateTime LastModified { get; init; }

    /// <summary>Installed packages.</summary>
    public IReadOnlyList<InstalledPackage> Packages { get; init; } = [];

    /// <summary>Configured package sources.</summary>
    public IReadOnlyList<PackageSource> Sources { get; init; } = [];
}
