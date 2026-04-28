# Node Development Guide

## Node categories

| Category  | Base class to use | When to use                                    |
|-----------|-------------------|------------------------------------------------|
| Trigger   | `BasePluginNode`  | Initiates a workflow (webhook, schedule, etc.)  |
| Action    | `BasePluginNode`  | Performs an operation (HTTP call, DB query)      |
| Logic     | `BasePluginNode`  | Controls flow (if/else, switch, loop)           |
| Transform | `BasePluginNode`  | Transforms data between nodes                   |

Set the category via the `Category` property on your node class.

## Creating a node

Every node needs:

1. A class that inherits `BasePluginNode` (or implements `INode` directly)
2. A `[NodeDefinition]` attribute with name, description, and icon
3. `[NodeInput]` / `[NodeOutput]` attributes defining ports
4. `[ConfigurationProperty]` attributes for user-configurable settings
5. An `ExecuteAsync` implementation

### Minimal example

```csharp
using Vyshyvanka.Core.Attributes;
using Vyshyvanka.Core.Enums;
using Vyshyvanka.Core.Interfaces;

[NodeDefinition(
    Name = "My Node",
    Description = "Does something",
    Icon = "fa-solid fa-gear")]
[NodeInput("input", Type = PortType.Object, IsRequired = true)]
[NodeOutput("output", Type = PortType.Object)]
[ConfigurationProperty("apiUrl", "string", Description = "API endpoint", IsRequired = true)]
public class MyNode : BasePluginNode
{
    public override string Type => "my-node";
    public override NodeCategory Category => NodeCategory.Action;

    public override async Task<NodeOutput> ExecuteAsync(
        NodeInput input, IExecutionContext context)
    {
        context.CancellationToken.ThrowIfCancellationRequested();

        var url = GetRequiredConfigValue<string>(input, "apiUrl");

        // Your logic here...

        return SuccessOutput(new { result = "done" });
    }
}
```

## Port types

| PortType  | Use for                        |
|-----------|--------------------------------|
| `Any`     | Accepts any data               |
| `Object`  | JSON objects                   |
| `Array`   | JSON arrays / collections      |
| `String`  | Text values                    |
| `Number`  | Numeric values                 |
| `Boolean` | True/false values              |

A connection is valid when source and target port types match, or either is `Any`.

## Configuration properties

Supported property types: `"string"`, `"number"`, `"boolean"`, `"object"`, `"array"`.

```csharp
[ConfigurationProperty("timeout", "number", Description = "Timeout in seconds")]
[ConfigurationProperty("headers", "object", Description = "Custom headers")]
[ConfigurationProperty("tags", "array", Description = "Tags to apply")]
[ConfigurationProperty("verbose", "boolean", Description = "Enable verbose logging")]
```

Read them in `ExecuteAsync`:

```csharp
var timeout = GetConfigValue<int?>(input, "timeout") ?? 30;
var headers = GetConfigValue<Dictionary<string, string>>(input, "headers");
var verbose = GetConfigValue<bool?>(input, "verbose") ?? false;
```

## Using credentials

If your node needs authentication, add the `[RequiresCredential]` attribute:

```csharp
[RequiresCredential(CredentialType.ApiKey)]
public class SecureNode : BasePluginNode
{
    public override async Task<NodeOutput> ExecuteAsync(
        NodeInput input, IExecutionContext context)
    {
        if (input.CredentialId.HasValue)
        {
            var creds = await context.Credentials.GetCredentialAsync(
                input.CredentialId.Value, context.CancellationToken);

            // creds is a Dictionary<string, string> with the decrypted values
        }

        // ...
    }
}
```

## Execution context

`IExecutionContext` gives you access to:

| Property            | Type                          | Description                          |
|---------------------|-------------------------------|--------------------------------------|
| `ExecutionId`       | `Guid`                        | Current execution ID                 |
| `WorkflowId`       | `Guid`                        | Current workflow ID                  |
| `Variables`         | `Dictionary<string, object>`  | Shared execution variables           |
| `NodeOutputs`       | `INodeOutputStore`            | Read outputs from upstream nodes     |
| `Credentials`       | `ICredentialProvider`          | Retrieve decrypted credentials       |
| `CancellationToken` | `CancellationToken`           | Always check this in long operations |

## Error handling

- Return `FailureOutput("message")` for expected errors
- Throw exceptions for unexpected errors — the host catches them
- Always check `context.CancellationToken` in async/long-running work

## Trigger nodes

Trigger nodes implement `ITriggerNode` in addition to `INode`:

```csharp
public class MyTrigger : BasePluginNode, ITriggerNode
{
    public override string Type => "my-trigger";
    public override NodeCategory Category => NodeCategory.Trigger;

    public Task<bool> ShouldTriggerAsync(TriggerContext context)
    {
        // Evaluate whether this trigger should fire
        return Task.FromResult(true);
    }

    public override Task<NodeOutput> ExecuteAsync(
        NodeInput input, IExecutionContext context)
    {
        // Produce the trigger's output data
        return Task.FromResult(SuccessOutput(new { triggered = true }));
    }
}
```
