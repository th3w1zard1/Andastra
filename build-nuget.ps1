# Build NuGet packages for TSLPatcher.Core and HoloPatcher
# Usage: .\build-nuget.ps1 [--publish] [--source <feed-url>] [--api-key <key>]

param(
    [switch]$Publish,
    [string]$Source = "https://api.nuget.org/v3/index.json",
    [string]$ApiKey = "",
    [string]$Configuration = "Release"
)

$ErrorActionPreference = "Stop"

Write-Host "Building NuGet packages..." -ForegroundColor Green

# Build TSLPatcher.Core package
Write-Host "`nBuilding TSLPatcher.Core..." -ForegroundColor Cyan
dotnet pack src/TSLPatcher.Core/TSLPatcher.Core.csproj --configuration $Configuration --no-build

if ($LASTEXITCODE -ne 0) {
    Write-Host "Failed to build TSLPatcher.Core package" -ForegroundColor Red
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
$tslCorePackage = Get-ChildItem -Path "src/TSLPatcher.Core/bin/$Configuration" -Filter "*.nupkg" | Select-Object -First 1
$holoPatcherPackage = Get-ChildItem -Path "src/HoloPatcher/bin/$Configuration" -Filter "*.nupkg" | Select-Object -First 1

if ($tslCorePackage) {
    Write-Host "`nTSLPatcher.Core package created: $($tslCorePackage.FullName)" -ForegroundColor Green
} else {
    Write-Host "`nTSLPatcher.Core package not found!" -ForegroundColor Red
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
        Write-Host "`nError: --api-key is required when using --publish" -ForegroundColor Red
        exit 1
    }

    Write-Host "`nPublishing packages to $Source..." -ForegroundColor Yellow

    # Publish TSLPatcher.Core
    Write-Host "Publishing TSLPatcher.Core..." -ForegroundColor Cyan
    dotnet nuget push $tslCorePackage.FullName --api-key $ApiKey --source $Source --skip-duplicate

    if ($LASTEXITCODE -ne 0) {
        Write-Host "Failed to publish TSLPatcher.Core" -ForegroundColor Red
        exit 1
    }

    # Publish HoloPatcher
    Write-Host "Publishing HoloPatcher..." -ForegroundColor Cyan
    dotnet nuget push $holoPatcherPackage.FullName --api-key $ApiKey --source $Source --skip-duplicate

    if ($LASTEXITCODE -ne 0) {
        Write-Host "Failed to publish HoloPatcher" -ForegroundColor Red
        exit 1
    }

    # Publish symbol packages if they exist
    $tslCoreSymbols = Get-ChildItem -Path "src/TSLPatcher.Core/bin/$Configuration" -Filter "*.snupkg" | Select-Object -First 1
    $holoPatcherSymbols = Get-ChildItem -Path "src/HoloPatcher/bin/$Configuration" -Filter "*.snupkg" | Select-Object -First 1

    if ($tslCoreSymbols) {
        Write-Host "Publishing TSLPatcher.Core symbols..." -ForegroundColor Cyan
        dotnet nuget push $tslCoreSymbols.FullName --api-key $ApiKey --source $Source --skip-duplicate
    }

    if ($holoPatcherSymbols) {
        Write-Host "Publishing HoloPatcher symbols..." -ForegroundColor Cyan
        dotnet nuget push $holoPatcherSymbols.FullName --api-key $ApiKey --source $Source --skip-duplicate
    }

    Write-Host "`nPackages published successfully!" -ForegroundColor Green
} else {
    Write-Host "`nPackages built successfully!" -ForegroundColor Green
    Write-Host "To publish, run: .\build-nuget.ps1 --publish --api-key YOUR_API_KEY" -ForegroundColor Yellow
}

