# Stride Game Packaging Guide

This document describes industry-standard practices for packaging and distributing Stride game engine projects, and how to use the provided packaging scripts.

## Industry Standards & Best Practices

### Build Configuration

1. **Release Mode Only**: Always build in Release configuration for distribution
   - Optimizes code and removes debug overhead
   - Reduces file size and improves performance

2. **Self-Contained Deployment**: Preferred for game distribution
   - Bundles .NET runtime with the application
   - Users don't need to install .NET separately
   - Larger file size but better user experience

3. **Platform-Specific Builds**: Build separately for each target platform
   - Windows: `win-x64`, `win-x86`, `win-arm64`
   - Linux: `linux-x64`, `linux-x86`, `linux-arm64`
   - macOS: `osx-x64`, `osx-arm64`

4. **ReadyToRun (R2R)**: Enable for faster startup times
   - Pre-compiles code to native format
   - Reduces JIT compilation overhead

### File Cleanup

According to Stride documentation, the following files should be removed from release builds:

1. **Debug Files**:
   - `*.pdb` - Debug symbol files
   - `*.xml` - XML documentation files
   - `*vshost*` - Visual Studio host process files

2. **Unnecessary Folders**:
   - Keep only: `x64`, `x86`, `Data` folders (and `arm64` for macOS)
   - Remove other intermediate/output folders

3. **Custom Configuration Files**:
   - Remove development-only configuration files not created by Stride

### Folder Structure

Industry-standard Stride game distribution structure:

```
GameName-v1.0.0-Windows-x64/
├── x64/                          # 64-bit executable and dependencies
│   ├── GameName.exe
│   ├── *.dll
│   └── ...
├── x86/                          # 32-bit executable (optional)
│   ├── GameName.exe
│   └── ...
├── Data/                         # Game assets and resources
│   ├── Assets/
│   └── Resources/
├── Documentation/                # User documentation
│   ├── README.md
│   ├── CHANGELOG.md
│   └── ...
├── LICENSE                       # License file
└── README.txt                    # Quick start guide
```

### Documentation Requirements

Include the following in every distribution:

1. **README.txt**: Quick start guide with:
   - Game name and version
   - System requirements
   - Installation instructions
   - Running instructions
   - License reference

2. **Documentation Folder**: User-facing documentation
   - User guides
   - Technical documentation
   - API references (if applicable)

3. **License File**: Open source license or EULA
   - `LICENSE`, `LICENSE.txt`, or `LICENSE.md`
   - Or `COPYING` / `COPYING.txt`

### System Requirements

Document minimum system requirements:

- **.NET Runtime**: Specify if framework-dependent
- **Graphics API**: DirectX 11, OpenGL 4.2, or Vulkan 1.0
- **Operating System**: Minimum version (Windows 7+, Linux distro, macOS version)
- **RAM**: Minimum and recommended amounts
- **Disk Space**: Installation size requirements

### Archive Formats

Standard distribution formats:

- **Windows**: ZIP (universal support)
- **Linux**: TAR.GZ (standard) or ZIP
- **macOS**: ZIP (universal) or TAR.GZ
- **Universal**: ZIP (works everywhere)

### Versioning

Follow semantic versioning: `MAJOR.MINOR.PATCH`

- **MAJOR**: Incompatible API changes
- **MINOR**: Backward-compatible functionality additions
- **PATCH**: Backward-compatible bug fixes

Include version in:
- Archive filename: `GameName-v1.0.0-Windows-x64.zip`
- README.txt
- Documentation

## Scripts Overview

### Core Scripts

The packaging system consists of several scripts that work together:

1. **Build-StrideGame.ps1** - Builds the game for target platforms
2. **Package-StrideGame.ps1** - Packages builds into distribution archives
3. **Distribute-StrideGame.ps1** - Orchestrates complete build + package pipeline
4. **StrideBuildHelpers.ps1** - Stride-specific validation helpers (module)
5. **New-StrideVersionFile.ps1** - Creates version information files
6. **Invoke-StrideCICD.ps1** - CI/CD wrapper for automated pipelines
7. **Add-StrideGameDocumentation.ps1** - Adds documentation to packages

