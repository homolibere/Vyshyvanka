# FlowForge.Plugin.GitLab

GitLab integration plugin for [FlowForge](https://github.com/flowforge/flowforge). Manage issues, repository files, releases, and repositories via the GitLab REST API v4.

## Nodes

| Node | Type | Operations |
|------|------|------------|
| **GitLab Webhook Trigger** | `gitlab-webhook-trigger` | Trigger node — fires on GitLab webhook events |
| **GitLab Issue** | `gitlab-issue` | create, get, edit, comment, lock |
| **GitLab Merge Request** | `gitlab-merge-request` | create, get, getAll, update, merge, approve, comment |
| **GitLab Pipeline** | `gitlab-pipeline` | get, getAll, create, retry, cancel, delete, getJobs |
| **GitLab File** | `gitlab-file` | create, get, list, edit, delete |
| **GitLab Tag** | `gitlab-tag` | create, get, getAll, delete |
| **GitLab Release** | `gitlab-release` | create, get, getAll, update, delete |
| **GitLab Repository** | `gitlab-repository` | get, getIssues, getUserRepos |

## Credentials

Attach an **ApiKey** credential with the following fields:

| Field | Description |
|-------|-------------|
| `accessToken` | GitLab personal access token ([create one here](https://gitlab.com/-/user_settings/personal_access_tokens)) |
| `baseUrl` | GitLab instance URL (default: `https://gitlab.com`). Set for self-hosted instances. |

## Quick Examples

### Create an Issue

```json
{
  "operation": "create",
  "projectId": "namespace/my-project",
  "title": "Fix CI pipeline",
  "description": "The deploy stage is failing on main.",
  "labels": "bug,ci"
}
```

### Get a Repository File

```json
{
  "operation": "get",
  "projectId": "namespace/my-project",
  "filePath": "src/main.ts",
  "branch": "develop"
}
```

### Create a Release

```json
{
  "operation": "create",
  "projectId": "12345",
  "tagName": "v1.2.0",
  "name": "Version 1.2.0",
  "description": "## What's new\n- Feature A\n- Bug fix B",
  "ref": "main"
}
```

### Create a Merge Request

```json
{
  "operation": "create",
  "projectId": "namespace/my-project",
  "title": "feat: add user notifications",
  "sourceBranch": "feature/notifications",
  "targetBranch": "main",
  "description": "Adds email notifications for new comments.",
  "reviewerIds": [42, 99],
  "removeSourceBranch": true
}
```

### Merge When Pipeline Succeeds

```json
{
  "operation": "merge",
  "projectId": "namespace/my-project",
  "mergeRequestIid": 15,
  "mergeWhenPipelineSucceeds": true,
  "squash": true
}
```

### Trigger a Pipeline

```json
{
  "operation": "create",
  "projectId": "namespace/my-project",
  "ref": "main",
  "variables": [
    { "key": "DEPLOY_ENV", "value": "staging" }
  ]
}
```

### List Failed Pipelines

```json
{
  "operation": "getAll",
  "projectId": "namespace/my-project",
  "status": "failed",
  "perPage": 10
}
```

### Get Pipeline Jobs

```json
{
  "operation": "getJobs",
  "projectId": "namespace/my-project",
  "pipelineId": 12345,
  "jobScope": "failed"
}
```

### List a User's Projects

```json
{
  "operation": "getUserRepos",
  "userId": "johndoe",
  "perPage": 50
}
```

## License

MIT
