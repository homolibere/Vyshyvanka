# Publishing Your Plugin

## Versioning

Package version is set in the csproj `<Version>` property. You can also override it at pack time:

```bash
dotnet pack src/Vyshyvanka.Plugin.YourName.csproj -c Release -p:PackageVersion=1.2.0
```

Use [semantic versioning](https://semver.org/): `MAJOR.MINOR.PATCH`.

## Publishing to NuGet.org

### One-time setup

1. Create an account at [nuget.org](https://www.nuget.org/)
2. Go to [API Keys](https://www.nuget.org/account/apikeys)
3. Create a key with glob pattern `Vyshyvanka.Plugin.YourName` and Push scope
4. Store the key as a GitHub repository secret named `NUGET_API_KEY`

### Manual publish

```bash
dotnet pack src/Vyshyvanka.Plugin.YourName.csproj -c Release

dotnet nuget push src/bin/Release/Vyshyvanka.Plugin.YourName.1.0.0.nupkg \
  --api-key YOUR_API_KEY \
  --source https://api.nuget.org/v3/index.json
```

### Automated publish via CI

The included GitHub Actions workflow (`.github/workflows/publish.yml`) publishes automatically when you push a version tag:

```bash
git tag v1.0.0
git push origin v1.0.0
```

The workflow:
1. Extracts the version from the tag (e.g., `v1.0.0` → `1.0.0`)
2. Builds and packs with that version
3. Pushes the `.nupkg` to NuGet.org using the `NUGET_API_KEY` secret

## Publishing to a private feed

Update the push command or CI workflow to target your private feed:

```bash
dotnet nuget push src/bin/Release/*.nupkg \
  --api-key YOUR_API_KEY \
  --source https://your-feed.example.com/v3/index.json
```

## Package contents

The NuGet package includes:
- The compiled plugin DLL and all dependencies (via `CopyLocalLockFileAssemblies`)
- XML documentation for IntelliSense
- The README.md (shown on the NuGet.org package page)

Vyshyvanka.Core is excluded from the package (`ExcludeAssets=runtime`) because the Vyshyvanka host provides it at runtime.
