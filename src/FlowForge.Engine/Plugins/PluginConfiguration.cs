using System.Collections.Concurrent;
using System.Text.Json;
using FlowForge.Core.Interfaces;
using Microsoft.Extensions.Logging;

namespace FlowForge.Engine.Plugins;

/// <summary>
/// Provides configuration access for plugins from environment variables and config files.
/// </summary>
public class PluginConfiguration : IPluginConfiguration
{
    private const string EnvVarPrefix = "FLOWFORGE_PLUGIN_";
    private const string ConfigFileExtension = ".config.json";
    
    private readonly ConcurrentDictionary<string, Dictionary<string, string>> _pluginConfigs = new();
    private readonly string? _pluginDirectory;
    private readonly ILogger<PluginConfiguration>? _logger;

    public PluginConfiguration(string? pluginDirectory = null, ILogger<PluginConfiguration>? logger = null)
    {
        _pluginDirectory = pluginDirectory;
        _logger = logger;
        Reload();
    }

    /// <inheritdoc />
    public string? GetValue(string pluginId, string key)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(pluginId);
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        
        var normalizedPluginId = NormalizePluginId(pluginId);
        
        // First check environment variable (highest priority)
        var envValue = GetEnvironmentValue(normalizedPluginId, key);
        if (envValue is not null)
        {
            return envValue;
        }
        
        // Then check loaded config
        if (_pluginConfigs.TryGetValue(normalizedPluginId, out var config) && 
            config.TryGetValue(key, out var value))
        {
            return value;
        }
        
