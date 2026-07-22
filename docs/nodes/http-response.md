# HTTP Response

**Type identifier:** `http-response`
**Category:** Action
**Credential:** None

## Description

Returns a synchronous HTTP response to the caller that triggered the workflow via a webhook. This node can only be used in workflows where the Webhook Trigger has `responseMode` set to `"lastNode"`.

Only one HTTP Response node may fire per workflow execution. If multiple HTTP Response nodes exist in parallel branches, the first one to execute wins.

## Configuration

| Property | Type | Required | Default | Description |
|----------|------|:--------:|---------|-------------|
| `statusCode` | number | No | `200` | HTTP status code to return |
| `contentType` | string | No | `application/json` | Content-Type header value |
| `headers` | object | No | — | Additional response headers as key-value pairs |
| `body` | string | No | — | Response body. If omitted, the input data is serialized as JSON. Supports expressions. |

## Ports

**Input:**
| Port | Type | Description |
|------|------|-------------|
| `input` | Any | Data to use as response body (if `body` config is not set) |

**Output:**
| Port | Type | Description |
|------|------|-------------|
| `output` | Object | Confirmation: `{ responseSent, statusCode, bodyLength }` |

## Behavior

1. Checks that the workflow was triggered by a Webhook Trigger in sync mode
2. Checks that no response has already been sent in this execution
3. Writes the configured status code, headers, and body to the held HTTP connection
4. The WebhookController immediately returns the response to the external caller
5. The workflow continues executing any remaining nodes after the HTTP Response

## Prerequisites

The Webhook Trigger node in the same workflow must have:
```json
{
  "responseMode": "lastNode",
  "responseTimeout": 30
}
```

If `responseMode` is not `"lastNode"` (or is omitted), the HTTP Response node will produce a failure output.

## Error Conditions

| Condition | Behavior |
|-----------|----------|
| No webhook context (async mode or non-webhook trigger) | Returns failure output with descriptive error |
| Response already sent by another HTTP Response node | Returns failure output |
| Cancellation requested | Returns failure output |

## Examples

### Return a JSON response

```json
{
  "statusCode": 200,
  "contentType": "application/json",
  "body": "{{ nodes.<previous-node>.data }}"
}
```

### Return a redirect

```json
{
  "statusCode": 302,
  "headers": {
    "Location": "{{ nodes.<gateway-call>.data.body.redirectUrl }}"
  },
  "body": ""
}
```

### Return an error

```json
{
  "statusCode": 400,
  "body": "{\"code\": \"INVALID_REQUEST\", \"message\": \"Missing required field\"}"
}
```

### Echo input data (no body configured)

When `body` is omitted, the input data from the upstream node is serialized as JSON and returned:

```json
{
  "statusCode": 200,
  "contentType": "application/json"
}
```
