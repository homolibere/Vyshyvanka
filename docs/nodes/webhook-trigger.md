# Webhook Trigger

Trigger a workflow when an HTTP request is received at a configured endpoint.

- **Category:** Trigger
- **Type ID:** `webhook-trigger`
- **Icon:** `fa-solid fa-tower-broadcast`
- **Base class:** `BaseTriggerNode`

## Ports

| Direction | Name | Display Name | Required |
|-----------|------|--------------|----------|
| Output | `output` | Request Data | — |

## Configuration

| Property | Type | Required | Description |
|----------|------|----------|-------------|
| `path` | string | Yes | Webhook URL path (e.g., `/my-webhook`) |
| `method` | string | No | HTTP method to accept. Options: `GET`, `POST`, `PUT`, `DELETE`, `ANY` |
| `responseMode` | string | No | When to respond to the caller. Options: `immediate` (respond right away), `lastNode` (respond after workflow completes) |

## Behavior

The Webhook Trigger activates when an incoming HTTP request matches the configured path and method. The node extracts the request data and passes it downstream.

The trigger validates that:
- The trigger context contains webhook data
- The `triggerType` field equals `"webhook"`

## Output Data

| Field | Type | Description |
|-------|------|-------------|
| `headers` | object | HTTP request headers |
| `body` | object/string | Request body (parsed as JSON if possible) |
| `query` | object | Query string parameters |
| `method` | string | HTTP method used (GET, POST, etc.) |
| `path` | string | Request path |
| `timestamp` | string | UTC timestamp when the webhook was received |

## Usage

Use the Webhook Trigger when you want to:

- Receive notifications from external services (GitHub, Stripe, etc.)
- Build API endpoints that trigger workflow logic
- Integrate with third-party systems via HTTP callbacks

## Expression Examples

```
{{$node.WebhookTrigger.data.body}}              // Full request body
{{$node.WebhookTrigger.data.headers.Authorization}} // Auth header
{{$node.WebhookTrigger.data.query.id}}          // Query parameter
{{$node.WebhookTrigger.data.method}}            // HTTP method
```

## Notes

- The webhook URL is automatically generated based on the workflow ID and the configured `path`.
- When `responseMode` is `lastNode`, the HTTP response is delayed until the workflow finishes, and the last node's output is returned as the response body.
- When `responseMode` is `immediate`, a `202 Accepted` response is returned immediately and the workflow runs asynchronously.
