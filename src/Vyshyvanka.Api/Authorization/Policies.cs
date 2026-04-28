namespace Vyshyvanka.Api.Authorization;

/// <summary>
/// Authorization policy names.
/// </summary>
public static class Policies
{
    public const string RequireAdmin = "RequireAdmin";
    public const string RequireEditor = "RequireEditor";
    public const string RequireViewer = "RequireViewer";

    public const string CanManageWorkflows = "CanManageWorkflows";
    public const string CanViewWorkflows = "CanViewWorkflows";
    public const string CanExecuteWorkflows = "CanExecuteWorkflows";
    public const string CanManageCredentials = "CanManageCredentials";
    public const string CanManageUsers = "CanManageUsers";
    public const string CanManagePackages = "CanManagePackages";
    public const string CanViewPackages = "CanViewPackages";
}

/// <summary>
/// Role names matching UserRole enum.
/// </summary>
public static class Roles
{
    public const string Admin = "Admin";
    public const string Editor = "Editor";
    public const string Viewer = "Viewer";
}
