using System.Text.Json;
using Vyshyvanka.Api.Models;
using Vyshyvanka.Core.Enums;
using Vyshyvanka.Core.Interfaces;
using Vyshyvanka.Engine.Credentials;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ExecutionContext = Vyshyvanka.Engine.Execution.ExecutionContext;

namespace Vyshyvanka.Api.Controllers;

/// <summary>
/// API controller for webhook trigger endpoints.
/// Webhooks are anonymous to allow external systems to trigger workflows.
/// </summary>
[ApiController]
[Route("api/[controller]")]
[AllowAnonymous]
public class WebhookController : ControllerBase
{
    private readonly IWorkflowEngine _workflowEngine;
    private readonly IWorkflowRepository _workflowRepository;
    private readonly IExecutionRepository _executionRepository;
    private readonly ICredentialService? _credentialService;
    private readonly ILogger<WebhookController> _logger;

    public WebhookController(
        IWorkflowEngine workflowEngine,
        IWorkflowRepository workflowRepository,
        IExecutionRepository executionRepository,
        ILogger<WebhookController> logger,
        ICredentialService? credentialService = null)
    {
        _workflowEngine = workflowEngine ?? throw new ArgumentNullException(nameof(workflowEngine));
        _workflowRepository = workflowRepository ?? throw new ArgumentNullException(nameof(workflowRepository));
        _executionRepository = executionRepository ?? throw new ArgumentNullException(nameof(executionRepository));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _credentialService = credentialService;
    }

