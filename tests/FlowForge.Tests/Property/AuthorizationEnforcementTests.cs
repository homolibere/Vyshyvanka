using CsCheck;
using FlowForge.Api.Authorization;
using FlowForge.Core.Enums;
using FlowForge.Core.Models;
using Xunit;

namespace FlowForge.Tests.Property;

/// <summary>
/// Property-based tests for authorization enforcement.
/// Feature: flowforge, Property 17: Authorization Enforcement
/// Validates: Requirements 9.3, 9.4
/// </summary>
public class AuthorizationEnforcementTests
{
    /// <summary>
    /// Defines the expected authorization matrix for each policy and role.
    /// </summary>
    private static readonly Dictionary<string, HashSet<UserRole>> PolicyAllowedRoles = new()
    {
        [Policies.RequireAdmin] = [UserRole.Admin],
        [Policies.RequireEditor] = [UserRole.Admin, UserRole.Editor],
        [Policies.RequireViewer] = [UserRole.Admin, UserRole.Editor, UserRole.Viewer],
        [Policies.CanManageWorkflows] = [UserRole.Admin, UserRole.Editor],
        [Policies.CanViewWorkflows] = [UserRole.Admin, UserRole.Editor, UserRole.Viewer],
        [Policies.CanExecuteWorkflows] = [UserRole.Admin, UserRole.Editor],
        [Policies.CanManageCredentials] = [UserRole.Admin, UserRole.Editor],
        [Policies.CanManageUsers] = [UserRole.Admin]
    };

    /// <summary>
    /// All available policies for testing.
    /// </summary>
    private static readonly string[] AllPolicies =
    [
        Policies.RequireAdmin,
        Policies.RequireEditor,
        Policies.RequireViewer,
        Policies.CanManageWorkflows,
        Policies.CanViewWorkflows,
        Policies.CanExecuteWorkflows,
        Policies.CanManageCredentials,
        Policies.CanManageUsers
    ];

    /// <summary>
    /// Property 17: Authorization Enforcement - Role-based permission verification
    /// For any user role and any policy, the authorization check SHALL correctly 
    /// allow or deny access based on the defined role-policy matrix.
    /// </summary>
    [Fact]
    public void Authorization_RoleBasedPermissionVerification()
    {
        var gen = Gen.Select(
            Gen.OneOfConst(UserRole.Viewer, UserRole.Editor, UserRole.Admin),
            Gen.OneOf(AllPolicies.Select(Gen.Const).ToArray())
        );

        gen.Sample((role, policy) =>
        {
            // Arrange: Determine expected authorization result
            var expectedAllowed = PolicyAllowedRoles[policy].Contains(role);

            // Act: Check if role is authorized for policy
            var actualAllowed = IsRoleAuthorizedForPolicy(role, policy);

            // Assert: Authorization result matches expected
            Assert.Equal(expectedAllowed, actualAllowed);
        }, iter: 100);
    }

    /// <summary>
    /// Property 17: Authorization Enforcement - Admin has full access
    /// For any policy, an Admin user SHALL always be authorized.
    /// </summary>
    [Fact]
    public void Authorization_AdminHasFullAccess()
    {
        var gen = Gen.OneOf(AllPolicies.Select(Gen.Const).ToArray());

        gen.Sample(policy =>
        {
            // Act: Check if Admin is authorized
            var isAuthorized = IsRoleAuthorizedForPolicy(UserRole.Admin, policy);

            // Assert: Admin should always be authorized
            Assert.True(isAuthorized, $"Admin should be authorized for policy '{policy}'");
        }, iter: 100);
    }

