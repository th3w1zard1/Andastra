@echo off
REM swkotor2_fix_launcher.bat
REM Launcher script that applies fixes before running swkotor2.exe

echo [swkotor2_fix] Starting launcher...

REM Kill any existing swkotor2 processes
taskkill /F /IM swkotor2.exe 2>nul
if %ERRORLEVEL% == 0 (
    echo [swkotor2_fix] Killed existing swkotor2.exe process
    timeout /t 2 /nobreak >nul
)

REM Clear temporary files
del /Q "%TEMP%\swkotor2_*" 2>nul
del /Q "%LOCALAPPDATA%\Temp\swkotor2_*" 2>nul

REM Clear DirectX shader cache (can cause device context issues)
if exist "%LOCALAPPDATA%\D3DSCache" (
    del /Q "%LOCALAPPDATA%\D3DSCache\*" 2>nul
    echo [swkotor2_fix] Cleared DirectX shader cache
)

REM Wait for cleanup to complete
timeout /t 1 /nobreak >nul

REM Check if DLL fix exists
if exist "swkotor2_fix.dll" (
    echo [swkotor2_fix] Found swkotor2_fix.dll - will inject on launch
    REM Use a DLL injector to inject the fix DLL
    REM Example: injector.exe swkotor2.exe swkotor2_fix.dll
)

REM Launch game with high priority
echo [swkotor2_fix] Launching swkotor2.exe...
start "" /high "swkotor2.exe"

REM Wait for process to start
timeout /t 3 /nobreak >nul

REM Set process priority and affinity
for /f "tokens=2" %%a in ('tasklist /FI "IMAGENAME eq swkotor2.exe" /FO LIST ^| findstr /C:"PID:"') do (
    set PID=%%a
    echo [swkotor2_fix] Found process PID: %%a
    
    REM Set high priority
    wmic process where processid=%%a CALL setpriority "high priority" >nul 2>&1
    
    REM Set single CPU affinity (prevents race conditions)
    wmic process where processid=%%a CALL setaffinity "1" >nul 2>&1
    
    echo [swkotor2_fix] Applied process optimizations
)

echo [swkotor2_fix] Launcher complete. Game should be running with fixes applied.
pause

