#Requires -Version 5.1

<#
.SYNOPSIS
    Creates version information files for Stride game distribution.

.DESCRIPTION
    Generates version.txt and VERSION files with build information for distribution.
    Useful for CI/CD pipelines and version tracking.

.PARAMETER OutputPath
    Path where version files will be created.

.PARAMETER GameName
    Name of the game.

.PARAMETER Version
    Version string (e.g., "1.0.0").

.PARAMETER BuildNumber
    Build number (typically from CI/CD).

.PARAMETER BuildDate
    Build date. Defaults to current date/time.

.PARAMETER CommitHash
    Git commit hash (optional).

.EXAMPLE
    .\New-StrideVersionFile.ps1 -OutputPath "dist" -GameName "Odyssey" -Version "1.0.0" -BuildNumber "123"
#>

[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$OutputPath,
    
    [Parameter(Mandatory = $true)]
    [string]$GameName,
    
    [Parameter(Mandatory = $true)]
    [string]$Version,
    
    [string]$BuildNumber = "",
    [string]$BuildDate = "",
    [string]$CommitHash = ""
)

$ErrorActionPreference = "Stop"

if (-not (Test-Path $OutputPath)) {
    New-Item -Path $OutputPath -ItemType Directory -Force | Out-Null
}

if ([string]::IsNullOrEmpty($BuildDate)) {
    $BuildDate = Get-Date -Format "yyyy-MM-dd HH:mm:ss UTC"
}

# Try to get commit hash from git if not provided
if ([string]::IsNullOrEmpty($CommitHash)) {
    $GitCmd = Get-Command git -ErrorAction SilentlyContinue
    if ($GitCmd) {
        try {
            $CommitHash = (git rev-parse HEAD 2>$null).Trim()
        } catch {
            # Ignore errors
        }
    }
}

# Create version.txt (human-readable)
$VersionTxtPath = Join-Path $OutputPath "version.txt"
$VersionTxtContent = @"
$GameName Version Information
================================

Version: $Version
Build Number: $(if ([string]::IsNullOrEmpty($BuildNumber)) { "N/A" } else { $BuildNumber })
Build Date: $BuildDate
Commit: $(if ([string]::IsNullOrEmpty($CommitHash)) { "N/A" } else { $CommitHash })
"@

Set-Content -Path $VersionTxtPath -Value $VersionTxtContent -Encoding UTF8
Write-Host "Created: $VersionTxtPath" -ForegroundColor Green

# Create VERSION (machine-readable, key=value format)
$VersionFilePath = Join-Path $OutputPath "VERSION"
$VersionFileContent = @"
GAME_NAME=$GameName
VERSION=$Version
BUILD_NUMBER=$BuildNumber
BUILD_DATE=$BuildDate
COMMIT_HASH=$CommitHash
"@

Set-Content -Path $VersionFilePath -Value $VersionFileContent -Encoding UTF8
Write-Host "Created: $VersionFilePath" -ForegroundColor Green

# Create version.json (structured format)
$VersionJsonPath = Join-Path $OutputPath "version.json"
$VersionJson = @{
    gameName = $GameName
    version = $Version
    buildNumber = $BuildNumber
    buildDate = $BuildDate
    commitHash = $CommitHash
} | ConvertTo-Json -Depth 10

Set-Content -Path $VersionJsonPath -Value $VersionJson -Encoding UTF8
Write-Host "Created: $VersionJsonPath" -ForegroundColor Green

Write-Host "`nVersion files created successfully." -ForegroundColor Cyan

