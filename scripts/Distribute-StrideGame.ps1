#Requires -Version 5.1

<#
.SYNOPSIS
    Complete distribution pipeline for Stride game projects.

.DESCRIPTION
    Orchestrates the complete build and packaging process for Stride game distribution.
    Builds for all specified platforms, cleans up files, adds documentation, and creates
    distribution archives following industry best practices.

.PARAMETER ProjectPath
    Path to the game project (.csproj file). Defaults to src/OdysseyRuntime/Odyssey.Game/Odyssey.Game.csproj

.PARAMETER GameName
    Name of the game for distribution. Defaults to "Odyssey"

.PARAMETER Version
    Version string for the distribution. Defaults to "1.0.0"

.PARAMETER Configuration
    Build configuration. Defaults to "Release"

.PARAMETER Platforms
    Comma-separated list of platforms. Options: Windows, Linux, macOS, All
    Defaults to "All"

.PARAMETER Architectures
    Comma-separated list of architectures. Options: x64, x86, arm64, All
    Defaults to "x64"

.PARAMETER SelfContained
    Whether to create self-contained deployments. Defaults to $true

.PARAMETER SingleFile
    Whether to publish as single-file executables. Defaults to $false

.PARAMETER Trimmed
    Whether to trim unused code (reduces size but may break reflection-heavy code).
    Defaults to $false

.PARAMETER ReadyToRun
    Whether to enable ReadyToRun compilation (faster startup). Defaults to $true

.PARAMETER OutputPath
    Base output path. Defaults to "dist"

.PARAMETER ArchiveFormat
    Archive format for distribution. Options: zip, tar, tar.gz. Defaults to "zip"

.PARAMETER IncludeDocumentation
    Whether to include documentation. Defaults to $true

.PARAMETER IncludeLicenses
    Whether to include license files. Defaults to $true

.PARAMETER Clean
    Clean build directories before building.

.PARAMETER SkipBuild
    Skip the build step (use existing build artifacts).

.PARAMETER SkipPackage
    Skip the packaging step (build only).

.PARAMETER CreateVersionFiles
    Whether to create version information files.
    Defaults to $true

.PARAMETER CreateChecksums
    Whether to create checksum files (SHA256) for archives. Defaults to $false

.PARAMETER Verbose
    Show verbose output during build and packaging.

.EXAMPLE
    .\Distribute-StrideGame.ps1 -GameName "Odyssey" -Version "1.0.0"

.EXAMPLE
    .\Distribute-StrideGame.ps1 -Platforms "Windows,Linux" -Architectures "x64" -Clean

.EXAMPLE
    .\Distribute-StrideGame.ps1 -SkipBuild -SkipPackage $false
#>

[CmdletBinding()]
param(
    [string]$ProjectPath = "src/OdysseyRuntime/Odyssey.Game/Odyssey.Game.csproj",
    [string]$GameName = "Odyssey",
    [string]$Version = "1.0.0",
    [ValidateSet("Debug", "Release")]
    [string]$Configuration = "Release",
    [string[]]$Platforms = @("All"),
    [string[]]$Architectures = @("x64"),
    [bool]$SelfContained = $true,
    [bool]$SingleFile = $false,
    [bool]$Trimmed = $false,
    [bool]$ReadyToRun = $true,
    [string]$OutputPath = "dist",
    [ValidateSet("zip", "tar", "tar.gz")]
    [string]$ArchiveFormat = "zip",
    [bool]$IncludeDocumentation = $true,
    [bool]$IncludeLicenses = $true,
    [switch]$Clean,
    [switch]$SkipBuild,
    [switch]$SkipPackage,
    [bool]$CreateChecksums = $false,
    [bool]$CreateVersionFiles = $true,
    [switch]$Verbose
)

$ErrorActionPreference = "Stop"

# Get script directory for calling other scripts
$ScriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path

Write-Host "========================================" -ForegroundColor Cyan
Write-Host "Stride Game Distribution Pipeline" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "Game: $GameName" -ForegroundColor White
Write-Host "Version: $Version" -ForegroundColor White
Write-Host "Configuration: $Configuration" -ForegroundColor White
Write-Host "Platforms: $($Platforms -join ', ')" -ForegroundColor White
Write-Host "Architectures: $($Architectures -join ', ')" -ForegroundColor White
Write-Host "Self-Contained: $SelfContained" -ForegroundColor White
Write-Host "Output: $OutputPath" -ForegroundColor White
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

