$file = "src/TSLPatcher.tests/Formats/NCSCompilerTests.cs"
$content = Get-Content $file -Raw

# Fix StackSnapshots[^N] patterns
$content = $content -replace 'StackSnapshots\[\^(\d+)\]', 'StackSnapshots[StackSnapshots.Count - $1]'

# Fix ActionSnapshots[^N] patterns  
$content = $content -replace 'ActionSnapshots\[\^(\d+)\]', 'ActionSnapshots[ActionSnapshots.Count - $1]'

# Fix ArgValues[^N] patterns
$content = $content -replace 'ArgValues\[\^(\d+)\]', 'ArgValues[ArgValues.Count - $1]'

# Fix Stack[^N] patterns (when accessing StackSnapshots[x].Stack[^N])
$content = $content -replace '\.Stack\[\^(\d+)\]', '.Stack[Stack.Count - $1]'

Set-Content $file -Value $content -NoNewline

