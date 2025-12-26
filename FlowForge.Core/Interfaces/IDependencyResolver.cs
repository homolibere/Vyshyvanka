using NuGet.Versioning;

namespace FlowForge.Core.Interfaces;

/// <summary>
/// Resolves package dependencies and detects version conflicts.
/// </summary>
public interface IDependencyResolver
{
    /// <summary>
    /// Resolves all dependencies for a package.
    /// </summary>
    /// <param name="packageId">The package identifier.</param>
    /// <param name="version">The package version.</param>
    /// <param name="installedPackages">Currently installed packages.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Resolution result with dependencies or conflicts.</returns>
    Task<DependencyResolutionResult> ResolveAsync(
        string packageId,
        NuGetVersion version,
        IEnumerable<InstalledPackage> installedPackages,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks if a package update would cause conflicts with installed packages.
    /// </summary>
    /// <param name="packageId">The package identifier to update.</param>
    /// <param name="newVersion">The target version.</param>
    /// <param name="installedPackages">Currently installed packages.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Compatibility result with any conflicts.</returns>
    Task<CompatibilityResult> CheckUpdateCompatibilityAsync(
        string packageId,
        NuGetVersion newVersion,
        IEnumerable<InstalledPackage> installedPackages,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Result of dependency resolution.
/// </summary>
public record DependencyResolutionResult
{
    /// <summary>Whether resolution succeeded without conflicts.</summary>
    public bool Success { get; init; }

    /// <summary>Resolved dependencies to install.</summary>
    public IReadOnlyList<PackageDependency> Dependencies { get; init; } = [];

    /// <summary>Version conflicts that prevent installation.</summary>
    public IReadOnlyList<DependencyConflict> Conflicts { get; init; } = [];
}

/// <summary>
/// A resolved package dependency.
/// </summary>
public record PackageDependency
{
    /// <summary>Package identifier.</summary>
    public required string PackageId { get; init; }

    /// <summary>Resolved version to install.</summary>
    public required NuGetVersion Version { get; init; }

    /// <summary>Whether this dependency is already installed.</summary>
    public bool IsAlreadyInstalled { get; init; }
}

/// <summary>
/// A dependency version conflict.
/// </summary>
public record DependencyConflict
{
    /// <summary>Package identifier with the conflict.</summary>
    public required string PackageId { get; init; }

    /// <summary>Version requested by the new package.</summary>
    public required NuGetVersion RequestedVersion { get; init; }

    /// <summary>Version currently installed.</summary>
    public required NuGetVersion InstalledVersion { get; init; }

    /// <summary>Package that requested the conflicting version.</summary>
    public required string RequestedBy { get; init; }
}

/// <summary>
/// Result of checking update compatibility.
/// </summary>
public record CompatibilityResult
{
    /// <summary>Whether the update is compatible.</summary>
    public bool IsCompatible { get; init; }

    /// <summary>Conflicts that would occur with the update.</summary>
    public IReadOnlyList<DependencyConflict> Conflicts { get; init; } = [];

    /// <summary>Warnings about potential issues.</summary>
    public IReadOnlyList<string> Warnings { get; init; } = [];
}
