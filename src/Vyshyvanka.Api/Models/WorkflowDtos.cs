using Vyshyvanka.Contracts.Workflows;
using Vyshyvanka.Core.Models;

namespace Vyshyvanka.Api.Models;

public static class WorkflowMappings
{
    public static WorkflowResponse ToResponse(this Workflow workflow)
    {
        return new WorkflowResponse
        {
            Id = workflow.Id,
            Name = workflow.Name,
            Description = workflow.Description,
            Version = workflow.Version,
            IsActive = workflow.IsActive,
            Nodes = workflow.Nodes.Select(n => new WorkflowNodeDto
            {
                Id = n.Id,
                Type = n.Type,
                Name = n.Name,
                Configuration = n.Configuration,
                Position = new PositionDto(n.Position.X, n.Position.Y),
                CredentialId = n.CredentialId
            }).ToList(),
            Connections = workflow.Connections.Select(c => new ConnectionDto
            {
                SourceNodeId = c.SourceNodeId,
                SourcePort = c.SourcePort,
                TargetNodeId = c.TargetNodeId,
                TargetPort = c.TargetPort
            }).ToList(),
            Settings = workflow.Settings is not null ? new WorkflowSettingsDto
            {
                TimeoutSeconds = workflow.Settings.Timeout.HasValue
                    ? (int)workflow.Settings.Timeout.Value.TotalSeconds
                    : null,
                MaxRetries = workflow.Settings.MaxRetries,
                ErrorHandling = workflow.Settings.ErrorHandling,
                MaxDegreeOfParallelism = workflow.Settings.MaxDegreeOfParallelism
            } : null,
            Tags = workflow.Tags,
            FolderId = workflow.FolderId,
            CreatedAt = workflow.CreatedAt,
            UpdatedAt = workflow.UpdatedAt,
            CreatedBy = workflow.CreatedBy
        };
    }
}
