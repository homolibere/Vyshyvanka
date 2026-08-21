namespace Vyshyvanka.Core.Enums;

/// <summary>
/// Lifecycle and activation state of a workflow.
/// </summary>
/// <remarks>
/// Activation governs only whether automatic triggers are armed (webhook listening and
/// cron/interval scheduling). It does NOT gate manual, API, or Designer execution, which is
/// always permitted subject to permissions and validation.
/// </remarks>
public enum WorkflowStatus
{
    /// <summary>
    /// Editable and never armed. Runs only manually or via direct API. Default for new workflows.
    /// </summary>
    Draft,

    /// <summary>
    /// Armed for automatic triggers: webhook listening and cron/interval scheduling.
    /// Also runnable manually.
    /// </summary>
    Active,

    /// <summary>
    /// Triggers disarmed (cron unscheduled, webhook not listening) but still runnable
    /// manually or via API. Distinguishes a previously-active workflow from one that was never armed.
    /// </summary>
    Paused
}
