# Setup script for NetSparkle keys
# Generates Ed25519 keys and provides instructions for GitHub Secrets

param(
    [switch]$Export,
    [string]$OutputDir = "keys"
)

Write-Host "NetSparkle Key Generation Setup" -ForegroundColor Cyan
Write-Host "================================" -ForegroundColor Cyan
Write-Host ""

# Check if NetSparkle tools are installed
Write-Host "Checking for NetSparkle tools..." -ForegroundColor Yellow
$toolInstalled = dotnet tool list -g | Select-String "NetSparkleUpdater.Tools.AppCastGenerator"

if (-not $toolInstalled) {
    Write-Host "Installing NetSparkle tools..." -ForegroundColor Yellow
    dotnet tool install -g NetSparkleUpdater.Tools.AppCastGenerator
    if ($LASTEXITCODE -ne 0) {
        Write-Host "Failed to install NetSparkle tools" -ForegroundColor Red
        exit 1
    }
}

Write-Host "✓ NetSparkle tools installed" -ForegroundColor Green
Write-Host ""

# Create output directory
if (-not (Test-Path $OutputDir)) {
    New-Item -ItemType Directory -Path $OutputDir | Out-Null
}

# Generate keys
Write-Host "Generating Ed25519 keys..." -ForegroundColor Yellow

# Try to find netsparkle-generate-appcast in PATH
$netsparkleCmd = Get-Command netsparkle-generate-appcast -ErrorAction SilentlyContinue
if (-not $netsparkleCmd) {
    # Try dotnet tool path
    $dotnetToolsPath = Join-Path $env:USERPROFILE ".dotnet" "tools"
    $netsparklePath = Join-Path $dotnetToolsPath "netsparkle-generate-appcast.exe"
    if (Test-Path $netsparklePath) {
        $netsparkleCmd = $netsparklePath
    } else {
        Write-Host "netsparkle-generate-appcast not found in PATH" -ForegroundColor Red
        Write-Host "Please ensure NetSparkle tools are installed: dotnet tool install -g NetSparkleUpdater.Tools.AppCastGenerator" -ForegroundColor Yellow
        exit 1
    }
}

# Build command arguments
$args = @("--generate-keys")
if ($Export) {
    $args += "--export", "true"
}

# Execute command
if ($netsparkleCmd -is [System.Management.Automation.ApplicationInfo]) {
    $keyOutput = & $netsparkleCmd.Source $args 2>&1 | Out-String
} else {
    $keyOutput = & $netsparkleCmd $args 2>&1 | Out-String
}

if ($LASTEXITCODE -ne 0) {
    Write-Host "Failed to generate keys" -ForegroundColor Red
    Write-Host $keyOutput
    exit 1
}

# Extract keys from output
$publicKey = ""
$privateKey = ""

if ($Export) {
    # Keys are in the output - handle both formats:
    # "Public Key: value" (single line) or "Public Key:\nvalue" (multi-line)
    $lines = $keyOutput -split "`r?`n"
    $inPublicKey = $false
    $inPrivateKey = $false
    
    foreach ($line in $lines) {
        $trimmedLine = $line.Trim()
        
        # Skip empty lines
        if ([string]::IsNullOrWhiteSpace($trimmedLine)) {
            continue
        }
        
        # Check for single-line format: "Public Key: value"
        if ($trimmedLine -match "^Public Key:\s*(.+)$") {
            $publicKey = $matches[1].Trim()
            $inPublicKey = $false
            $inPrivateKey = $false
        }
        elseif ($trimmedLine -match "^Private Key:\s*(.+)$") {
            $privateKey = $matches[1].Trim()
            $inPublicKey = $false
            $inPrivateKey = $false
        }
        # Check for label-only line (multi-line format): "Public Key:"
        elseif ($trimmedLine -eq "Public Key:") {
            $inPublicKey = $true
            $inPrivateKey = $false
        }
        elseif ($trimmedLine -eq "Private Key:") {
            $inPrivateKey = $true
            $inPublicKey = $false
        }
        # If we're in a key section, the next non-empty line is the key value
        elseif ($inPublicKey) {
            $publicKey = $trimmedLine
            $inPublicKey = $false
        }
        elseif ($inPrivateKey) {
            $privateKey = $trimmedLine
            $inPrivateKey = $false
        }
    }
} else {
    # Keys are in files (default location)
    $userProfile = $env:USERPROFILE
    if ([string]::IsNullOrEmpty($userProfile)) {
        $userProfile = $env:HOME
    }
    
    $keysDir = Join-Path $userProfile ".netsparkle"
    $publicKeyFile = Join-Path $keysDir "public_key.txt"
    $privateKeyFile = Join-Path $keysDir "private_key.txt"
    
    if (Test-Path $publicKeyFile) {
        $publicKey = (Get-Content $publicKeyFile -Raw).Trim()
        Copy-Item $publicKeyFile (Join-Path $OutputDir "public_key.txt")
    }
    
    if (Test-Path $privateKeyFile) {
        $privateKey = (Get-Content $privateKeyFile -Raw).Trim()
        Copy-Item $privateKeyFile (Join-Path $OutputDir "private_key.txt")
    }
}

