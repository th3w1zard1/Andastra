# Dialogue Timing Bug Patch

## Problem

After playing Knights of the Old Republic II for an extended period (1-2+ hours), dialogue begins skipping extremely fast - no audio plays and conversations advance immediately to the end. This is caused by a memory leak that corrupts the dialogue timer values.

## Solution

This patch adds validation to ensure the dialogue timer is always in a valid range, preventing the corruption from causing immediate dialogue advancement.

## Usage

### Quick Start

1. **Download the patch script**: `PatchDialogueTimingBug.ps1`

2. **Run PowerShell as Administrator** (right-click PowerShell → "Run as Administrator")

3. **Navigate to the script location**:
   ```powershell
   cd "C:\Path\To\Scripts"
   ```

4. **Run the patch**:
   ```powershell
   .\PatchDialogueTimingBug.ps1
   ```

The script will:
- Automatically find your `swkotor2.exe` installation
- Create a backup (`swkotor2.exe.backup`)
- Apply the fix
- Verify the patch was applied

### Manual Game Path

If the script can't find your game automatically:

```powershell
.\PatchDialogueTimingBug.ps1 -GamePath "C:\Program Files\LucasArts\SWKotOR2"
```

### Verify Patch Status

Check if the game is already patched:

```powershell
.\PatchDialogueTimingBug.ps1 -Verify
```

### Restore Backup

If you need to restore the original file:

```powershell
.\PatchDialogueTimingBug.ps1 -Restore
```

Or with a specific path:

```powershell
.\PatchDialogueTimingBug.ps1 -Restore -GamePath "C:\Path\To\Game"
```

## What the Patch Does

The patch modifies `swkotor2.exe` to add timer validation code that:

1. **Checks timer validity**: Ensures the dialogue timer is in the range 0-60000ms (0-60 seconds)
2. **Resets corrupted timers**: If the timer is negative or too large (due to memory corruption), it resets to a safe default (1500ms)
3. **Prevents immediate advancement**: Stops dialogue from skipping when the timer is corrupted

## Technical Details

- **Patch Location**: Function `FUN_005e8f80` at offset `0x005e93d2-0x005e93ee`
- **What it fixes**: Timer corruption at offset `0x188` in the dialogue object structure
- **Method**: Adds validation code in unused NOP space before the timer check

## Compatibility

- **Tested on**: Standard retail version of Knights of the Old Republic II
- **May work on**: Steam, GOG, and other versions (if they use the same binary structure)
- **Backup**: Always creates a backup before patching

## Troubleshooting

### "Could not find patch location"

This means your `swkotor2.exe` is a different version than expected. The patch may need to be updated for your specific version.

**Solution**: 
- Check if you have any mods that modify the executable
- Try the patch on a clean installation
- Report the issue with your game version information

### "Not enough space for patch"

The game executable doesn't have enough unused space for the patch code.

**Solution**: This is rare, but may indicate a heavily modified executable. Try on a clean installation.

### "Access Denied" or Permission Errors

You need administrator privileges to modify the game executable.

**Solution**: Right-click PowerShell and select "Run as Administrator"

### Patch Applied But Bug Still Occurs

1. Verify the patch was applied: `.\PatchDialogueTimingBug.ps1 -Verify`
2. Try restoring and reapplying: `.\PatchDialogueTimingBug.ps1 -Restore` then run the patch again
3. The memory leak may be in a different location - additional patches may be needed

## Uninstall

To remove the patch and restore the original file:

```powershell
.\PatchDialogueTimingBug.ps1 -Restore
```

The backup file (`swkotor2.exe.backup`) will remain in case you need to restore again later.

## Support

If you encounter issues:
1. Check that you're using the correct game version
2. Verify you have administrator privileges
3. Ensure the game is not running when applying the patch
4. Check that no antivirus is blocking the script

## Credits

- Bug analysis using Ghidra MCP
- Patch based on reverse engineering of `swkotor2.exe`

