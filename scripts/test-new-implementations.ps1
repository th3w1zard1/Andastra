#!/usr/bin/env pwsh
# Test runner for newly implemented tests

$testProject = "src/Tests/HolocronToolset.Tests/HolocronToolset.NET.Tests.csproj"
$outputFile = "new-tests-results.txt"

Write-Host "Building test project..." -ForegroundColor Cyan
dotnet build $testProject -c Debug > "$outputFile" 2>&1

if ($LASTEXITCODE -ne 0) {
    Write-Host "Build failed!" -ForegroundColor Red
    Get-Content $outputFile | Select-Object -Last 50
    exit 1
}

Write-Host "`nRunning DesignerControls tests..." -ForegroundColor Cyan
dotnet test $testProject --filter "FullyQualifiedName~DesignerControlsTests" --logger "console;verbosity=normal" >> "$outputFile" 2>&1

Write-Host "`nRunning EditorHelpDialog tests..." -ForegroundColor Cyan
dotnet test $testProject --filter "FullyQualifiedName~EditorHelpDialogTests" --logger "console;verbosity=normal" >> "$outputFile" 2>&1

Write-Host "`nTest results saved to: $outputFile" -ForegroundColor Yellow
Write-Host "`nShowing summary:" -ForegroundColor Cyan
Get-Content $outputFile | Select-String -Pattern "(Passed|Failed|Skipped)!" -Context 0,2

if ($LASTEXITCODE -eq 0) {
    Write-Host "`n✓ Tests PASSED" -ForegroundColor Green
} else {
    Write-Host "`n✗ Tests FAILED" -ForegroundColor Red
}

exit $LASTEXITCODE

