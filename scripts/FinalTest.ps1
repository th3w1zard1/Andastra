$ErrorActionPreference = 'Continue'
$testDir = "test_final_$(Get-Random)"
$resultFile = "test_result.txt"

# Cleanup
if (Test-Path $testDir) { Remove-Item $testDir -Recurse -Force }
if (Test-Path $resultFile) { Remove-Item $resultFile -Force }

# Create test
New-Item -ItemType Directory -Path $testDir | Out-Null
"namespace Test.Old { }" | Out-File -FilePath "$testDir\test.cs"

$before = Get-Content "$testDir\test.cs" -Raw
"BEFORE: $before" | Out-File -FilePath $resultFile

# Run script
$start = Get-Date
.\scripts\EngineNamespaceRenamer.ps1 -RootPath $testDir -OldFolderName "Old" -NewFolderName "New" -OldNamespace "Test.Old" -NewNamespace "Test.New" -NoValidation -NoFolderRename -Timeout 30 2>&1 | Out-File -FilePath $resultFile -Append
$elapsed = ((Get-Date) - $start).TotalSeconds

"`nElapsed: $elapsed seconds" | Out-File -FilePath $resultFile -Append

# Check result
if (Test-Path "$testDir\test.cs") {
    $after = Get-Content "$testDir\test.cs" -Raw
    "AFTER: $after" | Out-File -FilePath $resultFile -Append

    if ($after -match "Test.New" -and $after -notmatch "Test.Old") {
        "RESULT: SUCCESS" | Out-File -FilePath $resultFile -Append
    } else {
        "RESULT: FAILED - Content incorrect" | Out-File -FilePath $resultFile -Append
    }
} else {
    "RESULT: FAILED - File missing" | Out-File -FilePath $resultFile -Append
}

# Show results
Get-Content $resultFile
Remove-Item $testDir -Recurse -Force -ErrorAction SilentlyContinue
Remove-Item $resultFile -Force -ErrorAction SilentlyContinue

