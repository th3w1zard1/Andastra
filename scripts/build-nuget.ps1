# Build NuGet packages for CSharpKOTOR and HoloPatcher
# Usage: .\build-nuget.ps1 [--publish] [--source <feed-url>] [--api-key <key>]
#
# API Key can be provided via:
# 1. --api-key parameter (highest priority)
# 2. NUGET_API_KEY environment variable
# 3. .env file in project root (NUGET_API_KEY=...)

param(
    [switch]$Publish,
    [string]$Source = "",
    [string]$ApiKey = "",
    [string]$Configuration = "Release"
)

$ErrorActionPreference = "Stop"

# Load .env file if it exists
if (Test-Path ".env") {
    Get-Content ".env" | ForEach-Object {
        if ($_ -match '^\s*([^#=]+)\s*=\s*(.+)\s*$') {
            $key = $matches[1].Trim()
            $value = $matches[2].Trim()
            [Environment]::SetEnvironmentVariable($key, $value, "Process")
        }
    }
}

# Get API key from parameter, environment variable, or .env file (in that order)
if ([string]::IsNullOrWhiteSpace($ApiKey)) {
    $ApiKey = $env:NUGET_API_KEY
}

# Get source from parameter, environment variable, or default
if ([string]::IsNullOrWhiteSpace($Source)) {
    $Source = $env:NUGET_SOURCE
    if ([string]::IsNullOrWhiteSpace($Source)) {
        $Source = "https://api.nuget.org/v3/index.json"
    }
}

Write-Host "Building NuGet packages..." -ForegroundColor Green

# Build CSharpKOTOR package
Write-Host "`nBuilding CSharpKOTOR..." -ForegroundColor Cyan
dotnet pack src/CSharpKOTOR/CSharpKOTOR.csproj --configuration $Configuration --no-build

if ($LASTEXITCODE -ne 0) {
    Write-Host "Failed to build CSharpKOTOR package" -ForegroundColor Red
    exit 1
}

# Build HoloPatcher package
Write-Host "`nBuilding HoloPatcher..." -ForegroundColor Cyan
dotnet pack src/HoloPatcher/HoloPatcher.csproj --configuration $Configuration --no-build

if ($LASTEXITCODE -ne 0) {
    Write-Host "Failed to build HoloPatcher package" -ForegroundColor Red
    exit 1
}

# Find package files
$tslCorePackage = Get-ChildItem -Path "src/CSharpKOTOR/bin/$Configuration" -Filter "*.nupkg" | Select-Object -First 1
$holoPatcherPackage = Get-ChildItem -Path "src/HoloPatcher/bin/$Configuration" -Filter "*.nupkg" | Select-Object -First 1

if ($tslCorePackage) {
    Write-Host "`nCSharpKOTOR package created: $($tslCorePackage.FullName)" -ForegroundColor Green
} else {
    Write-Host "`nCSharpKOTOR package not found!" -ForegroundColor Red
    exit 1
}

if ($holoPatcherPackage) {
    Write-Host "HoloPatcher package created: $($holoPatcherPackage.FullName)" -ForegroundColor Green
} else {
    Write-Host "HoloPatcher package not found!" -ForegroundColor Red
    exit 1
}

# Publish if requested
if ($Publish) {
    if ([string]::IsNullOrWhiteSpace($ApiKey)) {
        Write-Host "`nError: API key is required when using --publish" -ForegroundColor Red
        Write-Host "Provide it via:" -ForegroundColor Yellow
        Write-Host "  1. --api-key parameter" -ForegroundColor Yellow
        Write-Host "  2. NUGET_API_KEY environment variable" -ForegroundColor Yellow
        Write-Host "  3. .env file (NUGET_API_KEY=...)" -ForegroundColor Yellow
        Write-Host "`nExample: Create .env file with: NUGET_API_KEY=your_key_here" -ForegroundColor Cyan
        exit 1
    }

    Write-Host "`nPublishing packages to $Source..." -ForegroundColor Yellow

    # Build push command arguments
    $pushArgs = @("nuget", "push", "--source", $Source, "--skip-duplicate")
    if (-not [string]::IsNullOrWhiteSpace($ApiKey)) {
        $pushArgs += "--api-key", $ApiKey
    }

    # Publish CSharpKOTOR
    Write-Host "Publishing CSharpKOTOR..." -ForegroundColor Cyan
    & dotnet $pushArgs $tslCorePackage.FullName

    if ($LASTEXITCODE -ne 0) {
        Write-Host "Failed to publish CSharpKOTOR" -ForegroundColor Red
        exit 1
    }

    # Publish HoloPatcher
    Write-Host "Publishing HoloPatcher..." -ForegroundColor Cyan
    & dotnet $pushArgs $holoPatcherPackage.FullName

    if ($LASTEXITCODE -ne 0) {
        Write-Host "Failed to publish HoloPatcher" -ForegroundColor Red
        exit 1
    }

    # Publish symbol packages if they exist
    $tslCoreSymbols = Get-ChildItem -Path "src/CSharpKOTOR/bin/$Configuration" -Filter "*.snupkg" | Select-Object -First 1
    $holoPatcherSymbols = Get-ChildItem -Path "src/HoloPatcher/bin/$Configuration" -Filter "*.snupkg" | Select-Object -First 1

    if ($tslCoreSymbols) {
        Write-Host "Publishing CSharpKOTOR symbols..." -ForegroundColor Cyan
        & dotnet $pushArgs $tslCoreSymbols.FullName
    }

    if ($holoPatcherSymbols) {
        Write-Host "Publishing HoloPatcher symbols..." -ForegroundColor Cyan
        & dotnet $pushArgs $holoPatcherSymbols.FullName
    }

    Write-Host "`nPackages published successfully!" -ForegroundColor Green
} else {
    Write-Host "`nPackages built successfully!" -ForegroundColor Green
    Write-Host "To publish, you can:" -ForegroundColor Yellow
    Write-Host "  1. Run: .\build-nuget.ps1 --publish" -ForegroundColor Cyan
    Write-Host "     (if you've run .\setup-nuget-key.ps1 to configure credentials)" -ForegroundColor Gray
    Write-Host "  2. Run: .\build-nuget.ps1 --publish --api-key YOUR_API_KEY" -ForegroundColor Cyan
    Write-Host "  3. Set NUGET_API_KEY environment variable, then: .\build-nuget.ps1 --publish" -ForegroundColor Cyan
}

