# swkotor2.exe Crash Fix - Quick Start Guide

## Overview

This directory contains scripts and code to fix the post-character creation crashes in `swkotor2.exe`.

## Quick Fix (No Compilation Required)

### Option 1: Use the Launcher Script

1. Copy `swkotor2_fix_launcher.bat` to your KOTOR 2 installation directory (where `swkotor2.exe` is located)
2. Run `swkotor2_fix_launcher.bat` instead of `swkotor2.exe`
3. The script will:
   - Clean up temporary files
   - Set process priority to High
   - Set CPU affinity to prevent race conditions
   - Clear DirectX cache

### Option 2: Manual Windows Compatibility Settings

1. Right-click `swkotor2.exe` → Properties → Compatibility
2. Check these options:
   - ✅ Run this program in compatibility mode for: **Windows XP (Service Pack 3)**
   - ✅ Disable fullscreen optimizations
   - ✅ Run this program as an administrator
   - ✅ Override high DPI scaling behavior → **Application**
3. Click Apply and OK
4. Launch the game

### Option 3: Graphics Driver Settings

**For NVIDIA:**
1. NVIDIA Control Panel → Manage 3D Settings → Program Settings
2. Add `swkotor2.exe`
3. Set:
   - Power management mode: **Prefer maximum performance**
   - Threaded optimization: **Off**
   - Vertical sync: **Off**

**For AMD:**
1. AMD Settings → Gaming → Global Settings
2. Set:
   - Wait for Vertical Refresh: **Always Off**
   - Frame Rate Target Control: **Off**

**For Intel Integrated:**
1. Intel Graphics Control Panel → 3D → Application Settings
2. Set Application Optimal Mode: **Performance**

## Advanced Fix: DLL Injection Patch

### Prerequisites

- Visual Studio 2019 or later (or MinGW)
- Windows SDK
- One of these hooking libraries:
  - [Microsoft Detours](https://github.com/Microsoft/Detours) (Recommended)
  - [MinHook](https://github.com/TsudaKageyu/minhook)
  - [EasyHook](https://easyhook.github.io/)

### Compilation Steps

#### Using Microsoft Detours (Recommended)

1. Download and install [Microsoft Detours](https://github.com/Microsoft/Detours)
2. Create a new DLL project in Visual Studio
3. Copy `swkotor2_fix_patch.cpp` to your project
4. Add Detours library to your project:
   ```cpp
   #include <detours.h>
   ```
5. Modify `DllMain` to use Detours:
   ```cpp
   case DLL_PROCESS_ATTACH:
       DetourTransactionBegin();
       DetourUpdateThread(GetCurrentThread());
       DetourAttach(&(PVOID&)OriginalSwapBuffers, HookedSwapBuffers);
       DetourAttach(&(PVOID&)OriginalGetDC, HookedGetDC);
       DetourAttach(&(PVOID&)OriginalReleaseDC, HookedReleaseDC);
       DetourTransactionCommit();
       break;
   ```
6. Build the DLL
7. Copy `swkotor2_fix.dll` to your KOTOR 2 installation directory
8. Use a DLL injector or modify the launcher script to inject it

#### Using MinHook

1. Download [MinHook](https://github.com/TsudaKageyu/minhook)
2. Add MinHook to your project
3. Modify the code to use MinHook's API:
   ```cpp
   #include "MinHook.h"
   
   MH_Initialize();
   MH_CreateHook(OriginalSwapBuffers, HookedSwapBuffers, (LPVOID*)&OriginalSwapBuffers);
   MH_EnableHook(OriginalSwapBuffers);
   ```

### Using the DLL

**Method 1: DLL Injector**
1. Download a DLL injector (e.g., [Xenos Injector](https://github.com/stevemkceb/Xenos-Injector))
2. Run the injector
3. Select `swkotor2.exe` as the target
4. Select `swkotor2_fix.dll` as the DLL to inject
5. Launch the game

**Method 2: Manual Injection**
1. Use a tool like [Process Hacker](https://processhacker.sourceforge.io/)
2. Right-click `swkotor2.exe` process → Miscellaneous → Inject DLL
3. Select `swkotor2_fix.dll`

**Method 3: Automatic Injection (Recommended)**
Modify `swkotor2_fix_launcher.bat` to automatically inject the DLL:
```batch
REM Add after launching the game:
timeout /t 2 /nobreak >nul
injector.exe swkotor2.exe swkotor2_fix.dll
```

## What the Fix Does

The DLL patch:

1. **Validates device context** before calling `SwapBuffers`
   - Checks for NULL handles
   - Validates pixel format
   - Returns safely on error instead of crashing

2. **Rate limits SwapBuffers calls**
   - Prevents rapid successive calls (max 60 FPS)
   - Adds small delays to prevent race conditions

3. **Validates window handles** before `GetDC`
   - Checks if window is still valid
   - Returns NULL safely on error

4. **Logs errors** to `swkotor2_fix.log` for debugging

## Testing

After applying fixes:

1. Launch the game using the launcher script
2. Create a new character
3. Complete character creation
4. Check if the game loads the module without crashing
5. If it still crashes, check `swkotor2_fix.log` for error messages

## Troubleshooting

**DLL not loading:**
- Make sure the DLL is in the same directory as `swkotor2.exe`
- Check that you're using the correct architecture (x86 for 32-bit game)
- Verify the DLL was compiled correctly

**Still crashing:**
- Check `swkotor2_fix.log` for specific error messages
- Try the Windows compatibility settings (Option 2)
- Try graphics driver settings (Option 3)
- Ensure you have the latest stable graphics drivers (not beta)

**Performance issues:**
- The rate limiting may cause slight performance impact
- Adjust `MIN_SWAP_INTERVAL_MS` in the code if needed
- Disable the DLL if performance is unacceptable

## Support

If none of these solutions work:
1. Check `swkotor2_fix.log` for error messages
2. Report your graphics card model and driver version
3. Report Windows version
4. Report amount of RAM
5. Check if the crash happens immediately or after a delay

## License

This fix is provided as-is for educational and compatibility purposes.

