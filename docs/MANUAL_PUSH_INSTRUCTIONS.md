# Manual NuGet Package Push Instructions

Since automated push isn't showing output, here are the exact commands to run manually:

## Step 1: Verify Package Exists

```powershell
Get-ChildItem "src/CSharpKOTOR/bin/Release" -Filter "*.nupkg"
```

## Step 2: Check Package Metadata Version

```powershell
$pkg = "src/CSharpKOTOR/bin/Release/CSharpKOTOR.2.0.0-alpha1.nupkg"
Add-Type -AssemblyName System.IO.Compression.FileSystem
$zip = [System.IO.Compression.ZipFile]::OpenRead($pkg)
$nuspec = $zip.Entries | Where-Object { $_.FullName -like "*.nuspec" } | Select-Object -First 1
$stream = $nuspec.Open()
$reader = New-Object System.IO.StreamReader($stream)
$xml = $reader.ReadToEnd()
$reader.Close()
$stream.Close()
$zip.Dispose()
if ($xml -match '<version>([^<]+)</version>') { Write-Host "Package version: $($matches[1])" }
```

## Step 3: Push Package

```powershell
$apiKey = "YOUR_NUGET_API_KEY_HERE"
$pkg = "src/CSharpKOTOR/bin/Release/CSharpKOTOR.2.0.0-alpha1.nupkg"
dotnet nuget push $pkg --api-key $apiKey --source https://api.nuget.org/v3/index.json --skip-duplicate
```

## Step 4: Verify on NuGet.org

After pushing, check: <https://www.nuget.org/packages/CSharpKOTOR/>

**Note:** The package filename shows `alpha1` but the actual version in the package metadata (from .nuspec) is what NuGet uses. If the metadata shows `2.0.0-alpha2`, that's the version that will be published.

## If Push Fails

Common errors:

- **403 Forbidden**: API key is invalid or expired
- **409 Conflict**: Package version already exists
- **400 Bad Request**: Package metadata is invalid

If you get a 409, the package already exists - check NuGet.org to see what version is published.
