# Build script for HoloPatcher.NET (PowerShell)

Write-Host "Building HoloPatcher.NET..." -ForegroundColor Green

# Restore dependencies
dotnet restore HoloPatcher.sln

# Build the solution
dotnet build HoloPatcher.sln --configuration Release

# Run tests
Write-Host "Running tests..." -ForegroundColor Cyan
dotnet test HoloPatcher.sln --configuration Release --no-build

Write-Host "Build complete!" -ForegroundColor Green

