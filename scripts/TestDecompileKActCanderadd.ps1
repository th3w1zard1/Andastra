# Test script to decompile k_act_canderadd.ncs and show the output
param(
    [string]$NcsFile = "",
    [string]$OutputFile = "test-k_act_canderadd-decompiled.nss"
)

$ErrorActionPreference = "Stop"

Write-Host "Testing decompilation of k_act_canderadd..."

# First, find the NCS file
if ([string]::IsNullOrEmpty($NcsFile)) {
    # Try to find it in the test work directory
    $possiblePaths = @(
        "roundtrip-work/k1/K1/Data/scripts.bif/k_act_canderadd.ncs",
        "vendor/DeNCS/test-work/Vanilla_KOTOR_Script_Source/K1/Data/scripts.bif/k_act_canderadd.ncs",
        "vendor/Vanilla_KOTOR_Script_Source/K1/Data/scripts.bif/k_act_canderadd.ncs"
    )

    foreach ($path in $possiblePaths) {
        if (Test-Path $path) {
            $NcsFile = $path
            Write-Host "Found NCS file: $NcsFile"
            break
        }
    }

    if ([string]::IsNullOrEmpty($NcsFile)) {
        Write-Host "NCS file not found. Compiling from source first..."
        # Try to find the NSS source
        $nssPaths = @(
            "vendor/DeNCS/test-work/Vanilla_KOTOR_Script_Source/K1/Data/scripts.bif/k_act_canderadd.nss",
            "vendor/Vanilla_KOTOR_Script_Source/K1/Data/scripts.bif/k_act_canderadd.nss"
        )

        $nssFile = $null
        foreach ($path in $nssPaths) {
            if (Test-Path $path) {
                $nssFile = $path
                break
            }
        }

        if ($nssFile) {
            Write-Host "Found NSS source: $nssFile"
            Write-Host "Compiling to NCS first..."
            # Compile using external compiler
            $tempNcs = "test-k_act_canderadd.ncs"
            # This would require the external compiler, so skip for now
            Write-Host "Please provide the NCS file path or compile it first"
            exit 1
        } else {
            Write-Host "NSS source not found either"
            exit 1
        }
    }
}

if (!(Test-Path $NcsFile)) {
    Write-Host "NCS file not found: $NcsFile"
    exit 1
}

Write-Host "Decompiling: $NcsFile"
Write-Host "Output: $OutputFile"
Write-Host ""

# Build the project first
Write-Host "Building CSharpKOTOR..."
dotnet build src/CSharpKOTOR/CSharpKOTOR.csproj --no-incremental | Out-Null
if ($LASTEXITCODE -ne 0) {
    Write-Host "Build failed!"
    exit 1
}

# Use RoundTripUtil to decompile
$testCode = @"
using System;
using System.IO;
using CSharpKOTOR.Formats.NCS.NCSDecomp;
using CSharpKOTOR.Common;

class TestDecompile {
    static void Main(string[] args) {
        string ncsFile = args[0];
        string outputFile = args[1];

        try {
            NcsFile ncs = new NcsFile(ncsFile);
            NcsFile output = new NcsFile(outputFile);

            RoundTripUtil.DecompileNcsToNssFile(ncs, output, "k1", System.Text.Encoding.UTF8);

            if (File.Exists(outputFile)) {
                Console.WriteLine("Decompilation successful!");
                Console.WriteLine("");
                Console.WriteLine("=== DECOMPILED OUTPUT ===");
                Console.WriteLine(File.ReadAllText(outputFile));
                Console.WriteLine("=== END OUTPUT ===");
            } else {
                Console.Error.WriteLine("Decompilation failed - no output file created");
                Environment.Exit(1);
            }
        } catch (Exception e) {
            Console.Error.WriteLine("Error: " + e.Message);
            Console.Error.WriteLine(e.StackTrace);
            Environment.Exit(1);
        }
    }
}
"@

$tempCsFile = "temp-test-decompile.cs"
$testCode | Out-File -FilePath $tempCsFile -Encoding UTF8

try {
    Write-Host "Compiling and running test program..."
    $cscPath = "C:\Program Files\dotnet\sdk\9.0.307\Roslyn\bincore\csc.dll"
    $dllPath = "src\CSharpKOTOR\bin\Debug\net9\CSharpKOTOR.dll"

    if (!(Test-Path $dllPath)) {
        Write-Host "CSharpKOTOR.dll not found at $dllPath"
        exit 1
    }

    # Compile the test
    dotnet exec "C:\Program Files\dotnet\sdk\9.0.307\Roslyn\bincore\csc.dll" /reference:$dllPath /out:test-decompile.exe $tempCsFile 2>&1 | Write-Host

    if (Test-Path "test-decompile.exe") {
        # Run it
        & .\test-decompile.exe $NcsFile $OutputFile 2>&1 | Write-Host
    } else {
        Write-Host "Compilation failed"
        exit 1
    }
} finally {
    if (Test-Path $tempCsFile) {
        Remove-Item $tempCsFile -ErrorAction SilentlyContinue
    }
    if (Test-Path "test-decompile.exe") {
        Remove-Item "test-decompile.exe" -ErrorAction SilentlyContinue
    }
    if (Test-Path "test-decompile.pdb") {
        Remove-Item "test-decompile.pdb" -ErrorAction SilentlyContinue
    }
}

