$files = Get-ChildItem -Path "src/TSLPatcher.tests" -Recurse -Filter "*.cs"
foreach ($file in $files) {
    $content = Get-Content $file.FullName -Raw
    $original = $content
    # Remove null-forgiving operator !
    $content = $content -replace '([a-zA-Z0-9_]+)!\.', '$1.'
    $content = $content -replace '([a-zA-Z0-9_]+)!\[', '$1['
    $content = $content -replace '([a-zA-Z0-9_]+)!;', '$1;'
    $content = $content -replace '([a-zA-Z0-9_]+)!\)', '$1)'
    $content = $content -replace '([a-zA-Z0-9_]+)!$', '$1'
    if ($content -ne $original) {
        Set-Content $file.FullName -Value $content -NoNewline
        Write-Host "Fixed: $($file.FullName)"
    }
}

