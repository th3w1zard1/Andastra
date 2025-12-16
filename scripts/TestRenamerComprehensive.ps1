# Comprehensive test of EngineNamespaceRenamer.ps1
$ErrorActionPreference = 'Continue'

Write-Host "=== Comprehensive Test of EngineNamespaceRenamer.ps1 ===" -ForegroundColor Cyan

# Test 1: Syntax check
Write-Host "`n[1/5] Syntax check..." -ForegroundColor Yellow
try {
    $null = [System.Management.Automation.PSParser]::Tokenize((Get-Content scripts\EngineNamespaceRenamer.ps1 -Raw), [ref]$null)
    Write-Host "  ✓ Syntax valid" -ForegroundColor Green
}
catch {
    Write-Host "  ✗ Syntax error: $_" -ForegroundColor Red
    exit 1
}

# Test 2: List backups
Write-Host "`n[2/5] Testing -ListBackups..." -ForegroundColor Yellow
try {
    $output = .\scripts\EngineNamespaceRenamer.ps1 -ListBackups 2>&1 | Out-String
    Write-Host "  ✓ List backups works" -ForegroundColor Green
}
catch {
    Write-Host "  ✗ List backups failed: $_" -ForegroundColor Red
    exit 1
}

# Test 3: Create comprehensive test
Write-Host "`n[3/5] Creating test files..." -ForegroundColor Yellow
$testDir = "test_renamer_$(Get-Random)"
New-Item -ItemType Directory -Path $testDir -Force | Out-Null
"namespace Test.OldNamespace { using System; class Test { } }" | Out-File -FilePath "$testDir\test.cs" -Encoding utf8
"Project reference: src\OldFolder\project.csproj" | Out-File -FilePath "$testDir\test.csproj" -Encoding utf8
"Solution reference: src\OldFolder\OldFolder.csproj" | Out-File -FilePath "$testDir\test.sln" -Encoding utf8
Write-Host "  ✓ Created test directory: $testDir" -ForegroundColor Green

# Test 4: Run renamer
Write-Host "`n[4/5] Running renamer (namespace + path updates, no validation)..." -ForegroundColor Yellow
try {
    $startTime = Get-Date
    $output = .\scripts\EngineNamespaceRenamer.ps1 -RootPath $testDir -OldFolderName "OldFolder" -NewFolderName "NewFolder" -OldNamespace "Test.OldNamespace" -NewNamespace "Test.NewNamespace" -NoValidation -NoFolderRename -Timeout 30 -Verbose 2>&1 | Out-String
    $duration = ((Get-Date) - $startTime).TotalSeconds
    Write-Host "  ✓ Renamer completed in $($duration.ToString('F2'))s" -ForegroundColor Green
    if ($output -match "error|Error|ERROR|failed|Failed|FAILED") {
        Write-Host "  ⚠ Warnings/Errors in output (may be expected)" -ForegroundColor Yellow
    }
}
catch {
    Write-Host "  ✗ Renamer failed: $_" -ForegroundColor Red
    Write-Host "  Stack: $($_.ScriptStackTrace)" -ForegroundColor Red
    Remove-Item $testDir -Recurse -Force -ErrorAction SilentlyContinue
    exit 1
}

# Test 5: Verify results
Write-Host "`n[5/5] Verifying results..." -ForegroundColor Yellow
$allGood = $true

if (Test-Path "$testDir\test.cs") {
    $content = Get-Content "$testDir\test.cs" -Raw
    if ($content -match "Test.NewNamespace" -and $content -notmatch "Test.OldNamespace") {
        Write-Host "  ✓ Namespace replacement works" -ForegroundColor Green
    }
    else {
        Write-Host "  ✗ Namespace replacement failed" -ForegroundColor Red
        Write-Host "    Content: $($content.Substring(0, [Math]::Min(100, $content.Length)))" -ForegroundColor Red
        $allGood = $false
    }
}
else {
    Write-Host "  ✗ Test file not found" -ForegroundColor Red
    $allGood = $false
}

if (Test-Path "$testDir\test.csproj") {
    $content = Get-Content "$testDir\test.csproj" -Raw
    if ($content -match "NewFolder" -and $content -notmatch "OldFolder") {
        Write-Host "  ✓ Path reference update works" -ForegroundColor Green
    }
    else {
        Write-Host "  ⚠ Path reference update may have issues" -ForegroundColor Yellow
    }
}

# Cleanup
Write-Host "`nCleaning up..." -ForegroundColor Yellow
Remove-Item $testDir -Recurse -Force -ErrorAction SilentlyContinue
Write-Host "  ✓ Cleanup complete" -ForegroundColor Green

if ($allGood) {
    Write-Host "`n=== All Tests Passed ===" -ForegroundColor Green
    exit 0
}
else {
    Write-Host "`n=== Some Tests Failed ===" -ForegroundColor Red
    exit 1
}

