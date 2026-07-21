using System.Text.Json;
using Vyshyvanka.Api.Extensions;
using Vyshyvanka.Api.Models;
using Vyshyvanka.Api.Services;
using Vyshyvanka.Core.Enums;
using Vyshyvanka.Core.Interfaces;
using Vyshyvanka.Core.Models;
using Vyshyvanka.Engine.Credentials;
using Vyshyvanka.Engine.Execution;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using ExecutionContext = Vyshyvanka.Engine.Execution.ExecutionContext;

namespace Vyshyvanka.Api.Controllers;

/// <summary>
/// API controller for webhook trigger endpoints.
/// Webhooks are anonymous to allow external systems to trigger workflows.
/// Security is enforced per-workflow via optional HMAC signature verification and IP allowlisting.
/// </summary>
[ApiController]
[Route("api/[controller]")]
[AllowAnonymous]
[EnableRateLimiting(RateLimitingExtensions.WebhookPolicy)]
[RequestSizeLimit(1_048_576)] // 1 MB max request body
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
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status403Forbidden)]
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
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> TriggerByPath(
        string webhookPath,
        CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Webhook triggered for path: {WebhookPath}", webhookPath);

        // Find an active workflow with a webhook trigger configured for this path
        var workflow = await _workflowRepository.GetByWebhookPathAsync(webhookPath, cancellationToken);

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

        // Find the webhook trigger node
        var webhookTrigger = workflow.Nodes.FirstOrDefault(n =>
            n.Type.Equals("webhook-trigger", StringComparison.OrdinalIgnoreCase));

        if (webhookTrigger is null)
        {
            return NotFound(new ApiError
            {
                Code = "WEBHOOK_NOT_CONFIGURED",
                Message = "This workflow does not have a webhook trigger configured"
            });
        }

        // Enable buffering so we can read the body multiple times (for signature + data)
        Request.EnableBuffering();

        // Security: Verify HMAC signature if secret is configured
        var secret = WebhookSecurityService.GetWebhookSecret(webhookTrigger);
        if (secret is not null)
        {
            var signatureValidation = await ValidateSignatureAsync(secret, cancellationToken);
            if (signatureValidation is not null)
            {
                return signatureValidation;
            }
        }

        // Security: Verify IP allowlist if configured
        var allowedIps = WebhookSecurityService.GetAllowedIps(webhookTrigger);
        if (allowedIps is not null)
        {
            var clientIp = HttpContext.Connection.RemoteIpAddress?.ToString();
            if (!WebhookSecurityService.IsIpAllowed(clientIp, allowedIps))
            {
                _logger.LogWarning(
                    "Webhook request from IP {ClientIp} rejected for workflow {WorkflowId} — not in allowlist",
                    clientIp, workflowId);

                return StatusCode(StatusCodes.Status403Forbidden, new ApiError
                {
                    Code = "WEBHOOK_IP_DENIED",
                    Message = "Request origin is not in the allowed IP list for this webhook"
                });
            }
        }

        // Determine response mode from trigger configuration
        var responseMode = GetTriggerConfigString(webhookTrigger, "responseMode") ?? "async";
        var responseTimeoutSeconds = GetTriggerConfigInt(webhookTrigger, "responseTimeout") ?? 30;
        var exposeAuthHeader = GetTriggerConfigBool(webhookTrigger, "exposeAuthorizationHeader");

        // Build webhook data from request (includes rawBody for sync mode)
        var webhookData = await BuildWebhookDataAsync(exposeAuthHeader, cancellationToken);

        // Create credential provider
        ICredentialProvider credentialProvider = _credentialService is not null
            ? new CredentialProvider(_credentialService)
            : NullCredentialProvider.Instance;

        // Create execution context
        var executionId = Guid.NewGuid();

        // Create webhook response writer for sync mode
        WebhookResponseWriter? responseWriter = responseMode.Equals("sync", StringComparison.OrdinalIgnoreCase)
            ? new WebhookResponseWriter()
            : null;

        var context = new ExecutionContext(
            executionId,
            workflow.Id,
            credentialProvider,
            cancellationToken,
            HttpContext.RequestServices,
            userId: null,
            _logger)
        {
            WebhookResponse = responseWriter
        };

        // Add webhook data to context
        context.Variables["webhook"] = webhookData;
        context.Variables["input"] = webhookData;

        // Sync mode: execute workflow and wait for HTTP Response node to write
        if (responseWriter is not null)
        {
            return await ExecuteSyncWebhookAsync(
                workflow, context, responseWriter, responseTimeoutSeconds, executionId, cancellationToken);
        }

        // Async mode (default): execute and return standard response
        return await ExecuteAsyncWebhookAsync(workflow, context, executionId, workflowId, cancellationToken);
    }

    private async Task<IActionResult> ExecuteSyncWebhookAsync(
        Workflow workflow,
        ExecutionContext context,
        WebhookResponseWriter responseWriter,
        int responseTimeoutSeconds,
        Guid executionId,
        CancellationToken cancellationToken)
    {
        // Start workflow execution as a task (but don't use Task.Run — stay on the request scope)
        var executionTask = _workflowEngine.ExecuteAsync(workflow, context, cancellationToken);

        // Race: wait for either the HTTP Response node to fire, or the workflow to complete, or timeout
        var timeout = TimeSpan.FromSeconds(responseTimeoutSeconds);
        var responseTask = responseWriter.WaitForResponseAsync(timeout, cancellationToken);

        // Wait for the response or for execution to finish (whichever comes first)
        await Task.WhenAny(responseTask, executionTask);

        var responseData = responseTask.IsCompletedSuccessfully ? await responseTask : null;

        if (responseData is null)
        {
            // No response from the node — either timeout or workflow finished without HTTP Response node
            if (executionTask.IsCompletedSuccessfully)
            {
                // Workflow completed without producing a sync response — return standard webhook response
                var result = await executionTask;
                var execution = await _executionRepository.GetByIdAsync(executionId, cancellationToken);

                return Ok(new WebhookResponse
                {
                    ExecutionId = executionId,
                    WorkflowId = workflow.Id,
                    Status = execution?.Status ?? (result.Success ? ExecutionStatus.Completed : ExecutionStatus.Failed),
                    Message = result.Success
                        ? "Workflow executed successfully"
                        : result.ErrorMessage ?? "Workflow execution failed",
                    OutputData = execution?.OutputData
                });
            }

            if (executionTask.IsFaulted)
            {
                var ex = executionTask.Exception?.InnerException ?? executionTask.Exception;
                _logger.LogError(ex, "Sync webhook workflow {WorkflowId} execution failed", workflow.Id);
                return BadRequest(new ApiError
                {
                    Code = "WEBHOOK_EXECUTION_FAILED",
                    Message = $"Workflow execution failed: {ex?.Message}"
                });
            }

            // Timeout
            _logger.LogWarning(
                "Sync webhook workflow {WorkflowId} execution {ExecutionId} did not produce an HTTP Response within {Timeout}s",
                workflow.Id, executionId, responseTimeoutSeconds);

            return StatusCode(StatusCodes.Status504GatewayTimeout, new ApiError
            {
                Code = "WEBHOOK_RESPONSE_TIMEOUT",
                Message = $"Workflow did not produce an HTTP Response within {responseTimeoutSeconds} seconds"
            });
        }

        // Ensure workflow execution completes (persist records) before we return
        try
        {
            await executionTask;
        }
        catch (Exception ex)
        {
            // Workflow failed after responding — log but don't change the response
            _logger.LogWarning(ex,
                "Sync webhook workflow {WorkflowId} execution failed after HTTP Response was sent",
                workflow.Id);
        }

        // Write the custom response from the HTTP Response node
        if (responseData.Headers is not null)
        {
            foreach (var (key, value) in responseData.Headers)
            {
                Response.Headers.Append(key, value);
            }
        }

        _logger.LogInformation(
            "Sync webhook workflow {WorkflowId}, execution {ExecutionId} responded with status {StatusCode}",
            workflow.Id, executionId, responseData.StatusCode);

        return new RawJsonResult(responseData.Body ?? string.Empty, responseData.StatusCode);
    }

    private async Task<IActionResult> ExecuteAsyncWebhookAsync(
        Workflow workflow,
        ExecutionContext context,
        Guid executionId,
        Guid workflowId,
        CancellationToken cancellationToken)
    {
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

    private static string? GetTriggerConfigString(WorkflowNode node, string key)
    {
        if (node.Configuration.ValueKind is JsonValueKind.Object &&
            node.Configuration.TryGetProperty(key, out var value) &&
            value.ValueKind == JsonValueKind.String)
        {
            return value.GetString();
        }

        return null;
    }

    private static int? GetTriggerConfigInt(WorkflowNode node, string key)
    {
        if (node.Configuration.ValueKind is JsonValueKind.Object &&
            node.Configuration.TryGetProperty(key, out var value) &&
            value.ValueKind == JsonValueKind.Number)
        {
            return value.GetInt32();
        }

        return null;
    }

    private static bool GetTriggerConfigBool(WorkflowNode node, string key)
    {
        if (node.Configuration.ValueKind is JsonValueKind.Object &&
            node.Configuration.TryGetProperty(key, out var value))
        {
            return value.ValueKind == JsonValueKind.True;
        }

        return false;
    }

    private async Task<IActionResult?> ValidateSignatureAsync(string secret, CancellationToken cancellationToken)
    {
        var signatureHeader = Request.Headers[WebhookSecurityService.SignatureHeader].FirstOrDefault();

        if (string.IsNullOrWhiteSpace(signatureHeader))
        {
            _logger.LogWarning("Webhook request missing {Header} header", WebhookSecurityService.SignatureHeader);
            return Unauthorized(new ApiError
            {
                Code = "WEBHOOK_SIGNATURE_MISSING",
                Message = $"The {WebhookSecurityService.SignatureHeader} header is required for this webhook"
            });
        }

        // Read the raw body for signature verification
        using var memoryStream = new MemoryStream();
        Request.Body.Position = 0;
        await Request.Body.CopyToAsync(memoryStream, cancellationToken);
        var bodyBytes = memoryStream.ToArray();
        Request.Body.Position = 0;

        if (!WebhookSecurityService.ValidateSignature(bodyBytes, signatureHeader, secret))
        {
            _logger.LogWarning("Webhook request has invalid HMAC signature");
            return Unauthorized(new ApiError
            {
                Code = "WEBHOOK_SIGNATURE_INVALID",
                Message = "The webhook signature is invalid"
            });
        }

        return null; // Signature is valid
    }

    private async Task<JsonElement> BuildWebhookDataAsync(bool exposeAuthorizationHeader, CancellationToken cancellationToken)
    {
        var webhookData = new Dictionary<string, object?>
        {
            ["method"] = Request.Method,
            ["path"] = Request.Path.Value,
            ["queryString"] = Request.QueryString.Value,
            ["headers"] = GetHeadersDictionary(exposeAuthorizationHeader),
            ["query"] = GetQueryDictionary(),
            ["remoteIp"] = HttpContext.Connection.RemoteIpAddress?.ToString()
        };

        // Read body if present
        if (Request.ContentLength > 0 || Request.ContentType is not null)
        {
            // Read raw body string first (needed for signature verification in workflows)
            Request.Body.Position = 0;
            using var reader = new StreamReader(Request.Body, leaveOpen: true);
            var rawBody = await reader.ReadToEndAsync(cancellationToken);
            Request.Body.Position = 0;

            webhookData["rawBody"] = rawBody;

            // Also provide parsed body for convenience
            if (!string.IsNullOrWhiteSpace(rawBody))
            {
                if (Request.ContentType?.Contains("application/json", StringComparison.OrdinalIgnoreCase) == true)
                {
                    try
                    {
                        webhookData["body"] = JsonSerializer.Deserialize<JsonElement>(rawBody);
                    }
                    catch
                    {
                        webhookData["body"] = rawBody;
                    }
                }
                else
                {
                    webhookData["body"] = rawBody;
                }
            }
        }

        return JsonSerializer.SerializeToElement(webhookData);
    }

    private Dictionary<string, string> GetHeadersDictionary(bool exposeAuthorizationHeader)
    {
        var headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var header in Request.Headers)
        {
            // Skip Cookie headers always
            if (header.Key.StartsWith("Cookie", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            // Skip Authorization unless explicitly exposed via trigger config
            if (!exposeAuthorizationHeader &&
                header.Key.StartsWith("Authorization", StringComparison.OrdinalIgnoreCase))
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

    private static bool HasWebhookTriggerWithPath(Workflow workflow, string path)
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

/// <summary>
/// Returns a pre-serialized JSON string directly to the response without double-encoding.
/// Used by the sync webhook mode to forward the HTTP Response node's body verbatim.
/// </summary>
internal sealed class RawJsonResult : IActionResult
{
    private readonly string _json;
    private readonly int _statusCode;

    public RawJsonResult(string json, int statusCode)
    {
        _json = json;
        _statusCode = statusCode;
    }

    public async Task ExecuteResultAsync(ActionContext context)
    {
        var response = context.HttpContext.Response;
        response.StatusCode = _statusCode;

        if (!response.Headers.ContainsKey("Content-Type"))
        {
            response.ContentType = "application/json; charset=utf-8";
        }

        await response.WriteAsync(_json);
    }
}
