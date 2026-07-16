namespace Vyshyvanka.Core.Enums;

/// <summary>
/// Identifies whether a workflow permission targets an individual user or a team.
/// </summary>
public enum PermissionTargetType
{
    /// <summary>Permission is granted to a specific user.</summary>
    User = 0,

    /// <summary>Permission is granted to all members of a team.</summary>
    Team = 1
}
