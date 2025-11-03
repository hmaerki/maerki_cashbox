# maerki_cashbox

bookkeeping software cashbox with VSCode extentsion

## Installation for User

Windows (cmd.exe) / Ubuntu (bash)

```bash
git clone https://github.com/hmaerki/maerki_cashbox.git

# Install VSCode Extension
code --force --install-extension cashboxExtension/muh2-0.0.1.vsix
```

**Running cashbox**

Open a `cashbox_journal.muh2` in VSCode.

Press `<shift-ctrl-c>` *cashbox* or `<shift-ctrl-p>` for *cashbox with Pdf*.

You should see
```bash
Do bookkeeping...
Write Txt...
Write Html...
Write Muh-File...
```

## Configuration

Top directory structure:

* ./maerki_cashbox_privat/maerki_informatik/2025/cashbox_journal.muh2
* ./maerki_cashbox/cashboxNet/...

Both git repos `maerki_cashbox_privat` and `maerki_cashbox` are side by side. This allows VSCode to find the cashbox binary.

This mechanims may be overridden with the environment variable `CASHBOX_BINARY` which has to point to `./maerki_cashbox`.

