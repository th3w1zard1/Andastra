<#
.SYNOPSIS
    Compares two NCS files and displays their instructions side-by-side.

.DESCRIPTION
    This script loads two NCS files, parses their instructions, and displays them
    in a format that makes it easy to identify differences. Useful for debugging
    bytecode mismatches in round-trip tests.

.PARAMETER OriginalFile
    Path to the first NCS file to compare (typically the original compiled file).

.PARAMETER RoundTripFile
    Path to the second NCS file to compare (typically the round-trip compiled file).

.PARAMETER AssemblyPath
    Path to the CSharpKOTOR.dll assembly. Defaults to the standard build output location.

.PARAMETER ShowOnly
    If specified, only show instructions from the specified file ("original" or "roundtrip").
    By default, shows both files side-by-side.

.EXAMPLE
    .\scripts\Compare-NcsFiles.ps1 -OriginalFile "test.ncs" -RoundTripFile "test.rt.ncs"

.EXAMPLE
    .\scripts\Compare-NcsFiles.ps1 -OriginalFile "test.ncs" -RoundTripFile "test.rt.ncs" -ShowOnly "original"
#>
param(
    [Parameter(Mandatory=$true)]
    [string]$OriginalFile,

    [Parameter(Mandatory=$true)]
    [string]$RoundTripFile,

    [string]$AssemblyPath = "src/CSharpKOTOR/bin/Debug/net9/CSharpKOTOR.dll",

    [ValidateSet("original", "roundtrip", "both")]
    [string]$ShowOnly = "both"
)

# Resolve paths relative to workspace root
$workspaceRoot = $PSScriptRoot | Split-Path -Parent
$originalPath = if ([System.IO.Path]::IsPathRooted($OriginalFile)) { $OriginalFile } else { Join-Path $workspaceRoot $OriginalFile }
$roundTripPath = if ([System.IO.Path]::IsPathRooted($RoundTripFile)) { $RoundTripFile } else { Join-Path $workspaceRoot $RoundTripFile }
$assemblyFullPath = if ([System.IO.Path]::IsPathRooted($AssemblyPath)) { $AssemblyPath } else { Join-Path $workspaceRoot $AssemblyPath }

# Validate files exist
if (-not (Test-Path $originalPath)) {
    Write-Error "Original file not found: $originalPath"
    exit 1
}

if (-not (Test-Path $roundTripPath)) {
    Write-Error "Round-trip file not found: $roundTripPath"
    exit 1
}

if (-not (Test-Path $assemblyFullPath)) {
    Write-Error "Assembly not found: $assemblyFullPath"
    Write-Host "Hint: Build the CSharpKOTOR project first: dotnet build src/CSharpKOTOR/CSharpKOTOR.csproj"
    exit 1
}

# Load assembly
try {
    Add-Type -Path $assemblyFullPath
}
catch {
    Write-Error "Failed to load assembly: $_"
    exit 1
}

# Load NCS files
try {
    $origNcs = [CSharpKOTOR.Formats.NCS.NCSAuto]::ReadNcs($originalPath)
    $rtNcs = [CSharpKOTOR.Formats.NCS.NCSAuto]::ReadNcs($roundTripPath)
}
catch {
    Write-Error "Failed to load NCS files: $_"
    exit 1
}

# Format instruction for display
function Format-Instruction {
    param(
        [int]$Index,
        [object]$Instruction
    )

    $argsStr = if ($Instruction.Args -and $Instruction.Args.Count -gt 0) {
        $Instruction.Args | ForEach-Object {
            if ($_ -is [string]) { "'$_'" } else { $_ }
        } | Join-String -Separator ", "
    } else {
        ""
    }

    $jumpStr = if ($Instruction.Jump) { " jump=→[$($Instruction.Jump.Offset)]" } else { "" }

    return "[$Index] $($Instruction.InsType) args=[$argsStr]$jumpStr offset=$($Instruction.Offset)"
}

# Display instructions
if ($ShowOnly -eq "original" -or $ShowOnly -eq "both") {
    Write-Host "=== ORIGINAL NCS INSTRUCTIONS ($($origNcs.Instructions.Count) total) ===" -ForegroundColor Cyan
    Write-Host "File: $originalPath"
    Write-Host ""
    for ($i = 0; $i -lt $origNcs.Instructions.Count; $i++) {
        Write-Host (Format-Instruction $i $origNcs.Instructions[$i])
    }
    Write-Host ""
}

if ($ShowOnly -eq "roundtrip" -or $ShowOnly -eq "both") {
    Write-Host "=== ROUND-TRIP NCS INSTRUCTIONS ($($rtNcs.Instructions.Count) total) ===" -ForegroundColor Cyan
    Write-Host "File: $roundTripPath"
    Write-Host ""
    for ($i = 0; $i -lt $rtNcs.Instructions.Count; $i++) {
        Write-Host (Format-Instruction $i $rtNcs.Instructions[$i])
    }
    Write-Host ""
}

# Side-by-side comparison if both are shown
if ($ShowOnly -eq "both") {
    Write-Host "=== SIDE-BY-SIDE COMPARISON ===" -ForegroundColor Yellow
    Write-Host ""

    $maxCount = [Math]::Max($origNcs.Instructions.Count, $rtNcs.Instructions.Count)

    for ($i = 0; $i -lt $maxCount; $i++) {
        $origInst = if ($i -lt $origNcs.Instructions.Count) { $origNcs.Instructions[$i] } else { $null }
        $rtInst = if ($i -lt $rtNcs.Instructions.Count) { $rtNcs.Instructions[$i] } else { $null }

        if ($origInst -and $rtInst) {
            if ($origInst.InsType -eq $rtInst.InsType -and
                ($origInst.Args.Count -eq $rtInst.Args.Count) -and
                ($origInst.Args.Count -eq 0 -or (Compare-Object $origInst.Args $rtInst.Args) -eq $null)) {
                Write-Host "[$i] ✓ MATCH: $($origInst.InsType)" -ForegroundColor Green
            } else {
                Write-Host "[$i] ✗ DIFFER: " -ForegroundColor Red -NoNewline
                Write-Host "Orig=$($origInst.InsType) RT=$($rtInst.InsType)"
            }
        } else if ($origInst) {
            Write-Host "[$i] ✗ MISSING IN ROUND-TRIP: $($origInst.InsType)" -ForegroundColor Red
        } else if ($rtInst) {
            Write-Host "[$i] ✗ EXTRA IN ROUND-TRIP: $($rtInst.InsType)" -ForegroundColor Red
        }
    }

    Write-Host ""
    Write-Host "Summary:" -ForegroundColor Yellow
    Write-Host "  Original: $($origNcs.Instructions.Count) instructions"
    Write-Host "  Round-trip: $($rtNcs.Instructions.Count) instructions"
    Write-Host "  Difference: $([Math]::Abs($origNcs.Instructions.Count - $rtNcs.Instructions.Count)) instructions"
}

