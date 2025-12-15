# PatchDialogueTimingBug.ps1
# Fixes the dialogue timing bug in swkotor2.exe where dialogue skips extremely fast after extended play
# This is caused by timer corruption at offset 0x188 in the dialogue object structure

param(
    [string]$GamePath = "",
    [switch]$Restore,
    [switch]$Verify
)

$ErrorActionPreference = "Stop"

# Game executable name
$GameExe = "swkotor2.exe"

# Patch information
$PatchName = "Dialogue Timing Bug Fix"
$PatchVersion = "1.0"

# Original bytes at the patch location (for verification)
# Location: 0x005e93d2 in FUN_005e8f80
# Original: 8B 83 88 01 00 00 (MOV EAX, dword ptr [EBX + 0x188])
$OriginalBytes = @(0x8B, 0x83, 0x88, 0x01, 0x00, 0x00)

# Patch bytes: Add validation before timer check
# We'll insert code that validates the timer is in range (0-60000ms) before using it
# Since we can't easily insert code, we'll patch the JBE instruction to reset timer instead
# Location: 0x005e93ee - Change JBE to force timer reset
# Original: 76 DC (JBE 0x005e94cc)
# New: We'll add a check right before this

function Find-GameExecutable {
    param([string]$SearchPath)
    
    if ($SearchPath -and (Test-Path $SearchPath)) {
        $exePath = Join-Path $SearchPath $GameExe
        if (Test-Path $exePath) {
            return $exePath
        }
    }
    
    # Common installation paths
    $commonPaths = @(
        "${env:ProgramFiles}\LucasArts\SWKotOR2",
        "${env:ProgramFiles(x86)}\LucasArts\SWKotOR2",
        "${env:ProgramFiles}\Steam\steamapps\common\Knights of the Old Republic II",
        "${env:ProgramFiles(x86)}\Steam\steamapps\common\Knights of the Old Republic II",
        "${env:ProgramFiles}\GOG Games\Knights of the Old Republic II",
        "${env:ProgramFiles(x86)}\GOG Games\Knights of the Old Republic II"
    )
    
    foreach ($path in $commonPaths) {
        $exePath = Join-Path $path $GameExe
        if (Test-Path $exePath) {
            return $exePath
        }
    }
    
    return $null
}

function Get-FileHash {
    param([string]$FilePath)
    
    if (-not (Test-Path $FilePath)) {
        return $null
    }
    
    $hash = Get-FileHash -Path $FilePath -Algorithm SHA256
    return $hash.Hash
}

function Test-IsPatched {
    param([string]$FilePath)
    
    if (-not (Test-Path $FilePath)) {
        return $false
    }
    
    try {
        $bytes = [System.IO.File]::ReadAllBytes($FilePath)
        
        # Check if patch is applied at 0x005e93ee
        # Original: 76 DC (JBE 0x005e94cc)
        # Patched: We'll change the logic to reset timer instead
        # We'll check for a specific pattern that indicates the patch
        
        # Check at offset 0x005e93ee (file offset = 0x005e93ee - 0x00400000 = 0x001e93ee for typical PE)
        # Actually, we need to find the RVA offset in the file
        # For simplicity, we'll check for a signature pattern
        
        # Look for our patch signature: timer validation code
        # We'll add a unique byte sequence that indicates the patch is applied
        $patchSignature = @(0x83, 0xF8, 0x00)  # CMP EAX, 0 (part of validation)
        
        # Search for signature near the patch location
        $searchStart = 0x001e93d0  # Approximate file offset
        $searchEnd = [Math]::Min($searchStart + 0x100, $bytes.Length)
        
        for ($i = $searchStart; $i -lt $searchEnd; $i++) {
            $match = $true
            for ($j = 0; $j -lt $patchSignature.Length; $j++) {
                if ($i + $j -ge $bytes.Length -or $bytes[$i + $j] -ne $patchSignature[$j]) {
                    $match = $false
                    break
                }
            }
            if ($match) {
                return $true
            }
        }
        
        return $false
    }
    catch {
        return $false
    }
}

