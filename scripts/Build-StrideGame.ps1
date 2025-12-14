#Requires -Version 5.1

<#
.SYNOPSIS
    Builds a Stride game project for distribution following industry best practices.

.DESCRIPTION
    Builds the Odyssey Game project in Release mode for the specified platform(s).
    Supports Windows, Linux, and macOS with x64, x86, and arm64 architectures.
    Follows Stride game engine and .NET publishing best practices:
    - Asset compilation via Stride build system
    - Self-contained deployments with .NET runtime
    - ReadyToRun compilation for faster startup
    - Optional trimming for smaller deployments
    - Proper folder structure validation

.PARAMETER ProjectPath
    Path to the game project (.csproj file). Defaults to src/OdysseyRuntime/Odyssey.Game/Odyssey.Game.csproj

.PARAMETER Configuration
    Build configuration. Defaults to "Release".

.PARAMETER Platforms
    Comma-separated list of platforms to build for. Options: Windows, Linux, macOS, All
    Defaults to "All"

.PARAMETER Architectures
    Comma-separated list of architectures to build for. Options: x64, x86, arm64, All
    Defaults to "x64"

.PARAMETER SelfContained
    Whether to create self-contained deployments (includes .NET runtime).
    Defaults to $true

.PARAMETER SingleFile
    Whether to publish as a single-file executable.
    Note: Single-file is a self-extracting archive and may slow startup.
    Defaults to $false

.PARAMETER Trimmed
    Whether to trim unused code (reduces size but may break reflection-heavy code).
    Defaults to $false

.PARAMETER ReadyToRun
    Whether to enable ReadyToRun compilation (faster startup, larger size).
    Defaults to $true

.PARAMETER OutputPath
    Base output path for builds. Defaults to "dist"

.PARAMETER Clean
    Clean build directories before building.

.PARAMETER Restore
    Restore NuGet packages before building.

.PARAMETER NoBuild
    Skip the build step (only restore packages).

.PARAMETER Verbose
    Show verbose build output.

.PARAMETER VerifyAssets
    Verify that Stride assets are properly compiled after build.
    Defaults to $true

.EXAMPLE
    .\Build-StrideGame.ps1

.EXAMPLE
    .\Build-StrideGame.ps1 -Platforms "Windows" -Architectures "x64,x86" -Trimmed

.EXAMPLE
    .\Build-StrideGame.ps1 -Platforms "Linux" -SelfContained $false -SingleFile

.EXAMPLE
    .\Build-StrideGame.ps1 -Clean -Restore -Verbose
#>

[CmdletBinding(SupportsShouldProcess = $true)]
param(
    [string]$ProjectPath = "src/OdysseyRuntime/Odyssey.Game/Odyssey.Game.csproj",
    [ValidateSet("Debug", "Release")]
    [string]$Configuration = "Release",
    [string[]]$Platforms = @("All"),
    [string[]]$Architectures = @("x64"),
    [bool]$SelfContained = $true,
    [bool]$SingleFile = $false,
    [bool]$Trimmed = $false,
    [bool]$ReadyToRun = $true,
    [string]$OutputPath = "dist",
    [switch]$Clean,
    [switch]$Restore,
    [switch]$NoBuild,
    [switch]$Verbose,
    [bool]$VerifyAssets = $true
)

$ErrorActionPreference = "Stop"

# Cross-platform path handling
$IsWindowsPlatform = ($IsWindows -or $env:OS -eq "Windows_NT")

function Normalize-Path {
    param([string]$Path)
    if ($IsWindowsPlatform) {
        return $Path -replace '/', '\'
    } else {
        return $Path -replace '\\', '/'
    }
}

function Test-Command {
    param([string]$Command)
    $Cmd = Get-Command $Command -ErrorAction SilentlyContinue
    return $null -ne $Cmd
}

function Write-VerboseOutput {
    param([string]$Message)
    if ($Verbose) {
        Write-Host $Message -ForegroundColor Gray
    }
}

$ProjectPath = Normalize-Path $ProjectPath
$OutputPath = Normalize-Path $OutputPath

