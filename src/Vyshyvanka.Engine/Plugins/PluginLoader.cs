using System.Collections.Concurrent;
using System.Reflection;
using System.Runtime.Loader;
using Vyshyvanka.Core.Interfaces;
using Microsoft.Extensions.Logging;

namespace Vyshyvanka.Engine.Plugins;

/// <summary>
/// Default implementation of plugin loader that discovers and loads plugin assemblies.
/// </summary>
public class PluginLoader : IPluginLoader
{
    private readonly ConcurrentDictionary<string, PluginLoadContext> _loadContexts = new();
    private readonly ConcurrentDictionary<string, PluginInfo> _plugins = new();
    private readonly IPluginValidator _validator;
    private readonly ILogger<PluginLoader>? _logger;

    public PluginLoader(IPluginValidator? validator = null, ILogger<PluginLoader>? logger = null)
    {
        _validator = validator ?? new PluginValidator();
        _logger = logger;
    }

    /// <inheritdoc />
    public IEnumerable<PluginInfo> LoadPlugins(string pluginDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(pluginDirectory);
        
        if (!Directory.Exists(pluginDirectory))
        {
            _logger?.LogWarning("Plugin directory does not exist: {Directory}", pluginDirectory);
            return [];
        }

        var loadedPlugins = new List<PluginInfo>();
        var pluginFiles = Directory.GetFiles(pluginDirectory, "*.dll", SearchOption.AllDirectories);

        foreach (var pluginFile in pluginFiles)
        {
            try
            {
                var pluginInfo = LoadPlugin(pluginFile);
                if (pluginInfo is not null)
                {
                    loadedPlugins.Add(pluginInfo);
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Failed to load plugin from {FilePath}", pluginFile);
            }
        }

        return loadedPlugins;
    }

    /// <inheritdoc />
    public void UnloadPlugin(string pluginId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(pluginId);

        if (_loadContexts.TryRemove(pluginId, out var context))
        {
            if (_plugins.TryRemove(pluginId, out var pluginInfo))
            {
                _logger?.LogInformation("Unloading plugin: {PluginId} ({PluginName})", pluginId, pluginInfo.Name);
            }
            
            context.Unload();
            
            // Request garbage collection to help unload the assembly
            GC.Collect();
            GC.WaitForPendingFinalizers();
        }
        else
        {
            _logger?.LogWarning("Plugin not found for unload: {PluginId}", pluginId);
        }
    }

    /// <inheritdoc />
    public PluginInfo? GetPlugin(string pluginId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(pluginId);
        return _plugins.GetValueOrDefault(pluginId);
    }

    /// <inheritdoc />
    public IEnumerable<PluginInfo> GetLoadedPlugins()
    {
        return _plugins.Values.Where(p => p.IsLoaded);
    }

    private PluginInfo? LoadPlugin(string pluginPath)
    {
        var fullPath = Path.GetFullPath(pluginPath);
        
        // Create a new load context for isolation
        var loadContext = new PluginLoadContext(fullPath);
        
        Assembly assembly;
        try
        {
            assembly = loadContext.LoadFromAssemblyPath(fullPath);
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Could not load assembly from {Path}", fullPath);
            return null;
        }

        // Get plugin metadata from attribute
        var pluginAttr = assembly.GetCustomAttribute<PluginAttribute>();
        if (pluginAttr is null)
        {
            // Not a Vyshyvanka plugin, skip it
            loadContext.Unload();
            return null;
        }

        // Check if already loaded
        if (_plugins.ContainsKey(pluginAttr.Id))
        {
            _logger?.LogWarning("Plugin {PluginId} is already loaded, skipping", pluginAttr.Id);
            loadContext.Unload();
            return null;
        }

        // Discover node types
        var nodeTypes = DiscoverNodeTypes(assembly);
        
        // Validate the plugin
        var validationResult = _validator.ValidatePlugin(assembly);
        if (!validationResult.IsValid)
        {
            var errorMessages = string.Join("; ", validationResult.Errors.Select(e => $"{e.Code}: {e.Message}"));
            _logger?.LogWarning("Plugin validation failed for {PluginId}: {Errors}", pluginAttr.Id, errorMessages);
            loadContext.Unload();
            
            return new PluginInfo
            {
                Id = pluginAttr.Id,
                Name = string.IsNullOrWhiteSpace(pluginAttr.Name) ? pluginAttr.Id : pluginAttr.Name,
                Version = pluginAttr.Version,
                FilePath = fullPath,
                LoadedAt = DateTime.UtcNow,
                IsLoaded = false,
                LoadError = errorMessages
            };
        }
        
        // Log warnings
        foreach (var warning in validationResult.Warnings)
        {
            _logger?.LogWarning("Plugin {PluginId} warning: {Code} - {Message}", 
                pluginAttr.Id, warning.Code, warning.Message);
        }
        
        var pluginInfo = new PluginInfo
        {
            Id = pluginAttr.Id,
            Name = string.IsNullOrWhiteSpace(pluginAttr.Name) ? pluginAttr.Id : pluginAttr.Name,
            Version = pluginAttr.Version,
            Description = pluginAttr.Description,
            Author = pluginAttr.Author,
            FilePath = fullPath,
            Assembly = assembly,
            NodeTypes = nodeTypes,
            LoadedAt = DateTime.UtcNow,
            IsLoaded = true
        };

        _loadContexts[pluginAttr.Id] = loadContext;
        _plugins[pluginAttr.Id] = pluginInfo;

        _logger?.LogInformation(
            "Loaded plugin: {PluginId} ({PluginName}) v{Version} with {NodeCount} node types",
            pluginInfo.Id, pluginInfo.Name, pluginInfo.Version, nodeTypes.Count);

        return pluginInfo;
    }

    private List<Type> DiscoverNodeTypes(Assembly assembly)
    {
        var nodeTypes = new List<Type>();
        
        try
        {
            var types = assembly.GetTypes()
                .Where(t => typeof(INode).IsAssignableFrom(t) && !t.IsInterface && !t.IsAbstract);
            
            nodeTypes.AddRange(types);
        }
        catch (ReflectionTypeLoadException ex)
        {
            // Some types couldn't be loaded, but we can still use the ones that did
            var loadedTypes = ex.Types
                .Where(t => t is not null && typeof(INode).IsAssignableFrom(t) && !t.IsInterface && !t.IsAbstract)
                .Cast<Type>();
            
            nodeTypes.AddRange(loadedTypes);
            
            _logger?.LogWarning(ex, "Some types could not be loaded from assembly");
        }

        return nodeTypes;
    }
}

/// <summary>
/// Custom AssemblyLoadContext for plugin isolation.
/// </summary>
internal class PluginLoadContext : AssemblyLoadContext
{
    private readonly AssemblyDependencyResolver _resolver;

    public PluginLoadContext(string pluginPath) : base(isCollectible: true)
    {
        _resolver = new AssemblyDependencyResolver(pluginPath);
    }

    protected override Assembly? Load(AssemblyName assemblyName)
    {
        // Try to resolve from plugin directory first
        var assemblyPath = _resolver.ResolveAssemblyToPath(assemblyName);
        if (assemblyPath is not null)
        {
            return LoadFromAssemblyPath(assemblyPath);
        }

        // Fall back to default context for shared assemblies
        return null;
    }

    protected override nint LoadUnmanagedDll(string unmanagedDllName)
    {
        var libraryPath = _resolver.ResolveUnmanagedDllToPath(unmanagedDllName);
        if (libraryPath is not null)
        {
            return LoadUnmanagedDllFromPath(libraryPath);
        }

        return nint.Zero;
    }
}
