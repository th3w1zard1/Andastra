# PowerShell script to copy test files to test_files directory
param(
    [string]$SourceFile = "G:\GitHub\PyKotor\vendor\Kotor-Randomizer\kotor Randomizer 2\Resources\k2patch\a_galaxymap.ncs"
)

$TestFilesDir = Join-Path $PSScriptRoot "test_files"
$DestFile = Join-Path $TestFilesDir "a_galaxymap.ncs"

# Create test_files directory if it doesn't exist
if (-not (Test-Path $TestFilesDir)) {
    New-Item -ItemType Directory -Path $TestFilesDir -Force | Out-Null
}

# Copy the test file if source exists
if (Test-Path $SourceFile) {
    Copy-Item -Path $SourceFile -Destination $DestFile -Force
    Write-Host "Copied test file: $DestFile"
} else {
    Write-Warning "Source file not found: $SourceFile"
    Write-Host "Please manually copy test NCS files to: $TestFilesDir"
}