# Validate .NET SDK
Write-Host "Checking .NET SDK..." -ForegroundColor Cyan
if (-not (Test-Command "dotnet")) {
    Write-Error ".NET SDK not found. Please install .NET SDK."
    exit 1
}

$DotNetVersion = dotnet --version
Write-Host "Using .NET SDK: $DotNetVersion" -ForegroundColor Green

# Validate project exists
if (-not (Test-Path $ProjectPath)) {
    Write-Error "Project file not found: $ProjectPath"
    exit 1
}

$ProjectPath = Resolve-Path $ProjectPath

# Platform and architecture mappings
$PlatformRIDs = @{
    Windows = @{
        x64 = "win-x64"
        x86 = "win-x86"
        arm64 = "win-arm64"
    }
    Linux = @{
        x64 = "linux-x64"
        x86 = "linux-x86"
        arm64 = "linux-arm64"
    }
    macOS = @{
        x64 = "osx-x64"
        arm64 = "osx-arm64"
    }
}

# Expand "All" keywords
if ($Platforms -contains "All") {
    $Platforms = @("Windows", "Linux", "macOS")
}

if ($Architectures -contains "All") {
    $Architectures = @("x64", "x86", "arm64")
}

# Filter valid platform/architecture combinations
$BuildTargets = @()
foreach ($Platform in $Platforms) {
    if (-not $PlatformRIDs.ContainsKey($Platform)) {
        Write-Warning "Unknown platform: $Platform. Skipping."
        continue
    }
    
    foreach ($Arch in $Architectures) {
        if ($PlatformRIDs[$Platform].ContainsKey($Arch)) {
            $RID = $PlatformRIDs[$Platform][$Arch]
            $BuildTargets += @{
                Platform = $Platform
                Architecture = $Arch
                RID = $RID
            }
        } else {
            Write-Warning "Architecture $Arch not supported for platform $Platform. Skipping."
        }
    }
}

if ($BuildTargets.Count -eq 0) {
    Write-Error "No valid build targets specified."
    exit 1
}

Write-Host "`n========================================" -ForegroundColor Cyan
Write-Host "Stride Game Build Configuration" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "Project: $ProjectPath" -ForegroundColor White
Write-Host "Configuration: $Configuration" -ForegroundColor White
Write-Host "Targets: $($BuildTargets.Count)" -ForegroundColor White
Write-Host "Self-Contained: $SelfContained" -ForegroundColor White
Write-Host "Single-File: $SingleFile" -ForegroundColor White
Write-Host "Trimmed: $Trimmed" -ForegroundColor White
Write-Host "ReadyToRun: $ReadyToRun" -ForegroundColor White
Write-Host "========================================`n" -ForegroundColor Cyan

# Restore packages
if ($Restore -or -not $NoBuild) {
    Write-Host "Restoring NuGet packages..." -ForegroundColor Yellow
    $RestoreArgs = @("restore", $ProjectPath)
    if ($Verbose) {
        $RestoreArgs += "--verbosity", "detailed"
    }
    
    & dotnet $RestoreArgs
    if ($LASTEXITCODE -ne 0) {
        Write-Error "Package restore failed."
        exit 1
    }
    Write-Host "Package restore completed." -ForegroundColor Green
}

if ($NoBuild) {
    Write-Host "Skipping build (NoBuild specified)." -ForegroundColor Yellow
    exit 0
}

# Build each target
$FailedBuilds = @()
$SuccessfulBuilds = @()

