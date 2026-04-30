using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Vyshyvanka.Core.Enums;
using Vyshyvanka.Core.Interfaces;
using Vyshyvanka.Engine.Nodes.Base;
using Vyshyvanka.Core.Attributes;

namespace Vyshyvanka.Engine.Nodes.Actions;

/// <summary>
/// An action node that executes another workflow as a sub-workflow,
/// passing input data as trigger parameters and returning the child workflow's output.
/// </summary>
[NodeDefinition(
    Name = "Execute Workflow",
    Description = "Execute another workflow with parameters and return its output",
    Icon = "fa-solid fa-diagram-project")]
[NodeInput("input", DisplayName = "Parameters", Type = PortType.Object)]
[NodeOutput("output", DisplayName = "Result", Type = PortType.Object)]
[ConfigurationProperty("workflowId", "string", Description = "ID of the workflow to execute", IsRequired = true)]
[ConfigurationProperty("waitForCompletion", "boolean",
    Description = "Wait for the child workflow to complete before continuing")]
[ConfigurationProperty("timeout", "number", Description = "Maximum execution time in seconds (default: 300)")]
public class ExecuteWorkflowNode : BaseActionNode
{
    private readonly string _id = Guid.NewGuid().ToString();

    /// <inheritdoc />
    public override string Id => _id;

    /// <inheritdoc />
    public override string Type => "execute-workflow";

    /// <inheritdoc />
    public override async Task<NodeOutput> ExecuteAsync(NodeInput input, IExecutionContext context)
    {
        var workflowIdStr = GetRequiredConfigValue<string>(input, "workflowId");
        var waitForCompletion = GetConfigValue<bool?>(input, "waitForCompletion") ?? true;
        var timeoutSeconds = GetConfigValue<int?>(input, "timeout") ?? 300;

        if (!Guid.TryParse(workflowIdStr, out var workflowId))
            return FailureOutput($"Invalid workflow ID: '{workflowIdStr}'");

        // Prevent infinite recursion — a workflow cannot execute itself
        if (workflowId == context.WorkflowId)
            return FailureOutput("A workflow cannot execute itself. This would cause infinite recursion.");

        // Resolve services from the execution context
        if (context.Services is null)
            return FailureOutput("Service provider is not available in the execution context");

        var workflowRepository = context.Services.GetService<IWorkflowRepository>();
        var workflowEngine = context.Services.GetService<IWorkflowEngine>();

        if (workflowRepository is null || workflowEngine is null)
            return FailureOutput("Required services (IWorkflowRepository, IWorkflowEngine) are not registered");

        // Load the target workflow
        var workflow = await workflowRepository.GetByIdAsync(workflowId, context.CancellationToken);
        if (workflow is null)
            return FailureOutput($"Workflow '{workflowId}' not found");

        if (!workflow.IsActive)
            return FailureOutput($"Workflow '{workflow.Name}' is not active");

        // Verify the executing user owns the target workflow (or is running without user context, e.g., webhooks)
        if (context.UserId is not null && workflow.CreatedBy != context.UserId)
            return FailureOutput($"Access denied: you do not have permission to execute workflow '{workflowId}'");

        // Create a child execution context
        var childExecutionId = Guid.NewGuid();
        var childContext = new Execution.ExecutionContext(
            childExecutionId,
            workflowId,
            context.Credentials,
            context.CancellationToken,
            context.Services,
            context.UserId);

        // Pass input data as trigger parameters
        if (input.Data.ValueKind != JsonValueKind.Undefined &&
            input.Data.ValueKind != JsonValueKind.Null)
        {
            childContext.Variables["input"] = input.Data;
        }

        try
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(context.CancellationToken);
            cts.CancelAfter(TimeSpan.FromSeconds(timeoutSeconds));

            var result = await workflowEngine.ExecuteAsync(workflow, childContext, cts.Token);

            // Extract the workflow output — prefer the top-level OutputData (set by the engine),
            // fall back to the last successful node's output.
            var workflowOutput = result.OutputData
                                 ?? result.NodeResults.LastOrDefault(r => r.Success)?.OutputData;

            var outputData = new Dictionary<string, object?>
            {
                ["executionId"] = childExecutionId,
                ["workflowId"] = workflowId,
                ["workflowName"] = workflow.Name,
                ["success"] = result.Success,
                ["data"] = workflowOutput,
                ["error"] = result.ErrorMessage,
                ["duration"] = result.Duration.TotalMilliseconds,
                ["nodeCount"] = result.NodeResults.Count
            };

            return result.Success
                ? SuccessOutput(outputData)
                : FailureOutput(
                    $"Child workflow '{workflow.Name}' failed: {result.ErrorMessage}");
        }
        catch (OperationCanceledException) when (!context.CancellationToken.IsCancellationRequested)
        {
            return FailureOutput($"Child workflow '{workflow.Name}' timed out after {timeoutSeconds} seconds");
        }
    }
}
