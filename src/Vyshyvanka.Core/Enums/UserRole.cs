namespace Vyshyvanka.Core.Enums;

/// <summary>
/// User roles for role-based access control.
/// </summary>
public enum UserRole
{
    /// <summary>Can view workflows and executions.</summary>
    Viewer,
    
    /// <summary>Can create and edit workflows.</summary>
    Editor,
    
    /// <summary>Full administrative access.</summary>
    Admin
}