        return null;
    }

    /// <inheritdoc />
    public string GetValue(string pluginId, string key, string defaultValue)
    {
        return GetValue(pluginId, key) ?? defaultValue;
    }

    /// <inheritdoc />
    public T? GetValue<T>(string pluginId, string key)
    {
        var value = GetValue(pluginId, key);
        if (value is null)
        {
            return default;
        }
        
        return ConvertValue<T>(value);
    }

    /// <inheritdoc />
    public T GetValue<T>(string pluginId, string key, T defaultValue)
    {
        var value = GetValue(pluginId, key);
        if (value is null)
        {
            return defaultValue;
        }
        
        var converted = ConvertValue<T>(value);
        return converted ?? defaultValue;
    }

    /// <inheritdoc />
    public IReadOnlyDictionary<string, string> GetAllValues(string pluginId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(pluginId);
        
        var normalizedPluginId = NormalizePluginId(pluginId);
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        
        // Add values from config file
        if (_pluginConfigs.TryGetValue(normalizedPluginId, out var config))
        {
            foreach (var kvp in config)
            {
                result[kvp.Key] = kvp.Value;
            }
        }
        
        // Override with environment variables (higher priority)
        var envPrefix = $"{EnvVarPrefix}{normalizedPluginId}_";
        foreach (var envVar in Environment.GetEnvironmentVariables().Keys.Cast<string>())
        {
            if (envVar.StartsWith(envPrefix, StringComparison.OrdinalIgnoreCase))
            {
                var key = envVar[envPrefix.Length..];
                var value = Environment.GetEnvironmentVariable(envVar);
                if (value is not null)
                {
                    result[key] = value;
                }
            }
        }
        
        return result;
    }

    /// <inheritdoc />
    public bool HasValue(string pluginId, string key)
    {
        return GetValue(pluginId, key) is not null;
    }

    /// <inheritdoc />
    public void Reload()
    {
        _pluginConfigs.Clear();
        
        // Load from environment variables
        LoadFromEnvironment();
        
        // Load from config files
        if (!string.IsNullOrWhiteSpace(_pluginDirectory) && Directory.Exists(_pluginDirectory))
        {
            LoadFromConfigFiles(_pluginDirectory);
        }
    }

    /// <summary>
    /// Loads configuration for a specific plugin from a config file.
    /// </summary>
    /// <param name="pluginId">The plugin identifier.</param>
    /// <param name="configFilePath">Path to the configuration file.</param>
    public void LoadPluginConfig(string pluginId, string configFilePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(pluginId);
        ArgumentException.ThrowIfNullOrWhiteSpace(configFilePath);
        
        if (!File.Exists(configFilePath))
        {
            _logger?.LogWarning("Plugin config file not found: {Path}", configFilePath);
            return;
        }
        
        try
        {
            var json = File.ReadAllText(configFilePath);
            var config = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(json);
            
            if (config is not null)
            {
                var normalizedPluginId = NormalizePluginId(pluginId);
                var pluginConfig = _pluginConfigs.GetOrAdd(normalizedPluginId, _ => new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase));
                
                foreach (var kvp in config)
                {
                    pluginConfig[kvp.Key] = GetJsonElementValue(kvp.Value);
                }
                
                _logger?.LogInformation("Loaded config for plugin {PluginId} from {Path}", pluginId, configFilePath);
            }
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to load plugin config from {Path}", configFilePath);
        }
    }

    private void LoadFromEnvironment()
    {
        foreach (var envVar in Environment.GetEnvironmentVariables().Keys.Cast<string>())
        {
            if (!envVar.StartsWith(EnvVarPrefix, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }
            
            var remainder = envVar[EnvVarPrefix.Length..];
            var underscoreIndex = remainder.IndexOf('_');
            
            if (underscoreIndex <= 0)
            {
                continue;
            }
            
            var pluginId = remainder[..underscoreIndex];
            var key = remainder[(underscoreIndex + 1)..];
            var value = Environment.GetEnvironmentVariable(envVar);
            
            if (string.IsNullOrWhiteSpace(key) || value is null)
            {
                continue;
            }
            
            var normalizedPluginId = NormalizePluginId(pluginId);
            var pluginConfig = _pluginConfigs.GetOrAdd(normalizedPluginId, _ => new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase));
            pluginConfig[key] = value;
        }
    }

    private void LoadFromConfigFiles(string directory)
    {
        var configFiles = Directory.GetFiles(directory, $"*{ConfigFileExtension}", SearchOption.AllDirectories);
        
        foreach (var configFile in configFiles)
        {
            var fileName = Path.GetFileNameWithoutExtension(configFile);
            // Remove .config from the name to get plugin ID
            if (fileName.EndsWith(".config", StringComparison.OrdinalIgnoreCase))
            {
                fileName = fileName[..^7];
            }
            
            LoadPluginConfig(fileName, configFile);
        }
    }

    private static string? GetEnvironmentValue(string normalizedPluginId, string key)
    {
        var envVarName = $"{EnvVarPrefix}{normalizedPluginId}_{key}";
        return Environment.GetEnvironmentVariable(envVarName);
    }

    private static string NormalizePluginId(string pluginId)
    {
        // Convert to uppercase and replace non-alphanumeric with underscore
        return string.Concat(pluginId.Select(c => char.IsLetterOrDigit(c) ? char.ToUpperInvariant(c) : '_'));
    }

    private static string GetJsonElementValue(JsonElement element)
    {
        return element.ValueKind switch
        {
            JsonValueKind.String => element.GetString() ?? string.Empty,
            JsonValueKind.Number => element.GetRawText(),
            JsonValueKind.True => "true",
            JsonValueKind.False => "false",
            JsonValueKind.Null => string.Empty,
            _ => element.GetRawText()
        };
    }

    private static T? ConvertValue<T>(string value)
    {
        var targetType = typeof(T);
        var underlyingType = Nullable.GetUnderlyingType(targetType) ?? targetType;
        
        try
        {
            if (underlyingType == typeof(string))
            {
                return (T)(object)value;
            }
            
            if (underlyingType == typeof(bool))
            {
                return (T)(object)bool.Parse(value);
            }
            
            if (underlyingType == typeof(int))
            {
                return (T)(object)int.Parse(value);
            }
            
            if (underlyingType == typeof(long))
            {
                return (T)(object)long.Parse(value);
            }
            
            if (underlyingType == typeof(double))
            {
                return (T)(object)double.Parse(value);
            }
            
            if (underlyingType == typeof(decimal))
            {
                return (T)(object)decimal.Parse(value);
            }
            
            if (underlyingType == typeof(TimeSpan))
            {
                return (T)(object)TimeSpan.Parse(value);
            }
            
            if (underlyingType == typeof(Guid))
            {
                return (T)(object)Guid.Parse(value);
            }
            
            if (underlyingType.IsEnum)
            {
                return (T)Enum.Parse(underlyingType, value, ignoreCase: true);
            }
            
            // Try JSON deserialization for complex types
            return JsonSerializer.Deserialize<T>(value);
        }
        catch
        {
            return default;
        }
    }
}
