namespace Vyshyvanka.Core.Enums;

/// <summary>
/// Categorizes nodes by their function in a workflow.
/// </summary>
public enum NodeCategory
{
    /// <summary>Nodes that initiate workflow execution.</summary>
    Trigger,
    
    /// <summary>Nodes that perform operations like HTTP requests, database queries.</summary>
    Action,
    
    /// <summary>Nodes that control workflow flow (conditionals, loops, merges).</summary>
    Logic,
    
    /// <summary>Nodes that transform data between other nodes.</summary>
    Transform
}
