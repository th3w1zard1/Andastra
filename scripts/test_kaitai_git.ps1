# Test script for Kaitai Struct GIT format compilation across multiple languages
# Tests compilation to at least 12 languages and verifies the compiler works

param(
    [switch]$Verbose,
    [string]$KscPath = "kaitai-struct-compiler"
)

$ErrorActionPreference = "Stop"

# Languages to test (at least 12 as requested)
$Languages = @(
    "python",
    "java",
    "javascript",
    "csharp",
    "cpp_stl",
    "ruby",
    "php",
    "perl",
    "go",
    "lua",
    "nim",
    "rust",
    "swift",
    "typescript"
)

$GitKsyPath = "src\Andastra\Parsing\Resource\Formats\GFF\Generics\GIT\GIT.ksy"
$TestOutputDir = "test_output_kaitai_git"
$TestResults = @()

Write-Host "Testing Kaitai Struct GIT format compilation" -ForegroundColor Cyan
Write-Host "KSY file: $GitKsyPath" -ForegroundColor Gray
Write-Host "Testing $($Languages.Count) languages" -ForegroundColor Gray
Write-Host ""

# Verify KSY file exists
if (-not (Test-Path $GitKsyPath)) {
    Write-Host "ERROR: KSY file not found: $GitKsyPath" -ForegroundColor Red
    exit 1
}

# Verify compiler is available
try {
    $null = & $KscPath --version 2>&1
    if ($LASTEXITCODE -ne 0) {
        throw "Compiler not found or not working"
    }
} catch {
    Write-Host "ERROR: Kaitai Struct compiler not found. Please install it:" -ForegroundColor Red
    Write-Host "  Windows: Download from https://kaitai.io/#download" -ForegroundColor Yellow
    Write-Host "  Linux: wget https://packages.kaitai.io/dists/unstable/main/binary-amd64/kaitai-struct-compiler_0.10_all.deb" -ForegroundColor Yellow
    Write-Host "  Or use: kaitai-struct-compiler --version" -ForegroundColor Yellow
    exit 1
}

# Clean and create output directory
if (Test-Path $TestOutputDir) {
    Remove-Item -Recurse -Force $TestOutputDir
}
New-Item -ItemType Directory -Path $TestOutputDir | Out-Null

$SuccessCount = 0
$FailureCount = 0

foreach ($lang in $Languages) {
    Write-Host "Testing $lang..." -NoNewline -ForegroundColor Yellow

    $langOutputDir = Join-Path $TestOutputDir $lang
    New-Item -ItemType Directory -Path $langOutputDir -Force | Out-Null

    try {
        $compileOutput = & $KscPath -t $lang -d $langOutputDir $GitKsyPath 2>&1

        if ($LASTEXITCODE -eq 0) {
            Write-Host " PASS" -ForegroundColor Green
            $SuccessCount++
            $TestResults += @{
                Language = $lang
                Status = "PASS"
                Output = $compileOutput
            }

            if ($Verbose) {
                Write-Host "  Output directory: $langOutputDir" -ForegroundColor Gray
                if ($compileOutput) {
                    Write-Host "  Compiler output:" -ForegroundColor Gray
                    $compileOutput | ForEach-Object { Write-Host "    $_" -ForegroundColor Gray }
                }
            }
        } else {
            Write-Host " FAIL" -ForegroundColor Red
            $FailureCount++
            $TestResults += @{
                Language = $lang
                Status = "FAIL"
                Error = $compileOutput
            }

            if ($Verbose) {
                Write-Host "  Error output:" -ForegroundColor Red
                $compileOutput | ForEach-Object { Write-Host "    $_" -ForegroundColor Red }
            }
        }
    } catch {
        Write-Host " ERROR" -ForegroundColor Red
        $FailureCount++
        $TestResults += @{
            Language = $lang
            Status = "ERROR"
            Error = $_.Exception.Message
        }

        if ($Verbose) {
            Write-Host "  Exception: $($_.Exception.Message)" -ForegroundColor Red
        }
    }
}

Write-Host ""
Write-Host "=" * 60 -ForegroundColor Cyan
Write-Host "Test Summary" -ForegroundColor Cyan
Write-Host "=" * 60 -ForegroundColor Cyan
Write-Host "Total languages tested: $($Languages.Count)" -ForegroundColor White
Write-Host "Successful: $SuccessCount" -ForegroundColor Green
Write-Host "Failed: $FailureCount" -ForegroundColor $(if ($FailureCount -eq 0) { "Green" } else { "Red" })
Write-Host ""

if ($FailureCount -gt 0) {
    Write-Host "Failed languages:" -ForegroundColor Red
    $TestResults | Where-Object { $_.Status -ne "PASS" } | ForEach-Object {
        Write-Host "  - $($_.Language): $($_.Status)" -ForegroundColor Red
        if ($_.Error) {
            Write-Host "    Error: $($_.Error -join '; ')" -ForegroundColor Yellow
        }
    }
    Write-Host ""
}

# Generate detailed report
$ReportPath = Join-Path $TestOutputDir "test_report.txt"
$ReportContent = @"
Kaitai Struct GIT Format Compilation Test Report
Generated: $(Get-Date -Format "yyyy-MM-dd HH:mm:ss")

Test Configuration:
- KSY File: $GitKsyPath
- Languages Tested: $($Languages.Count)
- Compiler: $KscPath

Results:
"@

foreach ($result in $TestResults) {
    $ReportContent += "`n$($result.Language): $($result.Status)"
    if ($result.Error) {
        $ReportContent += "`n  Error: $($result.Error -join '`n  ')"
    }
}

$ReportContent | Out-File -FilePath $ReportPath -Encoding UTF8
Write-Host "Detailed report saved to: $ReportPath" -ForegroundColor Gray

if ($FailureCount -eq 0) {
    Write-Host "All tests passed!" -ForegroundColor Green
    exit 0
} else {
    Write-Host "Some tests failed. See report for details." -ForegroundColor Red
    exit 1
}

