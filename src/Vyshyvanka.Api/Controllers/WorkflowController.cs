using Vyshyvanka.Api.Authorization;
using Vyshyvanka.Api.Models;
using Vyshyvanka.Core.Interfaces;
using Vyshyvanka.Core.Models;
using Vyshyvanka.Engine.Validation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Vyshyvanka.Api.Controllers;

/// <summary>
/// API controller for workflow CRUD operations.
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public class WorkflowController : ControllerBase
{
    private readonly IWorkflowRepository _repository;
    private readonly WorkflowValidator _validator;
    private readonly ICurrentUserService _currentUserService;
    private readonly ILogger<WorkflowController> _logger;

    public WorkflowController(
        IWorkflowRepository repository,
        WorkflowValidator validator,
        ICurrentUserService currentUserService,
        ILogger<WorkflowController> logger)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _validator = validator ?? throw new ArgumentNullException(nameof(validator));
        _currentUserService = currentUserService ?? throw new ArgumentNullException(nameof(currentUserService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Gets all workflows with pagination.
    /// Non-Admin users only see their own workflows.
    /// </summary>
    [HttpGet]
    [Authorize(Policy = Policies.CanViewWorkflows)]
    [ProducesResponseType(typeof(PagedResponse<WorkflowResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedResponse<WorkflowResponse>>> GetAll(
        [FromQuery] int skip = 0,
        [FromQuery] int take = 50,
        [FromQuery] string? search = null,
        CancellationToken cancellationToken = default)
    {
        take = Math.Clamp(take, 1, 100);
        _logger.LogDebug("Getting workflows: skip={Skip}, take={Take}, search={Search}", skip, take, search);

        IReadOnlyList<Workflow> workflows;

        if (User.IsInRole(Roles.Admin))
        {
            workflows = string.IsNullOrWhiteSpace(search)
                ? await _repository.GetAllAsync(skip, take, cancellationToken)
                : await _repository.SearchAsync(search, skip, take, cancellationToken);
        }
        else
        {
            var userId = _currentUserService.UserId;
            if (userId is null)
            {
                workflows = [];
            }
            else if (string.IsNullOrWhiteSpace(search))
            {
                workflows = await _repository.GetByCreatorAsync(userId.Value, skip, take, cancellationToken);
            }
            else
            {
                // Search then filter by owner — pagination is approximate for non-Admin users
                var allResults = await _repository.SearchAsync(search, skip, take, cancellationToken);
                workflows = allResults.Where(w => w.CreatedBy == userId.Value).ToList();
            }
        }

        var response = new PagedResponse<WorkflowResponse>
        {
            Items = workflows.Select(WorkflowResponse.FromModel).ToList(),
            Skip = skip,
            Take = take,
            TotalCount = workflows.Count
        };

        return Ok(response);
    }

    /// <summary>
    /// Gets a workflow by ID.
    /// </summary>
    [HttpGet("{id:guid}")]
    [Authorize(Policy = Policies.CanViewWorkflows)]
    [ProducesResponseType(typeof(WorkflowResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<WorkflowResponse>> GetById(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Getting workflow {WorkflowId}", id);

        var workflow = await _repository.GetByIdAsync(id, cancellationToken);
        if (workflow is null || !IsOwnerOrAdmin(workflow))
        {
            return NotFound(new ApiError
            {
                Code = "WORKFLOW_NOT_FOUND",
                Message = $"Workflow with ID '{id}' was not found"
            });
        }

        return Ok(WorkflowResponse.FromModel(workflow));
    }


    /// <summary>
    /// Creates a new workflow.
    /// </summary>
    [HttpPost]
    [Authorize(Policy = Policies.CanManageWorkflows)]
    [ProducesResponseType(typeof(WorkflowResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<WorkflowResponse>> Create(
        [FromBody] CreateWorkflowRequest request,
        CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Creating workflow: {WorkflowName}", request.Name);

        var workflow = MapToWorkflow(request, _currentUserService.UserId ?? Guid.Empty);

        // Validate the workflow
        var validationResult = _validator.Validate(workflow);
        if (!validationResult.IsValid)
        {
            return BadRequest(new ApiError
            {
                Code = "WORKFLOW_VALIDATION_FAILED",
                Message = "Workflow validation failed",
                Details = validationResult.Errors.ToDictionary(
                    e => e.Path,
                    e => new[] { e.Message })
            });
        }

        var created = await _repository.CreateAsync(workflow, cancellationToken);
        _logger.LogInformation("Created workflow {WorkflowId}: {WorkflowName}", created.Id, created.Name);

        return CreatedAtAction(
            nameof(GetById),
            new { id = created.Id },
            WorkflowResponse.FromModel(created));
    }

    /// <summary>
    /// Updates an existing workflow.
    /// </summary>
    [HttpPut("{id:guid}")]
    [Authorize(Policy = Policies.CanManageWorkflows)]
    [ProducesResponseType(typeof(WorkflowResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<WorkflowResponse>> Update(
        Guid id,
        [FromBody] UpdateWorkflowRequest request,
        CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Updating workflow {WorkflowId}", id);

        var existing = await _repository.GetByIdAsync(id, cancellationToken);
        if (existing is null || !IsOwnerOrAdmin(existing))
        {
            return NotFound(new ApiError
            {
                Code = "WORKFLOW_NOT_FOUND",
                Message = $"Workflow with ID '{id}' was not found"
            });
        }

        // Check version for optimistic concurrency
        if (existing.Version != request.Version)
        {
            return Conflict(new ApiError
            {
                Code = "WORKFLOW_VERSION_CONFLICT",
                Message =
                    $"Workflow has been modified. Expected version {request.Version}, but current version is {existing.Version}"
            });
        }

        var workflow = MapToWorkflow(request, existing);

        // Validate the workflow
        var validationResult = _validator.Validate(workflow);
        if (!validationResult.IsValid)
        {
            return BadRequest(new ApiError
            {
                Code = "WORKFLOW_VALIDATION_FAILED",
                Message = "Workflow validation failed",
                Details = validationResult.Errors.ToDictionary(
                    e => e.Path,
                    e => new[] { e.Message })
            });
        }

        var updated = await _repository.UpdateAsync(workflow, cancellationToken);
        _logger.LogInformation("Updated workflow {WorkflowId}: {WorkflowName}", updated.Id, updated.Name);

        return Ok(WorkflowResponse.FromModel(updated));
    }


    /// <summary>
    /// Deletes a workflow.
    /// </summary>
    [HttpDelete("{id:guid}")]
    [Authorize(Policy = Policies.CanManageWorkflows)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Deleting workflow {WorkflowId}", id);

        var existing = await _repository.GetByIdAsync(id, cancellationToken);
        if (existing is null || !IsOwnerOrAdmin(existing))
        {
            return NotFound(new ApiError
            {
                Code = "WORKFLOW_NOT_FOUND",
                Message = $"Workflow with ID '{id}' was not found"
            });
        }

        await _repository.DeleteAsync(id, cancellationToken);

        _logger.LogInformation("Deleted workflow {WorkflowId}", id);
        return NoContent();
    }

    /// <summary>
    /// Gets active workflows.
    /// Non-Admin users only see their own active workflows.
    /// </summary>
    [HttpGet("active")]
    [Authorize(Policy = Policies.CanViewWorkflows)]
    [ProducesResponseType(typeof(PagedResponse<WorkflowResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedResponse<WorkflowResponse>>> GetActive(
        [FromQuery] int skip = 0,
        [FromQuery] int take = 50,
        CancellationToken cancellationToken = default)
    {
        take = Math.Clamp(take, 1, 100);
        _logger.LogDebug("Getting active workflows: skip={Skip}, take={Take}", skip, take);

        var allActive = await _repository.GetActiveAsync(skip, take, cancellationToken);

        IReadOnlyList<Workflow> workflows;
        if (User.IsInRole(Roles.Admin))
        {
            workflows = allActive;
        }
        else
        {
            var userId = _currentUserService.UserId;
            workflows = userId is null
                ? []
                : allActive.Where(w => w.CreatedBy == userId.Value).ToList();
        }

        var response = new PagedResponse<WorkflowResponse>
        {
            Items = workflows.Select(WorkflowResponse.FromModel).ToList(),
            Skip = skip,
            Take = take,
            TotalCount = workflows.Count
        };

        return Ok(response);
    }

    private static Workflow MapToWorkflow(CreateWorkflowRequest request, Guid createdBy)
    {
        var now = DateTime.UtcNow;
        return new Workflow
        {
            Id = Guid.NewGuid(),
            Name = request.Name,
            Description = request.Description,
            Version = 1,
            IsActive = request.IsActive,
            Nodes = request.Nodes.Select(MapToNode).ToList(),
            Connections = request.Connections.Select(MapToConnection).ToList(),
            Settings = MapToSettings(request.Settings),
            Tags = request.Tags,
            CreatedAt = now,
            UpdatedAt = now,
            CreatedBy = createdBy
        };
    }

    private static Workflow MapToWorkflow(UpdateWorkflowRequest request, Workflow existing)
    {
        return existing with
        {
            Name = request.Name,
            Description = request.Description,
            Version = existing.Version + 1,
            IsActive = request.IsActive,
            Nodes = request.Nodes.Select(MapToNode).ToList(),
            Connections = request.Connections.Select(MapToConnection).ToList(),
            Settings = MapToSettings(request.Settings),
            Tags = request.Tags,
            UpdatedAt = DateTime.UtcNow
        };
    }

    private static WorkflowNode MapToNode(WorkflowNodeDto dto)
    {
        return new WorkflowNode
        {
            Id = dto.Id,
            Type = dto.Type,
            Name = dto.Name,
            Configuration = dto.Configuration ?? default,
            Position = new Position(dto.Position.X, dto.Position.Y),
            CredentialId = dto.CredentialId
        };
    }

    private static Connection MapToConnection(ConnectionDto dto)
    {
        return new Connection
        {
            SourceNodeId = dto.SourceNodeId,
            SourcePort = dto.SourcePort,
            TargetNodeId = dto.TargetNodeId,
            TargetPort = dto.TargetPort
        };
    }

    private static WorkflowSettings MapToSettings(WorkflowSettingsDto? dto)
    {
        if (dto is null)
        {
            return new WorkflowSettings();
        }

        return new WorkflowSettings
        {
            Timeout = dto.TimeoutSeconds.HasValue
                ? TimeSpan.FromSeconds(dto.TimeoutSeconds.Value)
                : null,
            MaxRetries = dto.MaxRetries,
            ErrorHandling = dto.ErrorHandling,
            MaxDegreeOfParallelism = dto.MaxDegreeOfParallelism
        };
    }

    private bool IsOwnerOrAdmin(Workflow workflow)
    {
        if (User.IsInRole("Admin"))
            return true;

        var userId = _currentUserService.UserId;
        return userId is not null && workflow.CreatedBy == userId;
    }
}
