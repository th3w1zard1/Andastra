# Script to check what PyKotor modules are missing from CSharpKOTOR
# This helps identify what still needs to be ported

$pykotorSrc = Join-Path $PSScriptRoot "..\vendor\PyKotor\Libraries\PyKotor\src"
$csharpSrc = Join-Path $PSScriptRoot "..\src\CSharpKOTOR"

Write-Host "Scanning Python files..."
$pyFiles = Get-ChildItem -Path $pykotorSrc -Recurse -Filter "*.py" |
    Where-Object { $_.FullName -notmatch '__pycache__' -and $_.FullName -notmatch '__init__\.py$' } |
    ForEach-Object {
        $relative = $_.FullName.Substring($pykotorSrc.Length + 1)
        $relative = $relative -replace '\\', '/'
        [PSCustomObject]@{
            Path = $relative
            Name = $_.Name
            Directory = $_.DirectoryName
        }
    }

Write-Host "Found $($pyFiles.Count) Python files"

Write-Host "`nScanning C# files..."
$csFiles = Get-ChildItem -Path $csharpSrc -Recurse -Filter "*.cs" |
    ForEach-Object {
        $relative = $_.FullName.Substring($csharpSrc.Length + 1)
        $relative = $relative -replace '\\', '/'
        [PSCustomObject]@{
            Path = $relative
            Name = $_.Name
        }
    }

Write-Host "Found $($csFiles.Count) C# files"

# Map Python modules to likely C# equivalents
$moduleMap = @{
    'pykotor/common/' = 'Common/'
    'pykotor/extract/' = 'Extract/'
    'pykotor/resource/formats/' = 'Formats/'
    'pykotor/resource/generics/' = 'Resource/Generics/'
    'pykotor/tools/' = 'Tools/'
    'pykotor/tslpatcher/' = 'TSLPatcher/'
    'utility/common/' = 'Utility/'
    'utility/system/' = 'Utility/System/'
}

Write-Host "`nAnalyzing coverage..."
$stats = @{
    Total = $pyFiles.Count
    Mapped = 0
    Unmapped = 0
}

foreach ($pyFile in $pyFiles) {
    $mapped = $false
    foreach ($key in $moduleMap.Keys) {
        if ($pyFile.Path.StartsWith($key)) {
            $mapped = $true
            break
        }
    }
    if ($mapped) {
        $stats.Mapped++
    } else {
        $stats.Unmapped++
    }
}

Write-Host "Python files mapped to known locations: $($stats.Mapped)"
Write-Host "Python files in unmapped locations: $($stats.Unmapped)"

Write-Host "`nPorting status by module:"
$modules = $pyFiles | Group-Object {
    if ($_.Path -match '^pykotor/([^/]+)') { "pykotor/$($matches[1])" }
    elseif ($_.Path -match '^utility/([^/]+)') { "utility/$($matches[1])" }
    else { "other" }
}

foreach ($module in $modules) {
    Write-Host "  $($module.Name): $($module.Count) files"
}

Write-Host "`nDone."
