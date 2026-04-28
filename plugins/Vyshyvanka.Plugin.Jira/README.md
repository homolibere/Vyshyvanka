# Vyshyvanka.Plugin.Jira

Jira Software integration plugin for [Vyshyvanka](https://github.com/vyshyvanka/vyshyvanka). Manage issues, comments, and users via the Jira Cloud REST API v3.

## Nodes

| Node | Type | Operations |
|------|------|------------|
| **Jira Issue** | `jira-issue` | create, update, delete, get, getAll (JQL), transition |
| **Jira Issue Comment** | `jira-issue-comment` | add, get, getAll, update, remove |
| **Jira User** | `jira-user` | get, search |

## Credentials

Attach a **BasicAuth** credential with the following fields:

| Field | Description |
|-------|-------------|
| `email` | Jira account email address |
| `apiToken` | Jira API token ([generate one here](https://id.atlassian.com/manage-profile/security/api-tokens)) |
| `domain` | Atlassian instance host, e.g. `yourcompany.atlassian.net` |

## Quick Examples

### Create an Issue

```json
{
  "operation": "create",
  "projectKey": "PROJ",
  "summary": "Fix login bug",
  "issueType": "Bug",
  "priority": "High",
  "description": "Users cannot log in after password reset."
}
```

### Search Issues with JQL

```json
{
  "operation": "getAll",
  "jql": "project = PROJ AND status = 'In Progress'",
  "maxResults": 25
}
```

### Transition an Issue

```json
{
  "operation": "transition",
  "issueIdOrKey": "PROJ-123",
  "transitionId": "31"
}
```

Omit `transitionId` to list available transitions for the issue.

### Add a Comment

```json
{
  "operation": "add",
  "issueIdOrKey": "PROJ-123",
  "body": "Deployed to staging — ready for QA."
}
```

## License

MIT
