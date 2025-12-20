# Comprehensive multi-language test for Kaitai Struct GIT format
# Tests compilation to at least 12 languages and validates the definitions

param(
    [switch]$Verbose,
    [switch]$Quick,  # Only test a few languages for quick validation
    [string]$KscPath = "kaitai-struct-compiler"
)

$ErrorActionPreference = "Stop"

# Comprehensive list of languages (at least 12 as requested, plus extras)
$AllLanguages = @(
    @{ Name = "python"; Description = "Python" },
    @{ Name = "java"; Description = "Java" },
    @{ Name = "javascript"; Description = "JavaScript" },
    @{ Name = "csharp"; Description = "C#" },
    @{ Name = "cpp_stl"; Description = "C++ STL" },
    @{ Name = "ruby"; Description = "Ruby" },
    @{ Name = "php"; Description = "PHP" },
    @{ Name = "perl"; Description = "Perl" },
    @{ Name = "go"; Description = "Go" },
    @{ Name = "lua"; Description = "Lua" },
    @{ Name = "nim"; Description = "Nim" },
    @{ Name = "rust"; Description = "Rust" },
    @{ Name = "swift"; Description = "Swift" },
    @{ Name = "typescript"; Description = "TypeScript" }
)

# For quick mode, only test the most common languages
$LanguagesToTest = if ($Quick) {
    $AllLanguages | Select-Object -First 5
}
else {
    $AllLanguages
}

$GitKsyPath = "src\Andastra\Parsing\Resource\Formats\GFF\Generics\GIT\GIT.ksy"
$TestOutputDir = "test_kaitai_multilang_output"
$TestResults = @()

Write-Host "=" * 70 -ForegroundColor Cyan
Write-Host "Kaitai Struct GIT Format - Multi-Language Compilation Test" -ForegroundColor Cyan
Write-Host "=" * 70 -ForegroundColor Cyan
Write-Host "KSY file: $GitKsyPath" -ForegroundColor Gray
Write-Host "Languages to test: $($LanguagesToTest.Count)" -ForegroundColor Gray
Write-Host "Mode: $(if ($Quick) { 'Quick (5 languages)' } else { 'Full (' + $LanguagesToTest.Count + ' languages)' })" -ForegroundColor Gray
Write-Host ""

# Verify KSY file exists
if (-not (Test-Path $GitKsyPath)) {
    Write-Host "ERROR: KSY file not found: $GitKsyPath" -ForegroundColor Red
    exit 1
}

# Verify compiler is available
Write-Host "Checking Kaitai Struct compiler..." -ForegroundColor Yellow

# Try multiple possible compiler names
$compilerFound = $false
$compilerNames = @("kaitai-struct-compiler", "ksc", "kaitai-struct-compiler.exe", "ksc.exe")

foreach ($compilerName in $compilerNames) {
    try {
        $null = & $compilerName --version 2>&1
        if ($LASTEXITCODE -eq 0) {
            $compilerFound = $true
            $KscPath = $compilerName
            break
        }
    }
    catch {
        # Try next compiler name
        continue
    }
}

if (-not $compilerFound) {
    Write-Host "WARNING: Kaitai Struct compiler not found or not working." -ForegroundColor Yellow
    Write-Host ""
    Write-Host "The tests will validate the KSY file structure but cannot test compilation." -ForegroundColor Yellow
    Write-Host ""
    Write-Host "To install the Kaitai Struct compiler:" -ForegroundColor Cyan
    Write-Host "  Windows (Chocolatey): choco install kaitai-struct-compiler" -ForegroundColor White
    Write-Host "  Windows (Manual):     Download from https://kaitai.io/#download" -ForegroundColor White
    Write-Host "  Linux:                 wget https://packages.kaitai.io/dists/unstable/main/binary-amd64/kaitai-struct-compiler_0.10_all.deb" -ForegroundColor White
    Write-Host "                         sudo dpkg -i kaitai-struct-compiler_0.10_all.deb" -ForegroundColor White
    Write-Host ""
    Write-Host "Continuing with structure validation only..." -ForegroundColor Yellow
    Write-Host ""

    # Run validation instead
    & "$PSScriptRoot\validate_kaitai_git.ps1" -SkipCompilation
    Write-Host ""
    Write-Host "NOTE: Compilation tests skipped - compiler not available" -ForegroundColor Yellow
    exit 0
}
else {
    $versionOutput = & $KscPath --version 2>&1
    Write-Host "Compiler found: $($versionOutput -join ' ')" -ForegroundColor Green
}

Write-Host ""

# Clean and create output directory
if (Test-Path $TestOutputDir) {
    Remove-Item -Recurse -Force $TestOutputDir
}
New-Item -ItemType Directory -Path $TestOutputDir | Out-Null

$SuccessCount = 0
$FailureCount = 0
$SkippedCount = 0

Write-Host "Testing compilation to each language..." -ForegroundColor Cyan
Write-Host ""

