namespace Vyshyvanka.Core.Enums;

/// <summary>
/// Indicates how a workflow execution was triggered.
/// </summary>
public enum ExecutionMode
{
    /// <summary>Manually triggered by a user.</summary>
    Manual,
    
    /// <summary>Triggered by a trigger node (webhook, event).</summary>
    Trigger,
    
    /// <summary>Triggered via API call.</summary>
    Api,
    
    /// <summary>Triggered by a schedule.</summary>
    Scheduled
}
