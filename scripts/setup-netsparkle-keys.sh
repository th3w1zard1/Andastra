#!/bin/bash
# Setup script for NetSparkle keys
# Generates Ed25519 keys and provides instructions for GitHub Secrets

set -e

EXPORT=false
OUTPUT_DIR="keys"

while [[ $# -gt 0 ]]; do
    case $1 in
        --export)
            EXPORT=true
            shift
            ;;
        --output-dir)
            OUTPUT_DIR="$2"
            shift 2
            ;;
        *)
            echo "Unknown option: $1"
            exit 1
            ;;
    esac
done

echo "NetSparkle Key Generation Setup"
echo "================================"
echo ""

# Check if NetSparkle tools are installed
echo "Checking for NetSparkle tools..."
if ! dotnet tool list -g | grep -q "NetSparkleUpdater.Tools.AppCastGenerator"; then
    echo "Installing NetSparkle tools..."
    dotnet tool install -g NetSparkleUpdater.Tools.AppCastGenerator
fi

echo "✓ NetSparkle tools installed"
echo ""

# Create output directory
mkdir -p "$OUTPUT_DIR"

# Generate keys
echo "Generating Ed25519 keys..."

# Check if netsparkle-generate-appcast is in PATH
if ! command -v netsparkle-generate-appcast &> /dev/null; then
    # Try dotnet tools path
    DOTNET_TOOLS_PATH="$HOME/.dotnet/tools"
    if [ -f "$DOTNET_TOOLS_PATH/netsparkle-generate-appcast" ]; then
        export PATH="$PATH:$DOTNET_TOOLS_PATH"
    else
        echo "Error: netsparkle-generate-appcast not found in PATH"
        echo "Please ensure NetSparkle tools are installed: dotnet tool install -g NetSparkleUpdater.Tools.AppCastGenerator"
        exit 1
    fi
fi

if [ "$EXPORT" = true ]; then
    KEY_OUTPUT=$(netsparkle-generate-appcast --generate-keys --export true 2>&1)
    
    # Extract keys from output
    PUBLIC_KEY=$(echo "$KEY_OUTPUT" | grep "Public Key:" | sed 's/Public Key: //' | tr -d '[:space:]')
    PRIVATE_KEY=$(echo "$KEY_OUTPUT" | grep "Private Key:" | sed 's/Private Key: //' | tr -d '[:space:]')
else
    # Keys are in default location
    KEYS_DIR="$HOME/.netsparkle"
    PUBLIC_KEY_FILE="$KEYS_DIR/public_key.txt"
    PRIVATE_KEY_FILE="$KEYS_DIR/private_key.txt"
    
    netsparkle-generate-appcast --generate-keys
    
    if [ -f "$PUBLIC_KEY_FILE" ]; then
        PUBLIC_KEY=$(cat "$PUBLIC_KEY_FILE" | tr -d '[:space:]')
        cp "$PUBLIC_KEY_FILE" "$OUTPUT_DIR/public_key.txt"
    fi
    
    if [ -f "$PRIVATE_KEY_FILE" ]; then
        PRIVATE_KEY=$(cat "$PRIVATE_KEY_FILE" | tr -d '[:space:]')
        cp "$PRIVATE_KEY_FILE" "$OUTPUT_DIR/private_key.txt"
    fi
fi

if [ -z "$PUBLIC_KEY" ] || [ -z "$PRIVATE_KEY" ]; then
    echo "Failed to extract keys"
    exit 1
fi

echo "✓ Keys generated successfully"
echo ""

# Display keys
echo "Generated Keys:"
echo "==============="
echo ""
echo "Public Key (use in UpdateManager.cs):"
echo "$PUBLIC_KEY"
echo ""
echo "Private Key (use in GitHub Secrets as NETSPARKLE_PRIVATE_KEY):"
echo "$PRIVATE_KEY"
echo ""

# Save to files
echo "$PUBLIC_KEY" > "$OUTPUT_DIR/public_key.txt"
echo "$PRIVATE_KEY" > "$OUTPUT_DIR/private_key.txt"

echo "Keys saved to:"
echo "  Public:  $OUTPUT_DIR/public_key.txt"
echo "  Private: $OUTPUT_DIR/private_key.txt"
echo ""

# Instructions
echo "Next Steps:"
echo "==========="
echo ""
echo "1. Add Public Key to UpdateManager.cs:"
echo "   Update src/HoloPatcher/UpdateManager.cs:"
echo "   Ed25519PublicKey = \"$PUBLIC_KEY\""
echo ""
echo "2. Add Private Key to GitHub Secrets:"
echo "   - Go to: https://github.com/th3w1zard1/HoloPatcher.NET/settings/secrets/actions"
echo "   - Click 'New repository secret'"
echo "   - Name: NETSPARKLE_PRIVATE_KEY"
echo "   - Value: $PRIVATE_KEY"
echo ""
echo "3. (Optional) Add Public Key as secret for reference:"
echo "   - Name: NETSPARKLE_PUBLIC_KEY"
echo "   - Value: $PUBLIC_KEY"
echo ""
echo "⚠️  IMPORTANT: Keep the private key secure! Never commit it to the repository."
echo ""

