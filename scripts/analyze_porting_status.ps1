# Script to analyze porting status between PyKotor and CSharpKOTOR
param(
    [string]$PyKotorPath = "vendor\PyKotor\Libraries\PyKotor\src\pykotor",
    [string]$CSharpKOTORPath = "src\CSharpKOTOR"
)

Write-Host "Analyzing porting status..." -ForegroundColor Cyan

# Get all Python files from PyKotor
$pyFiles = Get-ChildItem -Path $PyKotorPath -Recurse -Filter "*.py" | Where-Object { $_.Name -ne "__init__.py" }
Write-Host "Found $($pyFiles.Count) Python files in PyKotor" -ForegroundColor Yellow

# Get all C# files from CSharpKOTOR
$csFiles = Get-ChildItem -Path $CSharpKOTORPath -Recurse -Filter "*.cs"
Write-Host "Found $($csFiles.Count) C# files in CSharpKOTOR" -ForegroundColor Yellow

# Create a mapping (simple name-based for now)
$portingMap = @{}
$missingFiles = @()

foreach ($pyFile in $pyFiles) {
    $relativePath = $pyFile.FullName -replace [regex]::Escape((Resolve-Path $PyKotorPath).Path + "\"), ""
    $moduleName = $pyFile.BaseName
    
    # Try to find corresponding C# file
    $found = $false
    $matchingCs = $csFiles | Where-Object { $_.BaseName -eq $moduleName -or $_.BaseName -like "*$moduleName*" }
    
    if ($matchingCs.Count -gt 0) {
        $portingMap[$relativePath] = @{
            Status = "PortExists"
            CsFile = $matchingCs[0].FullName
        }
        $found = $true
    }
    
    if (-not $found) {
        $missingFiles += $relativePath
        $portingMap[$relativePath] = @{
            Status = "Missing"
        }
    }
}

Write-Host "`nPorting Status Summary:" -ForegroundColor Cyan
Write-Host "  Ported: $($portingMap.Values | Where-Object { $_.Status -eq 'PortExists' } | Measure-Object | Select-Object -ExpandProperty Count)" -ForegroundColor Green
Write-Host "  Missing: $($missingFiles.Count)" -ForegroundColor Red

Write-Host "`nMissing files (first 50):" -ForegroundColor Yellow
$missingFiles | Select-Object -First 50 | ForEach-Object { Write-Host "  $_" -ForegroundColor Red }

# Export to JSON for further analysis
$portingMap | ConvertTo-Json -Depth 3 | Out-File "porting_analysis.json" -Encoding UTF8
Write-Host "`nFull analysis exported to porting_analysis.json" -ForegroundColor Green
