# FlowForge.Plugin.AdvancedHttp

Example NuGet plugin for FlowForge providing advanced HTTP client nodes.

## Nodes Included

| Node Type | Description |
|-----------|-------------|
| `http-retry-request` | HTTP requests with automatic retry and exponential backoff |
| `http-polling` | Poll an endpoint until a condition is met |
| `http-batch` | Execute multiple HTTP requests in parallel or sequentially |
| `graphql-request` | Execute GraphQL queries and mutations |

## Installation

### From NuGet
```bash
dotnet add package FlowForge.Plugin.AdvancedHttp
```

### From Source
```bash
dotnet build -c Release
# Copy the DLL to your FlowForge plugins directory
cp bin/Release/net10.0/FlowForge.Plugin.AdvancedHttp.dll /path/to/flowforge/plugins/
```

## Node Configuration

### HTTP Retry Request (`http-retry-request`)

```json
{
  "url": "https://api.example.com/data",
  "method": "GET",
  "headers": { "Accept": "application/json" },
  "maxRetries": 3,
  "initialDelayMs": 1000,
  "maxDelayMs": 30000,
  "retryOnStatusCodes": [408, 429, 500, 502, 503, 504]
}
```

### HTTP Polling (`http-polling`)

```json
{
  "url": "https://api.example.com/job/123/status",
  "intervalMs": 5000,
  "maxAttempts": 60,
  "successJsonPath": "status",
  "successValue": "completed",
  "failureJsonPath": "status",
  "failureValue": "failed"
}
```

### HTTP Batch (`http-batch`)

```json
{
  "mode": "parallel",
  "maxConcurrency": 5,
  "stopOnError": false,
  "requests": [
    { "id": "user", "url": "https://api.example.com/user/1" },
    { "id": "orders", "url": "https://api.example.com/orders?userId=1" }
  ]
}
```

### GraphQL Request (`graphql-request`)

```json
{
  "endpoint": "https://api.example.com/graphql",
  "query": "query GetUser($id: ID!) { user(id: $id) { name email } }",
  "variables": { "id": "123" },
  "operationName": "GetUser"
}
```

## Creating Your Own Plugin

1. Create a new class library targeting `net10.0`
2. Reference `FlowForge.Core`
3. Add the `[assembly: Plugin(...)]` attribute
4. Implement `INode` for each node type
5. Build and deploy to the plugins directory

See the source code in this project for examples.
