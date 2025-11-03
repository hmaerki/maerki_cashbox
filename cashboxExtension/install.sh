#!/bin/bash

# Install muh2 VS Code Extension from GitHub
# Usage: curl -sSL https://raw.githubusercontent.com/hmaerki/maerki_util_cashbox_muh2/main/install.sh | bash

set -e

REPO_URL="https://github.com/hmaerki/maerki_util_cashbox_muh2.git"
TEMP_DIR=$(mktemp -d)
EXTENSION_NAME="muh2"

echo "Installing $EXTENSION_NAME VS Code extension from GitHub..."

# Check if VS Code is installed
if ! command -v code &> /dev/null; then
    echo "Error: VS Code 'code' command not found. Please install VS Code and ensure 'code' is in your PATH."
    exit 1
fi

# Check if Node.js is installed
if ! command -v node &> /dev/null; then
    echo "Error: Node.js not found. Please install Node.js (version 18+)."
    exit 1
fi

# Check if npm is installed
if ! command -v npm &> /dev/null; then
    echo "Error: npm not found. Please install npm."
    exit 1
fi

echo "Cloning repository..."
git clone "$REPO_URL" "$TEMP_DIR"
cd "$TEMP_DIR"

echo "Installing dependencies..."
npm install

echo "Compiling TypeScript..."
npm run compile

echo "Installing vsce..."
npm install @vscode/vsce

echo "Packaging extension..."
npx @vscode/vsce package

echo "Installing extension in VS Code..."
VSIX_FILE=$(ls *.vsix | head -n1)
code --install-extension "$VSIX_FILE"

echo "Cleaning up..."
cd /
rm -rf "$TEMP_DIR"

echo "✅ $EXTENSION_NAME extension installed successfully!"
echo "Restart VS Code to activate the extension."
echo "Open a .muh2 file to test syntax highlighting and completion."