$filePath = "src\Andastra\Runtime\Games\Eclipse\EclipseArea.cs"
$lines = Get-Content $filePath
$newLines = @()
$startLine = 12872  # Line 12873 is index 12872 (0-based)

for ($i = 0; $i -lt $lines.Length; $i++) {
    if ($i -ge $startLine -and $i -lt ($lines.Length - 1)) {
        # Add 4 spaces to lines that start with 4 spaces (but not lines that are already at 8+ spaces for nested content)
        if ($lines[$i] -match '^(\s{4})(.+)$' -and $lines[$i] -notmatch '^(\s{8,})') {
            $newLines += "        " + $matches[2]
        } else {
            $newLines += $lines[$i]
        }
    } else {
        $newLines += $lines[$i]
    }
}

$newLines | Set-Content $filePath