### Build-StrideGame.ps1

Builds the game project for specified platforms and architectures with comprehensive validation.

**Usage:**
```powershell
.\Build-StrideGame.ps1 [-ProjectPath <path>] [-Configuration Release] `
    [-Platforms Windows,Linux,macOS] [-Architectures x64,x86,arm64] `
    [-SelfContained] [-SingleFile] [-Trimmed] [-ReadyToRun] `
    [-OutputPath dist] [-Clean] [-Restore] [-Verbose] `
    [-RetryCount <n>] [-RetryDelaySeconds <n>] [-ClearNuGetCache]
```

**Key Features:**
- Asset compilation verification
- Build output validation
- Cross-platform support (Windows/Linux/macOS)
- ReadyToRun compilation for faster startup
- Optional code trimming for smaller deployments
- Retry logic for transient build failures
- NuGet cache clearing option
- Comprehensive error reporting with context
- Robust input validation

**Examples:**
```powershell
# Build for all platforms (x64 only)
.\Build-StrideGame.ps1

# Build for Windows only (x64 and x86) with trimming
.\Build-StrideGame.ps1 -Platforms Windows -Architectures x64,x86 -Trimmed

# Build self-contained single-file executables
.\Build-StrideGame.ps1 -SingleFile -SelfContained

# Clean build with verbose output
.\Build-StrideGame.ps1 -Clean -Verbose

# Framework-dependent build (requires .NET on target machine)
.\Build-StrideGame.ps1 -SelfContained $false

# Build with retry logic for transient failures
.\Build-StrideGame.ps1 -RetryCount 3 -RetryDelaySeconds 10

# Build with NuGet cache clearing (helps with corrupted packages)
.\Build-StrideGame.ps1 -ClearNuGetCache
```

### Package-StrideGame.ps1

Cleans up build artifacts and creates distribution packages with comprehensive validation.

**Usage:**
```powershell
.\Package-StrideGame.ps1 [-BuildPath dist] [-GameName Odyssey] `
    [-Version 1.0.0] [-CreateArchive] [-ArchiveFormat zip|tar|tar.gz] `
    [-ArchiveCompression Optimal|Fastest|NoCompression] `
    [-IncludeDocumentation] [-IncludeLicenses] [-CreateChecksums] `
    [-ValidateStructure] [-KeepDebugSymbols] [-Verbose]
```

**Key Features:**
- Automatic debug file cleanup (.pdb, .xml, vshost files)
- Folder structure validation (Stride best practices)
- Cross-platform archive creation (ZIP/TAR/TAR.GZ)
- Checksum generation (SHA256) for integrity verification
- Size optimization reporting
- Comprehensive documentation inclusion

**Examples:**
```powershell
# Package with default settings
.\Package-StrideGame.ps1 -GameName "Odyssey" -Version "1.0.0"

# Package with checksums and verbose output
.\Package-StrideGame.ps1 -CreateChecksums -Verbose

# Package with TAR.GZ archives (Linux/macOS standard)
.\Package-StrideGame.ps1 -ArchiveFormat tar.gz

# Package without validation (faster, less safe)
.\Package-StrideGame.ps1 -ValidateStructure $false

# Package with fastest compression
.\Package-StrideGame.ps1 -ArchiveCompression Fastest
```

### Distribute-StrideGame.ps1

Complete distribution pipeline: builds and packages in one command with all options.

**Usage:**
```powershell
.\Distribute-StrideGame.ps1 [-ProjectPath <path>] [-GameName Odyssey] `
    [-Version 1.0.0] [-Platforms All] [-Architectures x64] `
    [-SelfContained] [-SingleFile] [-Trimmed] [-ReadyToRun] `
    [-ArchiveFormat zip] [-CreateChecksums] [-Clean] [-Verbose] `
    [-SkipBuild] [-SkipPackage]
```

