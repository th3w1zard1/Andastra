#Requires -Version 5.1

<#
.SYNOPSIS
    CI/CD wrapper for Stride game build and distribution pipeline.

.DESCRIPTION
    Designed for use in CI/CD pipelines (GitHub Actions, Azure DevOps, etc.).
    Automatically detects CI/CD environment and sets appropriate defaults.
    Provides artifact publishing capabilities.

.PARAMETER ProjectPath
    Path to the game project (.csproj file).

.PARAMETER GameName
    Name of the game.

.PARAMETER Version
    Version string. If not provided, attempts to extract from git tag or defaults to "1.0.0".

.PARAMETER Platforms
    Platforms to build for. Defaults to all supported platforms.

.PARAMETER PublishArtifacts
    Whether to publish artifacts for CI/CD system.
    Defaults to $true in CI/CD environments.

.PARAMETER ArtifactPath
    Path to publish artifacts to. Defaults to CI/CD artifact paths.

.EXAMPLE
    # In GitHub Actions
    .\Invoke-StrideCICD.ps1 -GameName "Odyssey" -Version "1.0.0"

.EXAMPLE
    # In Azure DevOps
    .\Invoke-StrideCICD.ps1 -GameName "Odyssey" -PublishArtifacts
#>

[CmdletBinding()]
param(
    [string]$ProjectPath = "src/OdysseyRuntime/Odyssey.Game/Odyssey.Game.csproj",
    [string]$GameName = "Odyssey",
    [string]$Version = "",
    [string[]]$Platforms = @("All"),
    [bool]$PublishArtifacts = $true,
    [string]$ArtifactPath = ""
)

$ErrorActionPreference = "Stop"

# Detect CI/CD environment
$IsGitHubActions = -not [string]::IsNullOrEmpty($env:GITHUB_ACTIONS)
$IsAzureDevOps = -not [string]::IsNullOrEmpty($env:SYSTEM_TEAMFOUNDATIONCOLLECTIONURI)
$IsJenkins = -not [string]::IsNullOrEmpty($env:JENKINS_URL)
$IsCI = $IsGitHubActions -or $IsAzureDevOps -or $IsJenkins -or (-not [string]::IsNullOrEmpty($env:CI))

Write-Host "========================================" -ForegroundColor Cyan
Write-Host "Stride Game CI/CD Pipeline" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan

if ($IsCI) {
    Write-Host "CI/CD Environment Detected:" -ForegroundColor Green
    if ($IsGitHubActions) {
        Write-Host "  - GitHub Actions" -ForegroundColor White
        Write-Host "  - Workflow: $env:GITHUB_WORKFLOW" -ForegroundColor Gray
        Write-Host "  - Run ID: $env:GITHUB_RUN_ID" -ForegroundColor Gray
    } elseif ($IsAzureDevOps) {
        Write-Host "  - Azure DevOps" -ForegroundColor White
        Write-Host "  - Build ID: $env:BUILD_BUILDID" -ForegroundColor Gray
    } elseif ($IsJenkins) {
        Write-Host "  - Jenkins" -ForegroundColor White
        Write-Host "  - Build Number: $env:BUILD_NUMBER" -ForegroundColor Gray
    } else {
        Write-Host "  - Generic CI" -ForegroundColor White
    }
    Write-Host ""
}

# Determine version
if ([string]::IsNullOrEmpty($Version)) {
    # Try to get version from git tag
    $GitCmd = Get-Command git -ErrorAction SilentlyContinue
    if ($GitCmd) {
        try {
            $GitTag = (git describe --tags --exact-match HEAD 2>$null).Trim()
            if (-not [string]::IsNullOrEmpty($GitTag)) {
                $Version = $GitTag -replace '^v', ''  # Remove 'v' prefix if present
                Write-Host "Version from git tag: $Version" -ForegroundColor Green
            }
        } catch {
            # Ignore
        }
    }
    
    # Try CI/CD environment variables
    if ([string]::IsNullOrEmpty($Version)) {
        $Version = $env:VERSION
    }
    if ([string]::IsNullOrEmpty($Version)) {
        $Version = $env:GITHUB_REF_NAME
        if ($Version -and $Version -match '^v?\d+\.\d+\.\d+') {
            $Version = $Version -replace '^v', ''
        }
    }
    
    # Default fallback
    if ([string]::IsNullOrEmpty($Version)) {
        $Version = "1.0.0"
        Write-Host "Using default version: $Version" -ForegroundColor Yellow
    }
}

