#!/bin/bash
# Build script for HoloPatcher.NET

echo "Building HoloPatcher.NET..."

# Restore dependencies
dotnet restore HoloPatcher.sln

# Build the solution
dotnet build HoloPatcher.sln --configuration Release

# Run tests
echo "Running tests..."
dotnet test HoloPatcher.sln --configuration Release --no-build

echo "Build complete!"

