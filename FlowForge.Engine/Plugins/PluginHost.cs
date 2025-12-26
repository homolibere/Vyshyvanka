using System.Collections.Concurrent;
using System.Text.Json;
using FlowForge.Core.Interfaces;
using Microsoft.Extensions.Logging;

namespace FlowForge.Engine.Plugins;

/// <summary>
/// Hosts plugin execution in isolation to prevent plugins from affecting core system stability.
/// </summary>
public class PluginHost : IPluginHost
{
    private readonly IPluginLoader _pluginLoader;
    private readonly ConcurrentDictionary<string, string> _nodeTypeToPluginId = new();
    private readonly ILogger<PluginHost>? _logger;

    public PluginHost(IPluginLoader pluginLoader, ILogger<PluginHost>? logger = null)
    {
        ArgumentNullException.ThrowIfNull(pluginLoader);
        _pluginLoader = pluginLoader;
        _logger = logger;
        
        // Build node type to plugin mapping
        RefreshNodeTypeMapping();
    }

    /// <inheritdoc />
    public async Task<NodeOutput> ExecuteNodeInIsolationAsync(
        INode node,
        NodeInput input,
        IExecutionContext context,
        TimeSpan timeout)
    {
        ArgumentNullException.ThrowIfNull(node);
        ArgumentNullException.ThrowIfNull(context);
        
        var pluginId = GetPluginIdForNode(node.Type);
        
        _logger?.LogDebug(
            "Executing plugin node {NodeType} from plugin {PluginId} with timeout {Timeout}",
            node.Type, pluginId ?? "unknown", timeout);
        
        using var timeoutCts = new CancellationTokenSource(timeout);
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(
            timeoutCts.Token, 
            context.CancellationToken);
        
        try
        {
            // Execute the node with timeout
            var executeTask = ExecuteWithExceptionHandlingAsync(node, input, context);
            
            // Use a delay task without cancellation token for timeout detection
            var timeoutTask = Task.Delay(timeout);
            var completedTask = await Task.WhenAny(executeTask, timeoutTask);
            
            if (completedTask == timeoutTask && !executeTask.IsCompleted)
            {
                // Timeout occurred
                _logger?.LogWarning(
                    "Plugin node {NodeType} execution timed out after {Timeout}",
                    node.Type, timeout);
                
                return new NodeOutput
                {
                    Data = JsonSerializer.SerializeToElement(new { }),
                    Success = false,
                    ErrorMessage = $"Plugin node execution timed out after {timeout.TotalSeconds} seconds"
                };
            }
            
            return await executeTask;
        }
        catch (OperationCanceledException) when (timeoutCts.IsCancellationRequested)
        {
            _logger?.LogWarning(
                "Plugin node {NodeType} execution timed out after {Timeout}",
                node.Type, timeout);
            
            return new NodeOutput
            {
                Data = JsonSerializer.SerializeToElement(new { }),
                Success = false,
                ErrorMessage = $"Plugin node execution timed out after {timeout.TotalSeconds} seconds"
            };
        }
        catch (OperationCanceledException)
        {
            _logger?.LogInformation("Plugin node {NodeType} execution was cancelled", node.Type);
            
            return new NodeOutput
            {
                Data = JsonSerializer.SerializeToElement(new { }),
                Success = false,
                ErrorMessage = "Plugin node execution was cancelled"
            };
        }
    }

    /// <inheritdoc />
    public bool IsPluginNode(string nodeType)
    {
        if (string.IsNullOrWhiteSpace(nodeType))
            return false;
            
        return _nodeTypeToPluginId.ContainsKey(nodeType);
    }

    /// <inheritdoc />
    public string? GetPluginIdForNode(string nodeType)
    {
        if (string.IsNullOrWhiteSpace(nodeType))
            return null;
            
        return _nodeTypeToPluginId.GetValueOrDefault(nodeType);
    }

    /// <summary>
    /// Refreshes the node type to plugin ID mapping from loaded plugins.
    /// </summary>
    public void RefreshNodeTypeMapping()
    {
        _nodeTypeToPluginId.Clear();
        
        foreach (var plugin in _pluginLoader.GetLoadedPlugins())
        {
            foreach (var nodeType in plugin.NodeTypes)
            {
                try
                {
                    var instance = Activator.CreateInstance(nodeType) as INode;
                    if (instance is not null && !string.IsNullOrWhiteSpace(instance.Type))
                    {
                        _nodeTypeToPluginId[instance.Type] = plugin.Id;
                    }
                }
                catch (Exception ex)
                {
                    _logger?.LogWarning(ex, 
                        "Failed to get node type from {TypeName} in plugin {PluginId}",
                        nodeType.Name, plugin.Id);
                }
            }
        }
    }

    private async Task<NodeOutput> ExecuteWithExceptionHandlingAsync(
        INode node,
        NodeInput input,
        IExecutionContext context)
    {
        try
        {
            return await node.ExecuteAsync(input, context);
        }
        catch (Exception ex)
        {
            // Catch all exceptions from plugin nodes to prevent them from crashing the system
            _logger?.LogError(ex, 
                "Plugin node {NodeType} threw an unhandled exception: {Message}",
                node.Type, ex.Message);
            
            return new NodeOutput
            {
                Data = JsonSerializer.SerializeToElement(new { }),
                Success = false,
                ErrorMessage = $"Plugin node failed: {ex.Message}"
            };
        }
    }
}
