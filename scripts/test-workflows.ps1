# Test script to validate workflow setup
# Run this before pushing workflows to ensure everything is configured correctly

param(
    [switch]$CheckSecrets,
    [switch]$ValidateYaml,
    [switch]$All
)

$ErrorActionPreference = "Stop"

if ($All) {
    $CheckSecrets = $true
    $ValidateYaml = $true
}

Write-Host "Workflow Validation Test" -ForegroundColor Cyan
Write-Host "========================" -ForegroundColor Cyan
Write-Host ""

$hasErrors = $false

# Check workflow files exist
Write-Host "Checking workflow files..." -ForegroundColor Yellow
$requiredWorkflows = @(
    ".github/workflows/ci.yml",
    ".github/workflows/test-builds.yml",
    ".github/workflows/build-all-platforms.yml",
    ".github/workflows/release-please.yml",
    ".github/workflows/netsparkle-appcast.yml",
    ".github/workflows/validate-workflows.yml"
)

foreach ($workflow in $requiredWorkflows) {
    if (Test-Path $workflow) {
        Write-Host "  ✓ $workflow" -ForegroundColor Green
    } else {
        Write-Host "  ✗ $workflow - MISSING" -ForegroundColor Red
        $hasErrors = $true
    }
}

Write-Host ""

# Check configuration files
Write-Host "Checking configuration files..." -ForegroundColor Yellow
$requiredConfigs = @(
    ".github/release-please-config.json",
    ".github/release-please-manifest.json",
    ".github/RELEASE_TEMPLATE.md"
)

foreach ($config in $requiredConfigs) {
    if (Test-Path $config) {
        Write-Host "  ✓ $config" -ForegroundColor Green
    } else {
        Write-Host "  ✗ $config - MISSING" -ForegroundColor Red
        $hasErrors = $true
    }
}

Write-Host ""

# Validate YAML syntax
if ($ValidateYaml -or $All) {
    Write-Host "Validating YAML syntax..." -ForegroundColor Yellow
    
    # Check if yamllint or similar is available
    $yamlValidator = Get-Command yamllint -ErrorAction SilentlyContinue
    if ($yamlValidator) {
        foreach ($workflow in $requiredWorkflows) {
            if (Test-Path $workflow) {
                yamllint $workflow
                if ($LASTEXITCODE -ne 0) {
                    $hasErrors = $true
                }
            }
        }
    } else {
        Write-Host "  ⚠ yamllint not found, skipping YAML validation" -ForegroundColor Yellow
        Write-Host "    Install with: pip install yamllint" -ForegroundColor Gray
    }
    
    Write-Host ""
}

# Check for required secrets (informational)
if ($CheckSecrets -or $All) {
    Write-Host "Checking required secrets..." -ForegroundColor Yellow
    Write-Host "  Required secrets (configure in GitHub):" -ForegroundColor White
    Write-Host "    - NETSPARKLE_PRIVATE_KEY" -ForegroundColor Gray
    Write-Host "    - NETSPARKLE_PUBLIC_KEY (optional)" -ForegroundColor Gray
    Write-Host ""
    Write-Host "  To check secrets, go to:" -ForegroundColor White
    Write-Host "    https://github.com/th3w1zard1/HoloPatcher.NET/settings/secrets/actions" -ForegroundColor Gray
    Write-Host ""
}

# Check UpdateManager configuration
Write-Host "Checking UpdateManager configuration..." -ForegroundColor Yellow
$updateManagerPath = "src/HoloPatcher/UpdateManager.cs"
if (Test-Path $updateManagerPath) {
    $content = Get-Content $updateManagerPath -Raw
    if ($content -match 'Ed25519PublicKey\s*=\s*""') {
        Write-Host "  ⚠ Ed25519PublicKey is empty in UpdateManager.cs" -ForegroundColor Yellow
        Write-Host "    Update with your public key from setup script" -ForegroundColor Gray
    } elseif ($content -match 'Ed25519PublicKey\s*=\s*"[^"]+"') {
        Write-Host "  ✓ Ed25519PublicKey is configured" -ForegroundColor Green
    } else {
        Write-Host "  ⚠ Could not verify Ed25519PublicKey configuration" -ForegroundColor Yellow
    }
} else {
    Write-Host "  ✗ UpdateManager.cs not found" -ForegroundColor Red
    $hasErrors = $true
}

Write-Host ""

# Check .gitignore
Write-Host "Checking .gitignore..." -ForegroundColor Yellow
if (Test-Path ".gitignore") {
    $gitignore = Get-Content ".gitignore" -Raw
    if ($gitignore -match "keys/") {
        Write-Host "  ✓ keys/ is in .gitignore" -ForegroundColor Green
    } else {
        Write-Host "  ⚠ keys/ not found in .gitignore" -ForegroundColor Yellow
    }
    
    if ($gitignore -match "appcast\.xml") {
        Write-Host "  ✓ appcast.xml is in .gitignore" -ForegroundColor Green
    } else {
        Write-Host "  ⚠ appcast.xml not found in .gitignore" -ForegroundColor Yellow
    }
} else {
    Write-Host "  ⚠ .gitignore not found" -ForegroundColor Yellow
}

Write-Host ""

# Summary
if ($hasErrors) {
    Write-Host "✗ Validation failed - fix errors above" -ForegroundColor Red
    exit 1
} else {
    Write-Host "✓ All checks passed!" -ForegroundColor Green
    Write-Host ""
    Write-Host "Next steps:" -ForegroundColor Cyan
    Write-Host "  1. Generate NetSparkle keys: .\scripts\setup-netsparkle-keys.ps1 -Export" -ForegroundColor White
    Write-Host "  2. Add secrets to GitHub repository settings" -ForegroundColor White
    Write-Host "  3. Update UpdateManager.cs with public key" -ForegroundColor White
    Write-Host "  4. Push workflows and test with a PR" -ForegroundColor White
}

