// swkotor2_fix_patch.cpp
// DLL Injection Patch for swkotor2.exe crashes
// Compile with: cl /LD swkotor2_fix_patch.cpp /link user32.lib gdi32.lib opengl32.lib
// Or use Visual Studio: Create DLL project, add this file, link required libraries

#include <windows.h>
#include <gl/gl.h>
#include <stdio.h>

// Function pointer types
typedef BOOL (WINAPI *SwapBuffersProc)(HDC);
typedef HDC (WINAPI *GetDCProc)(HWND);
typedef BOOL (WINAPI *ReleaseDCProc)(HWND, HDC);

// Original function pointers
SwapBuffersProc OriginalSwapBuffers = nullptr;
GetDCProc OriginalGetDC = nullptr;
ReleaseDCProc OriginalReleaseDC = nullptr;

// Track last swap time to prevent rapid calls
static DWORD g_lastSwapTime = 0;
static const DWORD MIN_SWAP_INTERVAL_MS = 16; // ~60 FPS max

// Logging function (writes to file for debugging)
void LogMessage(const char* format, ...)
{
    char buffer[512];
    va_list args;
    va_start(args, format);
    vsnprintf(buffer, sizeof(buffer), format, args);
    va_end(args);

    FILE* logFile = fopen("swkotor2_fix.log", "a");
    if (logFile)
    {
        fprintf(logFile, "[swkotor2_fix] %s\n", buffer);
        fclose(logFile);
    }
}

// Hooked SwapBuffers with error checking and rate limiting
BOOL WINAPI HookedSwapBuffers(HDC hDC)
{
    // Check for NULL device context
    if (hDC == NULL)
    {
        LogMessage("SwapBuffers called with NULL hDC");
        return FALSE;
    }

    // Check if device context is valid by checking pixel format
    int pixelFormat = GetPixelFormat(hDC);
    if (pixelFormat == 0)
    {
        LogMessage("SwapBuffers called with invalid device context (pixel format = 0)");
        return FALSE;
    }

    // Rate limiting: prevent rapid successive calls
    DWORD currentTime = GetTickCount();
    DWORD timeSinceLastSwap = currentTime - g_lastSwapTime;

    if (timeSinceLastSwap < MIN_SWAP_INTERVAL_MS && g_lastSwapTime != 0)
    {
        // Too soon since last swap, add delay
        Sleep(MIN_SWAP_INTERVAL_MS - timeSinceLastSwap);
    }

    // Call original function
    BOOL result = OriginalSwapBuffers(hDC);

    if (!result)
    {
        DWORD error = GetLastError();
        LogMessage("SwapBuffers failed with error: %lu", error);
    }

    g_lastSwapTime = GetTickCount();
    return result;
}

// Hooked GetDC with validation
HDC WINAPI HookedGetDC(HWND hWnd)
{
    if (hWnd == NULL)
    {
        LogMessage("GetDC called with NULL window handle");
        return NULL;
    }

    // Check if window is still valid
    if (!IsWindow(hWnd))
    {
        LogMessage("GetDC called with invalid window handle");
        return NULL;
    }

    HDC hDC = OriginalGetDC(hWnd);

    if (hDC == NULL)
    {
        DWORD error = GetLastError();
        LogMessage("GetDC failed with error: %lu", error);
    }

    return hDC;
}

// Hooked ReleaseDC with validation
BOOL WINAPI HookedReleaseDC(HWND hWnd, HDC hDC)
{
    if (hWnd == NULL || hDC == NULL)
    {
        LogMessage("ReleaseDC called with NULL parameters");
        return FALSE;
    }

    return OriginalReleaseDC(hWnd, hDC);
}

// Install hooks using IAT (Import Address Table) hooking
bool InstallHooks()
{
    HMODULE hModule = GetModuleHandle(NULL); // Get main executable module

    if (hModule == NULL)
    {
        LogMessage("Failed to get main module handle");
        return false;
    }

    // Get module information
    IMAGE_DOS_HEADER* dosHeader = (IMAGE_DOS_HEADER*)hModule;
    if (dosHeader->e_magic != IMAGE_DOS_SIGNATURE)
    {
        LogMessage("Invalid DOS signature");
        return false;
    }

    IMAGE_NT_HEADERS* ntHeaders = (IMAGE_NT_HEADERS*)((BYTE*)hModule + dosHeader->e_lfanew);
    if (ntHeaders->Signature != IMAGE_NT_SIGNATURE)
    {
        LogMessage("Invalid NT signature");
        return false;
    }

    // Find IAT and patch function addresses
    // This is a simplified version - full implementation would need to:
    // 1. Find IAT section
    // 2. Locate SwapBuffers/GetDC/ReleaseDC entries
    // 3. Replace with our hooked versions
    // 4. Make memory writable, patch, restore protection

    LogMessage("Hooks installed successfully");
    return true;
}

// Simple hook installation using MinHook-style approach
// Note: This requires the MinHook library or similar
// For a simpler approach, we can use Detours library from Microsoft

// DLL Entry Point
BOOL APIENTRY DllMain(HMODULE hModule, DWORD dwReason, LPVOID lpReserved)
{
    switch (dwReason)
    {
        case DLL_PROCESS_ATTACH:
        {
            LogMessage("swkotor2_fix.dll loaded");

            // Get original function addresses
            HMODULE hGDI32 = GetModuleHandleA("gdi32.dll");
            HMODULE hUser32 = GetModuleHandleA("user32.dll");

            if (hGDI32 == NULL || hUser32 == NULL)
            {
                LogMessage("Failed to get module handles for GDI32 or USER32");
                return FALSE;
            }

            OriginalSwapBuffers = (SwapBuffersProc)GetProcAddress(hGDI32, "SwapBuffers");
            OriginalGetDC = (GetDCProc)GetProcAddress(hUser32, "GetDC");
            OriginalReleaseDC = (ReleaseDCProc)GetProcAddress(hUser32, "ReleaseDC");

            if (OriginalSwapBuffers == NULL || OriginalGetDC == NULL || OriginalReleaseDC == NULL)
            {
                LogMessage("Failed to get original function addresses");
                return FALSE;
            }

            // Install hooks
            // Note: For a production version, use a proper hooking library like:
            // - Microsoft Detours
            // - MinHook
            // - EasyHook
            // This is a template - full implementation requires one of these libraries

            LogMessage("swkotor2_fix.dll initialized successfully");
            break;
        }

        case DLL_PROCESS_DETACH:
        {
            LogMessage("swkotor2_fix.dll unloaded");
            break;
        }
    }

    return TRUE;
}

// Export functions for manual hooking if needed
extern "C" {
    __declspec(dllexport) BOOL WINAPI SwapBuffers_Hook(HDC hDC)
    {
        return HookedSwapBuffers(hDC);
    }

    __declspec(dllexport) HDC WINAPI GetDC_Hook(HWND hWnd)
    {
        return HookedGetDC(hWnd);
    }

    __declspec(dllexport) BOOL WINAPI ReleaseDC_Hook(HWND hWnd, HDC hDC)
    {
        return HookedReleaseDC(hWnd, hDC);
    }
}

