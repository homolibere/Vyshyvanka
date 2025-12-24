using Microsoft.AspNetCore.Authorization;

namespace FlowForge.Api.Authorization;

/// <summary>
/// Extension methods for configuring authorization policies.
/// </summary>
public static class AuthorizationExtensions
{
    /// <summary>
    /// Adds FlowForge authorization policies.
    /// </summary>
    public static AuthorizationOptions AddFlowForgePolicies(this AuthorizationOptions options)
    {
        // Role-based policies
        options.AddPolicy(Policies.RequireAdmin, policy =>
            policy.RequireRole(Roles.Admin));
        
        options.AddPolicy(Policies.RequireEditor, policy =>
            policy.RequireRole(Roles.Admin, Roles.Editor));
        
        options.AddPolicy(Policies.RequireViewer, policy =>
            policy.RequireRole(Roles.Admin, Roles.Editor, Roles.Viewer));
        
        // Feature-based policies
        options.AddPolicy(Policies.CanManageWorkflows, policy =>
            policy.RequireRole(Roles.Admin, Roles.Editor));
        
        options.AddPolicy(Policies.CanViewWorkflows, policy =>
            policy.RequireRole(Roles.Admin, Roles.Editor, Roles.Viewer));
        
        options.AddPolicy(Policies.CanExecuteWorkflows, policy =>
            policy.RequireRole(Roles.Admin, Roles.Editor));
        
        options.AddPolicy(Policies.CanManageCredentials, policy =>
            policy.RequireRole(Roles.Admin, Roles.Editor));
        
        options.AddPolicy(Policies.CanManageUsers, policy =>
            policy.RequireRole(Roles.Admin));
        
        return options;
    }
}
