import * as vscode from 'vscode';
const fs = require('fs');
const path = require('path');

// Global output channel
let outputChannel: vscode.OutputChannel;

// Global terminal for reuse
let cashboxTerminal: vscode.Terminal | undefined;

export function activate(context: vscode.ExtensionContext) {
    // Create output channel for the extension
    outputChannel = vscode.window.createOutputChannel('Muh2 Cashbox Extension');
    outputChannel.appendLine('Muh2 Cashbox Extension activated');

    // Register commands
    const runCommand = vscode.commands.registerCommand('cashbox.run', () => {
        runCashboxCommand();
    });

    const pdfCommand = vscode.commands.registerCommand('cashbox.pdf', () => {
        generatePdfCommand();
    });

    // Register completion provider for muh2 language
    const provider = vscode.languages.registerCompletionItemProvider(
        { language: 'muh2' },
        new Muh2CompletionProvider(outputChannel),
        ' ' // Trigger on space
    );

    context.subscriptions.push(runCommand);
    context.subscriptions.push(pdfCommand);
    context.subscriptions.push(provider);
    context.subscriptions.push(outputChannel);
}

function getOrCreateCashboxTerminal(cwd: string): vscode.Terminal {
    // Check if the existing terminal is still valid
    if (cashboxTerminal && cashboxTerminal.exitStatus === undefined) {
        // Terminal is still active, return it
        return cashboxTerminal;
    }
    
    // Create a new terminal
    cashboxTerminal = vscode.window.createTerminal({
        name: 'Cashbox Run',
        cwd: cwd
    });
    
    return cashboxTerminal;
}

function runCashboxCommandCommon(args: string) {
    outputChannel.appendLine('Running Cashbox command...');

    function error_msg(err: string) {
        outputChannel.appendLine(`ERROR: ${err}`);
        vscode.window.showErrorMessage(err);
    }

    // Check if the active file is cashbox_journal.muh2
    const activeEditor = vscode.window.activeTextEditor;
    if (!activeEditor) {
        error_msg('No active editor found. Expect "cashbox_journal.muh2" to be the topmost window.');
        return;
    }

    const activeFileName = path.basename(activeEditor.document.uri.fsPath);
    outputChannel.appendLine(`DEBUG: activeFileName=${activeFileName}`);
    if (activeFileName !== 'cashbox_journal.muh2') {
        error_msg(`Expect 'cashbox_journal.muh2' as topmost window!`);
        return;
    }

    // Retrieve cashbox binary and arguments
    // Retrieve the path to the cashbox binary from a file called 'cashbox_run.path'
    const cashbox_run_path = 'cashbox_run.path';
    const cwd = path.dirname(activeEditor.document.uri.fsPath);
    outputChannel.appendLine(`DEBUG: cwd=${cwd}`);
    const cashboxFilePath = path.join(cwd, cashbox_run_path);
    outputChannel.appendLine(`DEBUG: cashboxFilePath=${cashboxFilePath}`);
    if (!fs.existsSync(cashboxFilePath)) {
        error_msg(`${cashboxFilePath}: Does not exist!`);
        return;
    }

    let cashbox_binary: string;
    try {
        cashbox_binary = fs.readFileSync(cashboxFilePath, 'utf8').trim();
    } catch (error) {
        error_msg(`Failed to read ${cashboxFilePath}: ${error}`);
        return;
    }
    outputChannel.appendLine(`DEBUG: cashboxFilePath=${cashboxFilePath} contains: cashbox_binary=${cashbox_binary}`);
    if (process.platform === 'win32' && !cashbox_binary.endsWith('.exe')) {
        cashbox_binary += '.exe';
    }
    let cashbox_binary_full = cashbox_binary;
    if (cashbox_binary.startsWith('.')) {
        cashbox_binary_full = path.resolve(path.join(cwd, cashbox_binary));
    }
    outputChannel.appendLine(`DEBUG: cashbox_binary_full=${cashbox_binary_full}`);
    if (!fs.existsSync(cashbox_binary_full)) {
        error_msg(`${cashbox_binary_full}: Does not exist! (Read from file "${cashbox_run_path}")`);
        return;
    }
    const cmd = `${cashbox_binary_full} ${args}`;
    outputChannel.appendLine(`DEBUG: cmd=${cmd}`);

    // Get or create terminal and run cashbox
    const terminal = getOrCreateCashboxTerminal(cwd);
    terminal.sendText(cmd);
    terminal.show();

    outputChannel.appendLine(`Executed: cwd=${cwd}, cmd=${cmd}`);
    // vscode.window.showInformationMessage('Cashbox run command started in terminal');
}
function runCashboxCommand() {
    runCashboxCommandCommon("--tags --txt --html")
}

function generatePdfCommand() {
    runCashboxCommandCommon("--tags --txt --html --pdf")
}

class Muh2CompletionProvider implements vscode.CompletionItemProvider {
    private outputChannel: vscode.OutputChannel;

    constructor(outputChannel: vscode.OutputChannel) {
        this.outputChannel = outputChannel;
    }

    provideCompletionItems(
        document: vscode.TextDocument,
        position: vscode.Position,
        token: vscode.CancellationToken,
        context: vscode.CompletionContext
    ): vscode.CompletionItem[] {
        // Log when the method is called
        this.outputChannel.appendLine('Hello Muh2 - provideCompletionItems() called');

        // Get the current line
        const line = document.lineAt(position.line);
        const lineText = line.text;

        let filename = 'codecompletion_buchungen.json'
        let completionItems: vscode.CompletionItem[] = [];
        try {
            const jsonPath = path.join(path.dirname(document.uri.fsPath), filename);
            this.outputChannel.appendLine(`${filename}: Loading`);

            const jsonData = JSON.parse(fs.readFileSync(jsonPath, 'utf8'));
            if (Array.isArray(jsonData)) {
                completionItems = jsonData.map(item =>
                    this.createCompletionItem(item.label || item.id, item.detail || filename)
                );
                this.outputChannel.appendLine(`${filename}: ${completionItems.length} buchungen`);
            }
        } catch (error) {
            this.outputChannel.appendLine(`${filename}: ERROR: Failed to read: ${error}`);
        }

        // Check if we're in a journal entry context
        const journalEntryRegex = /^\d{4}-\d{2}-\d{2}\w+\s+(b|f|vorschlag)\s+\-?\d+\.\d+\s+$/;
        const textBeforeCursor = lineText.substring(0, position.character);

        // Only provide completions if we're at the identifier position
        if (journalEntryRegex.test(textBeforeCursor)) {
            this.outputChannel.appendLine(`Providing ${completionItems.length} completions for journal entry pattern`);
            return completionItems;
        }

        // Also check for partial matches when user starts typing
        const partialRegex = /^\d{4}-\d{2}-\d{2}\w+\s+(b|f|vorschlag)\s+\-?\d+\.\d+\s+[a-zA-Z_-]*$/;
        if (partialRegex.test(textBeforeCursor)) {
            this.outputChannel.appendLine(`Providing ${completionItems.length} completions for partial pattern`);
            return completionItems;
        }

        this.outputChannel.appendLine('No completion pattern matched, returning empty list');
        return [];
    }

    private createCompletionItem(label: string, detail: string): vscode.CompletionItem {
        const item = new vscode.CompletionItem(label, vscode.CompletionItemKind.Value);
        item.detail = detail;
        item.insertText = label;
        return item;
    }
}

export function deactivate() { }