    /// <summary>
    /// Triggers a workflow via webhook by workflow ID.
    /// Accepts any HTTP method and passes request data to the workflow.
    /// </summary>
    [HttpGet("{workflowId:guid}")]
    [HttpPost("{workflowId:guid}")]
    [HttpPut("{workflowId:guid}")]
    [HttpDelete("{workflowId:guid}")]
    [HttpPatch("{workflowId:guid}")]
    [ProducesResponseType(typeof(WebhookResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> TriggerByWorkflowId(
        Guid workflowId,
        CancellationToken cancellationToken = default)
    {
        return await TriggerWebhookAsync(workflowId, cancellationToken);
    }

    /// <summary>
    /// Triggers a workflow via webhook by path.
    /// The path is matched against workflow webhook configurations.
    /// </summary>
    [HttpGet("path/{*webhookPath}")]
    [HttpPost("path/{*webhookPath}")]
    [HttpPut("path/{*webhookPath}")]
    [HttpDelete("path/{*webhookPath}")]
    [HttpPatch("path/{*webhookPath}")]
    [ProducesResponseType(typeof(WebhookResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> TriggerByPath(
        string webhookPath,
        CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Webhook triggered for path: {WebhookPath}", webhookPath);

        // Search for workflows with matching webhook path
        var workflows = await _workflowRepository.SearchAsync(webhookPath, 0, 10, cancellationToken);
        var workflow = workflows.FirstOrDefault(w =>
            w.IsActive &&
            HasWebhookTriggerWithPath(w, webhookPath));

        if (workflow is null)
        {
            return NotFound(new ApiError
            {
                Code = "WEBHOOK_NOT_FOUND",
                Message = $"No active workflow found with webhook path '{webhookPath}'"
            });
        }

        return await TriggerWebhookAsync(workflow.Id, cancellationToken);
    }


    private async Task<IActionResult> TriggerWebhookAsync(
        Guid workflowId,
        CancellationToken cancellationToken)
    {
        _logger.LogDebug("Webhook triggered for workflow {WorkflowId}", workflowId);

        // Get the workflow
        var workflow = await _workflowRepository.GetByIdAsync(workflowId, cancellationToken);
        if (workflow is null)
        {
            return NotFound(new ApiError
            {
                Code = "WORKFLOW_NOT_FOUND",
                Message = $"Workflow with ID '{workflowId}' was not found"
            });
        }

        if (!workflow.IsActive)
        {
            return BadRequest(new ApiError
            {
                Code = "WORKFLOW_INACTIVE",
                Message = "Cannot trigger an inactive workflow"
            });
        }

        // Verify the workflow has a webhook trigger configured
        var hasWebhookTrigger = workflow.Nodes.Any(n =>
            n.Type.Equals("webhook-trigger", StringComparison.OrdinalIgnoreCase));

        if (!hasWebhookTrigger)
        {
            return NotFound(new ApiError
            {
                Code = "WEBHOOK_NOT_CONFIGURED",
                Message = "This workflow does not have a webhook trigger configured"
            });
        }

        // Build webhook data from request
        var webhookData = await BuildWebhookDataAsync(cancellationToken);

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
            userId: null);

        // Add webhook data to context
        context.Variables["webhook"] = webhookData;
        context.Variables["input"] = webhookData;

        try
        {
            // Execute the workflow
            var result = await _workflowEngine.ExecuteAsync(workflow, context, cancellationToken);

            // Get the persisted execution record
            var execution = await _executionRepository.GetByIdAsync(executionId, cancellationToken);

            var response = new WebhookResponse
            {
                ExecutionId = executionId,
                WorkflowId = workflow.Id,
                Status = execution?.Status ?? (result.Success ? ExecutionStatus.Completed : ExecutionStatus.Failed),
                Message = result.Success
                    ? "Workflow executed successfully"
                    : result.ErrorMessage ?? "Workflow execution failed"
            };

            // If the workflow produced output, include it in the response
            if (execution?.OutputData.HasValue == true)
            {
                response = response with { OutputData = execution.OutputData };
            }

            _logger.LogInformation(
                "Webhook triggered workflow {WorkflowId}, execution {ExecutionId} completed with status {Status}",
                workflow.Id, executionId, response.Status);

            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error executing workflow {WorkflowId} via webhook", workflowId);
            return BadRequest(new ApiError
            {
                Code = "WEBHOOK_EXECUTION_FAILED",
                Message = $"Workflow execution failed: {ex.Message}"
            });
        }
    }

    private async Task<JsonElement> BuildWebhookDataAsync(CancellationToken cancellationToken)
    {
        var webhookData = new Dictionary<string, object?>
        {
            ["method"] = Request.Method,
            ["path"] = Request.Path.Value,
            ["queryString"] = Request.QueryString.Value,
            ["headers"] = GetHeadersDictionary(),
            ["query"] = GetQueryDictionary()
        };

        // Read body if present
        if (Request.ContentLength > 0 || Request.ContentType is not null)
        {
            webhookData["body"] = await ReadBodyAsync(cancellationToken);
        }

        return JsonSerializer.SerializeToElement(webhookData);
    }

    private Dictionary<string, string> GetHeadersDictionary()
    {
        var headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var header in Request.Headers)
        {
            // Skip sensitive headers
            if (header.Key.StartsWith("Authorization", StringComparison.OrdinalIgnoreCase) ||
                header.Key.StartsWith("Cookie", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            headers[header.Key] = header.Value.ToString();
        }

        return headers;
    }

    private Dictionary<string, string> GetQueryDictionary()
    {
        var query = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var param in Request.Query)
        {
            query[param.Key] = param.Value.ToString();
        }

        return query;
    }

    private async Task<object?> ReadBodyAsync(CancellationToken cancellationToken)
    {
        try
        {
            Request.EnableBuffering();
            using var reader = new StreamReader(Request.Body, leaveOpen: true);
            var bodyString = await reader.ReadToEndAsync(cancellationToken);
            Request.Body.Position = 0;

            if (string.IsNullOrWhiteSpace(bodyString))
            {
                return null;
            }

            // Try to parse as JSON
            if (Request.ContentType?.Contains("application/json", StringComparison.OrdinalIgnoreCase) == true)
            {
                try
                {
                    return JsonSerializer.Deserialize<JsonElement>(bodyString);
                }
                catch
                {
                    // If JSON parsing fails, return as string
                    return bodyString;
                }
            }

            return bodyString;
        }
        catch
        {
            return null;
        }
    }

    private static bool HasWebhookTriggerWithPath(Core.Models.Workflow workflow, string path)
    {
        // Check if any node is a webhook trigger with matching path
        return workflow.Nodes.Any(n =>
            n.Type.Equals("webhook-trigger", StringComparison.OrdinalIgnoreCase) &&
            n.Configuration.TryGetProperty("path", out var pathProp) &&
            pathProp.GetString()?.Equals(path, StringComparison.OrdinalIgnoreCase) == true);
    }
}

/// <summary>
/// Response from a webhook trigger.
/// </summary>
public record WebhookResponse
{
    /// <summary>ID of the triggered execution.</summary>
    public Guid ExecutionId { get; init; }

    /// <summary>ID of the workflow that was executed.</summary>
    public Guid WorkflowId { get; init; }

    /// <summary>Status of the execution.</summary>
    public ExecutionStatus Status { get; init; }

    /// <summary>Status message.</summary>
    public string Message { get; init; } = string.Empty;

    /// <summary>Output data from the workflow (if any).</summary>
    public JsonElement? OutputData { get; init; }
}
