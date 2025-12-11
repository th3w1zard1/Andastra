# NuGet Package Distribution

Both `TSLPatcher.Core` and `HoloPatcher` are configured to be distributed as NuGet packages, allowing other C# projects to reference them directly instead of using the CLI.

## Building NuGet Packages

### Build Packages Locally

To build NuGet packages for both projects:

```bash
# Build packages in Release configuration
dotnet pack --configuration Release

# Packages will be created in:
# - src/TSLPatcher.Core/bin/Release/TSLPatcher.Core.2.0.0-alpha1.nupkg
# - src/HoloPatcher/bin/Release/HoloPatcher.2.0.0-alpha1.nupkg
```

### Build Individual Packages

```bash
# Build only TSLPatcher.Core
dotnet pack src/TSLPatcher.Core/TSLPatcher.Core.csproj --configuration Release

# Build only HoloPatcher
dotnet pack src/HoloPatcher/HoloPatcher.csproj --configuration Release
```

## Installing Packages

### From Local Package Source

1. Create a local NuGet feed directory:

```bash
mkdir nuget-packages
```

2. Copy the `.nupkg` files to this directory

3. Add the local feed to your project's `nuget.config` or use the `--source` parameter:

```bash
dotnet add package TSLPatcher.Core --source ./nuget-packages
dotnet add package HoloPatcher --source ./nuget-packages
```

### From NuGet.org (after publishing)

Once published to NuGet.org, install via:

```bash
dotnet add package TSLPatcher.Core
dotnet add package HoloPatcher
```

Or add to your `.csproj`:

```xml
<ItemGroup>
  <PackageReference Include="TSLPatcher.Core" Version="2.0.0-alpha1" />
  <PackageReference Include="HoloPatcher" Version="2.0.0-alpha1" />
</ItemGroup>
```

## Using the Packages

### Using TSLPatcher.Core

The core library provides the patching engine:

```csharp
using TSLPatcher.Core.Patcher;
using TSLPatcher.Core.Config;
using TSLPatcher.Core.Logger;

// Create a logger
var logger = new PatchLogger();

// Create installer
var installer = new ModInstaller(
    modPath: @"C:\Mods\MyMod",
    gamePath: @"C:\Games\KOTOR2",
    changesIniPath: @"C:\Mods\MyMod\changes.ini",
    logger: logger
);

// Install the mod
installer.Install();
```

### Using HoloPatcher

HoloPatcher can be used as a library for programmatic access:

```csharp
using HoloPatcher;

// Access core functionality through HoloPatcher classes
// (Implementation depends on what public APIs are exposed)
```

## Publishing to NuGet.org

1. **Get a NuGet API Key**:
   - Sign in to [nuget.org](https://www.nuget.org)
   - Go to Account Settings → API Keys
   - Create a new API key

2. **Publish packages**:

```bash
# Publish TSLPatcher.Core
dotnet nuget push src/TSLPatcher.Core/bin/Release/TSLPatcher.Core.*.nupkg --api-key YOUR_API_KEY --source https://api.nuget.org/v3/index.json

# Publish HoloPatcher
dotnet nuget push src/HoloPatcher/bin/Release/HoloPatcher.*.nupkg --api-key YOUR_API_KEY --source https://api.nuget.org/v3/index.json
```

3. **Publish symbol packages** (optional):

```bash
dotnet nuget push src/TSLPatcher.Core/bin/Release/TSLPatcher.Core.*.snupkg --api-key YOUR_API_KEY --source https://api.nuget.org/v3/index.json
```

## Package Dependencies

- **TSLPatcher.Core**: Standalone library with no dependencies on HoloPatcher
- **HoloPatcher**: Depends on TSLPatcher.Core (automatically included when installing HoloPatcher)

## Version Management

Update the version in the `.csproj` files:

```xml
<Version>2.0.0-alpha1</Version>
```

Follow [Semantic Versioning](https://semver.org/):

- **Major**: Breaking changes
- **Minor**: New features, backward compatible
- **Patch**: Bug fixes, backward compatible
- **Pre-release**: Use suffixes like `-alpha1`, `-beta1`, `-rc1`

## Notes

- Packages are automatically generated on Release builds when `GeneratePackageOnBuild` is `true`
- Symbol packages (`.snupkg`) are included for debugging support
- Both packages support multiple target frameworks (.NET 6.0, 7.0, 8.0, 9.0, 10.0, and .NET Framework 4.6.2, 4.8)
