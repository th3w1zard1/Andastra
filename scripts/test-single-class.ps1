#!/usr/bin/env pwsh
# Test runner for individual test classes

param(
    [string]$TestClass = "ConfigVersionTests"
)

$testProject = "src/Tests/HolocronToolset.Tests/HolocronToolset.NET.Tests.csproj"
$outputFile = "test-results-$TestClass.txt"

Write-Host "Testing: $TestClass" -ForegroundColor Cyan
Write-Host "Output will be saved to: $outputFile" -ForegroundColor Yellow

try {
    dotnet test $testProject --filter "FullyQualifiedName~$TestClass" --logger "console;verbosity=detailed" > $outputFile 2>&1

    if ($LASTEXITCODE -eq 0) {
        Write-Host "`n✓ Tests PASSED" -ForegroundColor Green
    } else {
        Write-Host "`n✗ Tests FAILED" -ForegroundColor Red
    }

    Write-Host "`nResults saved to: $outputFile" -ForegroundColor Yellow
    Write-Host "`nShowing last 50 lines:" -ForegroundColor Cyan
    Get-Content $outputFile | Select-Object -Last 50
} catch {
    Write-Host "Error running tests: $_" -ForegroundColor Red
}

