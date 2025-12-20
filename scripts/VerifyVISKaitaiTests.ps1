# Quick verification script to check VIS Kaitai compiler test structure
# This verifies the tests are properly structured to test multiple languages

Write-Host "Verifying VIS Kaitai Compiler Tests Structure..." -ForegroundColor Green
Write-Host ""

$testFile = "src\Andastra\Tests\Formats\VISKaitaiCompilerTests.cs"

if (-not (Test-Path $testFile)) {
    Write-Host "ERROR: Test file not found: $testFile" -ForegroundColor Red
    exit 1
}

$content = Get-Content $testFile -Raw

# Check for required elements
$checks = @{
    "15+ languages defined" = ($content -match "python.*java.*javascript.*csharp.*cpp_stl.*go.*ruby.*php.*rust.*swift.*lua.*nim.*perl")
    "At least 12 languages test" = ($content -match "TestCompileVISToAtLeastDozenLanguages")
    "All languages test" = ($content -match "TestCompileVISToAllLanguages")
    "Individual language tests" = (($content | Select-String -Pattern "TestCompileVISTo(Python|Java|JavaScript|CSharp|Cpp|Go|Ruby|Php|Rust|Swift|Lua|Nim|Perl|Kotlin|TypeScript)").Matches.Count -ge 12)
    "Compiler availability test" = ($content -match "TestKaitaiCompilerAvailable")
    "KSY file validation" = ($content -match "TestVISKsyFileExists|TestVISKsyFileValid")
    "Definition completeness test" = ($content -match "TestVISKaitaiStructDefinitionCompleteness")
    "Theory-based parameterized test" = ($content -match "TestKaitaiStructCompilation.*Theory")
}

$allPassed = $true
foreach ($check in $checks.GetEnumerator()) {
    $status = if ($check.Value) { "PASS" } else { "FAIL" }
    $color = if ($check.Value) { "Green" } else { "Red" }
    Write-Host "  $($check.Key): $status" -ForegroundColor $color
    if (-not $check.Value) {
        $allPassed = $false
    }
}

Write-Host ""

# Count language tests
$langTests = ($content | Select-String -Pattern "public void TestCompileVISTo\w+\(\)").Matches.Count
Write-Host "Individual language compilation tests: $langTests" -ForegroundColor Cyan

# Count total test methods
$totalTests = ($content | Select-String -Pattern "\[Fact\(|\[Theory\(").Matches.Count
Write-Host "Total test methods: $totalTests" -ForegroundColor Cyan

Write-Host ""

if ($allPassed -and $langTests -ge 12 -and $totalTests -ge 20) {
    Write-Host "✓ All structural checks passed!" -ForegroundColor Green
    Write-Host "✓ Tests are properly structured to test at least 12 languages" -ForegroundColor Green
    exit 0
} else {
    Write-Host "✗ Some structural checks failed" -ForegroundColor Red
    exit 1
}