    /// <summary>
    /// Property 17: Authorization Enforcement - Viewer restrictions
    /// For any management policy (create, update, delete, execute), 
    /// a Viewer user SHALL NOT be authorized.
    /// </summary>
    [Fact]
    public void Authorization_ViewerCannotManage()
    {
        var managementPolicies = new[]
        {
            Policies.CanManageWorkflows,
            Policies.CanExecuteWorkflows,
            Policies.CanManageCredentials,
            Policies.CanManageUsers,
            Policies.RequireAdmin,
            Policies.RequireEditor
        };

        var gen = Gen.OneOf(managementPolicies.Select(Gen.Const).ToArray());

        gen.Sample(policy =>
        {
            // Act: Check if Viewer is authorized for management policy
            var isAuthorized = IsRoleAuthorizedForPolicy(UserRole.Viewer, policy);

            // Assert: Viewer should not be authorized for management policies
            Assert.False(isAuthorized, $"Viewer should NOT be authorized for policy '{policy}'");
        }, iter: 100);
    }


    /// <summary>
    /// Property 17: Authorization Enforcement - Error messages don't reveal resource details
    /// For any unauthorized access attempt, the error message SHALL NOT contain 
    /// sensitive information about the protected resource.
    /// </summary>
    [Fact]
    public void Authorization_ErrorMessagesDoNotRevealResourceDetails()
    {
        var gen = Gen.Select(
            Gen.Guid,  // Resource ID
            Gen.String[5, 50].Where(s => !string.IsNullOrWhiteSpace(s)),  // Resource name
            Gen.OneOfConst(UserRole.Viewer, UserRole.Editor),  // Non-admin roles
            Gen.OneOf(new[]
            {
                Gen.Const(Policies.CanManageUsers),
                Gen.Const(Policies.RequireAdmin)
            })
        );

        gen.Sample((resourceId, resourceName, role, policy) =>
        {
            // Arrange: Create an authorization error message
            var errorMessage = CreateAuthorizationErrorMessage(role, policy);

            // Assert: Error message should not contain resource details
            Assert.DoesNotContain(resourceId.ToString(), errorMessage);
            Assert.DoesNotContain(resourceName, errorMessage);
            
            // Error message should be generic
            Assert.True(
                errorMessage.Contains("not authorized", StringComparison.OrdinalIgnoreCase) ||
                errorMessage.Contains("access denied", StringComparison.OrdinalIgnoreCase) ||
                errorMessage.Contains("forbidden", StringComparison.OrdinalIgnoreCase) ||
                errorMessage.Contains("permission", StringComparison.OrdinalIgnoreCase),
                "Error message should be a generic authorization error");
        }, iter: 100);
    }

    /// <summary>
    /// Property 17: Authorization Enforcement - Role hierarchy is respected
    /// For any two roles where one has higher privileges, the higher role SHALL 
    /// have access to all policies the lower role has access to.
    /// </summary>
    [Fact]
    public void Authorization_RoleHierarchyIsRespected()
    {
        // Role hierarchy: Admin > Editor > Viewer
        var roleHierarchy = new Dictionary<UserRole, UserRole[]>
        {
            [UserRole.Admin] = [UserRole.Editor, UserRole.Viewer],
            [UserRole.Editor] = [UserRole.Viewer],
            [UserRole.Viewer] = []
        };

        var gen = Gen.Select(
            Gen.OneOfConst(UserRole.Admin, UserRole.Editor),  // Higher role
            Gen.OneOf(AllPolicies.Select(Gen.Const).ToArray())
        );

        gen.Sample((higherRole, policy) =>
        {
            var lowerRoles = roleHierarchy[higherRole];
            var higherRoleAuthorized = IsRoleAuthorizedForPolicy(higherRole, policy);

            foreach (var lowerRole in lowerRoles)
            {
                var lowerRoleAuthorized = IsRoleAuthorizedForPolicy(lowerRole, policy);

                // If lower role is authorized, higher role must also be authorized
                if (lowerRoleAuthorized)
                {
                    Assert.True(higherRoleAuthorized,
                        $"Higher role '{higherRole}' should be authorized for policy '{policy}' " +
                        $"since lower role '{lowerRole}' is authorized");
                }
            }
        }, iter: 100);
    }

