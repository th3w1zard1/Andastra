$files = Get-ChildItem -Path "src/TSLPatcher.tests" -Recurse -Filter "*.cs"
foreach ($file in $files) {
    $content = Get-Content $file.FullName -Raw
    $original = $content

    # Remove all null-forgiving operators (but not != or other operators)
    # Match ! followed by . [ ; ) or end of identifier
    $content = $content -replace '([a-zA-Z0-9_\[\]"]+)!([\.\[\;\)\s])', '$1$2'

    if ($content -ne $original) {
        Set-Content $file.FullName -Value $content -NoNewline
        Write-Host "Fixed: $($file.Name)"
    }
}