# Step 1: Build
if (-not $SkipBuild) {
    Write-Host "STEP 1: Building game..." -ForegroundColor Yellow
    Write-Host ""
    
    $BuildParams = @{
        ProjectPath = $ProjectPath
        Configuration = $Configuration
        Platforms = $Platforms
        Architectures = $Architectures
        SelfContained = $SelfContained
        SingleFile = $SingleFile
        Trimmed = $Trimmed
        ReadyToRun = $ReadyToRun
        OutputPath = $OutputPath
        Clean = $Clean.IsPresent
        Restore = $true
    }
    
    if ($Verbose) {
        $BuildParams["Verbose"] = $true
    }
    
    & (Join-Path $ScriptDir "Build-StrideGame.ps1") @BuildParams
    
    if ($LASTEXITCODE -ne 0) {
        Write-Error "Build step failed."
        exit 1
    }
    
    Write-Host ""
    Write-Host "Build step completed successfully." -ForegroundColor Green
    Write-Host ""
} else {
    Write-Host "STEP 1: Skipping build (using existing artifacts)" -ForegroundColor Yellow
    Write-Host ""
}

# Step 1.5: Create version files
if ($CreateVersionFiles -and -not $SkipBuild) {
    Write-Host "STEP 1.5: Creating version files..." -ForegroundColor Yellow
    Write-Host ""
    
    # Try to get build number from environment (CI/CD)
    $BuildNumber = $env:BUILD_NUMBER
    if ([string]::IsNullOrEmpty($BuildNumber)) {
        $BuildNumber = $env:GITHUB_RUN_NUMBER
    }
    if ([string]::IsNullOrEmpty($BuildNumber)) {
        $BuildNumber = $env:CI_PIPELINE_ID
    }
    
    $VersionScriptPath = Join-Path $ScriptDir "New-StrideVersionFile.ps1"
    if (Test-Path $VersionScriptPath) {
        $VersionParams = @{
            OutputPath = $OutputPath
            GameName = $GameName
            Version = $Version
        }
        
        if (-not [string]::IsNullOrEmpty($BuildNumber)) {
            $VersionParams["BuildNumber"] = $BuildNumber
        }
        
        & $VersionScriptPath @VersionParams
        
        if ($LASTEXITCODE -ne 0) {
            Write-Warning "Version file creation had issues, but continuing..."
        }
    } else {
        Write-Warning "Version file script not found, skipping version file creation"
    }
    
    Write-Host ""
}

# Step 2: Package
if (-not $SkipPackage) {
    Write-Host "STEP 2: Packaging distribution..." -ForegroundColor Yellow
    Write-Host ""
    
    $PackageParams = @{
        BuildPath = $OutputPath
        GameName = $GameName
        Version = $Version
        CreateArchive = $true
        ArchiveFormat = $ArchiveFormat
        IncludeDocumentation = $IncludeDocumentation
        IncludeLicenses = $IncludeLicenses
        KeepDebugSymbols = $false
        CreateChecksums = $CreateChecksums
    }
    
    if ($Verbose) {
        $PackageParams["Verbose"] = $true
    }
    
    & (Join-Path $ScriptDir "Package-StrideGame.ps1") @PackageParams
    
    if ($LASTEXITCODE -ne 0) {
        Write-Error "Packaging step failed."
        exit 1
    }
    
    Write-Host ""
    Write-Host "Packaging step completed successfully." -ForegroundColor Green
    Write-Host ""
} else {
    Write-Host "STEP 2: Skipping packaging" -ForegroundColor Yellow
    Write-Host ""
}

# Summary
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "Distribution Complete!" -ForegroundColor Green
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "Output directory: $OutputPath" -ForegroundColor White

if (-not $SkipPackage -and $ArchiveFormat) {
    $Archives = Get-ChildItem -Path $OutputPath -Filter "*.$ArchiveFormat" -ErrorAction SilentlyContinue
    if ($Archives) {
        Write-Host ""
        Write-Host "Created archives:" -ForegroundColor White
        foreach ($Archive in $Archives) {
            $SizeMB = [math]::Round($Archive.Length / 1MB, 2)
            Write-Host "  - $($Archive.Name) ($SizeMB MB)" -ForegroundColor Gray
        }
    }
}

Write-Host ""

