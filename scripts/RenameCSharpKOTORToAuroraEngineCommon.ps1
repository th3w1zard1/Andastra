# Script to rename CSharpKOTOR to AuroraEngine.Common
# This script performs comprehensive find/replace operations for the namespace rename

param(
    [string]$RootPath = ".",
    [switch]$WhatIf
)

$ErrorActionPreference = "Stop"

Write-Host "Renaming CSharpKOTOR to AuroraEngine.Common..."
Write-Host "Root path: $RootPath"
if ($WhatIf) {
    Write-Host "WHAT-IF MODE: No changes will be made" -ForegroundColor Yellow
}

# Get all C# files (exclude build artifacts, history, temp files)
$csFiles = Get-ChildItem -Path $RootPath -Filter "*.cs" -Recurse | Where-Object {
    $_.FullName -notmatch "\\bin\\" -and
    $_.FullName -notmatch "\\obj\\" -and
    $_.FullName -notmatch "\\.git\\" -and
    $_.FullName -notmatch "\\.history\\" -and
    $_.FullName -notmatch "\\temp_" -and
    $_.FullName -notmatch "\\~dotnetcleanup"
}

$totalFiles = $csFiles.Count
$processedFiles = 0
$changedFiles = 0

foreach ($file in $csFiles) {
    $processedFiles++
    if ($processedFiles % 100 -eq 0) {
        Write-Host "Processing file $processedFiles of $totalFiles..."
    }

    $content = Get-Content -Path $file.FullName -Raw -Encoding UTF8
    $originalContent = $content
    $changed = $false

    # Replace namespace declarations
    $content = $content -replace 'namespace CSharpKOTOR\.', 'namespace AuroraEngine.Common.'
    if ($content -ne $originalContent) { $changed = $true }

    # Replace using statements
    $content = $content -replace 'using CSharpKOTOR\.', 'using AuroraEngine.Common.'
    if ($content -ne $originalContent) { $changed = $true }

    # Replace type references in code (CSharpKOTOR. -> AuroraEngine.Common.)
    $content = $content -replace '\bCSharpKOTOR\.', 'AuroraEngine.Common.'
    if ($content -ne $originalContent) { $changed = $true }

    if ($changed) {
        $changedFiles++
        if (-not $WhatIf) {
            Set-Content -Path $file.FullName -Value $content -Encoding UTF8 -NoNewline
            Write-Host "Updated: $($file.FullName)" -ForegroundColor Green
        } else {
            Write-Host "Would update: $($file.FullName)" -ForegroundColor Yellow
        }
    }
}

# Update .csproj files
$csprojFiles = Get-ChildItem -Path $RootPath -Filter "*.csproj" -Recurse | Where-Object {
    $_.FullName -notmatch "\\bin\\" -and
    $_.FullName -notmatch "\\obj\\" -and
    $_.FullName -notmatch "\\.git\\"
}

foreach ($file in $csprojFiles) {
    $content = Get-Content -Path $file.FullName -Raw -Encoding UTF8
    $originalContent = $content
    $changed = $false

    # Replace project references
    $content = $content -replace 'CSharpKOTOR\.csproj', 'AuroraEngine.Common.csproj'
    if ($content -ne $originalContent) { $changed = $true }

    # Replace package references
    $content = $content -replace '<PackageReference Include="CSharpKOTOR"', '<PackageReference Include="AuroraEngine.Common"'
    if ($content -ne $originalContent) { $changed = $true }

    if ($changed) {
        $changedFiles++
        if (-not $WhatIf) {
            Set-Content -Path $file.FullName -Value $content -Encoding UTF8 -NoNewline
            Write-Host "Updated project: $($file.FullName)" -ForegroundColor Green
        } else {
            Write-Host "Would update project: $($file.FullName)" -ForegroundColor Yellow
        }
    }
}

# Update solution file
$slnFiles = Get-ChildItem -Path $RootPath -Filter "*.sln" -Recurse | Where-Object {
    $_.FullName -notmatch "\\.git\\"
}

foreach ($file in $slnFiles) {
    $content = Get-Content -Path $file.FullName -Raw -Encoding UTF8
    $originalContent = $content
    $changed = $false

    # Replace project references in solution
    $content = $content -replace 'CSharpKOTOR\.csproj', 'AuroraEngine.Common.csproj'
    $content = $content -replace '"CSharpKOTOR"', '"AuroraEngine.Common"'
    if ($content -ne $originalContent) { $changed = $true }

    if ($changed) {
        $changedFiles++
        if (-not $WhatIf) {
            Set-Content -Path $file.FullName -Value $content -Encoding UTF8 -NoNewline
            Write-Host "Updated solution: $($file.FullName)" -ForegroundColor Green
        } else {
            Write-Host "Would update solution: $($file.FullName)" -ForegroundColor Yellow
        }
    }
}

Write-Host ""
Write-Host "Summary:" -ForegroundColor Cyan
Write-Host "  Total files processed: $totalFiles"
Write-Host "  Files changed: $changedFiles"
if ($WhatIf) {
    Write-Host ""
    Write-Host "Run without -WhatIf to apply changes" -ForegroundColor Yellow
}

