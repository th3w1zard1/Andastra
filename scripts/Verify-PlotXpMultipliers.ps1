# Verify Plot XP Multiplier Constants from swkotor2.exe
# This script helps verify the values at the specified addresses in Ghidra
#
# Addresses to verify:
# - 0x007b99b4: PLOT_XP_BASE_MULTIPLIER (_DAT_007b99b4)
# - 0x007b5f88: PLOT_XP_ADDITIONAL_MULTIPLIER (_DAT_007b5f88)
#
# Usage in Ghidra:
# 1. Open swkotor2.exe in Ghidra
# 2. Go to Address: 0x007b99b4
# 3. View as: float (32-bit)
# 4. Note the value
# 5. Repeat for 0x007b5f88
#
# Expected values (to be verified):
# - PLOT_XP_BASE_MULTIPLIER: Likely 1.0f (pass-through multiplier)
# - PLOT_XP_ADDITIONAL_MULTIPLIER: Likely 1.0f (pass-through multiplier)
#
# These constants are used in:
# - FUN_005e6870 @ 0x005e6870: Uses _DAT_007b99b4
# - FUN_0057eb20 @ 0x0057eb20: Uses _DAT_007b5f88
#
# Formula:
# multiplierValue = plotXpPercentage * PLOT_XP_BASE_MULTIPLIER
# finalXP = (baseXP * multiplierValue) * PLOT_XP_ADDITIONAL_MULTIPLIER

param(
    [string]$GhidraProjectPath = "C:\Users\boden\Andastra Ghidra Project.gpr",
    [string]$ProgramPath = "swkotor2.exe"
)

Write-Host "Plot XP Multiplier Verification Script" -ForegroundColor Cyan
Write-Host "=====================================" -ForegroundColor Cyan
Write-Host ""
Write-Host "This script documents the addresses that need to be verified in Ghidra."
Write-Host ""
Write-Host "Addresses to check:" -ForegroundColor Yellow
Write-Host "  0x007b99b4 - PLOT_XP_BASE_MULTIPLIER (_DAT_007b99b4)" -ForegroundColor White
Write-Host "  0x007b5f88 - PLOT_XP_ADDITIONAL_MULTIPLIER (_DAT_007b5f88)" -ForegroundColor White
Write-Host ""
Write-Host "Functions that use these values:" -ForegroundColor Yellow
Write-Host "  FUN_005e6870 @ 0x005e6870 - Uses _DAT_007b99b4" -ForegroundColor White
Write-Host "  FUN_0057eb20 @ 0x0057eb20 - Uses _DAT_007b5f88" -ForegroundColor White
Write-Host ""
Write-Host "To verify in Ghidra:" -ForegroundColor Yellow
Write-Host "  1. Open $ProgramPath in Ghidra" -ForegroundColor White
Write-Host "  2. Navigate to each address" -ForegroundColor White
Write-Host "  3. View the memory location as a float (32-bit)" -ForegroundColor White
Write-Host "  4. Record the value" -ForegroundColor White
Write-Host "  5. Update DialogueManager.cs with the verified values" -ForegroundColor White
Write-Host ""
Write-Host "Note: These are data segment constants (float values)." -ForegroundColor Cyan
Write-Host "      The logical default is 1.0f (pass-through multipliers)." -ForegroundColor Cyan
Write-Host ""

