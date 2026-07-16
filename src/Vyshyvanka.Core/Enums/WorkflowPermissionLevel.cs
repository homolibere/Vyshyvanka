namespace Vyshyvanka.Core.Enums;

/// <summary>
/// Permission level for shared workflow access.
/// Levels are hierarchical: each level includes all permissions of lower levels.
/// </summary>
public enum WorkflowPermissionLevel
{
    /// <summary>Can view the workflow definition (read-only).</summary>
    View = 0,

    /// <summary>Can trigger workflow execution. Implies View.</summary>
    Execute = 1,

    /// <summary>Can modify the workflow definition. Implies Execute and View.</summary>
    Edit = 2
}
