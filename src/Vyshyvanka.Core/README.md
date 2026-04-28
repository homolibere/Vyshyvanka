# Vyshyvanka.Core

Core contracts, models, and interfaces for building **Vyshyvanka** plugins.

## What's included

| Namespace | Contents |
|-----------|----------|
| `Vyshyvanka.Core.Interfaces` | `INode`, `ITriggerNode`, `IExecutionContext`, `INodeOutputStore`, `ICredentialProvider` |
| `Vyshyvanka.Core.Attributes` | `[NodeDefinition]`, `[NodeInput]`, `[NodeOutput]`, `[ConfigurationProperty]`, `[RequiresCredential]` |
| `Vyshyvanka.Core.Enums` | `NodeCategory`, `PortType`, `ExecutionStatus`, `CredentialType` |
| `Vyshyvanka.Core.Models` | `Workflow`, `WorkflowNode`, `Connection`, `Execution`, `Credential` |
| `Vyshyvanka.Core.Exceptions` | `VyshyvankaException` and domain-specific exception types |

## Quick start

```bash
dotnet new classlib -n Vyshyvanka.Plugin.MyPlugin
cd Vyshyvanka.Plugin.MyPlugin
dotnet add package Vyshyvanka.Core
```

## Creating a node

```csharp
using Vyshyvanka.Core.Attributes;
using Vyshyvanka.Core.Enums;
using Vyshyvanka.Core.Interfaces;

[NodeDefinition(Name = "My Action", Description = "Does something useful", Icon = "fa-bolt")]
[NodeInput("input", Type = PortType.Object, IsRequired = true)]
[NodeOutput("output", Type = PortType.Object)]
[ConfigurationProperty("url", "string", Description = "Target URL", IsRequired = true)]
public class MyActionNode : INode
{
    public string Id { get; set; } = string.Empty;
    public string Type => "my-action";
    public NodeCategory Category => NodeCategory.Action;

    public async Task<NodeOutput> ExecuteAsync(NodeInput input, IExecutionContext context)
    {
        // Your logic here
        return new NodeOutput { Data = input.Data, Success = true };
    }
}
```

## Plugin project setup

Your `.csproj` should enable dynamic loading and exclude the Core runtime (the host provides it):

```xml
<PropertyGroup>
  <EnableDynamicLoading>true</EnableDynamicLoading>
  <CopyLocalLockFileAssemblies>true</CopyLocalLockFileAssemblies>
</PropertyGroup>

<ItemGroup>
  <PackageReference Include="Vyshyvanka.Core">
    <Private>false</Private>
    <ExcludeAssets>runtime</ExcludeAssets>
  </PackageReference>
</ItemGroup>
```

## License

MIT
