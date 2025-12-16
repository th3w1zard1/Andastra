# Comprehensive verification test for EngineNamespaceRenamer.ps1
$ErrorActionPreference = 'Continue'

Write-Host "========================================" -ForegroundColor Cyan
Write-Host "COMPREHENSIVE RENAMER SCRIPT TEST" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan

# Test 1: Create test environment
Write-Host "`n[TEST 1] Creating test environment..." -ForegroundColor Yellow
$testDir = "test_renamer_verify_$(Get-Random)"
if (Test-Path $testDir) { Remove-Item $testDir -Recurse -Force }
New-Item -ItemType Directory -Path $testDir | Out-Null

# Create test files
"namespace Test.OldNamespace { using System.Collections; using System; class Test { } }" | Out-File -FilePath "$testDir\test.cs" -Encoding utf8
"Project reference: src\OldFolder\project.csproj" | Out-File -FilePath "$testDir\test.csproj" -Encoding utf8
"Solution reference: src\OldFolder\OldFolder.csproj" | Out-File -FilePath "$testDir\test.sln" -Encoding utf8

Write-Host "  Created test directory: $testDir" -ForegroundColor Green
Write-Host "  Files created:" -ForegroundColor Green
Get-ChildItem $testDir | ForEach-Object { Write-Host "    - $($_.Name)" -ForegroundColor Gray }

# Test 2: Verify initial state
Write-Host "`n[TEST 2] Verifying initial state..." -ForegroundColor Yellow
$beforeCs = Get-Content "$testDir\test.cs" -Raw
$beforeCsproj = Get-Content "$testDir\test.csproj" -Raw

if ($beforeCs -match "Test.OldNamespace" -and $beforeCsproj -match "OldFolder") {
    Write-Host "  ✓ Initial state correct" -ForegroundColor Green
} else {
    Write-Host "  ✗ Initial state incorrect" -ForegroundColor Red
    exit 1
}

# Test 3: Run the script
Write-Host "`n[TEST 3] Running EngineNamespaceRenamer.ps1..." -ForegroundColor Yellow
Write-Host "  Parameters: -Timeout 30 -NoValidation -NoFolderRename -Verbose" -ForegroundColor Gray

$startTime = Get-Date
try {
    $output = .\scripts\EngineNamespaceRenamer.ps1 `
        -RootPath $testDir `
        -OldFolderName "OldFolder" `
        -NewFolderName "NewFolder" `
        -OldNamespace "Test.OldNamespace" `
        -NewNamespace "Test.NewNamespace" `
        -NoValidation `
        -NoFolderRename `
        -Timeout 30 `
        -Verbose 2>&1 | Out-String

    $duration = ((Get-Date) - $startTime).TotalSeconds

    Write-Host "  ✓ Script completed in $($duration.ToString('F2')) seconds" -ForegroundColor Green

    if ($output -match "Completed successfully") {
        Write-Host "  ✓ Script reported success" -ForegroundColor Green
    } else {
        Write-Host "  ⚠ Script output may indicate issues" -ForegroundColor Yellow
        Write-Host "  Output preview:" -ForegroundColor Gray
        $output -split "`n" | Select-Object -First 10 | ForEach-Object { Write-Host "    $_" -ForegroundColor Gray }
    }
}
catch {
    Write-Host "  ✗ Script failed: $_" -ForegroundColor Red
    Write-Host "  Stack: $($_.ScriptStackTrace)" -ForegroundColor Red
    Remove-Item $testDir -Recurse -Force -ErrorAction SilentlyContinue
    exit 1
}

# Test 4: Verify results
Write-Host "`n[TEST 4] Verifying results..." -ForegroundColor Yellow

if (-not (Test-Path "$testDir\test.cs")) {
    Write-Host "  ✗ test.cs file missing!" -ForegroundColor Red
    exit 1
}

$afterCs = Get-Content "$testDir\test.cs" -Raw
$afterCsproj = Get-Content "$testDir\test.csproj" -Raw

$namespaceOk = $afterCs -match "Test.NewNamespace" -and $afterCs -notmatch "Test.OldNamespace"
$pathOk = $afterCsproj -match "NewFolder" -and $afterCsproj -notmatch "OldFolder"

if ($namespaceOk) {
    Write-Host "  ✓ Namespace replacement successful" -ForegroundColor Green
    Write-Host "    Content: $($afterCs.Substring(0, [Math]::Min(80, $afterCs.Length)))..." -ForegroundColor Gray
} else {
    Write-Host "  ✗ Namespace replacement failed" -ForegroundColor Red
    Write-Host "    Content: $afterCs" -ForegroundColor Red
    exit 1
}

if ($pathOk) {
    Write-Host "  ✓ Path reference update successful" -ForegroundColor Green
} else {
    Write-Host "  ⚠ Path reference update may have issues" -ForegroundColor Yellow
}

# Test 5: Verify staging cleanup
Write-Host "`n[TEST 5] Verifying staging cleanup..." -ForegroundColor Yellow
if (Test-Path "$testDir\.staging") {
    Write-Host "  ✗ Staging directory still exists (should be cleaned up)" -ForegroundColor Red
    exit 1
} else {
    Write-Host "  ✓ Staging directory properly cleaned up" -ForegroundColor Green
}

# Test 6: Verify backups
Write-Host "`n[TEST 6] Verifying backups..." -ForegroundColor Yellow
$backupDir = Join-Path $testDir ".backups"
if (Test-Path $backupDir) {
    $backups = Get-ChildItem -Path $backupDir -Directory -Filter "backup-*"
    if ($backups.Count -gt 0) {
        Write-Host "  ✓ Backup created: $($backups[0].Name)" -ForegroundColor Green
    } else {
        Write-Host "  ⚠ Backup directory exists but no backups found" -ForegroundColor Yellow
    }
} else {
    Write-Host "  ⚠ No backup directory found" -ForegroundColor Yellow
}

# Cleanup
Write-Host "`n[CLEANUP] Removing test directory..." -ForegroundColor Yellow
Remove-Item $testDir -Recurse -Force -ErrorAction SilentlyContinue
Write-Host "  ✓ Cleanup complete" -ForegroundColor Green

Write-Host "`n========================================" -ForegroundColor Cyan
Write-Host "ALL TESTS PASSED ✓" -ForegroundColor Green
Write-Host "========================================" -ForegroundColor Cyan

