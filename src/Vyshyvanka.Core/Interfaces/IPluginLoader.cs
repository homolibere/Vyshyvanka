using System.Reflection;

namespace Vyshyvanka.Core.Interfaces;

/// <summary>
/// Loads and manages plugin assemblies.
/// </summary>
public interface IPluginLoader
{
    /// <summary>
    /// Discovers and loads all plugins from the specified directory.
    /// </summary>
    /// <param name="pluginDirectory">Directory containing plugin assemblies.</param>
    /// <returns>Information about loaded plugins.</returns>
    IEnumerable<PluginInfo> LoadPlugins(string pluginDirectory);
    
    /// <summary>
    /// Unloads a plugin and releases its resources.
    /// </summary>
    /// <param name="pluginId">Unique identifier of the plugin to unload.</param>
    void UnloadPlugin(string pluginId);
    
    /// <summary>
    /// Gets information about a loaded plugin.
    /// </summary>
    /// <param name="pluginId">Unique identifier of the plugin.</param>
    /// <returns>Plugin information, or null if not found.</returns>
    PluginInfo? GetPlugin(string pluginId);
    
    /// <summary>
    /// Gets all currently loaded plugins.
    /// </summary>
    /// <returns>Collection of loaded plugin information.</returns>
    IEnumerable<PluginInfo> GetLoadedPlugins();
}

/// <summary>
/// Information about a loaded plugin.
/// </summary>
public record PluginInfo
{
    /// <summary>Unique identifier for the plugin.</summary>
    public string Id { get; init; } = string.Empty;
    
    /// <summary>Display name of the plugin.</summary>
    public string Name { get; init; } = string.Empty;
    
    /// <summary>Plugin version.</summary>
    public string Version { get; init; } = string.Empty;
    
    /// <summary>Description of what the plugin provides.</summary>
    public string Description { get; init; } = string.Empty;
    
    /// <summary>Author of the plugin.</summary>
    public string Author { get; init; } = string.Empty;
    
    /// <summary>Path to the plugin assembly file.</summary>
    public string FilePath { get; init; } = string.Empty;
    
    /// <summary>The loaded assembly.</summary>
    public Assembly? Assembly { get; init; }
    
    /// <summary>Node types provided by this plugin.</summary>
    public IReadOnlyList<Type> NodeTypes { get; init; } = [];
    
    /// <summary>When the plugin was loaded.</summary>
    public DateTime LoadedAt { get; init; }
    
    /// <summary>Whether the plugin is currently loaded.</summary>
    public bool IsLoaded { get; init; }
    
    /// <summary>Error message if loading failed.</summary>
    public string? LoadError { get; init; }
}

/// <summary>
/// Attribute for providing plugin metadata.
/// </summary>
[AttributeUsage(AttributeTargets.Assembly)]
public class PluginAttribute : Attribute
{
    /// <summary>Unique identifier for the plugin.</summary>
    public string Id { get; }
    
    /// <summary>Display name of the plugin.</summary>
    public string Name { get; set; } = string.Empty;
    
    /// <summary>Plugin version.</summary>
    public string Version { get; set; } = "1.0.0";
    
    /// <summary>Description of what the plugin provides.</summary>
    public string Description { get; set; } = string.Empty;
    
    /// <summary>Author of the plugin.</summary>
    public string Author { get; set; } = string.Empty;

    public PluginAttribute(string id)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        Id = id;
    }
}
