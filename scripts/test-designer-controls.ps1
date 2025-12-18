#!/usr/bin/env pwsh
# Test runner for DesignerControlsTests

$testProject = "src/Tests/HolocronToolset.Tests/HolocronToolset.NET.Tests.csproj"
$outputFile = "designer-controls-test-results.txt"

Write-Host "Building test project..." -ForegroundColor Cyan
dotnet build $testProject -c Debug > "$outputFile" 2>&1

if ($LASTEXITCODE -ne 0) {
    Write-Host "Build failed!" -ForegroundColor Red
    Get-Content $outputFile
    exit 1
}

Write-Host "Running DesignerControls tests..." -ForegroundColor Cyan
dotnet test $testProject --filter "FullyQualifiedName~DesignerControlsTests" --logger "console;verbosity=detailed" >> "$outputFile" 2>&1

Write-Host "`nTest results saved to: $outputFile" -ForegroundColor Yellow
Write-Host "`nShowing results:" -ForegroundColor Cyan
Get-Content $outputFile

if ($LASTEXITCODE -eq 0) {
    Write-Host "`n✓ Tests PASSED" -ForegroundColor Green
} else {
    Write-Host "`n✗ Tests FAILED" -ForegroundColor Red
}

exit $LASTEXITCODE

