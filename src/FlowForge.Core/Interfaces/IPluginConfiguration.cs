namespace FlowForge.Core.Interfaces;

/// <summary>
/// Provides configuration access for plugins.
/// </summary>
public interface IPluginConfiguration
{
    /// <summary>
    /// Gets a configuration value for a plugin.
    /// </summary>
    /// <param name="pluginId">The plugin identifier.</param>
    /// <param name="key">The configuration key.</param>
    /// <returns>The configuration value, or null if not found.</returns>
    string? GetValue(string pluginId, string key);
    
    /// <summary>
    /// Gets a configuration value with a default fallback.
    /// </summary>
    /// <param name="pluginId">The plugin identifier.</param>
    /// <param name="key">The configuration key.</param>
    /// <param name="defaultValue">Default value if not found.</param>
    /// <returns>The configuration value, or the default if not found.</returns>
    string GetValue(string pluginId, string key, string defaultValue);
    
    /// <summary>
    /// Gets a typed configuration value.
    /// </summary>
    /// <typeparam name="T">The type to convert to.</typeparam>
    /// <param name="pluginId">The plugin identifier.</param>
    /// <param name="key">The configuration key.</param>
    /// <returns>The typed configuration value, or default if not found.</returns>
    T? GetValue<T>(string pluginId, string key);
    
    /// <summary>
    /// Gets a typed configuration value with a default fallback.
    /// </summary>
    /// <typeparam name="T">The type to convert to.</typeparam>
    /// <param name="pluginId">The plugin identifier.</param>
    /// <param name="key">The configuration key.</param>
    /// <param name="defaultValue">Default value if not found.</param>
    /// <returns>The typed configuration value, or the default if not found.</returns>
    T GetValue<T>(string pluginId, string key, T defaultValue);
    
    /// <summary>
    /// Gets all configuration values for a plugin.
    /// </summary>
    /// <param name="pluginId">The plugin identifier.</param>
    /// <returns>Dictionary of all configuration key-value pairs.</returns>
    IReadOnlyDictionary<string, string> GetAllValues(string pluginId);
    
    /// <summary>
    /// Checks if a configuration key exists for a plugin.
    /// </summary>
    /// <param name="pluginId">The plugin identifier.</param>
    /// <param name="key">The configuration key.</param>
    /// <returns>True if the key exists.</returns>
    bool HasValue(string pluginId, string key);
    
    /// <summary>
    /// Reloads configuration from all sources.
    /// </summary>
    void Reload();
}