foreach ($langInfo in $LanguagesToTest) {
    $lang = $langInfo.Name
    $langDesc = $langInfo.Description

    Write-Host "Testing $langDesc ($lang)..." -NoNewline -ForegroundColor Yellow

    $langOutputDir = Join-Path $TestOutputDir $lang
    New-Item -ItemType Directory -Path $langOutputDir -Force | Out-Null

    $startTime = Get-Date

    try {
        # Compile to the target language
        $compileOutput = & $KscPath -t $lang -d $langOutputDir $GitKsyPath 2>&1
        $compileTime = (Get-Date) - $startTime

        if ($LASTEXITCODE -eq 0) {
            # Check if output files were generated
            $outputFiles = Get-ChildItem -Path $langOutputDir -Recurse -File | Measure-Object
            $hasOutput = $outputFiles.Count -gt 0

            if ($hasOutput) {
                Write-Host " PASS" -ForegroundColor Green
                if ($Verbose) {
                    Write-Host "    Compilation time: $($compileTime.TotalMilliseconds)ms" -ForegroundColor Gray
                    Write-Host "    Output files: $($outputFiles.Count)" -ForegroundColor Gray
                }
                $SuccessCount++
                $TestResults += @{
                    Language    = $lang
                    Description = $langDesc
                    Status      = "PASS"
                    CompileTime = $compileTime
                    OutputFiles = $outputFiles.Count
                    Output      = $compileOutput
                }
            }
            else {
                Write-Host " WARN (no output files)" -ForegroundColor Yellow
                $SkippedCount++
                $TestResults += @{
                    Language    = $lang
                    Description = $langDesc
                    Status      = "WARN"
                    Error       = "Compilation succeeded but no output files generated"
                }
            }
        }
        else {
            Write-Host " FAIL" -ForegroundColor Red
            $FailureCount++
            $TestResults += @{
                Language    = $lang
                Description = $langDesc
                Status      = "FAIL"
                Error       = $compileOutput
            }

            if ($Verbose) {
                Write-Host "    Error output:" -ForegroundColor Red
                $compileOutput | ForEach-Object { Write-Host "      $_" -ForegroundColor Red }
            }
        }
    }
    catch {
        Write-Host " ERROR" -ForegroundColor Red
        $FailureCount++
        $TestResults += @{
            Language    = $lang
            Description = $langDesc
            Status      = "ERROR"
            Error       = $_.Exception.Message
        }

        if ($Verbose) {
            Write-Host "    Exception: $($_.Exception.Message)" -ForegroundColor Red
        }
    }
}

Write-Host ""
Write-Host "=" * 70 -ForegroundColor Cyan
Write-Host "Test Summary" -ForegroundColor Cyan
Write-Host "=" * 70 -ForegroundColor Cyan
Write-Host "Total languages tested: $($LanguagesToTest.Count)" -ForegroundColor White
Write-Host "Successful: $SuccessCount" -ForegroundColor Green
Write-Host "Failed: $FailureCount" -ForegroundColor $(if ($FailureCount -eq 0) { "Green" } else { "Red" })
Write-Host "Warnings: $SkippedCount" -ForegroundColor $(if ($SkippedCount -eq 0) { "Green" } else { "Yellow" })
Write-Host ""

# Detailed results
if ($FailureCount -gt 0 -or $SkippedCount -gt 0) {
    Write-Host "Detailed Results:" -ForegroundColor Cyan
    Write-Host ""

    $TestResults | Where-Object { $_.Status -ne "PASS" } | ForEach-Object {
        Write-Host "  $($_.Description) ($($_.Language)): $($_.Status)" -ForegroundColor $(switch ($_.Status) {
                "FAIL" { "Red" }
                "ERROR" { "Red" }
                "WARN" { "Yellow" }
                default { "White" }
            })
        if ($_.Error) {
            $errorMsg = if ($_.Error -is [array]) { $_.Error -join '; ' } else { $_.Error.ToString() }
            if ($errorMsg.Length -gt 100) {
                $errorMsg = $errorMsg.Substring(0, 100) + "..."
            }
            Write-Host "    $errorMsg" -ForegroundColor Gray
        }
    }
    Write-Host ""
}

# Generate detailed report
$ReportPath = Join-Path $TestOutputDir "test_report.txt"
$ReportContent = @"
Kaitai Struct GIT Format - Multi-Language Compilation Test Report
Generated: $(Get-Date -Format "yyyy-MM-dd HH:mm:ss")

Test Configuration:
- KSY File: $GitKsyPath
- Languages Tested: $($LanguagesToTest.Count)
- Mode: $(if ($Quick) { 'Quick' } else { 'Full' })
- Compiler: $KscPath

Results Summary:
- Total: $($LanguagesToTest.Count)
- Successful: $SuccessCount
- Failed: $FailureCount
- Warnings: $SkippedCount

Detailed Results:
"@

foreach ($result in $TestResults) {
    $ReportContent += "`n$($result.Description) ($($result.Language)): $($result.Status)"
    if ($result.CompileTime) {
        $ReportContent += "`n  Compile time: $($result.CompileTime.TotalMilliseconds)ms"
    }
    if ($result.OutputFiles) {
        $ReportContent += "`n  Output files: $($result.OutputFiles)"
    }
    if ($result.Error) {
        $errorMsg = if ($result.Error -is [array]) { $result.Error -join "`n  " } else { $result.Error.ToString() }
        $ReportContent += "`n  Error: $errorMsg"
    }
}

$ReportContent | Out-File -FilePath $ReportPath -Encoding UTF8
Write-Host "Detailed report saved to: $ReportPath" -ForegroundColor Gray
Write-Host ""

# Final status
if ($FailureCount -eq 0 -and $SkippedCount -eq 0) {
    Write-Host "All tests passed successfully!" -ForegroundColor Green
    exit 0
}
elseif ($FailureCount -eq 0) {
    Write-Host "All compilations succeeded, but some had warnings." -ForegroundColor Yellow
    exit 0
}
else {
    Write-Host "Some tests failed. See report for details." -ForegroundColor Red
    exit 1
}