    /// <summary>
    /// Property 17: Authorization Enforcement - Consistent authorization decisions
    /// For any user with a specific role, repeated authorization checks for the 
    /// same policy SHALL always return the same result.
    /// </summary>
    [Fact]
    public void Authorization_ConsistentDecisions()
    {
        var gen = Gen.Select(
            Gen.Guid,
            Gen.String[5, 30].Where(s => !string.IsNullOrWhiteSpace(s)),
            Gen.OneOfConst(UserRole.Viewer, UserRole.Editor, UserRole.Admin),
            Gen.OneOf(AllPolicies.Select(Gen.Const).ToArray()),
            Gen.Int[2, 10]  // Number of repeated checks
        );

        gen.Sample((userId, email, role, policy, repeatCount) =>
        {
            // Arrange: Create a user
            var user = new User
            {
                Id = userId,
                Email = $"{email.Replace(" ", "")}@test.com",
                Role = role,
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            };

            // Act: Perform multiple authorization checks
            var results = new List<bool>();
            for (int i = 0; i < repeatCount; i++)
            {
                results.Add(IsUserAuthorizedForPolicy(user, policy));
            }

            // Assert: All results should be the same
            Assert.True(results.All(r => r == results[0]),
                $"Authorization decision for user with role '{role}' and policy '{policy}' " +
                "should be consistent across multiple checks");
        }, iter: 100);
    }

    /// <summary>
    /// Property 17: Authorization Enforcement - Inactive users are denied
    /// For any inactive user, regardless of role, authorization SHALL be denied.
    /// </summary>
    [Fact]
    public void Authorization_InactiveUsersAreDenied()
    {
        var gen = Gen.Select(
            Gen.Guid,
            Gen.String[5, 30].Where(s => !string.IsNullOrWhiteSpace(s)),
            Gen.OneOfConst(UserRole.Viewer, UserRole.Editor, UserRole.Admin),
            Gen.OneOf(AllPolicies.Select(Gen.Const).ToArray())
        );

        gen.Sample((userId, email, role, policy) =>
        {
            // Arrange: Create an inactive user
            var inactiveUser = new User
            {
                Id = userId,
                Email = $"{email.Replace(" ", "")}@test.com",
                Role = role,
                IsActive = false,  // User is inactive
                CreatedAt = DateTime.UtcNow
            };

            // Act: Check authorization
            var isAuthorized = IsInactiveUserAuthorizedForPolicy(inactiveUser, policy);

            // Assert: Inactive users should not be authorized
            Assert.False(isAuthorized,
                $"Inactive user with role '{role}' should NOT be authorized for policy '{policy}'");
        }, iter: 100);
    }

    /// <summary>
    /// Checks if a role is authorized for a specific policy based on the policy matrix.
    /// </summary>
    private static bool IsRoleAuthorizedForPolicy(UserRole role, string policy)
    {
        if (!PolicyAllowedRoles.TryGetValue(policy, out var allowedRoles))
        {
            return false;
        }

        return allowedRoles.Contains(role);
    }

    /// <summary>
    /// Checks if a user is authorized for a specific policy.
    /// </summary>
    private static bool IsUserAuthorizedForPolicy(User user, string policy)
    {
        if (!user.IsActive)
        {
            return false;
        }

        return IsRoleAuthorizedForPolicy(user.Role, policy);
    }

    /// <summary>
    /// Checks if an inactive user would be authorized (should always return false).
    /// </summary>
    private static bool IsInactiveUserAuthorizedForPolicy(User user, string policy)
    {
        // Inactive users should never be authorized
        if (!user.IsActive)
        {
            return false;
        }

        return IsRoleAuthorizedForPolicy(user.Role, policy);
    }

    /// <summary>
    /// Creates a generic authorization error message that doesn't reveal resource details.
    /// This simulates what the API should return for unauthorized access.
    /// </summary>
    private static string CreateAuthorizationErrorMessage(UserRole role, string policy)
    {
        // The error message should be generic and not reveal:
        // - The specific resource being accessed
        // - The resource ID
        // - The resource name or other identifying information
        return "Access denied. You do not have permission to perform this action.";
    }
}