**Key Features:**
- Orchestrates complete build + package pipeline
- Passes through all build and packaging options
- Comprehensive error handling and reporting
- Progress tracking and summaries

**Examples:**
```powershell
# Complete distribution for all platforms
.\Distribute-StrideGame.ps1 -GameName "Odyssey" -Version "1.0.0"

# Build and package with trimming and checksums
.\Distribute-StrideGame.ps1 -Trimmed -CreateChecksums -Verbose

# Build and package for specific platforms only
.\Distribute-StrideGame.ps1 -Platforms Windows,Linux -Architectures x64

# Skip build step (use existing artifacts)
.\Distribute-StrideGame.ps1 -SkipBuild

# Skip packaging step (build only)
.\Distribute-StrideGame.ps1 -SkipPackage

# Framework-dependent with single-file deployment
.\Distribute-StrideGame.ps1 -SelfContained $false -SingleFile
```

### Add-StrideGameDocumentation.ps1

Adds documentation to existing distribution packages.

**Usage:**
```powershell
.\Add-StrideGameDocumentation.ps1 [-BuildPath dist] `
    [-DocumentationPath docs] [-IncludeReadme] [-IncludeChangelog] `
    [-CreateIndex]
```

**Examples:**
```powershell
# Add documentation with default settings
.\Add-StrideGameDocumentation.ps1

# Add documentation and create HTML index
.\Add-StrideGameDocumentation.ps1 -CreateIndex $true

# Add documentation without README
.\Add-StrideGameDocumentation.ps1 -IncludeReadme $false
```

### New-StrideVersionFile.ps1

Creates version information files for distribution tracking.

**Usage:**
```powershell
.\New-StrideVersionFile.ps1 -OutputPath "dist" -GameName "Odyssey" `
    -Version "1.0.0" -BuildNumber "123" -CommitHash "abc123"
```

**Creates:**
- `version.txt` - Human-readable version information
- `VERSION` - Key=value format for scripting
- `version.json` - Structured JSON format

**Examples:**
```powershell
# Basic version file
.\New-StrideVersionFile.ps1 -OutputPath "dist" -GameName "Odyssey" -Version "1.0.0"

# With CI/CD build information
.\New-StrideVersionFile.ps1 -OutputPath "dist" -GameName "Odyssey" `
    -Version "1.0.0" -BuildNumber $env:BUILD_NUMBER -CommitHash $env:GITHUB_SHA
```

### Invoke-StrideCICD.ps1

CI/CD wrapper script that automatically detects CI/CD environments and configures the build pipeline.

**Usage:**
```powershell
.\Invoke-StrideCICD.ps1 -GameName "Odyssey" -Version "1.0.0"
```

**Features:**
- Auto-detects CI/CD environment (GitHub Actions, Azure DevOps, Jenkins)
- Extracts version from git tags or environment variables
- Sets appropriate artifact paths
- Publishes artifacts for CI/CD systems
- Provides verbose logging in CI environments

**Examples:**
```powershell
# In GitHub Actions workflow
- name: Build and Package
  run: .\scripts\Invoke-StrideCICD.ps1 -GameName "Odyssey"

# In Azure DevOps pipeline
- task: PowerShell@2
  inputs:
    targetType: 'filePath'
    filePath: 'scripts/Invoke-StrideCICD.ps1'
    arguments: '-GameName "Odyssey" -Version "$(Build.BuildNumber)"'
```

## CI/CD Integration

### GitHub Actions Example

```yaml
name: Build and Release

on:
  push:
    tags:
      - 'v*'
  workflow_dispatch:

jobs:
  build:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v3
      
      - name: Setup .NET
        uses: actions/setup-dotnet@v3
        with:
          dotnet-version: '9.0.x'
      
      - name: Build and Package
        run: .\scripts\Invoke-StrideCICD.ps1 -GameName "Odyssey"
        shell: pwsh
      
      - name: Upload Artifacts
        uses: actions/upload-artifact@v3
        with:
          name: game-distributions
          path: dist/*.zip
```

### Azure DevOps Example

