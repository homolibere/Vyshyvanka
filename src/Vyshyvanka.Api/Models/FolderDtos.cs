using Vyshyvanka.Contracts.Folders;
using Vyshyvanka.Core.Models;

namespace Vyshyvanka.Api.Models;

public static class FolderMappings
{
    public static FolderResponse ToResponse(this Folder folder) => new()
    {
        Id = folder.Id,
        Name = folder.Name,
        Color = folder.Color,
        OwnerId = folder.OwnerId,
        WorkflowCount = folder.WorkflowCount,
        CreatedAt = folder.CreatedAt
    };
}
