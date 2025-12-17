# Script to add "using Andastra.Parsing.Common;" to files that reference Game, ResRef, LocalizedString, etc.
# but don't already have the using statement

$rootDir = Join-Path $PSScriptRoot ".." | Resolve-Path
$parsingDir = Join-Path $rootDir "src\Andastra\Parsing"

# Find all .cs files in the Parsing directory
$csFiles = Get-ChildItem -Path $parsingDir -Filter "*.cs" -Recurse | Where-Object {
    $content = Get-Content $_.FullName -Raw -ErrorAction SilentlyContinue
    if ($null -eq $content) { return $false }

    # Check if file references common types but doesn't have the using statement
    $hasGame = $content -match '\bGame\s' -or $content -match '\bResRef\b' -or $content -match '\bLocalizedString\b' -or $content -match '\bLanguage\b' -or $content -match '\bFileResource\b' -or $content -match '\bColor\b' -or $content -match '\bInventoryItem\b' -or $content -match '\bEquipmentSlot\b' -or $content -match '\bSurfaceMaterial\b' -or $content -match '\bFace\b' -or $content -match '\bRawBinaryWriter\b' -or $content -match '\bLocationResult\b' -or $content -match '\bResources\b' -or $content -match '\bCaseAwarePath\b'
    $hasUsing = $content -match 'using\s+Andastra\.Parsing\.Common\s*;'

    return $hasGame -and -not $hasUsing
}

Write-Host "Found $($csFiles.Count) files that need the using statement"

$addedCount = 0
foreach ($file in $csFiles) {
    $content = Get-Content $file.FullName -Raw
    $lines = Get-Content $file.FullName

    # Find the last using statement
    $lastUsingIndex = -1
    for ($i = 0; $i -lt $lines.Count; $i++) {
        if ($lines[$i] -match '^\s*using\s+') {
            $lastUsingIndex = $i
        }
        elseif ($lines[$i] -match '^\s*namespace\s+' -and $lastUsingIndex -ge 0) {
            break
        }
    }

    if ($lastUsingIndex -ge 0) {
        # Insert the using statement after the last using
        $newLines = @()
        for ($i = 0; $i -le $lastUsingIndex; $i++) {
            $newLines += $lines[$i]
        }
        $newLines += "using Andastra.Parsing.Common;"
        for ($i = $lastUsingIndex + 1; $i -lt $lines.Count; $i++) {
            $newLines += $lines[$i]
        }

        $newContent = $newLines -join "`r`n"
        Set-Content -Path $file.FullName -Value $newContent -NoNewline
        $addedCount++
        Write-Host "Added using statement to: $($file.Name)"
    }
}

Write-Host "Added using statement to $addedCount files"


