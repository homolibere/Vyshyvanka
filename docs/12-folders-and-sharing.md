# Workflow Folders & Sharing

This document describes the folder organization and workflow sharing features added in this iteration.

## Folders

Workflows can be organized into **flat folders** (one level, no nesting). Each user owns their own set of folders.

### Behavior

- Folders are per-user — each user manages their own folder structure.
- A workflow's `FolderId` is nullable. `null` means the workflow lives at the root (unfiled).
- Deleting a folder moves its workflows to root (`FolderId` set to `null` via `ON DELETE SET NULL`).
- Folder names are unique per owner (enforced by a unique index on `(OwnerId, Name)`).

### Data Model

| Field | Type | Description |
|-------|------|-------------|
| `Id` | Guid | Primary key |
| `Name` | string(100) | Display name |
| `Color` | string(7)? | Optional hex color for UI |
| `OwnerId` | Guid | FK to Users |
| `CreatedAt` | DateTime | Creation timestamp |

### API Endpoints

| Method | Route | Description |
|--------|-------|-------------|
| GET | `/api/folder` | List all folders for current user |
| GET | `/api/folder/{id}` | Get a folder by ID |
| POST | `/api/folder` | Create a folder |
| PUT | `/api/folder/{id}` | Update folder name/color |
| DELETE | `/api/folder/{id}` | Delete folder (workflows move to root) |
| PATCH | `/api/workflow/{id}/folder` | Move a workflow to a folder (or to root) |

---

## Sharing

Workflows can be shared with individual users or teams. The owner controls sharing and decides the credential policy for each share.

### Permission Levels (Hierarchical)

| Level | Implies | Capabilities |
|-------|---------|-------------|
| `View` | — | See workflow definition (read-only) |
| `Execute` | View | Trigger workflow execution |
| `Edit` | Execute, View | Modify the workflow definition |

When multiple grants apply (e.g., direct user grant + team grant), the highest level wins.

### Credential Sharing Policy

When sharing a workflow that uses credentials, the owner picks one of:

| Policy | Behavior |
|--------|----------|
| `UseOwnerCredentials` | Owner's credentials are used during execution. The owner explicitly consents to this when sharing. |
| `RequireOwnCredentials` | The executor must have their own matching credentials configured. Execution fails if they don't. |

This is set per-share (each permission grant carries its own policy).

### Permission Target Types

| Type | Description |
|------|-------------|
| `User` | Permission granted directly to a specific user |
| `Team` | Permission granted to all members of a team |

### Data Model — WorkflowPermission

| Field | Type | Description |
|-------|------|-------------|
| `Id` | Guid | Primary key |
| `WorkflowId` | Guid | FK to Workflows (CASCADE delete) |
| `TargetType` | string | "User" or "Team" |
| `TargetId` | Guid | User or Team ID |
| `PermissionLevel` | string | "View", "Execute", or "Edit" |
| `CredentialPolicy` | string | "UseOwnerCredentials" or "RequireOwnCredentials" |
| `GrantedBy` | Guid | User who granted this |
| `GrantedAt` | DateTime | When granted |

Unique constraint on `(WorkflowId, TargetType, TargetId)` prevents duplicate grants.

### API Endpoints

| Method | Route | Description |
|--------|-------|-------------|
| GET | `/api/workflow/{id}/sharing` | List permissions for a workflow (owner/admin only) |
| POST | `/api/workflow/{id}/sharing` | Share workflow with user/team |
| DELETE | `/api/workflow/{id}/sharing/{permissionId}` | Revoke a permission |

### Execution Flow for Shared Workflows

