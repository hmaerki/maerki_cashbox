
```bash
docker build -t yeoman .
mkdir app
docker run -it --rm -v $(pwd)/app:/app --user $(id -u):$(id -g) yeoman
```

```
? What type of extension do you want to create? New Language Support
Enter the URL (http, https) or the file path of the tmLanguage grammar or press ENTER to start with a new grammar.
? URL or file to import, or none for new: 
? What's the name of your extension? muh2
? What's the identifier of your extension? muh2
? What's the description of your extension? Cashbox muh2
Enter the id of the language. The id is an identifier and is single, lower-case name such as 'php', 'javascript'
? Language id: muh2
Enter the name of the language. The name will be shown in the VS Code editor mode selector.
? Language name: Cashbox muh2
Enter the file extensions of the language. Use commas to separate multiple entries (e.g. .ruby, .rb)
? File extensions: .muh2
Enter the root scope name of the grammar (e.g. source.ruby)
? Scope names: source.muh2
? Initialize a git repository? Yes

Writing in /app/muh2...
   create muh2/syntaxes/muh2.tmLanguage.json
   create muh2/.vscode/launch.json
   create muh2/package.json
   create muh2/README.md
   create muh2/CHANGELOG.md
   create muh2/vsc-extension-quickstart.md
   create muh2/language-configuration.json
   create muh2/.vscodeignore
   create muh2/.gitignore
   create muh2/.gitattributes

Changes to package.json were detected.
Skipping package manager install.


Your extension muh2 has been created!

To start editing with Visual Studio Code, use the following commands:

     code muh2

Open vsc-extension-quickstart.md inside the new extension for further instructions
on how to modify, test and publish your extension.

For more information, also visit http://code.visualstudio.com and follow us @code.
```

## Manual steps required to build the extension

### Prerequisites
- Node.js (version 18 or higher)
- npm (comes with Node.js)
- VS Code

### Build Steps

1. **Install dependencies:**
   ```bash
   npm install
   ```

2. **Compile TypeScript:**
   ```bash
   npm run compile
   ```
   
   Or for continuous compilation during development:
   ```bash
   npm run watch
   ```

3. **Test the extension:**
   - Press `F5` in VS Code to launch Extension Development Host
   - Open a `.muh2` file in the new VS Code window
   - Test syntax highlighting and code completion

4. **Package the extension (optional):**
   ```bash
   # Install vsce locally if not already installed
   npm install @vscode/vsce
   
   # Package the extension
   npx @vscode/vsce package
   ```
   This creates a `.vsix` file that can be installed in VS Code.

## Installation Options

### Option 1: Install from GitHub Repository (Direct)

1. **Clone and build locally:**
   ```bash
   git clone https://github.com/hmaerki/maerki_cashbox.git
   cd maerki_cashbox/cashboxExtension
   npm install
   npm run compile
   npx @vscode/vsce package
   code --install-extension muh2-0.0.1.vsix
   ```

2. **Or use the VS Code command line:**
   ```bash
   # Install directly from GitHub (requires git and build tools)
   git clone https://github.com/hmaerki/maerki_cashbox.git
   cd maerki_cashbox/cashboxExtension
   npm install && npm run compile
   code --install-extension .
   ```

### Option 2: Install Pre-built VSIX from Releases

If you create releases on GitHub with pre-built `.vsix` files:

```bash
# Download the .vsix file from GitHub releases
wget https://github.com/hmaerki/maerki_util_cashbox_muh2/releases/download/v0.0.1/muh2-0.0.1.vsix
code --install-extension muh2-0.0.1.vsix
```

### Option 3: Publish to VS Code Marketplace

1. **Setup publisher account:**
   ```bash
   npm install -g vsce
   vsce create-publisher hmaerki
   ```

2. **Publish extension:**
   ```bash
   vsce publish
   ```

3. **Install from marketplace:**
   - Open VS Code
   - Go to Extensions (Ctrl+Shift+X)
   - Search for "muh2" by "hmaerki"
   - Click Install

### Development Workflow

- **Edit syntax highlighting:** Modify `syntaxes/muh2.tmLanguage.json`
- **Edit language config:** Modify `language-configuration.json`  
- **Edit completion provider:** Modify `src/extension.ts` then run `npm run compile`
- **Test changes:** Press `F5` to launch new Extension Development Host

### File Structure
```
├── src/extension.ts          # Main extension code (completion provider)
├── syntaxes/                 # TextMate grammar files
├── language-configuration.json # Language configuration
├── package.json              # Extension manifest
├── tsconfig.json             # TypeScript configuration
└── out/                      # Compiled JavaScript (generated)
```