foreach ($Target in $BuildTargets) {
    $RID = $Target.RID
    $Platform = $Target.Platform
    $Arch = $Target.Architecture
    
    $PublishDir = Join-Path $OutputPath "$Platform-$Arch"
    $PublishDir = Normalize-Path $PublishDir
    
    Write-Host "`n" + ("=" * 50) -ForegroundColor Cyan
    Write-Host "Building for $Platform ($Arch) - RID: $RID" -ForegroundColor Green
    Write-Host ("=" * 50) -ForegroundColor Cyan
    
    # Clean if requested
    if ($Clean -and (Test-Path $PublishDir)) {
        if ($PSCmdlet.ShouldProcess($PublishDir, "Clean output directory")) {
            Write-Host "Cleaning output directory: $PublishDir" -ForegroundColor Yellow
            Remove-Item -Path $PublishDir -Recurse -Force -ErrorAction SilentlyContinue
        }
    }
    
    # Ensure output directory exists
    $OutputParent = Split-Path -Parent $PublishDir -ErrorAction SilentlyContinue
    if ($OutputParent -and -not (Test-Path $OutputParent)) {
        New-Item -Path $OutputParent -ItemType Directory -Force | Out-Null
    }
    
    # Build publish arguments
    $PublishArgs = @(
        "publish"
        $ProjectPath
        "--configuration", $Configuration
        "--runtime", $RID
        "--output", $PublishDir
        "-p:SelfContained=$SelfContained"
    )
    
    if ($ReadyToRun) {
        $PublishArgs += "-p:PublishReadyToRun=true"
    } else {
        $PublishArgs += "-p:PublishReadyToRun=false"
    }
    
    if ($SingleFile) {
        $PublishArgs += "-p:PublishSingleFile=true"
        # Include all content for self-extract (required for assets)
        $PublishArgs += "-p:IncludeAllContentForSelfExtract=true"
    }
    
    if ($Trimmed) {
        $PublishArgs += "-p:PublishTrimmed=true"
        # Enable trimming warnings
        $PublishArgs += "-p:TrimmerDefaultAction=link"
    }
    
    if ($Verbose) {
        $PublishArgs += "--verbosity", "detailed"
    } else {
        $PublishArgs += "--verbosity", "minimal"
    }
    
    # Execute publish
    Write-VerboseOutput "Executing: dotnet $($PublishArgs -join ' ')"
    
    $StartTime = Get-Date
    & dotnet $PublishArgs
    $BuildDuration = (Get-Date) - $StartTime
    
    if ($LASTEXITCODE -ne 0) {
        Write-Error "Build failed for $Platform ($Arch) after $($BuildDuration.TotalSeconds.ToString('F2')) seconds"
        $FailedBuilds += "$Platform-$Arch"
        continue
    }
    
    Write-Host "Build completed successfully in $($BuildDuration.TotalSeconds.ToString('F2')) seconds" -ForegroundColor Green
    
    # Verify build output
    Write-Host "Verifying build output..." -ForegroundColor Yellow
    
    $HasExecutable = $false
    if ($IsWindowsPlatform -or $Platform -eq "Windows") {
        $HasExecutable = (Get-ChildItem -Path $PublishDir -Filter "*.exe" -Recurse -ErrorAction SilentlyContinue).Count -gt 0
    } else {
        # Check for executable files (no extension or specific patterns)
        $Files = Get-ChildItem -Path $PublishDir -File -Recurse -ErrorAction SilentlyContinue
        foreach ($File in $Files) {
            if ($File.Extension -eq "" -or $File.Name -like "*Odyssey*") {
                $HasExecutable = $true
                break
            }
        }
    }
    
    if (-not $HasExecutable) {
        Write-Warning "No executable found in build output for $Platform ($Arch)"
    } else {
        Write-Host "Executable found." -ForegroundColor Green
    }
    
    # Verify Stride assets if requested
    if ($VerifyAssets) {
        Write-Host "Verifying Stride assets..." -ForegroundColor Yellow
        
        # Load Stride build helpers if available
        $HelpersPath = Join-Path (Split-Path -Parent $MyInvocation.MyCommand.Path) "StrideBuildHelpers.ps1"
        if (Test-Path $HelpersPath) {
            try {
                . $HelpersPath
                
                $AssetValidation = Test-StrideAssetCompilation -BuildPath $PublishDir -Platform $Platform
                Write-StrideBuildReport -ValidationResults $AssetValidation -BuildPath $PublishDir
                
                # Additional shader compilation check
                $ShadersCompiled = Test-StrideShaderCompilation -BuildPath $PublishDir
                if ($ShadersCompiled) {
                    Write-Host "Shader compilation: Verified" -ForegroundColor Green
                } else {
                    Write-VerboseOutput "No compiled shaders detected (may be compiled at runtime or not present)"
                }
                
                # Package structure validation
                $PackageStructure = Get-StridePackageStructure -BuildPath $PublishDir -Platform $Platform
                if (-not $PackageStructure.IsValid) {
                    Write-Warning "Package structure validation issues detected"
                    if ($PackageStructure.MissingFolders.Count -gt 0) {
                        Write-Warning "Missing expected folders: $($PackageStructure.MissingFolders -join ', ')"
                    }
                }
            } catch {
                Write-Warning "Could not load Stride build helpers: $_"
                # Fall back to basic verification
                $DataFolders = @(
                    Join-Path $PublishDir "Data"
                    Join-Path $PublishDir "data"
                    Join-Path $PublishDir "Assets"
                )
                
                $FoundAssets = $false
                foreach ($DataFolder in $DataFolders) {
                    if (Test-Path $DataFolder) {
                        $AssetFiles = Get-ChildItem -Path $DataFolder -Recurse -ErrorAction SilentlyContinue
                        if ($AssetFiles.Count -gt 0) {
                            Write-Host "Found $($AssetFiles.Count) asset file(s) in $(Split-Path -Leaf $DataFolder)" -ForegroundColor Green
                            $FoundAssets = $true
                            break
                        }
                    }
                }
                
                if (-not $FoundAssets) {
                    Write-VerboseOutput "No asset files found in build output. Assets may not be compiled."
                    Write-VerboseOutput "This is normal if your project doesn't use Stride assets, or if assets are embedded."
                }
            }
        } else {
            # Basic verification without helpers
            $DataFolders = @(
                Join-Path $PublishDir "Data"
                Join-Path $PublishDir "data"
                Join-Path $PublishDir "Assets"
            )
            
            $FoundAssets = $false
            foreach ($DataFolder in $DataFolders) {
                if (Test-Path $DataFolder) {
                    $AssetFiles = Get-ChildItem -Path $DataFolder -Recurse -ErrorAction SilentlyContinue
                    if ($AssetFiles.Count -gt 0) {
                        Write-Host "Found $($AssetFiles.Count) asset file(s) in $(Split-Path -Leaf $DataFolder)" -ForegroundColor Green
                        $FoundAssets = $true
                        break
                    }
                }
            }
            
            if (-not $FoundAssets) {
                Write-VerboseOutput "No asset files found in build output. Assets may not be compiled."
                Write-VerboseOutput "This is normal if your project doesn't use Stride assets, or if assets are embedded."
            }
        }
    }
    
    $SuccessfulBuilds += @{
        Platform = $Platform
        Architecture = $Arch
        Path = $PublishDir
        Duration = $BuildDuration
    }
}

Write-Host "`n" + ("=" * 50) -ForegroundColor Cyan
Write-Host "Build Summary" -ForegroundColor Cyan
Write-Host ("=" * 50) -ForegroundColor Cyan

Write-Host "`nSuccessful builds: $($SuccessfulBuilds.Count)" -ForegroundColor Green
foreach ($Build in $SuccessfulBuilds) {
    Write-Host "  - $($Build.Platform)-$($Build.Architecture): $($Build.Path) ($($Build.Duration.TotalSeconds.ToString('F2'))s)" -ForegroundColor Gray
}

if ($FailedBuilds.Count -gt 0) {
    Write-Host "`nFailed builds: $($FailedBuilds.Count)" -ForegroundColor Red
    foreach ($Build in $FailedBuilds) {
        Write-Host "  - $Build" -ForegroundColor Red
    }
    Write-Host ""
    Write-Error "Some builds failed. Check the output above for details."
    exit 1
}

Write-Host "`nAll builds completed successfully!" -ForegroundColor Cyan
Write-Host "Output directory: $OutputPath" -ForegroundColor Cyan
Write-Host ""
