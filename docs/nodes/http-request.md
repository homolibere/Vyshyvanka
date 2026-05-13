# HTTP Request

Make HTTP requests to external APIs and services.

- **Category:** Action
- **Type ID:** `http-request`
- **Icon:** `fa-solid fa-globe`
- **Base class:** `BaseActionNode`

## Ports

| Direction | Name | Display Name | Required |
|-----------|------|--------------|----------|
| Input | `input` | Input | No |
| Output | `output` | Response | — |

## Configuration

| Property | Type | Required | Description |
|----------|------|----------|-------------|
| `url` | string | Yes | The URL to send the request to |
| `method` | string | Yes | HTTP method. Options: `GET`, `POST`, `PUT`, `DELETE` |
| `headers` | object | No | HTTP headers as key-value pairs |
| `body` | object | No | Request body (for POST/PUT/PATCH) |
| `queryParameters` | object | No | Query string parameters as key-value pairs |
| `timeout` | number | No | Request timeout in seconds (default: 30) |
| `contentType` | string | No | Content-Type header value (default: `application/json`) |

## Credentials

When a credential is attached to this node, it is applied to the request automatically:

| Credential Type | Behavior |
|----------------|----------|
| API Key (`apiKey` field) | Added as a header. Uses `headerName` (default: `Authorization`) with `prefix` (default: `Bearer`) |
| Basic Auth (`username` + `password`) | Encoded as `Authorization: Basic <base64>` |
| Custom headers | All credential key-value pairs added as request headers |

## Output Data

| Field | Type | Description |
|-------|------|-------------|
| `statusCode` | number | HTTP status code (200, 404, 500, etc.) |
| `statusText` | string | HTTP reason phrase |
| `headers` | object | Response headers |
| `body` | object/string | Response body (parsed as JSON if possible, raw string otherwise) |
| `isSuccess` | boolean | `true` if status code is 2xx |

## Error Handling

The node catches and reports these error conditions:

| Error | Output |
|-------|--------|
| Network failure | `"HTTP request failed: <message>"` |
| Timeout | `"HTTP request timed out"` |
| Cancellation | `"HTTP request was cancelled"` |
| Other | `"HTTP request error: <message>"` |

## Usage

Use the HTTP Request node when you want to:

- Call REST APIs
- Fetch data from external services
- Send webhooks to other systems
- Upload data to cloud services

## Expression Examples

```
{{$node.HttpRequest.data.body}}              // Response body
{{$node.HttpRequest.data.statusCode}}        // Status code
{{$node.HttpRequest.data.headers.content-type}} // Response header
{{$node.HttpRequest.data.isSuccess}}         // Whether request succeeded
```

## Notes

- Query parameters are URL-encoded automatically.
- The request body is only sent for POST, PUT, and PATCH methods.
- If the response body is valid JSON, it is parsed into an object. Otherwise, it is returned as a raw string.
- The `timeout` configuration controls the HTTP client timeout, not the node execution timeout.
