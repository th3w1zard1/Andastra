#!/bin/bash
# Bash script to copy test files to test_files directory

SOURCE_FILE="${1:-G:/GitHub/PyKotor/vendor/Kotor-Randomizer/kotor Randomizer 2/Resources/k2patch/a_galaxymap.ncs}"
TEST_FILES_DIR="$(dirname "$0")/test_files"
DEST_FILE="$TEST_FILES_DIR/a_galaxymap.ncs"

# Create test_files directory if it doesn't exist
mkdir -p "$TEST_FILES_DIR"

# Copy the test file if source exists
if [ -f "$SOURCE_FILE" ]; then
    cp "$SOURCE_FILE" "$DEST_FILE"
    echo "Copied test file: $DEST_FILE"
else
    echo "Warning: Source file not found: $SOURCE_FILE"
    echo "Please manually copy test NCS files to: $TEST_FILES_DIR"
fi