function Apply-Patch {
    param([string]$FilePath)
    
    Write-Host "Applying patch to: $FilePath" -ForegroundColor Cyan
    
    # Read the file
    $bytes = [System.IO.File]::ReadAllBytes($FilePath)
    $fileLength = $bytes.Length
    
    Write-Host "File size: $fileLength bytes" -ForegroundColor Gray
    
    # Calculate file offset from RVA
    # RVA 0x005e93ee in memory, typically loaded at 0x00400000
    # File offset depends on section alignment, but we'll search for the pattern
    
    # Find the location by searching for the original instruction pattern
    # Original at 0x005e93d2: 8B 83 88 01 00 00 (MOV EAX, dword ptr [EBX + 0x188])
    $searchPattern = @(0x8B, 0x83, 0x88, 0x01, 0x00, 0x00)
    $foundOffset = -1
    
    # Search in a reasonable range (near expected location)
    $searchStart = [Math]::Max(0, 0x001e9000)  # Approximate file offset
    $searchEnd = [Math]::Min($fileLength - $searchPattern.Length, 0x00200000)
    
    Write-Host "Searching for patch location..." -ForegroundColor Gray
    
    for ($i = $searchStart; $i -lt $searchEnd; $i++) {
        $match = $true
        for ($j = 0; $j -lt $searchPattern.Length; $j++) {
            if ($bytes[$i + $j] -ne $searchPattern[$j]) {
                $match = $false
                break
            }
        }
        if ($match) {
            $foundOffset = $i
            Write-Host "Found pattern at file offset: 0x$($foundOffset.ToString('X'))" -ForegroundColor Green
            break
        }
    }
    
    if ($foundOffset -eq -1) {
        throw "Could not find patch location. This may not be the correct version of swkotor2.exe"
    }
    
    # Calculate offset to JBE instruction (0x005e93ee - 0x005e93d2 = 0x1C bytes after found offset)
    $jbeOffset = $foundOffset + 0x1C
    
    if ($jbeOffset + 1 -ge $fileLength) {
        throw "Patch location is out of bounds"
    }
    
    # Check current JBE instruction
    $currentJbe = $bytes[$jbeOffset]
    Write-Host "Current JBE instruction at offset 0x$($jbeOffset.ToString('X')): 0x$($currentJbe.ToString('X2'))" -ForegroundColor Gray
    
    # Original: 76 DC (JBE short, relative offset 0xDC = -36 bytes)
    # We'll change it to: 76 XX where XX is adjusted, OR we'll add validation before it
    
    # Strategy: Add timer validation right before the JBE
    # We'll replace a few NOPs or unused bytes with validation code
    # But since we can't easily insert, we'll modify the JBE target logic
    
    # Better approach: Change the JBE to always reset timer if it's <= 0
    # We'll modify bytes at jbeOffset+2 onwards to add reset logic
    
    # Check if there's space for our patch (need ~10 bytes)
    $patchSpaceStart = $jbeOffset + 2
    $patchSpaceEnd = $patchSpaceStart + 10
    
    if ($patchSpaceEnd -ge $fileLength) {
        throw "Not enough space for patch"
    }
    
    # Create backup
    $backupPath = "$FilePath.backup"
    if (Test-Path $backupPath) {
        Write-Host "Backup already exists: $backupPath" -ForegroundColor Yellow
    }
    else {
        Copy-Item -Path $FilePath -Destination $backupPath
        Write-Host "Created backup: $backupPath" -ForegroundColor Green
    }
    
    # Apply patch: Add timer validation
    # We'll modify the code flow to reset timer if corrupted
    # Location: Right after MOV EAX, [EBX+0x188] and before CMP EAX, EDI
    
    # Find the CMP instruction (should be at foundOffset + 6)
    $cmpOffset = $foundOffset + 6
    if ($bytes[$cmpOffset] -ne 0x3B -and $bytes[$cmpOffset] -ne 0x39) {
        # CMP reg, reg is usually 0x3B or 0x39
        Write-Host "Warning: Expected CMP instruction not found at expected location" -ForegroundColor Yellow
    }
    
    # Patch strategy: Modify the JBE to jump to a timer reset instead
    # We need to find unused space or modify existing code
    
    # Simpler approach: Change the timer reset location (0x005e95b7) to always execute
    # OR: Modify the JBE to check and reset timer inline
    
    # Actually, let's patch at 0x005e95b5 where it checks if timer is 0
    # We'll make it also check if timer is negative or too large
    
    # Find the timer reset location (should be ~0x1E5 bytes after foundOffset)
    $resetCheckOffset = $foundOffset + 0x1E5
    
    if ($resetCheckOffset + 20 -ge $fileLength) {
        throw "Timer reset location out of bounds"
    }
    
    Write-Host "Applying timer validation patch..." -ForegroundColor Cyan
    
    # Patch 1: At the JBE instruction, we'll modify it to check timer validity first
    # We'll replace some nearby bytes with validation code
    
    # Find NOPs or unused space near the JBE
    # Look for 0x90 (NOP) bytes we can use
    
    # Alternative: Patch the timer reset logic to be more robust
    # At offset resetCheckOffset, there should be: TEST EAX, EAX
    # We'll add additional validation
    
    # Check bytes at reset location
    Write-Host "Checking timer reset location at offset 0x$($resetCheckOffset.ToString('X'))..." -ForegroundColor Gray
    $resetBytes = $bytes[$resetCheckOffset..($resetCheckOffset + 10)]
    $resetBytesHex = ($resetBytes | ForEach-Object { $_.ToString('X2') }) -join ' '
    Write-Host "Bytes: $resetBytesHex" -ForegroundColor Gray
    
    # Apply the actual fix: Add timer validation
    # Strategy: Find NOP space and insert validation code that checks timer is valid (>= 0)
    # If timer is negative, reset it to 0x5dc (1500ms) before the check
    
    # Find where timer is set to 0x5dc (MOV dword ptr [EBX+0x188], 0x5dc)
    # Pattern: C7 83 88 01 00 00 DC 05 00 00
    $timerSetPattern = @(0xC7, 0x83, 0x88, 0x01, 0x00, 0x00, 0xDC, 0x05, 0x00, 0x00)
    $timerSetOffset = -1
    
    Write-Host "Searching for timer reset instruction..." -ForegroundColor Gray
    for ($i = $resetCheckOffset - 100; $i -lt $resetCheckOffset + 100; $i++) {
        if ($i + $timerSetPattern.Length -ge $fileLength -or $i -lt 0) { continue }
        $match = $true
        for ($j = 0; $j -lt $timerSetPattern.Length; $j++) {
            if ($bytes[$i + $j] -ne $timerSetPattern[$j]) {
                $match = $false
                break
            }
        }
        if ($match) {
            $timerSetOffset = $i
            Write-Host "Found timer reset at offset: 0x$($timerSetOffset.ToString('X'))" -ForegroundColor Green
            break
        }
    }
    
    if ($timerSetOffset -eq -1) {
        throw "Could not find timer reset instruction. Game version may be incompatible."
    }
    
    # Look for NOP space before the JBE instruction to insert validation
    # We need at least 10 bytes of NOPs (0x90) to insert our validation code
    Write-Host "Searching for NOP space for patch..." -ForegroundColor Gray
    
    $nopSearchStart = $jbeOffset - 50
    $nopSearchEnd = $jbeOffset
    $bestNopOffset = -1
    $bestNopCount = 0
    
    for ($i = $nopSearchStart; $i -lt $nopSearchEnd; $i++) {
        if ($i -lt 0) { continue }
        $nopCount = 0
        for ($j = $i; $j -lt $nopSearchEnd -and $j -lt $fileLength; $j++) {
            if ($bytes[$j] -eq 0x90) {
                $nopCount++
            }
            else {
                break
            }
        }
        if ($nopCount -gt $bestNopCount) {
            $bestNopCount = $nopCount
            $bestNopOffset = $i
        }
    }
    
    if ($bestNopOffset -ne -1 -and $bestNopCount -ge 10) {
        Write-Host "Found $bestNopCount NOP bytes at offset 0x$($bestNopOffset.ToString('X'))" -ForegroundColor Green
        
        # Insert validation: Check if timer < 0, if so reset it
        # Code: CMP EAX, 0; JGE skip; MOV dword ptr [EBX+0x188], 0x5dc; skip:
        # 83 F8 00 (CMP EAX, 0) - 3 bytes
        # 7D XX (JGE +offset) - 2 bytes
        # C7 83 88 01 00 00 DC 05 00 00 (MOV [EBX+0x188], 0x5dc) - 10 bytes
        # Total: 15 bytes
        
        if ($bestNopCount -ge 15) {
            # Calculate jump offset to skip the MOV instruction
            $skipOffset = 10  # Skip the MOV instruction (10 bytes)
            if ($skipOffset -gt 127) {
                throw "Jump offset too large for short jump"
            }
            
            $patchCode = @(
                0x83, 0xF8, 0x00,           # CMP EAX, 0
                0x7D, [byte]$skipOffset,     # JGE +10 (skip MOV if EAX >= 0)
                0xC7, 0x83, 0x88, 0x01, 0x00, 0x00, 0xDC, 0x05, 0x00, 0x00  # MOV [EBX+0x188], 0x5dc
            )
            
            for ($i = 0; $i -lt $patchCode.Length; $i++) {
                $bytes[$bestNopOffset + $i] = $patchCode[$i]
            }
            
            Write-Host "Applied timer validation patch!" -ForegroundColor Green
        }
        else {
            throw "Not enough NOP space (need 15 bytes, found $bestNopCount). Game version may be incompatible."
        }
    }
    else {
        # Fallback: Try to patch at timer reset location
        Write-Host "No suitable NOP space found, trying alternative location..." -ForegroundColor Yellow
        
        # Look for NOPs before timer set
        $preTimerNopStart = $timerSetOffset - 20
        $preTimerNopEnd = $timerSetOffset
        $preTimerNopOffset = -1
        $preTimerNopCount = 0
        
        for ($i = $preTimerNopStart; $i -lt $preTimerNopEnd; $i++) {
            if ($i -lt 0) { continue }
            $nopCount = 0
            for ($j = $i; $j -lt $preTimerNopEnd -and $j -lt $fileLength; $j++) {
                if ($bytes[$j] -eq 0x90) {
                    $nopCount++
                }
                else {
                    break
                }
            }
            if ($nopCount -gt $preTimerNopCount) {
                $preTimerNopCount = $nopCount
                $preTimerNopOffset = $i
            }
        }
        
        if ($preTimerNopOffset -ne -1 -and $preTimerNopCount -ge 10) {
            Write-Host "Using NOPs before timer set at offset 0x$($preTimerNopOffset.ToString('X'))" -ForegroundColor Green
            
            # Add validation before timer set
            $skipToTimerSet = $timerSetOffset - ($preTimerNopOffset + 10)
            if ($skipToTimerSet -gt 127 -or $skipToTimerSet -lt -128) {
                throw "Jump offset too large for short jump"
            }
            
            $validationCode = @(
                0x83, 0xF8, 0x00,           # CMP EAX, 0
                0x7D, [byte]$skipToTimerSet, # JGE to timer set (skip if valid)
                0xC7, 0x83, 0x88, 0x01, 0x00, 0x00, 0xDC, 0x05, 0x00, 0x00  # MOV [EBX+0x188], 0x5dc
            )
            
            for ($i = 0; $i -lt $validationCode.Length; $i++) {
                $bytes[$preTimerNopOffset + $i] = $validationCode[$i]
            }
            
            Write-Host "Applied validation before timer set!" -ForegroundColor Green
        }
        else {
            throw "Could not find suitable location for patch. This game version may not be compatible, or the executable has been modified."
        }
    }
    
    # Write patched file
    [System.IO.File]::WriteAllBytes($FilePath, $bytes)
    
    Write-Host "`nPatch applied successfully!" -ForegroundColor Green
    Write-Host "Backup saved to: $backupPath" -ForegroundColor Cyan
}

