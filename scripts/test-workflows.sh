#!/bin/bash
# Test script to validate workflow setup
# Run this before pushing workflows to ensure everything is configured correctly

set -e

CHECK_SECRETS=false
VALIDATE_YAML=false
ALL=false

while [[ $# -gt 0 ]]; do
    case $1 in
        --check-secrets)
            CHECK_SECRETS=true
            shift
            ;;
        --validate-yaml)
            VALIDATE_YAML=true
            shift
            ;;
        --all)
            ALL=true
            CHECK_SECRETS=true
            VALIDATE_YAML=true
            shift
            ;;
        *)
            echo "Unknown option: $1"
            exit 1
            ;;
    esac
done

echo "Workflow Validation Test"
echo "========================"
echo ""

HAS_ERRORS=false

# Check workflow files exist
echo "Checking workflow files..."
REQUIRED_WORKFLOWS=(
    ".github/workflows/ci.yml"
    ".github/workflows/test-builds.yml"
    ".github/workflows/build-all-platforms.yml"
    ".github/workflows/release-please.yml"
    ".github/workflows/netsparkle-appcast.yml"
    ".github/workflows/validate-workflows.yml"
)

for workflow in "${REQUIRED_WORKFLOWS[@]}"; do
    if [ -f "$workflow" ]; then
        echo "  ✓ $workflow"
    else
        echo "  ✗ $workflow - MISSING"
        HAS_ERRORS=true
    fi
done

echo ""

# Check configuration files
echo "Checking configuration files..."
REQUIRED_CONFIGS=(
    ".github/release-please-config.json"
    ".github/release-please-manifest.json"
    ".github/RELEASE_TEMPLATE.md"
)

for config in "${REQUIRED_CONFIGS[@]}"; do
    if [ -f "$config" ]; then
        echo "  ✓ $config"
    else
        echo "  ✗ $config - MISSING"
        HAS_ERRORS=true
    fi
done

echo ""

# Validate YAML syntax
if [ "$VALIDATE_YAML" = true ] || [ "$ALL" = true ]; then
    echo "Validating YAML syntax..."
    
    if command -v yamllint &> /dev/null; then
        for workflow in "${REQUIRED_WORKFLOWS[@]}"; do
            if [ -f "$workflow" ]; then
                yamllint "$workflow" || HAS_ERRORS=true
            fi
        done
    else
        echo "  ⚠ yamllint not found, skipping YAML validation"
        echo "    Install with: pip install yamllint"
    fi
    
    echo ""
fi

# Check for required secrets (informational)
if [ "$CHECK_SECRETS" = true ] || [ "$ALL" = true ]; then
    echo "Checking required secrets..."
    echo "  Required secrets (configure in GitHub):"
    echo "    - NETSPARKLE_PRIVATE_KEY"
    echo "    - NETSPARKLE_PUBLIC_KEY (optional)"
    echo ""
    echo "  To check secrets, go to:"
    echo "    https://github.com/th3w1zard1/HoloPatcher.NET/settings/secrets/actions"
    echo ""
fi

# Check UpdateManager configuration
echo "Checking UpdateManager configuration..."
UPDATE_MANAGER_PATH="src/HoloPatcher/UpdateManager.cs"
if [ -f "$UPDATE_MANAGER_PATH" ]; then
    if grep -q 'Ed25519PublicKey\s*=\s*""' "$UPDATE_MANAGER_PATH"; then
        echo "  ⚠ Ed25519PublicKey is empty in UpdateManager.cs"
        echo "    Update with your public key from setup script"
    elif grep -q 'Ed25519PublicKey\s*=\s*"[^"]\+"' "$UPDATE_MANAGER_PATH"; then
        echo "  ✓ Ed25519PublicKey is configured"
    else
        echo "  ⚠ Could not verify Ed25519PublicKey configuration"
    fi
else
    echo "  ✗ UpdateManager.cs not found"
    HAS_ERRORS=true
fi

echo ""

# Check .gitignore
echo "Checking .gitignore..."
if [ -f ".gitignore" ]; then
    if grep -q "keys/" ".gitignore"; then
        echo "  ✓ keys/ is in .gitignore"
    else
        echo "  ⚠ keys/ not found in .gitignore"
    fi
    
    if grep -q "appcast\.xml" ".gitignore"; then
        echo "  ✓ appcast.xml is in .gitignore"
    else
        echo "  ⚠ appcast.xml not found in .gitignore"
    fi
else
    echo "  ⚠ .gitignore not found"
fi

echo ""

# Summary
if [ "$HAS_ERRORS" = true ]; then
    echo "✗ Validation failed - fix errors above"
    exit 1
else
    echo "✓ All checks passed!"
    echo ""
    echo "Next steps:"
    echo "  1. Generate NetSparkle keys: ./scripts/setup-netsparkle-keys.sh --export"
    echo "  2. Add secrets to GitHub repository settings"
    echo "  3. Update UpdateManager.cs with public key"
    echo "  4. Push workflows and test with a PR"
fi

