$ErrorActionPreference = 'Continue'
$testDir = "test_renamer_final"
$logFile = "test_output.log"

# Cleanup
if (Test-Path $testDir) { Remove-Item $testDir -Recurse -Force }
if (Test-Path $logFile) { Remove-Item $logFile -Force }

# Create test
New-Item -ItemType Directory -Path $testDir | Out-Null
"namespace Test.Old { }" | Out-File -FilePath "$testDir\test.cs"

Write-Host "=== TEST START ===" | Tee-Object -FilePath $logFile -Append
Write-Host "BEFORE:" | Tee-Object -FilePath $logFile -Append
Get-Content "$testDir\test.cs" | Tee-Object -FilePath $logFile -Append

Write-Host "`nRUNNING SCRIPT..." | Tee-Object -FilePath $logFile -Append
$start = Get-Date
.\scripts\EngineNamespaceRenamer.ps1 -RootPath $testDir -OldFolderName "Old" -NewFolderName "New" -OldNamespace "Test.Old" -NewNamespace "Test.New" -NoValidation -NoFolderRename -Timeout 30 2>&1 | Tee-Object -FilePath $logFile -Append
$elapsed = ((Get-Date) - $start).TotalSeconds

Write-Host "`nCompleted in $elapsed seconds" | Tee-Object -FilePath $logFile -Append
Write-Host "AFTER:" | Tee-Object -FilePath $logFile -Append
if (Test-Path "$testDir\test.cs") {
    Get-Content "$testDir\test.cs" | Tee-Object -FilePath $logFile -Append
} else {
    Write-Host "FILE MISSING!" | Tee-Object -FilePath $logFile -Append
}

Write-Host "`n=== TEST COMPLETE ===" | Tee-Object -FilePath $logFile -Append
Write-Host "Full log saved to: $logFile"

Remove-Item $testDir -Recurse -Force -ErrorAction SilentlyContinue