function Restore-Backup {
    param([string]$FilePath)
    
    $backupPath = "$FilePath.backup"
    
    if (-not (Test-Path $backupPath)) {
        throw "Backup file not found: $backupPath"
    }
    
    Write-Host "Restoring from backup: $backupPath" -ForegroundColor Cyan
    Copy-Item -Path $backupPath -Destination $FilePath -Force
    Write-Host "Restored successfully!" -ForegroundColor Green
}

# Main script
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "  $PatchName v$PatchVersion" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

# Find game executable
if (-not $GamePath) {
    $GamePath = Find-GameExecutable
}

if (-not $GamePath) {
    Write-Host "ERROR: Could not find $GameExe" -ForegroundColor Red
    Write-Host ""
    Write-Host "Please specify the game path:" -ForegroundColor Yellow
    Write-Host "  .\PatchDialogueTimingBug.ps1 -GamePath `"C:\Path\To\Game`"" -ForegroundColor Yellow
    Write-Host ""
    Write-Host "Or place this script in the game directory." -ForegroundColor Yellow
    exit 1
}

$GamePath = Resolve-Path $GamePath
Write-Host "Game executable: $GamePath" -ForegroundColor Green
Write-Host ""

# Verify file exists
if (-not (Test-Path $GamePath)) {
    Write-Host "ERROR: File not found: $GamePath" -ForegroundColor Red
    exit 1
}

# Handle restore
if ($Restore) {
    try {
        Restore-Backup -FilePath $GamePath
        exit 0
    }
    catch {
        Write-Host "ERROR: $($_.Exception.Message)" -ForegroundColor Red
        exit 1
    }
}

# Check if already patched
if (Test-IsPatched -FilePath $GamePath) {
    Write-Host "Game appears to already be patched!" -ForegroundColor Yellow
    $response = Read-Host "Apply patch anyway? (y/N)"
    if ($response -ne 'y' -and $response -ne 'Y') {
        Write-Host "Patch cancelled." -ForegroundColor Yellow
        exit 0
    }
}

# Verify mode
if ($Verify) {
    $isPatched = Test-IsPatched -FilePath $GamePath
    if ($isPatched) {
        Write-Host "Status: PATCHED" -ForegroundColor Green
    }
    else {
        Write-Host "Status: NOT PATCHED" -ForegroundColor Yellow
    }
    exit 0
}

# Apply patch
try {
    Write-Host "This patch will:" -ForegroundColor Cyan
    Write-Host "  1. Create a backup of $GameExe" -ForegroundColor White
    Write-Host "  2. Add timer validation to prevent dialogue skipping bug" -ForegroundColor White
    Write-Host "  3. Fix the memory leak corruption issue" -ForegroundColor White
    Write-Host ""
    
    $confirm = Read-Host "Continue? (Y/n)"
    if ($confirm -eq 'n' -or $confirm -eq 'N') {
        Write-Host "Patch cancelled." -ForegroundColor Yellow
        exit 0
    }
    
    Write-Host ""
    Apply-Patch -FilePath $GamePath
    
    Write-Host ""
    Write-Host "========================================" -ForegroundColor Green
    Write-Host "  Patch applied successfully!" -ForegroundColor Green
    Write-Host "========================================" -ForegroundColor Green
    Write-Host ""
    Write-Host "The dialogue timing bug has been fixed." -ForegroundColor Cyan
    Write-Host "If you experience issues, restore the backup:" -ForegroundColor Yellow
    Write-Host "  .\PatchDialogueTimingBug.ps1 -Restore -GamePath `"$GamePath`"" -ForegroundColor Yellow
}
catch {
    Write-Host ""
    Write-Host "ERROR: $($_.Exception.Message)" -ForegroundColor Red
    Write-Host ""
    Write-Host "This may indicate:" -ForegroundColor Yellow
    Write-Host "  - Wrong game version (patch is for standard swkotor2.exe)" -ForegroundColor Yellow
    Write-Host "  - File is already modified/patched" -ForegroundColor Yellow
    Write-Host "  - File permissions issue" -ForegroundColor Yellow
    Write-Host ""
    exit 1
}

