using Vyshyvanka.Contracts.Executions;
using Vyshyvanka.Core.Models;

namespace Vyshyvanka.Api.Models;

public static class ExecutionMappings
{
    public static ExecutionResponse ToResponse(this Execution execution)
    {
        return new ExecutionResponse
        {
            Id = execution.Id,
            WorkflowId = execution.WorkflowId,
            WorkflowVersion = execution.WorkflowVersion,
            Status = execution.Status,
            Mode = execution.Mode,
            StartedAt = execution.StartedAt,
            CompletedAt = execution.CompletedAt,
            Duration = execution.CompletedAt.HasValue
                ? execution.CompletedAt.Value - execution.StartedAt
                : null,
            TriggerData = execution.TriggerData,
            OutputData = execution.OutputData,
            ErrorMessage = execution.ErrorMessage,
            NodeExecutions = execution.NodeExecutions
                .Select(ne => ne.ToResponse())
                .ToList()
        };
    }

    public static NodeExecutionResponse ToResponse(this NodeExecution nodeExecution)
    {
        return new NodeExecutionResponse
        {
            NodeId = nodeExecution.NodeId,
            Status = nodeExecution.Status,
            StartedAt = nodeExecution.StartedAt,
            CompletedAt = nodeExecution.CompletedAt,
            Duration = nodeExecution.CompletedAt.HasValue
                ? nodeExecution.CompletedAt.Value - nodeExecution.StartedAt
                : null,
            InputData = nodeExecution.InputData,
            OutputData = nodeExecution.OutputData,
            ErrorMessage = nodeExecution.ErrorMessage
        };
    }

    public static ExecutionSummaryResponse ToSummaryResponse(this Execution execution)
    {
        return new ExecutionSummaryResponse
        {
            Id = execution.Id,
            WorkflowId = execution.WorkflowId,
            WorkflowVersion = execution.WorkflowVersion,
            Status = execution.Status,
            Mode = execution.Mode,
            StartedAt = execution.StartedAt,
            CompletedAt = execution.CompletedAt,
            Duration = execution.CompletedAt.HasValue
                ? execution.CompletedAt.Value - execution.StartedAt
                : null,
            ErrorMessage = execution.ErrorMessage
        };
    }
}
