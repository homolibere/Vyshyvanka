using Vyshyvanka.Api.Authorization;
using Vyshyvanka.Api.Models;
using Vyshyvanka.Core.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Vyshyvanka.Api.Controllers;

/// <summary>
/// API controller for team management.
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public class TeamController(
    ITeamService teamService,
    ICurrentUserService currentUserService,
    ILogger<TeamController> logger) : ControllerBase
{
    /// <summary>
    /// Gets all teams the current user belongs to.
    /// </summary>
    [HttpGet]
    [Authorize(Policy = Policies.CanViewWorkflows)]
    [ProducesResponseType(typeof(List<TeamResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<List<TeamResponse>>> GetAll(CancellationToken cancellationToken = default)
    {
        var userId = currentUserService.UserId;
        if (userId is null)
            return Unauthorized();

        var teams = await teamService.GetUserTeamsAsync(userId.Value, cancellationToken);
        return Ok(teams.Select(TeamResponse.FromModel).ToList());
    }

    /// <summary>
    /// Gets a team by ID.
    /// </summary>
    [HttpGet("{id:guid}")]
    [Authorize(Policy = Policies.CanViewWorkflows)]
    [ProducesResponseType(typeof(TeamResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<TeamResponse>> GetById(Guid id, CancellationToken cancellationToken = default)
    {
        var userId = currentUserService.UserId;
        if (userId is null)
            return Unauthorized();

        var team = await teamService.GetByIdAsync(id, userId.Value, cancellationToken);
        if (team is null)
        {
            return NotFound(new ApiError
            {
                Code = "TEAM_NOT_FOUND",
                Message = $"Team with ID '{id}' was not found"
            });
        }

        return Ok(TeamResponse.FromModel(team));
    }

    /// <summary>
    /// Creates a new team.
    /// </summary>
    [HttpPost]
    [Authorize(Policy = Policies.CanManageWorkflows)]
    [ProducesResponseType(typeof(TeamResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<TeamResponse>> Create(
        [FromBody] CreateTeamRequest request,
        CancellationToken cancellationToken = default)
    {
        var userId = currentUserService.UserId ?? Guid.Empty;

        try
        {
            var team = await teamService.CreateAsync(request.Name, request.Description, userId, cancellationToken);
            logger.LogInformation("Created team {TeamId}: {TeamName}", team.Id, team.Name);
            return CreatedAtAction(nameof(GetById), new { id = team.Id }, TeamResponse.FromModel(team));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new ApiError
            {
                Code = "TEAM_CREATION_FAILED",
                Message = ex.Message
            });
        }
    }

    /// <summary>
    /// Updates a team.
    /// </summary>
    [HttpPut("{id:guid}")]
    [Authorize(Policy = Policies.CanManageWorkflows)]
    [ProducesResponseType(typeof(TeamResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<TeamResponse>> Update(
        Guid id,
        [FromBody] UpdateTeamRequest request,
        CancellationToken cancellationToken = default)
    {
        var userId = currentUserService.UserId ?? Guid.Empty;

        try
        {
            var team = await teamService.UpdateAsync(id, request.Name, request.Description, userId, cancellationToken);
            logger.LogInformation("Updated team {TeamId}: {TeamName}", team.Id, team.Name);
            return Ok(TeamResponse.FromModel(team));
        }
        catch (InvalidOperationException ex)
        {
            return NotFound(new ApiError
            {
                Code = "TEAM_NOT_FOUND",
                Message = ex.Message
            });
        }
        catch (UnauthorizedAccessException ex)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new ApiError
            {
                Code = "FORBIDDEN",
                Message = ex.Message
            });
        }
    }

    /// <summary>
    /// Deletes a team.
    /// </summary>
    [HttpDelete("{id:guid}")]
    [Authorize(Policy = Policies.CanManageWorkflows)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken = default)
    {
        var userId = currentUserService.UserId ?? Guid.Empty;

        try
        {
            var deleted = await teamService.DeleteAsync(id, userId, cancellationToken);
            if (!deleted)
            {
                return NotFound(new ApiError
                {
                    Code = "TEAM_NOT_FOUND",
                    Message = $"Team with ID '{id}' was not found"
                });
            }

            logger.LogInformation("Deleted team {TeamId}", id);
            return NoContent();
        }
        catch (UnauthorizedAccessException ex)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new ApiError
            {
                Code = "FORBIDDEN",
                Message = ex.Message
            });
        }
    }

    /// <summary>
    /// Adds a member to a team.
    /// </summary>
    [HttpPost("{id:guid}/members")]
    [Authorize(Policy = Policies.CanManageWorkflows)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> AddMember(
        Guid id,
        [FromBody] AddTeamMemberRequest request,
        CancellationToken cancellationToken = default)
    {
        var userId = currentUserService.UserId ?? Guid.Empty;

        try
        {
            await teamService.AddMemberAsync(id, request.UserId, request.Role, userId, cancellationToken);
            logger.LogInformation("Added user {MemberId} to team {TeamId}", request.UserId, id);
            return NoContent();
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new ApiError
            {
                Code = "MEMBER_ADD_FAILED",
                Message = ex.Message
            });
        }
        catch (UnauthorizedAccessException ex)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new ApiError
            {
                Code = "FORBIDDEN",
                Message = ex.Message
            });
        }
    }

    /// <summary>
    /// Removes a member from a team.
    /// </summary>
    [HttpDelete("{id:guid}/members/{memberId:guid}")]
    [Authorize(Policy = Policies.CanManageWorkflows)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> RemoveMember(
        Guid id,
        Guid memberId,
        CancellationToken cancellationToken = default)
    {
        var userId = currentUserService.UserId ?? Guid.Empty;

        try
        {
            await teamService.RemoveMemberAsync(id, memberId, userId, cancellationToken);
            logger.LogInformation("Removed user {MemberId} from team {TeamId}", memberId, id);
            return NoContent();
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new ApiError
            {
                Code = "MEMBER_REMOVE_FAILED",
                Message = ex.Message
            });
        }
        catch (UnauthorizedAccessException ex)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new ApiError
            {
                Code = "FORBIDDEN",
                Message = ex.Message
            });
        }
    }
}
