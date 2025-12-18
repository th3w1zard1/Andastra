# Run HolocronToolset tests and save output
$ErrorActionPreference = "Continue"
$testProject = "src/Tests/HolocronToolset.Tests/HolocronToolset.NET.Tests.csproj"
$outputFile = "test-output.txt"

Write-Host "Running HolocronToolset tests..."
dotnet test $testProject --verbosity normal 2>&1 | Tee-Object -FilePath $outputFile

Write-Host "`nTest output saved to: $outputFile"
Write-Host "`nTest Summary:"
Get-Content $outputFile | Select-String -Pattern "(Passed|Failed|Skipped)!" -Context 0,2

