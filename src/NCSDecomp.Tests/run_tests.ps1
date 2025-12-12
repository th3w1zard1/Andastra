# Test runner script
Write-Host "Building test project..." -ForegroundColor Cyan
dotnet build src\KNCSDecomp.Tests\KNCSDecomp.Tests.csproj

if ($LASTEXITCODE -ne 0) {
    Write-Host "Build failed!" -ForegroundColor Red
    exit 1
}

Write-Host "`nRunning tests..." -ForegroundColor Cyan
$output = dotnet test src\KNCSDecomp.Tests\KNCSDecomp.Tests.csproj --verbosity normal --logger "console;verbosity=normal" 2>&1

Write-Host $output

$exitCode = $LASTEXITCODE
if ($exitCode -eq 0) {
    Write-Host "`nAll tests passed!" -ForegroundColor Green
} else {
    Write-Host "`nSome tests failed!" -ForegroundColor Red
}

exit $exitCode