```yaml
trigger:
  tags:
    include:
      - v*

pool:
  vmImage: 'windows-latest'

steps:
  - task: UseDotNet@2
    inputs:
      packageType: 'sdk'
      version: '9.0.x'
  
  - task: PowerShell@2
    inputs:
      targetType: 'filePath'
      filePath: 'scripts/Invoke-StrideCICD.ps1'
      arguments: '-GameName "Odyssey" -Version "$(Build.BuildNumber)"'
  
  - task: PublishBuildArtifacts@1
    inputs:
      pathToPublish: 'dist'
      artifactName: 'game-distributions'
```

## Complete Workflow Example

### Step 1: Build for All Platforms

```powershell
.\Build-StrideGame.ps1 -Platforms All -Architectures x64 -SelfContained
```

This creates builds in `dist/Windows-x64/`, `dist/Linux-x64/`, `dist/macOS-x64/`

### Step 2: Package Distribution

```powershell
.\Package-StrideGame.ps1 -GameName "Odyssey" -Version "1.0.0" -BuildPath "dist"
```

This:
- Cleans up debug files
- Adds documentation
- Adds license files
- Creates README.txt
- Creates ZIP archives

### Step 3: (Optional) Add Additional Documentation

```powershell
.\Add-StrideGameDocumentation.ps1 -BuildPath "dist" -CreateIndex $true
```

### Or Use the All-in-One Command

```powershell
.\Distribute-StrideGame.ps1 -GameName "Odyssey" -Version "1.0.0" `
    -Platforms All -Architectures x64 -Clean
```

## Cross-Platform Compatibility

All scripts are designed to work on Windows, Linux, and macOS:

- **Windows**: PowerShell 5.1+ or PowerShell Core 6+
- **Linux**: PowerShell Core 6+ (install via package manager)
- **macOS**: PowerShell Core 6+ (install via Homebrew: `brew install powershell`)

The scripts automatically detect the platform and use appropriate commands:
- Path separators (`\` vs `/`)
- Archive creation tools (built-in .NET vs system commands)
- File permissions handling

## Advanced Options

### Code Trimming

The `-Trimmed` option removes unused code to reduce deployment size:
```powershell
.\Build-StrideGame.ps1 -Trimmed
```

**Warning:** Trimming can break code that uses reflection. Test thoroughly before shipping trimmed builds.

### ReadyToRun Compilation

Enabled by default, ReadyToRun pre-compiles code for faster startup:
```powershell
.\Build-StrideGame.ps1 -ReadyToRun $true  # Default
```

Disable only if you need smaller builds or encounter compatibility issues.

### Single-File Deployment

Single-file creates a self-extracting archive:
```powershell
.\Build-StrideGame.ps1 -SingleFile
```

**Note:** Single-file apps may have slower startup and require temp space extraction.

### Checksums

Generate SHA256 checksums for archive integrity:
```powershell
.\Package-StrideGame.ps1 -CreateChecksums
```

Creates `.sha256` files alongside archives for verification.

## Distribution Checklist

Before distributing your game, verify:

- [ ] All builds are in Release configuration
- [ ] Debug files (.pdb, .xml) are removed
- [ ] vshost files are removed
- [ ] Unnecessary folders are cleaned up
- [ ] Stride assets are properly compiled (Data folder present)
- [ ] Documentation folder is included
- [ ] License file is included
- [ ] README.txt is present with system requirements
- [ ] Version is consistent across all files
- [ ] Archives are properly named with version
- [ ] Checksums are generated (if using -CreateChecksums)
- [ ] Archives can be extracted and run on target platforms
- [ ] Builds tested on target platforms before distribution

## References

- [Stride Distribution Documentation](https://doc.stride3d.net/latest/en/manual/files-and-folders/distribute-a-game.html)
- [.NET Application Publishing Overview](https://learn.microsoft.com/en-us/dotnet/core/deploying/)
- [Stride Project Structure](https://doc.stride3d.net/latest/en/manual/files-and-folders/project-structure.html)

