#!/bin/bash
# Fable SDK Generation Script
# Run this script to generate multi-language SDKs from F# source

set -e

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PROJECT_DIR="$SCRIPT_DIR/Ocis.Client.SDK"

echo "=== Fable SDK Generator ==="
echo ""

# Check if fable is available
if ! command -v fable &> /dev/null; then
    echo "Error: fable not found in PATH"
    echo "Install it with: dotnet tool install -g fable"
    exit 1
fi

echo "Fable version:"
fable --version
echo ""

# Ensure output directories exist
mkdir -p "$SCRIPT_DIR/sdk/ts"
mkdir -p "$SCRIPT_DIR/sdk/py"
mkdir -p "$SCRIPT_DIR/sdk/dart"
mkdir -p "$SCRIPT_DIR/sdk/rust"

# Generate TypeScript SDK
echo "Generating TypeScript SDK..."
fable "$PROJECT_DIR" --lang typescript -o "$SCRIPT_DIR/sdk/ts" --extension .ts
echo "TypeScript SDK generated: $SCRIPT_DIR/sdk/ts"
echo ""

# Generate Python SDK
echo "Generating Python SDK..."
fable "$PROJECT_DIR" --lang python -o "$SCRIPT_DIR/sdk/py"
echo "Python SDK generated: $SCRIPT_DIR/sdk/py"
echo ""

# Generate dart SDK
echo "Generating Python SDK..."
fable "$PROJECT_DIR" --lang dart -o "$SCRIPT_DIR/sdk/dart"
echo "Python SDK generated: $SCRIPT_DIR/sdk/dart"
echo ""

# Generate rust SDK
echo "Generating Python SDK..."
fable "$PROJECT_DIR" --lang rust -o "$SCRIPT_DIR/sdk/rust"
echo "Python SDK generated: $SCRIPT_DIR/sdk/rust"
echo ""

echo "=== SDK Generation Complete ==="
