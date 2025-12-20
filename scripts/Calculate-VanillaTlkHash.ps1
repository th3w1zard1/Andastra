#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Calculates the SHA1 hash of a vanilla dialog.tlk file for KOTOR/TSL installations.

.DESCRIPTION
    This script calculates the SHA1 hash of a vanilla (unmodified) dialog.tlk file.
    The hash is used by the TSLPatcher uninstall system to detect if the TLK has been
    modified by mods and needs restoration.

    To obtain the vanilla hash:
    1. Ensure you have a clean, unmodified game installation
    2. Verify no mods have been installed (check override folder is empty)
    3. Run this script pointing to the dialog.tlk file
    4. Copy the output hash to UninstallHelpers.cs

.PARAMETER TlkPath
    Path to the dialog.tlk file to calculate hash for.

.PARAMETER Game
    Game type: "K1" for Knights of the Old Republic, "TSL" for The Sith Lords.

.EXAMPLE
    .\Calculate-VanillaTlkHash.ps1 -TlkPath "C:\Games\KOTOR2\dialog.tlk" -Game "TSL"
    Calculates the SHA1 hash for a TSL vanilla dialog.tlk file.

.EXAMPLE
    .\Calculate-VanillaTlkHash.ps1 -TlkPath "C:\Games\KOTOR\dialog.tlk" -Game "K1"
    Calculates the SHA1 hash for a K1 vanilla dialog.tlk file.

.OUTPUTS
    System.String
    Returns the SHA1 hash as a lowercase hexadecimal string.

.NOTES
    - The hash must be calculated from a verified vanilla (unmodified) installation
    - K1 vanilla dialog.tlk should have exactly 49,265 entries
    - TSL vanilla dialog.tlk should have exactly 136,329 entries
    - Verify the entry count matches before using the hash
    - Hash format: lowercase hexadecimal string (e.g., "a1b2c3d4e5f6...")
#>

[CmdletBinding()]
param(
    [Parameter(Mandatory = $true, HelpMessage = "Path to the dialog.tlk file")]
    [ValidateScript({
        if (-not (Test-Path $_ -PathType Leaf)) {
            throw "File not found: $_"
        }
        if (-not $_.EndsWith(".tlk", [System.StringComparison]::OrdinalIgnoreCase)) {
            throw "File must be a .tlk file: $_"
        }
        $true
    })]
    [string]$TlkPath,

    [Parameter(Mandatory = $true, HelpMessage = "Game type: K1 or TSL")]
    [ValidateSet("K1", "TSL")]
    [string]$Game
)

$ErrorActionPreference = "Stop"

try {
    # Verify file exists
    if (-not (Test-Path $TlkPath -PathType Leaf)) {
        Write-Error "File not found: $TlkPath"
        exit 1
    }

    # Calculate SHA1 hash
    Write-Host "Calculating SHA1 hash for: $TlkPath" -ForegroundColor Cyan
    Write-Host "Game: $Game" -ForegroundColor Cyan
    
    $hash = Get-FileHash -Path $TlkPath -Algorithm SHA1 -ErrorAction Stop
    $sha1Hash = $hash.Hash.ToLowerInvariant()
    
    Write-Host ""
    Write-Host "SHA1 Hash (lowercase):" -ForegroundColor Green
    Write-Host $sha1Hash -ForegroundColor Yellow
    Write-Host ""
    
    # Output in format suitable for code
    Write-Host "Code format:" -ForegroundColor Green
    Write-Host "{ Game.$Game, `"$sha1Hash`" }" -ForegroundColor Yellow
    Write-Host ""
    
    # Verify entry count if possible (optional check)
    Write-Host "Note: Verify the entry count matches expected vanilla count:" -ForegroundColor Cyan
    if ($Game -eq "K1") {
        Write-Host "  K1 vanilla: 49,265 entries" -ForegroundColor Cyan
    } else {
        Write-Host "  TSL vanilla: 136,329 entries" -ForegroundColor Cyan
    }
    Write-Host ""
    
    # Return the hash
    Write-Output $sha1Hash
    exit 0
}
catch {
    Write-Error "Error calculating hash: $_"
    exit 1
}

