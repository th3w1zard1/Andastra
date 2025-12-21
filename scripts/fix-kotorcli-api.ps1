# Script to fix System.CommandLine API usage in KotorCLI
# This script fixes common API usage patterns

param(
    [string]$File
)

$content = Get-Content $File -Raw

# Fix Argument constructors - remove default value and description parameters
$content = $content -replace 'new Argument<string>\(`"([^"]+)`",\s*\(\)\s*=>\s*[^,]+,\s*`"([^"]+)`"\)', 'new Argument<string>("$1")'
$content = $content -replace 'new Argument<string>\(`"([^"]+)`",\s*\(\)\s*=>\s*null,\s*`"([^"]+)`"\)', 'new Argument<string>("$1")'
$content = $content -replace 'new Argument<string>\(`"([^"]+)`",\s*`"([^"]+)`"\)', 'new Argument<string>("$1")'
$content = $content -replace 'new Argument<string\[\]>\(`"([^"]+)`",\s*\(\)\s*=>\s*[^,]+,\s*`"([^"]+)`"\)', 'new Argument<string[]>("$1")'
$content = $content -replace 'new Argument<string\[\]>\(`"([^"]+)`",\s*`"([^"]+)`"\)', 'new Argument<string[]>("$1")'

# Fix Option IsRequired to Required property
$content = $content -replace '\.IsRequired\s*=\s*true', '.Required = true'

# Fix AddAlias - remove it (aliases go in constructor)
$content = $content -replace '\.AddAlias\(`"([^"]+)`"\);', '// Alias: $1'

# Fix Option constructor with array aliases - ensure proper format
# This is already correct in most places, but let's ensure consistency

# Fix AddGlobalOption to Options.Add (for root command)
$content = $content -replace 'rootCommand\.AddGlobalOption\(', 'rootCommand.Options.Add('

Set-Content $File -Value $content -NoNewline

