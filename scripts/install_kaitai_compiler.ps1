# Installation script for Kaitai Struct Compiler
# Supports Windows (Chocolatey, manual download) and Linux (apt/deb)

param(
    [switch]$Force,
    [string]$InstallPath = "$env:LOCALAPPDATA\kaitai-struct-compiler"
)

$ErrorActionPreference = "Stop"

Write-Host "Kaitai Struct Compiler Installation Script" -ForegroundColor Cyan
Write-Host ""

# Check if already installed
$compilerFound = $false
$compilerNames = @("kaitai-struct-compiler", "ksc")

foreach ($compilerName in $compilerNames) {
    try {
        $null = & $compilerName --version 2>&1
        if ($LASTEXITCODE -eq 0) {
            $compilerFound = $true
            Write-Host "Compiler already installed: $compilerName" -ForegroundColor Green
            & $compilerName --version
            if (-not $Force) {
                Write-Host "Use -Force to reinstall" -ForegroundColor Yellow
                exit 0
            }
        }
    } catch {
        continue
    }
}

if ($compilerFound -and -not $Force) {
    exit 0
}

# Detect OS
$isWindows = $PSVersionTable.Platform -eq "Win32NT" -or $env:OS -eq "Windows_NT"
$isLinux = $PSVersionTable.Platform -eq "Unix" -or (Test-Path "/etc/os-release")

if ($isWindows) {
    Write-Host "Detected Windows platform" -ForegroundColor Cyan
    
    # Try Chocolatey first
    if (Get-Command choco -ErrorAction SilentlyContinue) {
        Write-Host "Installing via Chocolatey..." -ForegroundColor Yellow
        choco install kaitai-struct-compiler -y
        if ($LASTEXITCODE -eq 0) {
            Write-Host "Successfully installed via Chocolatey!" -ForegroundColor Green
            & refreshenv
            kaitai-struct-compiler --version
            exit 0
        }
    }
    
    # Manual installation
    Write-Host "Chocolatey not available. Manual installation required." -ForegroundColor Yellow
    Write-Host ""
    Write-Host "Please download and install from:" -ForegroundColor Cyan
    Write-Host "  https://kaitai.io/#download" -ForegroundColor White
    Write-Host ""
    Write-Host "Or install Chocolatey and run:" -ForegroundColor Cyan
    Write-Host "  choco install kaitai-struct-compiler" -ForegroundColor White
    
} elseif ($isLinux) {
    Write-Host "Detected Linux platform" -ForegroundColor Cyan
    
    # Check for apt/dpkg
    if (Get-Command dpkg -ErrorAction SilentlyContinue) {
        Write-Host "Installing via apt/dpkg..." -ForegroundColor Yellow
        
        $debUrl = "https://packages.kaitai.io/dists/unstable/main/binary-amd64/kaitai-struct-compiler_0.10_all.deb"
        $debFile = "$env:TMP/kaitai-struct-compiler.deb"
        
        try {
            Write-Host "Downloading from $debUrl..." -ForegroundColor Gray
            Invoke-WebRequest -Uri $debUrl -OutFile $debFile -UseBasicParsing
            
            Write-Host "Installing package..." -ForegroundColor Gray
            sudo dpkg -i $debFile
            if ($LASTEXITCODE -ne 0) {
                Write-Host "Fixing dependencies..." -ForegroundColor Yellow
                sudo apt-get install -f -y
            }
            
            Write-Host "Verifying installation..." -ForegroundColor Gray
            kaitai-struct-compiler --version
            
            Write-Host "Successfully installed!" -ForegroundColor Green
            
            # Cleanup
            Remove-Item $debFile -ErrorAction SilentlyContinue
            
            exit 0
        } catch {
            Write-Host "ERROR: Installation failed: $_" -ForegroundColor Red
            Remove-Item $debFile -ErrorAction SilentlyContinue
            exit 1
        }
    } else {
        Write-Host "ERROR: dpkg not found. Please install manually." -ForegroundColor Red
        Write-Host "See: https://kaitai.io/#download" -ForegroundColor Yellow
        exit 1
    }
} else {
    Write-Host "ERROR: Unsupported platform" -ForegroundColor Red
    Write-Host "Please install manually from: https://kaitai.io/#download" -ForegroundColor Yellow
    exit 1
}

