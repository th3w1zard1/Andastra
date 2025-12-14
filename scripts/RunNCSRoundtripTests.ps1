# Run NCS Roundtrip Tests and capture output
param(
    [switch]$Verbose
)

$ErrorActionPreference = "Continue"

Write-Host "Building test project..."
dotnet build src/TSLPatcher.Tests/TSLPatcher.Tests.csproj --no-incremental 2>&1 | Out-Null

if ($LASTEXITCODE -ne 0) {
    Write-Host "Build failed!"
    exit 1
}

Write-Host "Running NCS Roundtrip Tests..."
$outputFile = "test_output_$(Get-Date -Format 'yyyyMMdd_HHmmss').txt"

dotnet test src/TSLPatcher.Tests/TSLPatcher.Tests.csproj `
    --filter "FullyQualifiedName~NCSRoundtripTests" `
    --logger "console;verbosity=normal" `
    --no-build `
    2>&1 | Tee-Object -FilePath $outputFile

Write-Host "`nTest output saved to: $outputFile"

# Extract failures
$failures = Get-Content $outputFile | Select-String -Pattern "(FAIL|BYTECODE MISMATCH|MISMATCH)" | Select-Object -First 10
if ($failures) {
    Write-Host "`n=== FAILURES DETECTED ===" -ForegroundColor Red
    $failures | ForEach-Object { Write-Host $_ -ForegroundColor Red }
} else {
    Write-Host "`n=== NO FAILURES DETECTED ===" -ForegroundColor Green
}


