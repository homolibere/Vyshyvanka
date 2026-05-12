using System.Text.Json;
using Vyshyvanka.Api.Authorization;
using Vyshyvanka.Api.Models;
using Vyshyvanka.Core.Enums;
using Vyshyvanka.Core.Interfaces;
using Vyshyvanka.Core.Models;
using Vyshyvanka.Engine.Credentials;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ExecutionContext = Vyshyvanka.Engine.Execution.ExecutionContext;

namespace Vyshyvanka.Api.Controllers;

/// <summary>
/// API controller for workflow execution operations.
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public class ExecutionController : ControllerBase
{
    private readonly IWorkflowEngine _workflowEngine;
    private readonly IWorkflowRepository _workflowRepository;
    private readonly IExecutionRepository _executionRepository;
    private readonly ICredentialService? _credentialService;
    private readonly ICurrentUserService _currentUserService;
    private readonly ILogger<ExecutionController> _logger;

    public ExecutionController(
        IWorkflowEngine workflowEngine,
        IWorkflowRepository workflowRepository,
        IExecutionRepository executionRepository,
        ICurrentUserService currentUserService,
        ILogger<ExecutionController> logger,
        ICredentialService? credentialService = null)
    {
        _workflowEngine = workflowEngine ?? throw new ArgumentNullException(nameof(workflowEngine));
        _workflowRepository = workflowRepository ?? throw new ArgumentNullException(nameof(workflowRepository));
        _executionRepository = executionRepository ?? throw new ArgumentNullException(nameof(executionRepository));
        _currentUserService = currentUserService ?? throw new ArgumentNullException(nameof(currentUserService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _credentialService = credentialService;
    }

    /// <summary>
    /// Triggers a workflow execution.
    /// </summary>
    [HttpPost]
    [Authorize(Policy = Policies.CanExecuteWorkflows)]
    [ProducesResponseType(typeof(ExecutionResponse), StatusCodes.Status202Accepted)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ExecutionResponse>> TriggerExecution(
        [FromBody] TriggerExecutionRequest request,
        CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Triggering execution for workflow {WorkflowId}", request.WorkflowId);

        // Get the workflow
        var workflow = await _workflowRepository.GetByIdAsync(request.WorkflowId, cancellationToken);
        if (workflow is null)
        {
            return NotFound(new ApiError
            {
                Code = "WORKFLOW_NOT_FOUND",
                Message = $"Workflow with ID '{request.WorkflowId}' was not found"
            });
        }

        // Verify ownership: only the workflow owner or an Admin can execute it
        if (!IsOwnerOrAdmin(workflow))
        {
            return StatusCode(StatusCodes.Status403Forbidden, new ApiError
            {
                Code = "FORBIDDEN",
                Message = "You do not have permission to execute this workflow"
            });
        }

        if (!workflow.IsActive)
        {
            return BadRequest(new ApiError
            {
                Code = "WORKFLOW_INACTIVE",
                Message = "Cannot execute an inactive workflow"
            });
        }

        // Prune workflow to target node if partial execution requested
        if (!string.IsNullOrWhiteSpace(request.TargetNodeId))
        {
            if (workflow.Nodes.All(n => n.Id != request.TargetNodeId))
            {
                return BadRequest(new ApiError
                {
                    Code = "NODE_NOT_FOUND",
                    Message = $"Node '{request.TargetNodeId}' does not exist in the workflow"
                });
            }

            workflow = PruneWorkflowToNode(workflow, request.TargetNodeId);
        }

        // Create credential provider
        ICredentialProvider credentialProvider = _credentialService is not null
            ? new CredentialProvider(_credentialService)
            : NullCredentialProvider.Instance;

        // Create execution context
        var executionId = Guid.NewGuid();
        var context = new ExecutionContext(
            executionId,
            workflow.Id,
            credentialProvider,
            cancellationToken,
            HttpContext.RequestServices,
            _currentUserService.UserId,
            _logger);

        // Add trigger data to context if provided
        if (request.InputData.HasValue)
        {
            context.Variables["input"] = request.InputData.Value;
        }

        // When excluding the target node, tell the engine to gather input only (don't execute it)
        if (!request.IncludeTargetNode && !string.IsNullOrWhiteSpace(request.TargetNodeId))
        {
            context.Variables["__gatherInputOnlyNodeId"] = request.TargetNodeId;
        }

        try
        {
            // Execute the workflow
            var result = await _workflowEngine.ExecuteAsync(workflow, context, cancellationToken);

            // Get the persisted execution record
            var execution = await _executionRepository.GetByIdAsync(executionId, cancellationToken);
            if (execution is null)
            {
                // Create a response from the result if not persisted
                return Accepted(new ExecutionResponse
                {
                    Id = executionId,
                    WorkflowId = workflow.Id,
                    WorkflowVersion = workflow.Version,
                    Status = result.Success ? ExecutionStatus.Completed : ExecutionStatus.Failed,
                    Mode = request.Mode,
                    StartedAt = DateTime.UtcNow,
                    CompletedAt = DateTime.UtcNow,
                    Duration = result.Duration,
                    ErrorMessage = result.ErrorMessage
                });
            }

            _logger.LogInformation(
                "Workflow {WorkflowId} execution {ExecutionId} completed with status {Status}",
                workflow.Id, execution.Id, execution.Status);

            return Accepted(ExecutionResponse.FromModel(execution));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error executing workflow {WorkflowId}", request.WorkflowId);
            return BadRequest(new ApiError
            {
                Code = "EXECUTION_FAILED",
                Message = $"Workflow execution failed: {ex.Message}"
            });
        }
    }

    /// <summary>
    /// Executes a single node with provided input data (no full workflow execution).
    /// Used for quick iteration when the node's configuration has no expression references.
    /// </summary>
    [HttpPost("node")]
    [Authorize(Policy = Policies.CanExecuteWorkflows)]
    [ProducesResponseType(typeof(NodeExecutionResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<NodeExecutionResponse>> ExecuteSingleNode(
        [FromBody] ExecuteNodeRequest request,
        CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Executing single node {NodeId} in workflow {WorkflowId}",
            request.NodeId, request.WorkflowId);

        var workflow = await _workflowRepository.GetByIdAsync(request.WorkflowId, cancellationToken);
        if (workflow is null)
        {
            return NotFound(new ApiError
            {
                Code = "WORKFLOW_NOT_FOUND",
                Message = $"Workflow with ID '{request.WorkflowId}' was not found"
            });
        }

        if (!IsOwnerOrAdmin(workflow))
        {
            return StatusCode(StatusCodes.Status403Forbidden, new ApiError
            {
                Code = "FORBIDDEN",
                Message = "You do not have permission to execute this workflow"
            });
        }

        var node = workflow.Nodes.FirstOrDefault(n => n.Id == request.NodeId);
        if (node is null)
        {
            return BadRequest(new ApiError
            {
                Code = "NODE_NOT_FOUND",
                Message = $"Node '{request.NodeId}' does not exist in the workflow"
            });
        }

        // Create credential provider
        ICredentialProvider credentialProvider = _credentialService is not null
            ? new CredentialProvider(_credentialService)
            : NullCredentialProvider.Instance;

        var executionId = Guid.NewGuid();
        var context = new ExecutionContext(
            executionId,
            workflow.Id,
            credentialProvider,
            cancellationToken,
            HttpContext.RequestServices,
            _currentUserService.UserId,
            _logger);

        try
        {
            var result = await _workflowEngine.ExecuteNodeWithInputAsync(
                node, request.InputData, context, cancellationToken);

            var nodeResult = result.NodeResults.FirstOrDefault();

            return Ok(new NodeExecutionResponse
            {
                NodeId = request.NodeId,
                Status = result.Success ? ExecutionStatus.Completed : ExecutionStatus.Failed,
                StartedAt = DateTime.UtcNow - result.Duration,
                CompletedAt = DateTime.UtcNow,
                Duration = result.Duration,
                InputData = request.InputData,
                OutputData = nodeResult?.OutputData is JsonElement el ? el : null,
                ErrorMessage = result.ErrorMessage
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error executing node {NodeId}", request.NodeId);
            return BadRequest(new ApiError
            {
                Code = "NODE_EXECUTION_FAILED",
                Message = $"Node execution failed: {ex.Message}"
            });
        }
    }


    /// <summary>
    /// Gets an execution by ID.
    /// </summary>
    [HttpGet("{id:guid}")]
    [Authorize(Policy = Policies.CanViewWorkflows)]
    [ProducesResponseType(typeof(ExecutionResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ExecutionResponse>> GetById(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Getting execution {ExecutionId}", id);

        var execution = await _executionRepository.GetByIdAsync(id, cancellationToken);
        if (execution is null)
        {
            return NotFound(new ApiError
            {
                Code = "EXECUTION_NOT_FOUND",
                Message = $"Execution with ID '{id}' was not found"
            });
        }

        return Ok(ExecutionResponse.FromModel(execution));
    }

    /// <summary>
    /// Gets execution history with filtering.
    /// </summary>
    [HttpGet]
    [Authorize(Policy = Policies.CanViewWorkflows)]
    [ProducesResponseType(typeof(PagedResponse<ExecutionSummaryResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedResponse<ExecutionSummaryResponse>>> GetHistory(
        [FromQuery] ExecutionQueryRequest query,
        CancellationToken cancellationToken = default)
    {
        _logger.LogDebug(
            "Getting execution history: workflowId={WorkflowId}, status={Status}, skip={Skip}, take={Take}",
            query.WorkflowId, query.Status, query.Skip, query.Take);

        var executionQuery = new ExecutionQuery
        {
            WorkflowId = query.WorkflowId,
            Status = query.Status,
            Mode = query.Mode,
            StartDateFrom = query.StartDateFrom,
            StartDateTo = query.StartDateTo,
            Skip = query.Skip,
            Take = query.Take
        };

        var executions = await _executionRepository.QueryAsync(executionQuery, cancellationToken);

        var response = new PagedResponse<ExecutionSummaryResponse>
        {
            Items = executions.Select(ExecutionSummaryResponse.FromModel).ToList(),
            Skip = query.Skip,
            Take = query.Take,
            TotalCount = executions.Count
        };

        return Ok(response);
    }

    /// <summary>
    /// Gets executions for a specific workflow.
    /// </summary>
    [HttpGet("workflow/{workflowId:guid}")]
    [Authorize(Policy = Policies.CanViewWorkflows)]
    [ProducesResponseType(typeof(PagedResponse<ExecutionSummaryResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedResponse<ExecutionSummaryResponse>>> GetByWorkflow(
        Guid workflowId,
        [FromQuery] int skip = 0,
        [FromQuery] int take = 50,
        CancellationToken cancellationToken = default)
    {
        take = Math.Clamp(take, 1, 100);
        _logger.LogDebug("Getting executions for workflow {WorkflowId}", workflowId);

        var executions = await _executionRepository.GetByWorkflowIdAsync(workflowId, skip, take, cancellationToken);

        var response = new PagedResponse<ExecutionSummaryResponse>
        {
            Items = executions.Select(ExecutionSummaryResponse.FromModel).ToList(),
            Skip = skip,
            Take = take,
            TotalCount = executions.Count
        };

        return Ok(response);
    }

    /// <summary>
    /// Gets executions by status.
    /// </summary>
    [HttpGet("status/{status}")]
    [Authorize(Policy = Policies.CanViewWorkflows)]
    [ProducesResponseType(typeof(PagedResponse<ExecutionSummaryResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedResponse<ExecutionSummaryResponse>>> GetByStatus(
        ExecutionStatus status,
        [FromQuery] int skip = 0,
        [FromQuery] int take = 50,
        CancellationToken cancellationToken = default)
    {
        take = Math.Clamp(take, 1, 100);
        _logger.LogDebug("Getting executions with status {Status}", status);

        var executions = await _executionRepository.GetByStatusAsync(status, skip, take, cancellationToken);

        var response = new PagedResponse<ExecutionSummaryResponse>
        {
            Items = executions.Select(ExecutionSummaryResponse.FromModel).ToList(),
            Skip = skip,
            Take = take,
            TotalCount = executions.Count
        };

        return Ok(response);
    }

    /// <summary>
    /// Cancels a running execution.
    /// </summary>
    [HttpPost("{id:guid}/cancel")]
    [Authorize(Policy = Policies.CanExecuteWorkflows)]
    [ProducesResponseType(StatusCodes.Status202Accepted)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> CancelExecution(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Cancelling execution {ExecutionId}", id);

        var execution = await _executionRepository.GetByIdAsync(id, cancellationToken);
        if (execution is null)
        {
            return NotFound(new ApiError
            {
                Code = "EXECUTION_NOT_FOUND",
                Message = $"Execution with ID '{id}' was not found"
            });
        }

        if (execution.Status != ExecutionStatus.Running && execution.Status != ExecutionStatus.Pending)
        {
            return BadRequest(new ApiError
            {
                Code = "EXECUTION_NOT_CANCELLABLE",
                Message = $"Execution with status '{execution.Status}' cannot be cancelled"
            });
        }

        await _workflowEngine.CancelExecutionAsync(id);
        _logger.LogInformation("Cancelled execution {ExecutionId}", id);

        return Accepted();
    }

    /// <summary>
    /// Prunes a workflow to only include nodes that are ancestors of (or equal to) the target node.
    /// Walks backward from the target through incoming connections to find all required nodes.
    /// </summary>
    private static Workflow PruneWorkflowToNode(Workflow workflow, string targetNodeId)
    {
        var requiredNodeIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { targetNodeId };
        var queue = new Queue<string>();
        queue.Enqueue(targetNodeId);

        while (queue.Count > 0)
        {
            var current = queue.Dequeue();
            var incomingConnections = workflow.Connections
                .Where(c => string.Equals(c.TargetNodeId, current, StringComparison.OrdinalIgnoreCase));

            foreach (var conn in incomingConnections)
            {
                if (requiredNodeIds.Add(conn.SourceNodeId))
                {
                    queue.Enqueue(conn.SourceNodeId);
                }
            }
        }

        return workflow with
        {
            Nodes = workflow.Nodes.Where(n => requiredNodeIds.Contains(n.Id)).ToList(),
            Connections = workflow.Connections.Where(c =>
                requiredNodeIds.Contains(c.SourceNodeId) &&
                requiredNodeIds.Contains(c.TargetNodeId)).ToList()
        };
    }

    private bool IsOwnerOrAdmin(Workflow workflow)
    {
        if (User.IsInRole(Roles.Admin))
            return true;

        var userId = _currentUserService.UserId;
        return userId is not null && workflow.CreatedBy == userId;
    }
}
