using System.Text.Json;
using FlowForge.Core.Enums;
using FlowForge.Core.Interfaces;
using FlowForge.Core.Models;
using ExecutionModel = FlowForge.Core.Models.Execution;
using NodeExecutionModel = FlowForge.Core.Models.NodeExecution;

namespace FlowForge.Engine.Execution;

/// <summary>
/// Workflow engine decorator that adds execution persistence.
/// Persists execution start, node completions, and final results.
/// </summary>
public class PersistentWorkflowEngine : IWorkflowEngine
{
    private readonly IWorkflowEngine _innerEngine;
    private readonly IExecutionRepository _repository;

    public PersistentWorkflowEngine(IWorkflowEngine innerEngine, IExecutionRepository repository)
    {
        _innerEngine = innerEngine ?? throw new ArgumentNullException(nameof(innerEngine));
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
    }

    /// <inheritdoc />
    public async Task<ExecutionResult> ExecuteAsync(
        Workflow workflow,
        IExecutionContext context,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(workflow);
        ArgumentNullException.ThrowIfNull(context);

        // Create initial execution record
        var execution = new ExecutionModel
        {
            Id = context.ExecutionId,
            WorkflowId = workflow.Id,
            WorkflowVersion = workflow.Version,
            Status = ExecutionStatus.Running,
            Mode = ExecutionMode.Manual, // Default, can be overridden via context
            StartedAt = DateTime.UtcNow,
            NodeExecutions = []
        };

        await _repository.CreateAsync(execution, cancellationToken);

        try
        {
            // Execute the workflow
            var result = await _innerEngine.ExecuteAsync(workflow, context, cancellationToken);

            // Persist node execution results
            await PersistNodeExecutionsAsync(context.ExecutionId, result.NodeResults, cancellationToken);

            // Update execution with final status
            var completedExecution = execution with
            {
                Status = result.Success ? ExecutionStatus.Completed : ExecutionStatus.Failed,
                CompletedAt = DateTime.UtcNow,
                OutputData = result.OutputData is not null 
                    ? JsonSerializer.SerializeToElement(result.OutputData) 
                    : null,
                ErrorMessage = result.ErrorMessage
            };

            await _repository.UpdateAsync(completedExecution, cancellationToken);

            return result;
        }
        catch (OperationCanceledException)
        {
            // Update execution as cancelled
            var cancelledExecution = execution with
            {
                Status = ExecutionStatus.Cancelled,
                CompletedAt = DateTime.UtcNow,
                ErrorMessage = "Execution was cancelled"
            };

            await _repository.UpdateAsync(cancelledExecution, cancellationToken);
            throw;
        }
        catch (Exception ex)
        {
            // Update execution as failed with error details
            var failedExecution = execution with
            {
                Status = ExecutionStatus.Failed,
                CompletedAt = DateTime.UtcNow,
                ErrorMessage = ex.Message
            };

            await _repository.UpdateAsync(failedExecution, cancellationToken);
            throw;
        }
    }


    /// <inheritdoc />
    public async Task<ExecutionResult> ExecuteNodeAsync(
        WorkflowNode node,
        IExecutionContext context,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(node);
        ArgumentNullException.ThrowIfNull(context);

        var startTime = DateTime.UtcNow;

        // Create node execution record
        var nodeExecution = new NodeExecutionModel
        {
            NodeId = node.Id,
            Status = ExecutionStatus.Running,
            StartedAt = startTime
        };

        await _repository.AddNodeExecutionAsync(context.ExecutionId, nodeExecution, cancellationToken);

        try
        {
            var result = await _innerEngine.ExecuteNodeAsync(node, context, cancellationToken);

            // Update node execution with result
            var completedNodeExecution = nodeExecution with
            {
                Status = result.Success ? ExecutionStatus.Completed : ExecutionStatus.Failed,
                CompletedAt = DateTime.UtcNow,
                OutputData = result.OutputData is not null 
                    ? JsonSerializer.SerializeToElement(result.OutputData) 
                    : null,
                ErrorMessage = result.ErrorMessage
            };

            await _repository.UpdateNodeExecutionAsync(
                context.ExecutionId, 
                completedNodeExecution, 
                cancellationToken);

            return result;
        }
        catch (Exception ex)
        {
            // Update node execution as failed
            var failedNodeExecution = nodeExecution with
            {
                Status = ExecutionStatus.Failed,
                CompletedAt = DateTime.UtcNow,
                ErrorMessage = ex.Message
            };

            await _repository.UpdateNodeExecutionAsync(
                context.ExecutionId, 
                failedNodeExecution, 
                cancellationToken);

            throw;
        }
    }

    /// <inheritdoc />
    public Task CancelExecutionAsync(Guid executionId)
    {
        return _innerEngine.CancelExecutionAsync(executionId);
    }

    private async Task PersistNodeExecutionsAsync(
        Guid executionId,
        IEnumerable<NodeExecutionResult> nodeResults,
        CancellationToken cancellationToken)
    {
        foreach (var nodeResult in nodeResults)
        {
            var nodeExecution = new NodeExecutionModel
            {
                NodeId = nodeResult.NodeId,
                Status = nodeResult.Success ? ExecutionStatus.Completed : ExecutionStatus.Failed,
                StartedAt = DateTime.UtcNow - nodeResult.Duration,
                CompletedAt = DateTime.UtcNow,
                OutputData = SerializeOutputData(nodeResult.OutputData),
                ErrorMessage = nodeResult.ErrorMessage
            };

            await _repository.AddNodeExecutionAsync(executionId, nodeExecution, cancellationToken);
        }
    }

    /// <summary>
    /// Safely serializes output data, handling null and undefined JsonElement values.
    /// </summary>
    private static JsonElement? SerializeOutputData(object? outputData)
    {
        if (outputData is null)
        {
            return null;
        }

        // Handle JsonElement with undefined value kind (default value)
        if (outputData is JsonElement jsonElement)
        {
            if (jsonElement.ValueKind == JsonValueKind.Undefined)
            {
                return null;
            }
            return jsonElement;
        }

        // Serialize other object types
        return JsonSerializer.SerializeToElement(outputData);
    }
}
