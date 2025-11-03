# Muh2 VS Code Extension

A VS Code extension for Cashbox muh2 files, providing syntax highlighting and intelligent code completion.

## Features

- **Syntax Highlighting**: Full syntax highlighting for `.muh2` files
- **Code Completion**: Intelligent autocompletion for cashbox entries
- **Real-time Feedback**: Dedicated output channel for debugging and logging

## Create release

* Update `"version": "0.0.1",` in [package.json](package.json).
* `git tag v0.0.1 && git push origin v0.0.1`

Might be also helpful: remote a tag
* `git tag -d v0.0.1 && git push --delete origin v0.0.1`

## Installation

### Quick Install (One-liner)

```bash
curl -sSL https://raw.githubusercontent.com/hmaerki/maerki_util_cashbox_muh2/main/install.sh | bash
```

### Manual Installation

1. **Prerequisites:**
   - VS Code installed with `code` command available
   - Node.js (version 18+)
   - npm
   - git

2. **Install steps:**
   ```bash
   git clone https://github.com/hmaerki/maerki_util_cashbox_muh2.git
   cd maerki_util_cashbox_muh2
   npm install
   npm run compile
   npm install @vscode/vsce
   npx @vscode/vsce package
   code --install-extension muh2-0.0.1.vsix
   ```

3. **Restart VS Code** to activate the extension

### Install from Release (when available)

```bash
# Download and install latest release
wget https://github.com/hmaerki/maerki_util_cashbox_muh2/releases/latest/download/muh2-0.0.2.vsix
code --install-extension muh2-0.0.2.vsix
```

Or use curl:
```bash
# Download and install latest release with curl
curl -L -o muh2-0.0.2.vsix https://github.com/hmaerki/maerki_util_cashbox_muh2/releases/latest/download/muh2-0.0.2.vsix
code --install-extension muh2-0.0.2.vsix
```

One-liner with automatic cleanup:
```bash
# Download, install, and cleanup
wget https://github.com/hmaerki/maerki_util_cashbox_muh2/releases/latest/download/muh2-0.0.2.vsix && code --install-extension muh2-0.0.2.vsix && rm muh2-0.0.2.vsix
```

## Usage

1. Open any `.muh2` file in VS Code
2. Enjoy syntax highlighting
3. Use autocompletion by typing journal entries like:
   ```
   2024-01-01A b 123.45 
   ```
4. View extension output in: `View` → `Output` → "Muh2 Extension"

## Requirements

Include if your extension adds any VS Code settings through the `contributes.configuration` extension point.

For example:

This extension contributes the following settings:

* `myExtension.enable`: Enable/disable this extension.
* `myExtension.thing`: Set to `blah` to do something.

## Known Issues

Calling out known issues can help limit users opening duplicate issues against your extension.

## Release Notes

Users appreciate release notes as you update your extension.

### 1.0.0

Initial release of ...

### 1.0.1

Fixed issue #.

### 1.1.0

Added features X, Y, and Z.

---

## Working with Markdown

You can author your README using Visual Studio Code. Here are some useful editor keyboard shortcuts:

* Split the editor (`Cmd+\` on macOS or `Ctrl+\` on Windows and Linux).
* Toggle preview (`Shift+Cmd+V` on macOS or `Shift+Ctrl+V` on Windows and Linux).
* Press `Ctrl+Space` (Windows, Linux, macOS) to see a list of Markdown snippets.

## For more information

* [Visual Studio Code's Markdown Support](http://code.visualstudio.com/docs/languages/markdown)
* [Markdown Syntax Reference](https://help.github.com/articles/markdown-basics/)

**Enjoy!**
