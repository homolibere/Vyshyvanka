using Vyshyvanka.Api.Authorization;
using Vyshyvanka.Core.Enums;
using Vyshyvanka.Core.Interfaces;
using Vyshyvanka.Core.Models;
using Microsoft.AspNetCore.Mvc;

namespace Vyshyvanka.Api.Controllers;

/// <summary>
/// Base controller providing shared permission-checking logic.
/// </summary>
public abstract class VyshyvankaControllerBase : ControllerBase
{
    protected readonly ICurrentUserService _currentUserService;
    protected readonly IWorkflowPermissionService _permissionService;

    protected VyshyvankaControllerBase(
        ICurrentUserService currentUserService,
        IWorkflowPermissionService permissionService)
    {
        _currentUserService = currentUserService ?? throw new ArgumentNullException(nameof(currentUserService));
        _permissionService = permissionService ?? throw new ArgumentNullException(nameof(permissionService));
    }

    protected async Task<bool> HasPermissionAsync(Workflow workflow, WorkflowPermissionLevel requiredLevel, CancellationToken cancellationToken)
    {
        var userId = _currentUserService.UserId;
        if (userId is null)
            return false;

        var isAdmin = User.IsInRole(Roles.Admin);
        return await _permissionService.HasPermissionAsync(
            workflow.Id, workflow.CreatedBy, userId.Value, requiredLevel, isAdmin, cancellationToken);
    }
}
