namespace FlowForge.Core.Interfaces;

/// <summary>
/// Hosts plugin execution in isolation to prevent plugins from affecting core system stability.
/// </summary>
public interface IPluginHost
{
    /// <summary>
    /// Executes a plugin node in isolation with timeout support.
    /// </summary>
    /// <param name="node">The node to execute.</param>
    /// <param name="input">Input data for the node.</param>
    /// <param name="context">Execution context.</param>
    /// <param name="timeout">Maximum execution time.</param>
    /// <returns>Node output, or error output if execution failed.</returns>
    Task<NodeOutput> ExecuteNodeInIsolationAsync(
        INode node,
        NodeInput input,
        IExecutionContext context,
        TimeSpan timeout);
    
    /// <summary>
    /// Checks if a node type is from a plugin (vs built-in).
    /// </summary>
    /// <param name="nodeType">The node type identifier.</param>
    /// <returns>True if the node is from a plugin.</returns>
    bool IsPluginNode(string nodeType);
    
    /// <summary>
    /// Gets the plugin ID that provides a node type.
    /// </summary>
    /// <param name="nodeType">The node type identifier.</param>
    /// <returns>Plugin ID, or null if not a plugin node.</returns>
    string? GetPluginIdForNode(string nodeType);
}
