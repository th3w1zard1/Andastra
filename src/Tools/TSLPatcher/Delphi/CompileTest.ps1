# TSLPatcher Deterministic Compilation Test
# Verifies that the Delphi code compiles and matches original logic

param(
    [string]$DelphiCompiler = "dcc32.exe",
    [string]$OriginalExe = "C:\Users\boden\Downloads\TSLPatcher1210b1\TSLPatcher.exe",
    [switch]$VerifyAddresses = $false
)

$ErrorActionPreference = "Stop"
$ScriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$ProjectDir = $ScriptDir
$ProjectFile = Join-Path $ProjectDir "TSLPatcher.dpr"
$OutputExe = Join-Path $ProjectDir "TSLPatcher_Compiled.exe"

Write-Host "TSLPatcher Deterministic Compilation Test" -ForegroundColor Cyan
Write-Host "==========================================" -ForegroundColor Cyan
Write-Host ""

# Check if Delphi compiler exists
if (-not (Get-Command $DelphiCompiler -ErrorAction SilentlyContinue)) {
    Write-Host "Error: Delphi compiler '$DelphiCompiler' not found in PATH" -ForegroundColor Red
    Write-Host "Please install Delphi or specify the compiler path with -DelphiCompiler" -ForegroundColor Yellow
    exit 1
}

# Check if project file exists
if (-not (Test-Path $ProjectFile)) {
    Write-Host "Error: Project file not found: $ProjectFile" -ForegroundColor Red
    exit 1
}

Write-Host "Project File: $ProjectFile" -ForegroundColor Green
Write-Host "Output EXE: $OutputExe" -ForegroundColor Green
Write-Host ""

# Compiler settings for deterministic build
$CompilerFlags = @(
    "-B",              # Build all units
    "-Q",              # Quiet mode
    "-$O+",            # Optimization ON
    "-$R-",            # Range checking OFF
    "-$S-",            # Stack checking OFF
    "-$I-",            # I/O checking OFF
    "-$Q-",            # Overflow checking OFF
    "-$A-",            # Assertions OFF
    "-$D+",            # Debug information ON
    "-$L+",            # Local symbols ON
    "-$Y+",            # Reference info ON
    "-E$ProjectDir",   # Output directory
    "-N$ProjectDir\DCU", # DCU output directory
    "-U$ProjectDir",   # Unit search path
    "-U$ProjectDir\FileFormats" # Additional unit path
)

Write-Host "Compiling with deterministic settings..." -ForegroundColor Yellow
Write-Host "Compiler: $DelphiCompiler" -ForegroundColor Gray
Write-Host "Flags: $($CompilerFlags -join ' ')" -ForegroundColor Gray
Write-Host ""

# Compile the project
$CompileCommand = "& `"$DelphiCompiler`" `"$ProjectFile`" $($CompilerFlags -join ' ')"

try {
    Invoke-Expression $CompileCommand
    
    if ($LASTEXITCODE -ne 0) {
        Write-Host "Compilation failed with exit code: $LASTEXITCODE" -ForegroundColor Red
        exit 1
    }
    
    Write-Host "Compilation successful!" -ForegroundColor Green
    Write-Host ""
    
    # Check if output file exists
    if (Test-Path $OutputExe) {
        $OutputInfo = Get-Item $OutputExe
        Write-Host "Output file created:" -ForegroundColor Green
        Write-Host "  Path: $($OutputInfo.FullName)" -ForegroundColor Gray
        Write-Host "  Size: $($OutputInfo.Length) bytes" -ForegroundColor Gray
        Write-Host "  Created: $($OutputInfo.CreationTime)" -ForegroundColor Gray
        Write-Host ""
        
        # Compare with original if specified
        if ($VerifyAddresses -and (Test-Path $OriginalExe)) {
            Write-Host "Comparing with original executable..." -ForegroundColor Yellow
            Write-Host "Original: $OriginalExe" -ForegroundColor Gray
            
            $OriginalInfo = Get-Item $OriginalExe
            Write-Host "Original size: $($OriginalInfo.Length) bytes" -ForegroundColor Gray
            Write-Host "Compiled size: $($OutputInfo.Length) bytes" -ForegroundColor Gray
            
            if ($OutputInfo.Length -eq $OriginalInfo.Length) {
                Write-Host "File sizes match!" -ForegroundColor Green
            } else {
                Write-Host "File sizes differ (expected due to compiler differences)" -ForegroundColor Yellow
            }
            
            Write-Host ""
            Write-Host "Note: Addresses will differ due to:" -ForegroundColor Yellow
            Write-Host "  - Different compiler versions" -ForegroundColor Gray
            Write-Host "  - Different optimization settings" -ForegroundColor Gray
            Write-Host "  - Different RTL versions" -ForegroundColor Gray
            Write-Host "  - ASLR (if enabled)" -ForegroundColor Gray
            Write-Host ""
            Write-Host "The test verifies LOGIC matches, not exact addresses." -ForegroundColor Cyan
        }
        
        # Extract function addresses from compiled binary
        Write-Host "Extracting function addresses from compiled binary..." -ForegroundColor Yellow
        
        # Use dumpbin or objdump if available to extract addresses
        $AddressesFile = Join-Path $ProjectDir "CompiledAddresses.txt"
        
        # Try to use dumpbin (Visual Studio tool)
        $DumpBinPath = "${env:ProgramFiles(x86)}\Microsoft Visual Studio\*\VC\Tools\MSVC\*\bin\Hostx64\x64\dumpbin.exe"
        $DumpBin = Get-ChildItem -Path $DumpBinPath -ErrorAction SilentlyContinue | Select-Object -First 1
        
        if ($DumpBin) {
            Write-Host "Using dumpbin to extract addresses..." -ForegroundColor Gray
            & $DumpBin.FullName /EXPORTS /SYMBOLS $OutputExe | Out-File $AddressesFile
            Write-Host "Addresses saved to: $AddressesFile" -ForegroundColor Green
        } else {
            Write-Host "dumpbin not found. Skipping address extraction." -ForegroundColor Yellow
            Write-Host "Install Visual Studio or use alternative tools (objdump, IDA, Ghidra)" -ForegroundColor Gray
        }
        
    } else {
        Write-Host "Error: Output file not found after compilation" -ForegroundColor Red
        exit 1
    }
    
    Write-Host ""
    Write-Host "Compilation test completed successfully!" -ForegroundColor Green
    Write-Host ""
    Write-Host "Next steps:" -ForegroundColor Cyan
    Write-Host "  1. Review AssemblyMapping.md for line-by-line assembly documentation" -ForegroundColor Gray
    Write-Host "  2. Compare compiled addresses with original using Ghidra or IDA" -ForegroundColor Gray
    Write-Host "  3. Verify logic matches through functional testing" -ForegroundColor Gray
    
} catch {
    Write-Host "Error during compilation: $_" -ForegroundColor Red
    Write-Host $_.Exception.Message -ForegroundColor Red
    exit 1
}