1. User triggers execution via `POST /api/execution`
2. `ExecutionController` checks `HasPermissionAsync` at `Execute` level
3. If user is not the owner, `ResolveCredentialProviderAsync` checks the `CredentialPolicy`:
   - `UseOwnerCredentials` → `OwnerCredentialProvider` (resolves credentials using the owner's stored credentials)
   - `RequireOwnCredentials` → standard `CredentialProvider` (uses executor's own credentials)
4. Execution proceeds normally through the engine

### Access Check Summary

| Operation | Required Level | Who Can Do It |
|-----------|---------------|---------------|
| View workflow | `View` | Owner, Admin, any shared user/team with View+ |
| Edit workflow | `Edit` | Owner, Admin, any shared user/team with Edit |
| Execute workflow | `Execute` | Owner, Admin, any shared user/team with Execute+ |
| Delete workflow | Owner only | Owner, Admin |
| Manage sharing | Owner only | Owner, Admin |

---

## Teams

Teams are named groups of users. Sharing a workflow with a team grants the permission to all current members.

### Behavior

- Any Editor or Admin can create teams.
- The team creator is automatically added as the first member with `Owner` role.
- Only the team owner can add/remove members and update team settings.
- Members can remove themselves (leave the team).
- The team owner cannot be removed — they must transfer ownership or delete the team.
- Team names are unique per owner.

### Data Model — Team

| Field | Type | Description |
|-------|------|-------------|
| `Id` | Guid | Primary key |
| `Name` | string(100) | Display name |
| `Description` | string(500)? | Optional description |
| `OwnerId` | Guid | FK to Users |
| `CreatedAt` | DateTime | Creation timestamp |

### Data Model — TeamMember (junction)

| Field | Type | Description |
|-------|------|-------------|
| `TeamId` | Guid | Composite PK, FK to Teams |
| `UserId` | Guid | Composite PK, FK to Users |
| `Role` | string | "Member" or "Owner" |
| `JoinedAt` | DateTime | When user joined |

### API Endpoints

| Method | Route | Description |
|--------|-------|-------------|
| GET | `/api/team` | List teams current user belongs to |
| GET | `/api/team/{id}` | Get team by ID (members only) |
| POST | `/api/team` | Create a team |
| PUT | `/api/team/{id}` | Update team (owner only) |
| DELETE | `/api/team/{id}` | Delete team (owner only) |
| POST | `/api/team/{id}/members` | Add a member (owner only) |
| DELETE | `/api/team/{id}/members/{userId}` | Remove a member |

---

## File Placement

| What | Where |
|------|-------|
| Enums | `Core/Enums/WorkflowPermissionLevel.cs`, `CredentialSharingPolicy.cs`, `PermissionTargetType.cs`, `TeamRole.cs` |
| Domain models | `Core/Models/Folder.cs`, `Team.cs`, `TeamMember.cs`, `WorkflowPermission.cs` |
| Service interfaces | `Core/Interfaces/IFolderRepository.cs`, `ITeamRepository.cs`, `IWorkflowPermissionRepository.cs`, `ITeamService.cs`, `IWorkflowPermissionService.cs` |
| EF entities | `Engine/Persistence/Entities/FolderEntity.cs`, `TeamEntity.cs`, `TeamMemberEntity.cs`, `WorkflowPermissionEntity.cs` |
| Repositories | `Engine/Persistence/FolderRepository.cs`, `TeamRepository.cs`, `WorkflowPermissionRepository.cs` |
| Services | `Engine/Sharing/WorkflowPermissionService.cs`, `TeamService.cs` |
| Credential provider | `Engine/Credentials/OwnerCredentialProvider.cs` |
| API controllers | `Api/Controllers/FolderController.cs`, `TeamController.cs`, `SharingController.cs` |
| API DTOs | `Api/Models/FolderDtos.cs`, `TeamDtos.cs`, `SharingDtos.cs` |
| Designer clients | `Designer/Services/FolderApiClient.cs`, `TeamApiClient.cs`, `SharingApiClient.cs` |
| Designer models | `Designer/Models/FolderModels.cs`, `TeamModels.cs`, `SharingModels.cs` |
| Migration | `Engine/Persistence/Migrations/20260716100636_AddFoldersTeamsAndPermissions.cs` |

---

## Future Work

- **Designer UI**: Folder sidebar in WorkflowBrowser, "Shared with me" virtual section, share dialog with user/team picker and credential policy selector, read-only indicator for view-only workflows.
- **Nested folders**: Current architecture uses flat folders. Adding `ParentId` to `Folder` would enable nesting without breaking existing data.
- **Team ownership transfer**: Allow transferring team ownership to another member.
- **Shared folder visibility**: Allow shared workflows to appear in the recipient's folder view (virtual placement).
- **Audit trail**: Log sharing events via the existing `IAuditLogService`.
