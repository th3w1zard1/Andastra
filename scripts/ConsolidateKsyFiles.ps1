# Script to consolidate all .ksy files from src/Andastra to bioware-kaitai-formats
# This ensures all Kaitai Struct format definitions are in one location

param(
    [string]$SourceDir = "src\Andastra\Parsing\Resource\Formats",
    [string]$TargetDir = "bioware-kaitai-formats\formats",
    [switch]$DryRun
)

$ErrorActionPreference = "Stop"

Write-Host "Consolidating .ksy files from $SourceDir to $TargetDir" -ForegroundColor Cyan
if ($DryRun) {
    Write-Host "DRY RUN MODE - No files will be copied" -ForegroundColor Yellow
}

function Copy-KsyFile {
    param(
        [string]$SourcePath,
        [string]$RelativePath
    )

    $TargetPath = Join-Path $TargetDir $RelativePath
    $TargetParent = Split-Path -Parent $TargetPath

    if (-not (Test-Path $SourcePath)) {
        Write-Warning "Source file not found: $SourcePath"
        return $false
    }

    if (-not (Test-Path $TargetParent)) {
        if (-not $DryRun) {
            New-Item -ItemType Directory -Path $TargetParent -Force | Out-Null
        }
        Write-Host "Created directory: $TargetParent" -ForegroundColor Green
    }

    if (Test-Path $TargetPath) {
        $SourceHash = (Get-FileHash -Path $SourcePath -Algorithm SHA256).Hash
        $TargetHash = (Get-FileHash -Path $TargetPath -Algorithm SHA256).Hash

        if ($SourceHash -eq $TargetHash) {
            Write-Host "  [SKIP] $RelativePath (identical)" -ForegroundColor Gray
            return $false
        } else {
            Write-Host "  [UPDATE] $RelativePath (files differ)" -ForegroundColor Yellow
            if (-not $DryRun) {
                Copy-Item -Path $SourcePath -Destination $TargetPath -Force
            }
            return $true
        }
    } else {
        Write-Host "  [COPY] $RelativePath" -ForegroundColor Green
        if (-not $DryRun) {
            Copy-Item -Path $SourcePath -Destination $TargetPath -Force
        }
        return $true
    }
}

# Find all .ksy files in source directory
$SourceFiles = Get-ChildItem -Path $SourceDir -Filter "*.ksy" -Recurse
$SourceBase = Resolve-Path $SourceDir

$CopiedCount = 0
$SkippedCount = 0
$UpdatedCount = 0

foreach ($file in $SourceFiles) {
    $RelativePath = $file.FullName.Substring($SourceBase.Path.Length + 1)

    $Changed = Copy-KsyFile -SourcePath $file.FullName -RelativePath $RelativePath

    if ($Changed) {
        if (Test-Path (Join-Path $TargetDir $RelativePath)) {
            $UpdatedCount++
        } else {
            $CopiedCount++
        }
    } else {
        $SkippedCount++
    }
}

Write-Host ""
Write-Host "Summary:" -ForegroundColor Cyan
Write-Host "  Copied: $CopiedCount" -ForegroundColor Green
Write-Host "  Updated: $UpdatedCount" -ForegroundColor Yellow
Write-Host "  Skipped: $SkippedCount" -ForegroundColor Gray

