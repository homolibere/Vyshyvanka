using Vyshyvanka.Core.Interfaces;
using Microsoft.Extensions.Logging;
using NuGet.Versioning;

namespace Vyshyvanka.Engine.Packages;

/// <summary>
/// Handles loading, validating, and unloading plugin assemblies from packages.
/// Coordinates between the plugin loader, validator, and node registry.
/// </summary>
public class PluginLoadingService(
    IPluginLoader pluginLoader,
    IPluginValidator pluginValidator,
    INodeRegistry nodeRegistry,
    IPackageCache packageCache,
    ILogger<PluginLoadingService>? logger = null) : IPluginLoadingService
{
    public async Task<PluginLoadResult> LoadAndValidatePluginsAsync(
        string packageId,
        NuGetVersion version,
        string installPath,
        CancellationToken cancellationToken = default)
    {
        var nodeTypes = new List<string>();
        var warnings = new List<string>();

        try
        {
            var plugins = pluginLoader.LoadPlugins(installPath);
            foreach (var plugin in plugins)
            {
                if (plugin.Assembly is null) continue;

                var validation = pluginValidator.ValidatePlugin(plugin.Assembly);
                if (!validation.IsValid)
                {
                    await packageCache.RemovePackageAsync(packageId, version);
                    return new PluginLoadResult(
                        Failure: new PackageInstallResult
                        {
                            Success = false,
                            Errors = validation.Errors.Select(e => $"Plugin validation failed: {e.Message}").ToList()
                        });
                }

                nodeRegistry.RegisterFromAssembly(plugin.Assembly);
                nodeTypes.AddRange(ResolveNodeTypeIdentifiers(plugin.NodeTypes));
                warnings.AddRange(validation.Warnings.Select(w => w.Message));
            }
        }
        catch (Exception ex)
        {
            logger?.LogDebug(ex, "No plugin assembly found in {PackageId}, treating as library dependency",
                packageId);
        }

        return new PluginLoadResult(NodeTypes: nodeTypes, Warnings: warnings);
    }

    public void UnloadPlugins(string packageId, string installPath)
    {
        pluginLoader.UnloadPlugin(packageId);
        foreach (var loadedPlugin in pluginLoader.GetLoadedPlugins())
        {
            if (string.Equals(loadedPlugin.FilePath, installPath, StringComparison.OrdinalIgnoreCase) ||
                loadedPlugin.FilePath.StartsWith(installPath, StringComparison.OrdinalIgnoreCase))
            {
                pluginLoader.UnloadPlugin(loadedPlugin.Id);
            }
        }
    }

    public void UnloadAndUnregisterPlugins(string packageId, string installPath)
    {
        // Find the plugin before unloading so we can unregister its assembly
        var oldPlugin = pluginLoader.GetPlugin(packageId);

        pluginLoader.UnloadPlugin(packageId);
        foreach (var loadedPlugin in pluginLoader.GetLoadedPlugins())
        {
            if (string.Equals(loadedPlugin.FilePath, installPath, StringComparison.OrdinalIgnoreCase) ||
                loadedPlugin.FilePath.StartsWith(installPath, StringComparison.OrdinalIgnoreCase))
            {
                oldPlugin ??= loadedPlugin;
                pluginLoader.UnloadPlugin(loadedPlugin.Id);
            }
        }

        if (oldPlugin?.Assembly is not null)
        {
            nodeRegistry.UnregisterFromAssembly(oldPlugin.Assembly);
        }
    }

    public List<string> ResolveNodeTypeIdentifiers(IEnumerable<Type> nodeTypes)
    {
        var identifiers = new List<string>();
        foreach (var nodeType in nodeTypes)
        {
            try
            {
                if (Activator.CreateInstance(nodeType) is INode nodeInstance)
                    identifiers.Add(nodeInstance.Type);
            }
            catch
            {
                // Skip types that can't be instantiated
            }
        }

        return identifiers;
    }

    public bool TryLoadPluginsForInitialization(string packageId, string installPath)
    {
        try
        {
            var plugins = pluginLoader.LoadPlugins(installPath);
            foreach (var plugin in plugins)
            {
                if (plugin.IsLoaded && plugin.Assembly is not null)
                {
                    nodeRegistry.RegisterFromAssembly(plugin.Assembly);
                }
            }

            return true;
        }
        catch (Exception ex)
        {
            logger?.LogWarning(ex, "Failed to load plugin from {PackageId}", packageId);
            return false;
        }
    }
}