# Set artifact paths based on CI/CD system
if ([string]::IsNullOrEmpty($ArtifactPath)) {
    if ($IsGitHubActions) {
        $ArtifactPath = $env:GITHUB_WORKSPACE
        if ([string]::IsNullOrEmpty($ArtifactPath)) {
            $ArtifactPath = "."
        }
        $ArtifactPath = Join-Path $ArtifactPath "dist"
    } elseif ($IsAzureDevOps) {
        $ArtifactPath = $env:BUILD_ARTIFACTSTAGINGDIRECTORY
        if ([string]::IsNullOrEmpty($ArtifactPath)) {
            $ArtifactPath = Join-Path $env:BUILD_BINARIESDIRECTORY "dist"
        }
    } else {
        $ArtifactPath = "dist"
    }
}

Write-Host "Configuration:" -ForegroundColor Cyan
Write-Host "  Game: $GameName" -ForegroundColor White
Write-Host "  Version: $Version" -ForegroundColor White
Write-Host "  Platforms: $($Platforms -join ', ')" -ForegroundColor White
Write-Host "  Output: $ArtifactPath" -ForegroundColor White
Write-Host "  Publish Artifacts: $PublishArtifacts" -ForegroundColor White
Write-Host "========================================`n" -ForegroundColor Cyan

# Get script directory
$ScriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path

# Run distribution pipeline
$DistributeParams = @{
    ProjectPath = $ProjectPath
    GameName = $GameName
    Version = $Version
    Platforms = $Platforms
    OutputPath = $ArtifactPath
    CreateChecksums = $true
    CreateVersionFiles = $true
    Clean = $true
}

if ($IsCI) {
    $DistributeParams["Verbose"] = $true
}

& (Join-Path $ScriptDir "Distribute-StrideGame.ps1") @DistributeParams

if ($LASTEXITCODE -ne 0) {
    Write-Error "CI/CD pipeline failed."
    exit 1
}

# Publish artifacts for CI/CD system
if ($PublishArtifacts) {
    Write-Host "`nPublishing artifacts..." -ForegroundColor Yellow
    
    if ($IsGitHubActions) {
        # GitHub Actions artifact upload
        $ArchiveFiles = Get-ChildItem -Path $ArtifactPath -Filter "*.zip" -ErrorAction SilentlyContinue
        $ArchiveFiles += Get-ChildItem -Path $ArtifactPath -Filter "*.tar.gz" -ErrorAction SilentlyContinue
        
        if ($ArchiveFiles.Count -gt 0) {
            Write-Host "Found $($ArchiveFiles.Count) archive file(s) to upload" -ForegroundColor Green
            
            # Note: Actual upload would be done by GitHub Actions upload-artifact action
            # This script just prepares the artifacts
            Write-Host "Artifacts ready for GitHub Actions upload-artifact action" -ForegroundColor Green
            Write-Host "Use: - uses: actions/upload-artifact@v3" -ForegroundColor Gray
            Write-Host "     with:" -ForegroundColor Gray
            Write-Host "       path: dist/*.zip" -ForegroundColor Gray
        }
    } elseif ($IsAzureDevOps) {
        # Azure DevOps artifact publishing would use PublishBuildArtifacts task
        Write-Host "Artifacts ready for Azure DevOps PublishBuildArtifacts task" -ForegroundColor Green
    }
}

Write-Host "`nCI/CD pipeline completed successfully!" -ForegroundColor Cyan
Write-Host "Artifacts location: $ArtifactPath" -ForegroundColor Cyan

