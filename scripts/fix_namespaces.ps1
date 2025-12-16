# Script to fix namespace and using statements in Andastra folder
param(
    [string]$RootPath = "G:\GitHub\HoloPatcher.NET\src\Andastra"
)

$files = Get-ChildItem -Path $RootPath -Filter "*.cs" -Recurse

foreach ($file in $files) {
    $content = Get-Content $file.FullName -Raw
    $originalContent = $content
    
    # Fix using statements
    $content = $content -replace 'using Andastra\.Formats\.Resources;', 'using Andastra.Parsing.Resource;'
    $content = $content -replace 'using Andastra\.Parsing\.Resources;', 'using Andastra.Parsing.Resource;'
    $content = $content -replace 'using Andastra\.Formats\.Script;', 'using Andastra.Parsing.Common.Script;'
    $content = $content -replace 'using Andastra\.Parsing\.Script;', 'using Andastra.Parsing.Common.Script;'
    $content = $content -replace 'using Andastra\.Formats\.Installation;', 'using Andastra.Parsing.Installation;'
    $content = $content -replace 'using Andastra\.Formats\.Tools;', 'using Andastra.Parsing.Tools;'
    $content = $content -replace 'using Andastra\.Formats\.Formats\.Chitin;', 'using Andastra.Parsing.Extract.Chitin;'
    $content = $content -replace 'using Andastra\.Formats\.LZMA;', 'using Andastra.Utility.LZMA;'
    $content = $content -replace 'using Andastra\.Formats\.Utility;', 'using Andastra.Utility;'
    $content = $content -replace 'using static Andastra\.Parsing\.GameExtensions;', 'using static Andastra.Parsing.Common.GameExtensions;'
    
    # Fix RawBinaryReader references
    $content = $content -replace 'RawBinaryReader\.', 'Andastra.Parsing.Common.RawBinaryReader.'
    $content = $content -replace 'RawBinaryReader ', 'Andastra.Parsing.Common.RawBinaryReader '
    $content = $content -replace 'RawBinaryReader\(', 'Andastra.Parsing.Common.RawBinaryReader('
    $content = $content -replace 'RawBinaryReader\.From', 'Andastra.Parsing.Common.RawBinaryReader.From'
    
    if ($content -ne $originalContent) {
        Set-Content -Path $file.FullName -Value $content -NoNewline
        Write-Host "Fixed: $($file.FullName)"
    }
}

Write-Host "Done fixing namespaces and usings"

