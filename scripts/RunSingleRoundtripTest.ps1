# Run a single NCS roundtrip test by filename pattern
# Usage: .\scripts\RunSingleRoundtripTest.ps1 -Pattern "k_act_com41"
# Usage: .\scripts\RunSingleRoundtripTest.ps1 -Pattern "k_act_com41" -Verbose

param(
    [Parameter(Mandatory=$true)]
    [string]$Pattern,
    
    [switch]$ShowDetails
)

$ErrorActionPreference = "Continue"

# Change to repository root - find .git or .sln file
$scriptDir = if ($PSScriptRoot) { $PSScriptRoot } else { Split-Path -Parent $MyInvocation.MyCommand.Path }
$repoRoot = $scriptDir
while ($repoRoot -and -not (Test-Path (Join-Path $repoRoot ".git")) -and -not (Get-ChildItem $repoRoot -Filter "*.sln" -ErrorAction SilentlyContinue)) {
    $repoRoot = Split-Path -Parent $repoRoot
    if (-not $repoRoot -or $repoRoot -eq (Split-Path -Parent $repoRoot)) {
        # Reached root, use script directory's parent
        $repoRoot = Split-Path -Parent $scriptDir
        break
    }
}
if (-not $repoRoot) { $repoRoot = Split-Path -Parent $scriptDir }
Push-Location $repoRoot
Write-Host "Repository root: $repoRoot" -ForegroundColor Gray

try {
    Write-Host "Running NCS roundtrip tests matching pattern: $Pattern" -ForegroundColor Cyan
    Write-Host ""
    
    # Build the test project first
    Write-Host "Building test project..." -ForegroundColor Yellow
    $buildResult = dotnet build src/Andastra/Tests/TSLPatcher.Tests.csproj --verbosity quiet 2>&1
    if ($LASTEXITCODE -ne 0) {
        Write-Host "Build failed!" -ForegroundColor Red
        $buildResult | Write-Host
        exit 1
    }
    
    # Run the test with filtering
    Write-Host "Running tests..." -ForegroundColor Yellow
    Write-Host ""
    
    $testOutput = dotnet test src/Andastra/Tests/TSLPatcher.Tests.csproj `
        --filter "FullyQualifiedName~NCSRoundtripTests" `
        --verbosity normal 2>&1
    
    # Filter output for the specific pattern
    $filtered = $testOutput | Select-String -Pattern $Pattern -Context 15,10
    
    if ($filtered) {
        Write-Host "=== Test Results for '$Pattern' ===" -ForegroundColor Cyan
        Write-Host ""
        $filtered | ForEach-Object {
            Write-Host $_.Line
            if ($_.Context.PreContext) {
                $_.Context.PreContext | ForEach-Object { Write-Host $_ -ForegroundColor Gray }
            }
            if ($_.Context.PostContext) {
                $_.Context.PostContext | ForEach-Object { Write-Host $_ -ForegroundColor Gray }
            }
            Write-Host ""
        }
    } else {
        Write-Host "No output found matching pattern '$Pattern'" -ForegroundColor Yellow
        Write-Host ""
        Write-Host "Full test output (last 50 lines):" -ForegroundColor Cyan
        $testOutput | Select-Object -Last 50
    }
    
    # Show summary
    $summary = $testOutput | Select-String -Pattern "Passed|Failed|Total tests"
    if ($summary) {
        Write-Host ""
        Write-Host "=== Test Summary ===" -ForegroundColor Cyan
        $summary | ForEach-Object { Write-Host $_.Line }
    }
    
} finally {
    Pop-Location
}

