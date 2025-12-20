# Setup script for Kaitai Struct Compiler
# Downloads and sets up the Kaitai Struct compiler for use in tests

param(
    [string]$InstallPath = "$env:USERPROFILE\.kaitai",
    [switch]$Force
)

$ErrorActionPreference = "Stop"

Write-Host "Setting up Kaitai Struct Compiler..." -ForegroundColor Green

# Check if Java is available
Write-Host "Checking for Java..." -ForegroundColor Yellow
try {
    $javaProcess = Start-Process -FilePath "java" -ArgumentList "-version" -Wait -PassThru -NoNewWindow -RedirectStandardError "java_version.txt" -RedirectStandardOutput "java_version_out.txt"
    $javaVersion = Get-Content "java_version.txt" -ErrorAction SilentlyContinue
    Remove-Item "java_version.txt" -ErrorAction SilentlyContinue
    Remove-Item "java_version_out.txt" -ErrorAction SilentlyContinue
    
    if ($null -eq $javaVersion -or $javaVersion.Count -eq 0) {
        # Try alternative method
        $javaVersion = & java -version 2>&1
    }
    
    if ($null -eq $javaVersion -or $javaVersion.Count -eq 0) {
        Write-Host "Java is not installed or not in PATH." -ForegroundColor Red
        Write-Host "Please install Java 8 or later from https://adoptium.net/" -ForegroundColor Yellow
        exit 1
    }
    Write-Host "Java found:" -ForegroundColor Green
    $javaVersion | Select-Object -First 1
} catch {
    Write-Host "Java is not installed or not in PATH." -ForegroundColor Red
    Write-Host "Please install Java 8 or later from https://adoptium.net/" -ForegroundColor Yellow
    exit 1
}

# Create install directory
if (-not (Test-Path $InstallPath)) {
    New-Item -ItemType Directory -Path $InstallPath -Force | Out-Null
}

$JarPath = Join-Path $InstallPath "kaitai-struct-compiler.jar"
$Version = "0.10"

# Check if already installed
if ((Test-Path $JarPath) -and -not $Force) {
    Write-Host "Kaitai Struct Compiler already installed at $JarPath" -ForegroundColor Green
    Write-Host "Use -Force to reinstall" -ForegroundColor Yellow
    exit 0
}

# Download Kaitai Struct Compiler
Write-Host "Downloading Kaitai Struct Compiler v$Version..." -ForegroundColor Yellow
$DownloadUrl = "https://github.com/kaitai-io/kaitai_struct_compiler/releases/download/$Version/kaitai-struct-compiler-$Version.zip"

$ZipPath = Join-Path $env:TEMP "kaitai-struct-compiler-$Version.zip"

try {
    Invoke-WebRequest -Uri $DownloadUrl -OutFile $ZipPath -UseBasicParsing
    Write-Host "Download complete." -ForegroundColor Green
} catch {
    Write-Host "Failed to download Kaitai Struct Compiler: $_" -ForegroundColor Red
    exit 1
}

# Extract JAR file
Write-Host "Extracting compiler..." -ForegroundColor Yellow
$ExtractPath = Join-Path $env:TEMP "kaitai-struct-compiler-$Version"
if (Test-Path $ExtractPath) {
    Remove-Item $ExtractPath -Recurse -Force
}

Expand-Archive -Path $ZipPath -DestinationPath $ExtractPath -Force

# Find and copy the JAR file
$JarFiles = Get-ChildItem -Path $ExtractPath -Filter "*.jar" -Recurse
if ($JarFiles.Count -eq 0) {
    Write-Host "No JAR file found in downloaded archive." -ForegroundColor Red
    exit 1
}

$SourceJar = $JarFiles[0].FullName
Copy-Item $SourceJar -Destination $JarPath -Force

# Cleanup
Remove-Item $ZipPath -Force
Remove-Item $ExtractPath -Recurse -Force

Write-Host "Kaitai Struct Compiler installed successfully at $JarPath" -ForegroundColor Green
Write-Host ""
Write-Host "Usage:" -ForegroundColor Cyan
Write-Host "  java -jar `"$JarPath`" -t python BWM.ksy -d output/" -ForegroundColor White
Write-Host ""
Write-Host "To use in tests, set environment variable:" -ForegroundColor Cyan
Write-Host "  `$env:KAITAI_COMPILER_JAR = `"$JarPath`"" -ForegroundColor White

