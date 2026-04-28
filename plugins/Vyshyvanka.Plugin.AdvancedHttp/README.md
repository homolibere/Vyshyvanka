# Vyshyvanka.Plugin.AdvancedHttp

Example NuGet plugin for Vyshyvanka providing advanced HTTP client nodes.

## Nodes Included

| Node Type | Description |
|-----------|-------------|
| `http-retry-request` | HTTP requests with automatic retry and exponential backoff |
| `http-polling` | Poll an endpoint until a condition is met |
| `http-batch` | Execute multiple HTTP requests in parallel or sequentially |
| `graphql-request` | Execute GraphQL queries and mutations |

## Installation

### Via Vyshyvanka Designer

Use the Plugin Manager in the Designer UI to search for and install `Vyshyvanka.Plugin.AdvancedHttp` from any configured NuGet source.

### Via NuGet CLI

```bash
dotnet add package Vyshyvanka.Plugin.AdvancedHttp
```

## Building Locally

### Build the plugin

```bash
dotnet build Vyshyvanka.Plugin.AdvancedHttp -c Release
```

Output goes to `bin/Release/net10.0/`. The `EnableDynamicLoading` and `CopyLocalLockFileAssemblies` settings in the csproj ensure all dependencies are copied alongside the plugin DLL so it can be loaded in isolation.

### Run the plugin locally

Copy the built output to your Vyshyvanka plugins directory:

```bash
cp -r Vyshyvanka.Plugin.AdvancedHttp/bin/Release/net10.0/ /path/to/vyshyvanka/plugins/Vyshyvanka.Plugin.AdvancedHttp/
```

On next startup (or via the Plugin Manager), Vyshyvanka will discover and load the plugin, registering its nodes in the Node Registry.

## Packaging as a NuGet Package

### Create the .nupkg

```bash
dotnet pack Vyshyvanka.Plugin.AdvancedHttp -c Release
```

This produces `Vyshyvanka.Plugin.AdvancedHttp.1.0.0.nupkg` in `bin/Release/`.

Package metadata (ID, version, authors, description, tags, license) is defined in the `<PropertyGroup>` of the csproj — no separate `.nuspec` file needed.

### Set the version

Update the `<Version>` property in the csproj before packing:

```xml
<Version>1.2.0</Version>
```

Or override at pack time:

```bash
dotnet pack Vyshyvanka.Plugin.AdvancedHttp -c Release -p:Version=1.2.0
```

### Publish to a NuGet feed

Push to nuget.org:

```bash
dotnet nuget push bin/Release/Vyshyvanka.Plugin.AdvancedHttp.1.0.0.nupkg \
  --api-key YOUR_API_KEY \
  --source https://api.nuget.org/v3/index.json
```

Push to a private feed:

```bash
dotnet nuget push bin/Release/Vyshyvanka.Plugin.AdvancedHttp.1.0.0.nupkg \
  --api-key YOUR_API_KEY \
  --source https://your-feed.example.com/v3/index.json
```

### Test the package locally

Host a local NuGet source directory and add it to Vyshyvanka's package sources:

```bash
# Create a local feed directory
mkdir -p ~/local-nuget-feed

# Copy the package into it
cp bin/Release/Vyshyvanka.Plugin.AdvancedHttp.1.0.0.nupkg ~/local-nuget-feed/

# Add the local source via the Vyshyvanka API or Designer Plugin Manager
# Source URL: ~/local-nuget-feed (absolute path)
```

Then install the package through the Designer's Plugin Manager or the packages API.

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
2. Reference `Vyshyvanka.Core` with `Private=false` and `ExcludeAssets=runtime`
3. Enable dynamic loading in the csproj:
   ```xml
   <EnableDynamicLoading>true</EnableDynamicLoading>
   <CopyLocalLockFileAssemblies>true</CopyLocalLockFileAssemblies>
   ```
4. Add the assembly-level `[Plugin]` attribute in a `PluginInfo.cs`:
   ```csharp
   using Vyshyvanka.Core.Interfaces;

   [assembly: Plugin(
       "your.plugin.id",
       Name = "Your Plugin",
       Version = "1.0.0",
       Description = "What your plugin does.",
       Author = "You")]
   ```
5. Implement node classes inheriting from `BaseTriggerNode`, `BaseActionNode`, or `BaseLogicNode`
6. Build, pack, and publish

See the source code in this project for a working reference.
