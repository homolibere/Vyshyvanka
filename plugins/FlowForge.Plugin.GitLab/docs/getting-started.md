# Getting Started

## Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)
- A FlowForge instance to test against

## Setup

1. Copy this template folder to a new directory (or use it as a GitHub template repo)
2. Rename the project:
   - Folder: `FlowForge.Plugin.Template/` → `FlowForge.Plugin.YourName/`
   - Solution: `FlowForge.Plugin.Template.slnx` → `FlowForge.Plugin.YourName.slnx`
   - Project: `src/FlowForge.Plugin.Template.csproj` → `src/FlowForge.Plugin.YourName.csproj`
   - Update `RootNamespace` and `PackageId` in the csproj
   - Update the solution file to point to the renamed csproj
   - Update namespaces in all `.cs` files
3. Update `src/PluginInfo.cs` with your plugin's ID, name, description, and author
4. Update `Directory.Packages.props` if you need a different FlowForge.Core version

## Build

```bash
dotnet build
```

## Pack

```bash
dotnet pack src/FlowForge.Plugin.YourName.csproj -c Release
```

The `.nupkg` file will be in `src/bin/Release/`.

## Test locally

Copy the build output to your FlowForge plugins directory:

```bash
cp -r src/bin/Release/net10.0/ /path/to/flowforge/plugins/FlowForge.Plugin.YourName/
```

Or add a local NuGet source and install via the FlowForge Designer Plugin Manager:

```bash
mkdir -p ~/local-nuget-feed
cp src/bin/Release/*.nupkg ~/local-nuget-feed/
```

Then add `~/local-nuget-feed` as a package source in the Designer.

## Project structure

```
├── FlowForge.Plugin.Template.slnx   # Solution file
├── Directory.Packages.props          # Central package version management
├── docs/                             # Plugin documentation
├── .github/workflows/                # CI/CD pipelines
└── src/
    ├── FlowForge.Plugin.Template.csproj
    ├── PluginInfo.cs                 # Assembly-level [Plugin] attribute
    └── Nodes/
        ├── BasePluginNode.cs         # Shared base class with helpers
        └── SampleActionNode.cs       # Example node — replace with yours
```
