# Quick test of EngineNamespaceRenamer.ps1
$ErrorActionPreference = 'Continue'

Write-Host "=== Testing EngineNamespaceRenamer.ps1 ===" -ForegroundColor Cyan

# Test 1: List backups
Write-Host "`nTest 1: List backups" -ForegroundColor Yellow
try {
    .\scripts\EngineNamespaceRenamer.ps1 -ListBackups 2>&1 | Out-String | Write-Host
    Write-Host "✓ List backups works" -ForegroundColor Green
}
catch {
    Write-Host "✗ List backups failed: $_" -ForegroundColor Red
}

# Test 2: Create test files
Write-Host "`nTest 2: Creating test files" -ForegroundColor Yellow
$testDir = "test_renamer_$(Get-Random)"
New-Item -ItemType Directory -Path $testDir -Force | Out-Null
"namespace Test.OldNamespace { class Test { } }" | Out-File -FilePath "$testDir\test.cs" -Encoding utf8
"Project reference: src\OldFolder\project.csproj" | Out-File -FilePath "$testDir\test.csproj" -Encoding utf8
Write-Host "Created test directory: $testDir" -ForegroundColor Green

# Test 3: Run renamer (no validation, no folder rename)
Write-Host "`nTest 3: Running renamer (namespace only)" -ForegroundColor Yellow
try {
    $startTime = Get-Date
    .\scripts\EngineNamespaceRenamer.ps1 -RootPath $testDir -OldFolderName "OldFolder" -NewFolderName "NewFolder" -OldNamespace "Test.OldNamespace" -NewNamespace "Test.NewNamespace" -NoValidation -NoFolderRename -Timeout 30 2>&1 | Out-String | Write-Host
    $duration = ((Get-Date) - $startTime).TotalSeconds
    Write-Host "✓ Renamer completed in $($duration.ToString('F2'))s" -ForegroundColor Green
}
catch {
    Write-Host "✗ Renamer failed: $_" -ForegroundColor Red
}

# Test 4: Verify results
Write-Host "`nTest 4: Verifying results" -ForegroundColor Yellow
if (Test-Path "$testDir\test.cs") {
    $content = Get-Content "$testDir\test.cs" -Raw
    if ($content -match "Test.NewNamespace") {
        Write-Host "✓ Namespace replacement works" -ForegroundColor Green
    }
    else {
        Write-Host "✗ Namespace replacement failed" -ForegroundColor Red
        Write-Host "Content: $content"
    }
}
else {
    Write-Host "✗ Test file not found" -ForegroundColor Red
}

# Cleanup
Write-Host "`nCleaning up..." -ForegroundColor Yellow
Remove-Item $testDir -Recurse -Force -ErrorAction SilentlyContinue
Write-Host "✓ Cleanup complete" -ForegroundColor Green

Write-Host "`n=== Test Complete ===" -ForegroundColor Cyan

