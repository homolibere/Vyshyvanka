using NuGet.Versioning;

namespace Vyshyvanka.Core.Interfaces;

/// <summary>
/// Result of loading and validating plugins from a package.
/// </summary>
/// <param name="NodeTypes">Node type identifiers discovered in the plugin.</param>
/// <param name="Warnings">Validation warnings.</param>
/// <param name="Failure">If non-null, the plugin load/validation failed.</param>
public record PluginLoadResult(
    List<string>? NodeTypes = null,
    List<string>? Warnings = null,
    PackageInstallResult? Failure = null)
{
    /// <summary>Node type identifiers discovered in the plugin.</summary>
    public List<string> NodeTypes { get; } = NodeTypes ?? [];

    /// <summary>Validation warnings.</summary>
    public List<string> Warnings { get; } = Warnings ?? [];
}

/// <summary>
/// Handles loading, validating, and unloading plugin assemblies from packages.
/// Coordinates between the plugin loader, validator, and node registry.
/// </summary>
public interface IPluginLoadingService
{
    /// <summary>
    /// Loads and validates plugins from a package install path, registering discovered nodes.
    /// </summary>
    /// <param name="packageId">The package identifier.</param>
    /// <param name="version">The package version.</param>
    /// <param name="installPath">Path where the package is extracted.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Result containing discovered node types, warnings, or failure details.</returns>
    Task<PluginLoadResult> LoadAndValidatePluginsAsync(
        string packageId,
        NuGetVersion version,
        string installPath,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Unloads existing plugins for a package, including any loaded from the given install path.
    /// </summary>
    /// <param name="packageId">The package identifier.</param>
    /// <param name="installPath">The install path to match loaded plugins against.</param>
    void UnloadPlugins(string packageId, string installPath);

    /// <summary>
    /// Unloads a plugin and unregisters its nodes from the node registry.
    /// </summary>
    /// <param name="packageId">The package identifier.</param>
    /// <param name="installPath">The install path to match loaded plugins against.</param>
    void UnloadAndUnregisterPlugins(string packageId, string installPath);

    /// <summary>
    /// Resolves node type identifiers from a collection of node types by instantiating them.
    /// </summary>
    /// <param name="nodeTypes">The node types to resolve.</param>
    /// <returns>List of node type identifier strings.</returns>
    List<string> ResolveNodeTypeIdentifiers(IEnumerable<Type> nodeTypes);

    /// <summary>
    /// Loads plugins from a path during initialization, registering their assemblies.
    /// Does not perform full validation — used for already-installed packages on startup.
    /// </summary>
    /// <param name="packageId">The package identifier.</param>
    /// <param name="installPath">Path where the package is extracted.</param>
    /// <returns>True if plugins were loaded successfully, false otherwise.</returns>
    bool TryLoadPluginsForInitialization(string packageId, string installPath);
}