if ([string]::IsNullOrEmpty($publicKey) -or [string]::IsNullOrEmpty($privateKey)) {
    Write-Host "Failed to extract keys from output" -ForegroundColor Red
    Write-Host "Output: $keyOutput"
    exit 1
}

Write-Host "✓ Keys generated successfully" -ForegroundColor Green
Write-Host ""

# Display keys
Write-Host "Generated Keys:" -ForegroundColor Cyan
Write-Host "===============" -ForegroundColor Cyan
Write-Host ""
Write-Host "Public Key (use in UpdateManager.cs):" -ForegroundColor Yellow
Write-Host $publicKey -ForegroundColor White
Write-Host ""
Write-Host "Private Key (use in GitHub Secrets as NETSPARKLE_PRIVATE_KEY):" -ForegroundColor Yellow
Write-Host $privateKey -ForegroundColor White
Write-Host ""

# Save to files
$publicKeyPath = Join-Path $OutputDir "public_key.txt"
$privateKeyPath = Join-Path $OutputDir "private_key.txt"

Set-Content -Path $publicKeyPath -Value $publicKey -NoNewline
Set-Content -Path $privateKeyPath -Value $privateKey -NoNewline

Write-Host "Keys saved to:" -ForegroundColor Cyan
Write-Host "  Public:  $publicKeyPath" -ForegroundColor White
Write-Host "  Private: $privateKeyPath" -ForegroundColor White
Write-Host ""

# Instructions
Write-Host "Next Steps:" -ForegroundColor Cyan
Write-Host "===========" -ForegroundColor Cyan
Write-Host ""
Write-Host "1. Add Public Key to UpdateManager.cs:" -ForegroundColor Yellow
Write-Host "   Update src/HoloPatcher/UpdateManager.cs:" -ForegroundColor White
Write-Host "   Ed25519PublicKey = `"$publicKey`"" -ForegroundColor Gray
Write-Host ""
Write-Host "2. Add Private Key to GitHub Secrets:" -ForegroundColor Yellow
Write-Host "   - Go to: https://github.com/th3w1zard1/HoloPatcher.NET/settings/secrets/actions" -ForegroundColor White
Write-Host "   - Click 'New repository secret'" -ForegroundColor White
Write-Host "   - Name: NETSPARKLE_PRIVATE_KEY" -ForegroundColor White
Write-Host "   - Value: $privateKey" -ForegroundColor Gray
Write-Host ""
Write-Host "3. (Optional) Add Public Key as secret for reference:" -ForegroundColor Yellow
Write-Host "   - Name: NETSPARKLE_PUBLIC_KEY" -ForegroundColor White
Write-Host "   - Value: $publicKey" -ForegroundColor Gray
Write-Host ""
Write-Host "⚠️  IMPORTANT: Keep the private key secure! Never commit it to the repository." -ForegroundColor Red
Write-Host ""

