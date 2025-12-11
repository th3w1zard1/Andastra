$ErrorActionPreference = "Continue"
$output = @()

$output += "=== Final Package Push ==="
$output += ""

# Get API key
$envContent = Get-Content ".env" -Raw
if ($envContent -match 'NUGET_API_KEY\s*=\s*"([^"]+)"') {
    $apiKey = $matches[1]
    $output += "API key loaded (length: $($apiKey.Length))"
} else {
    $output += "ERROR: No API key found"
    $output | Out-File "push-log.txt"
    exit 1
}

# Find package
$pkg = Get-ChildItem "src/CSharpKOTOR/bin/Release" -Filter "*.nupkg" | Sort-Object LastWriteTime -Descending | Select-Object -First 1
if (-not $pkg) {
    $output += "ERROR: No package found"
    $output | Out-File "push-log.txt"
    exit 1
}

$output += "Package: $($pkg.Name)"
$output += "Size: $([math]::Round($pkg.Length/1KB, 2)) KB"

# Check metadata
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

$output += "Package ID: $packageId"
$output += "Package Version: $packageVersion"

# Push
$output += ""
$output += "Pushing to NuGet.org..."
$pushResult = dotnet nuget push $pkg.FullName --api-key $apiKey --source https://api.nuget.org/v3/index.json --skip-duplicate 2>&1
$output += $pushResult
$output += ""
$output += "Exit code: $LASTEXITCODE"

if ($LASTEXITCODE -eq 0) {
    $output += "SUCCESS: Package pushed!"
    $output += "Check: https://www.nuget.org/packages/$packageId/"
} else {
    $output += "FAILED: Push failed"
}

$output | Out-File "push-log.txt" -Encoding UTF8
Write-Host ($output -join "`n")

