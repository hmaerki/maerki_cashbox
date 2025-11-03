# VSCode Extension for `cashbox_journal.muh2` files

A VS Code extension for Cashbox muh2 files, providing syntax highlighting and intelligent code completion.

It is intended to be used with the cashbox bookkeeping software.

## Features

- **Syntax Highlighting**: Full syntax highlighting for `.muh2` files
- **Code Completion**: Intelligent autocompletion for cashbox entries

## User

Main entry point for cashbox: https://github.com/hmaerki/maerki_cashbox/blob/main/README.md

## Developer

### Build and deploy

```bash
# Install npm
sudo apt-get npm
cd cashboxExtension
npm install
```

```bash
# Compile and Deploy
cd cashboxExtension
npm run compile && npx @vscode/vsce package && code --force --install-extension muh2-0.0.1.vsix
```

The debug output is in `OUTPUT -> Muh2 Cashbox Extension`.
