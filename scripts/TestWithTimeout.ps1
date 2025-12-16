# Test script with proper timeout using Start-Process and taskkill
param(
    [int]$TimeoutSeconds = 120
)

$ErrorActionPreference = 'Continue'

Write-Host "=== TEST WITH TIMEOUT ($TimeoutSeconds seconds) ===" -ForegroundColor Cyan

$testDir = "test_timeout_$(Get-Random)"
if (Test-Path $testDir) { Remove-Item $testDir -Recurse -Force }
New-Item -ItemType Directory -Path $testDir | Out-Null
"namespace Test.Old { }" | Out-File -FilePath "$testDir\test.cs" -Encoding utf8

Write-Host "Created test directory: $testDir"
Write-Host "BEFORE:"
Get-Content "$testDir\test.cs"

Write-Host "`nStarting script with timeout..."
$scriptPath = ".\scripts\EngineNamespaceRenamer.ps1"
$processArgs = @(
    "-RootPath", $testDir,
    "-OldFolderName", "Old",
    "-NewFolderName", "New",
    "-OldNamespace", "Test.Old",
    "-NewNamespace", "Test.New",
    "-NoValidation",
    "-NoFolderRename",
    "-Timeout", "30"
)

$startTime = Get-Date
$process = Start-Process -FilePath "powershell.exe" -ArgumentList @("-ExecutionPolicy", "Bypass", "-File", $scriptPath) + $processArgs -PassThru -NoNewWindow -RedirectStandardOutput "test_output.log" -RedirectStandardError "test_error.log"

# Wait with timeout
$timedOut = $false
$waited = 0
while (-not $process.HasExited -and $waited -lt $TimeoutSeconds) {
    Start-Sleep -Seconds 1
    $waited++
    if ($waited % 10 -eq 0) {
        Write-Host "  Still running... ($waited/$TimeoutSeconds seconds)" -ForegroundColor Yellow
    }
}

if (-not $process.HasExited) {
    Write-Host "`nTIMEOUT: Killing process..." -ForegroundColor Red
    taskkill /F /PID $process.Id 2>&1 | Out-Null
    $timedOut = $true
    Write-Host "Process killed"
} else {
    $elapsed = ((Get-Date) - $startTime).TotalSeconds
    Write-Host "`nProcess completed in $elapsed seconds" -ForegroundColor Green
}

# Show output
if (Test-Path "test_output.log") {
    Write-Host "`nOutput:" -ForegroundColor Cyan
    Get-Content "test_output.log" | Select-Object -Last 30
    Remove-Item "test_output.log" -Force -ErrorAction SilentlyContinue
}

if (Test-Path "test_error.log") {
    Write-Host "`nErrors:" -ForegroundColor Red
    Get-Content "test_error.log" | Select-Object -Last 20
    Remove-Item "test_error.log" -Force -ErrorAction SilentlyContinue
}

# Verify results
if (-not $timedOut) {
    Write-Host "`nAFTER:" -ForegroundColor Cyan
    if (Test-Path "$testDir\test.cs") {
        Get-Content "$testDir\test.cs"
        $content = Get-Content "$testDir\test.cs" -Raw
        if ($content -match "Test.New" -and $content -notmatch "Test.Old") {
            Write-Host "`n✓ SUCCESS!" -ForegroundColor Green
        } else {
            Write-Host "`n✗ FAILED: Content not updated" -ForegroundColor Red
        }
    } else {
        Write-Host "File missing!" -ForegroundColor Red
    }
}

Remove-Item $testDir -Recurse -Force -ErrorAction SilentlyContinue

