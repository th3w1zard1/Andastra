# Test that verifies originals aren't modified if staging copy fails
$ErrorActionPreference = 'Continue'

Write-Host "=== TEST: Copy Failure Protection ===" -ForegroundColor Cyan

# Create test with read-only file to simulate copy failure
$testDir = "test_copy_failure"
if (Test-Path $testDir) { Remove-Item $testDir -Recurse -Force }
New-Item -ItemType Directory -Path $testDir | Out-Null

# Create original file
"namespace Test.Old { }" | Out-File -FilePath "$testDir\test.cs" -Encoding utf8
$originalContent = Get-Content "$testDir\test.cs" -Raw

Write-Host "`nOriginal content:" -ForegroundColor Yellow
Write-Host $originalContent

# Create a read-only staging directory to simulate copy failure
$stagingPath = "$testDir\.staging"
New-Item -ItemType Directory -Path $stagingPath | Out-Null
# Make staging directory read-only to cause copy issues
$acl = Get-Acl $stagingPath
$acl.SetAccessRuleProtection($true, $false)
Set-Acl -Path $stagingPath -AclObject $acl

Write-Host "`nAttempting to run script (should fail safely)..." -ForegroundColor Yellow

try {
    .\scripts\EngineNamespaceRenamer.ps1 `
        -RootPath $testDir `
        -OldFolderName "Old" `
        -NewFolderName "New" `
        -OldNamespace "Test.Old" `
        -NewNamespace "Test.New" `
        -NoValidation `
        -NoFolderRename `
        -Timeout 30 2>&1 | Out-String | Write-Host
    
    Write-Host "`nScript completed (may have failed)" -ForegroundColor Yellow
}
catch {
    Write-Host "`nScript threw error: $_" -ForegroundColor Yellow
}

# Verify original file is unchanged
Write-Host "`nVerifying original file..." -ForegroundColor Yellow
$afterContent = Get-Content "$testDir\test.cs" -Raw

if ($originalContent -eq $afterContent) {
    Write-Host "✓ Original file unchanged (SAFE)" -ForegroundColor Green
} else {
    Write-Host "✗ Original file was modified (UNSAFE!)" -ForegroundColor Red
    Write-Host "  Before: $originalContent"
    Write-Host "  After:  $afterContent"
    exit 1
}

# Cleanup
Remove-Item $testDir -Recurse -Force -ErrorAction SilentlyContinue
Write-Host "`n✓ Test passed: Originals protected on copy failure" -ForegroundColor Green

