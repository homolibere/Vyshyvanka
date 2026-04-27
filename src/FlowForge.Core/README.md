# FlowForge.Core

Core contracts, models, and interfaces for building **FlowForge** plugins.

## What's included

| Namespace | Contents |
|-----------|----------|
| `FlowForge.Core.Interfaces` | `INode`, `ITriggerNode`, `IExecutionContext`, `INodeOutputStore`, `ICredentialProvider` |
| `FlowForge.Core.Attributes` | `[NodeDefinition]`, `[NodeInput]`, `[NodeOutput]`, `[ConfigurationProperty]`, `[RequiresCredential]` |
| `FlowForge.Core.Enums` | `NodeCategory`, `PortType`, `ExecutionStatus`, `CredentialType` |
| `FlowForge.Core.Models` | `Workflow`, `WorkflowNode`, `Connection`, `Execution`, `Credential` |
| `FlowForge.Core.Exceptions` | `FlowForgeException` and domain-specific exception types |

## Quick start

```bash
dotnet new classlib -n FlowForge.Plugin.MyPlugin
cd FlowForge.Plugin.MyPlugin
dotnet add package FlowForge.Core
```

## Creating a node

```csharp
using FlowForge.Core.Attributes;
using FlowForge.Core.Enums;
using FlowForge.Core.Interfaces;

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
  <PackageReference Include="FlowForge.Core">
    <Private>false</Private>
    <ExcludeAssets>runtime</ExcludeAssets>
  </PackageReference>
</ItemGroup>
```

## License

MIT
