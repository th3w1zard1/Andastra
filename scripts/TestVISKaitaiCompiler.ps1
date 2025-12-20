# Test script to verify VIS.ksy compiles to multiple languages
# This validates that the Kaitai Struct compiler works with VIS.ksy

param(
    [string[]]$Languages = @("python", "java", "javascript", "csharp", "cpp_stl", "go", "ruby", "php", "rust", "swift", "lua", "nim", "perl", "kotlin", "typescript"),
    [string]$OutputDir = "$PSScriptRoot\..\test_files\kaitai_compiled\vis_test"
)

$ErrorActionPreference = "Continue"

Write-Host "Testing VIS.ksy compilation to multiple languages..." -ForegroundColor Green
Write-Host ""

# Find Kaitai compiler
$jarPath = $env:KAITAI_COMPILER_JAR
if ([string]::IsNullOrEmpty($jarPath) -or -not (Test-Path $jarPath)) {
    $jarPath = "$env:USERPROFILE\.kaitai\kaitai-struct-compiler.jar"
}

if (-not (Test-Path $jarPath)) {
    Write-Host "Kaitai Struct compiler not found. Please run SetupKaitaiCompiler.ps1 first." -ForegroundColor Red
    exit 1
}

Write-Host "Using compiler: $jarPath" -ForegroundColor Cyan

# Find VIS.ksy
$visKsy = "$PSScriptRoot\..\src\Andastra\Parsing\Resource\Formats\VIS\VIS.ksy"
if (-not (Test-Path $visKsy)) {
    Write-Host "VIS.ksy not found at: $visKsy" -ForegroundColor Red
    exit 1
}

Write-Host "Compiling: $visKsy" -ForegroundColor Cyan
Write-Host ""

# Create output directory
New-Item -ItemType Directory -Path $OutputDir -Force | Out-Null

$results = @{}
$successCount = 0
$failCount = 0

foreach ($lang in $Languages) {
    Write-Host "Testing $lang..." -NoNewline

    $langOutputDir = Join-Path $OutputDir $lang
    New-Item -ItemType Directory -Path $langOutputDir -Force | Out-Null

    $process = Start-Process -FilePath "java" -ArgumentList "-jar", "`"$jarPath`"", "-t", $lang, "`"$visKsy`"", "-d", "`"$langOutputDir`"" -Wait -PassThru -NoNewWindow -RedirectStandardOutput "$langOutputDir\stdout.txt" -RedirectStandardError "$langOutputDir\stderr.txt"

    if ($process.ExitCode -eq 0) {
        $results[$lang] = "SUCCESS"
        $successCount++
        Write-Host " SUCCESS" -ForegroundColor Green
    } else {
        $error = Get-Content "$langOutputDir\stderr.txt" -ErrorAction SilentlyContinue | Select-Object -First 3
        $results[$lang] = "FAILED: $($error -join '; ')"
        $failCount++
        Write-Host " FAILED" -ForegroundColor Red
        if ($error) {
            Write-Host "  Error: $($error[0])" -ForegroundColor Yellow
        }
    }
}

Write-Host ""
Write-Host ("=" * 60) -ForegroundColor Cyan
Write-Host "Compilation Results:" -ForegroundColor Green
Write-Host "  Successful: $successCount" -ForegroundColor Green
$failColor = if ($failCount -gt 0) { "Red" } else { "Green" }
Write-Host "  Failed: $failCount" -ForegroundColor $failColor
Write-Host ""

if ($successCount -ge 12) {
    Write-Host "At least 12 languages compiled successfully!" -ForegroundColor Green
    exit 0
} else {
    Write-Host "Less than 12 languages compiled successfully." -ForegroundColor Red
    Write-Host ""
    Write-Host "Detailed results:" -ForegroundColor Yellow
    foreach ($lang in $Languages) {
        $status = $results[$lang]
        $color = if ($status -like "SUCCESS*") { "Green" } else { "Red" }
        Write-Host "  $lang : $status" -ForegroundColor $color
    }
    exit 1
}

