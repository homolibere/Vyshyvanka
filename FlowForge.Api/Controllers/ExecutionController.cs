using FlowForge.Api.Authorization;
using FlowForge.Api.Models;
using FlowForge.Core.Enums;
using FlowForge.Core.Interfaces;
using FlowForge.Engine.Credentials;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ExecutionContext = FlowForge.Engine.Execution.ExecutionContext;

namespace FlowForge.Api.Controllers;

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
    private readonly ILogger<ExecutionController> _logger;

    public ExecutionController(
        IWorkflowEngine workflowEngine,
        IWorkflowRepository workflowRepository,
        IExecutionRepository executionRepository,
        ILogger<ExecutionController> logger,
        ICredentialService? credentialService = null)
    {
        _workflowEngine = workflowEngine ?? throw new ArgumentNullException(nameof(workflowEngine));
        _workflowRepository = workflowRepository ?? throw new ArgumentNullException(nameof(workflowRepository));
        _executionRepository = executionRepository ?? throw new ArgumentNullException(nameof(executionRepository));
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
        
        if (!workflow.IsActive)
        {
            return BadRequest(new ApiError
            {
                Code = "WORKFLOW_INACTIVE",
                Message = "Cannot execute an inactive workflow"
            });
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
            cancellationToken);
        
        // Add trigger data to context if provided
        if (request.InputData.HasValue)
        {
            context.Variables["input"] = request.InputData.Value;
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
}
