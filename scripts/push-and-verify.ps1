$ErrorActionPreference = "Stop"
$VerbosePreference = "Continue"

Write-Host "=== NuGet Package Push and Verification ===" -ForegroundColor Cyan
Write-Host ""

# Step 1: Get API key
Write-Host "Step 1: Loading API key..." -ForegroundColor Yellow
$envContent = Get-Content ".env" -Raw
if ($envContent -match 'NUGET_API_KEY\s*=\s*"([^"]+)"') {
    $apiKey = $matches[1]
    Write-Host "  API key loaded (length: $($apiKey.Length))" -ForegroundColor Green
} else {
    Write-Host "  ERROR: Could not find NUGET_API_KEY in .env" -ForegroundColor Red
    exit 1
}

# Step 2: Check package exists
Write-Host "`nStep 2: Checking package..." -ForegroundColor Yellow
$pkg = Get-ChildItem "src/CSharpKOTOR/bin/Release" -Filter "*.nupkg" | Sort-Object LastWriteTime -Descending | Select-Object -First 1
if (-not $pkg) {
    Write-Host "  ERROR: No package found!" -ForegroundColor Red
    exit 1
}
Write-Host "  Found: $($pkg.Name)" -ForegroundColor Green
Write-Host "  Size: $([math]::Round($pkg.Length/1KB, 2)) KB" -ForegroundColor Gray
Write-Host "  Path: $($pkg.FullName)" -ForegroundColor Gray

# Step 3: Check package metadata
Write-Host "`nStep 3: Checking package metadata..." -ForegroundColor Yellow
Add-Type -AssemblyName System.IO.Compression.FileSystem
$zip = [System.IO.Compression.ZipFile]::OpenRead($pkg.FullName)
$nuspec = $zip.Entries | Where-Object { $_.FullName -like "*.nuspec" } | Select-Object -First 1
$stream = $nuspec.Open()
$reader = New-Object System.IO.StreamReader($stream)
$xml = $reader.ReadToEnd()
$reader.Close()
$stream.Close()
$zip.Dispose()

$packageId = if ($xml -match '<id>([^<]+)</id>') { $matches[1] } else { "UNKNOWN" }
$packageVersion = if ($xml -match '<version>([^<]+)</version>') { $matches[1] } else { "UNKNOWN" }

Write-Host "  Package ID: $packageId" -ForegroundColor Cyan
Write-Host "  Package Version: $packageVersion" -ForegroundColor $(if ($packageVersion -eq "2.0.0-alpha2") { "Green" } else { "Yellow" })

if ($packageVersion -ne "2.0.0-alpha2") {
    Write-Host "  WARNING: Version mismatch! Expected 2.0.0-alpha2, got $packageVersion" -ForegroundColor Yellow
    Write-Host "  The package will be pushed with version $packageVersion" -ForegroundColor Yellow
}

# Step 4: Push package
Write-Host "`nStep 4: Pushing package to NuGet.org..." -ForegroundColor Yellow
Write-Host "  Source: https://api.nuget.org/v3/index.json" -ForegroundColor Gray
Write-Host "  Package: $($pkg.Name)" -ForegroundColor Gray

$pushOutput = dotnet nuget push $pkg.FullName --api-key $apiKey --source https://api.nuget.org/v3/index.json --skip-duplicate 2>&1
$exitCode = $LASTEXITCODE

Write-Host "`nPush Output:" -ForegroundColor Cyan
Write-Host $pushOutput

if ($exitCode -eq 0) {
    Write-Host "`n✓ SUCCESS: Package pushed successfully!" -ForegroundColor Green
    Write-Host "`nPackage should be available at:" -ForegroundColor Cyan
    Write-Host "  https://www.nuget.org/packages/$packageId/" -ForegroundColor White
    Write-Host "`nNote: It may take a few minutes for the package to appear on the website." -ForegroundColor Yellow
} else {
    Write-Host "`n✗ FAILED: Push failed with exit code $exitCode" -ForegroundColor Red
    Write-Host "`nCommon issues:" -ForegroundColor Yellow
    Write-Host "  - Invalid API key" -ForegroundColor Gray
    Write-Host "  - Package already exists with this version" -ForegroundColor Gray
    Write-Host "  - Package metadata is invalid" -ForegroundColor Gray
    Write-Host "  - Network connectivity issues" -ForegroundColor Gray
    exit 1
}

