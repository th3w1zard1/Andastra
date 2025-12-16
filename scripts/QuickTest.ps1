$testDir = "test_quick"
if (Test-Path $testDir) { Remove-Item $testDir -Recurse -Force }
New-Item -ItemType Directory -Path $testDir | Out-Null
"namespace Test.Old { }" | Out-File -FilePath "$testDir\test.cs"
Write-Host "BEFORE:"
Get-Content "$testDir\test.cs"
Write-Host "`nRUNNING SCRIPT..."
.\scripts\EngineNamespaceRenamer.ps1 -RootPath $testDir -OldFolderName "Old" -NewFolderName "New" -OldNamespace "Test.Old" -NewNamespace "Test.New" -NoValidation -NoFolderRename -Timeout 30
Write-Host "`nAFTER:"
Get-Content "$testDir\test.cs"
Remove-Item $testDir -Recurse -Force

