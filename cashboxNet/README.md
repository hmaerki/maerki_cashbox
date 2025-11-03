# cashboxNet

## Add binaries

Reset binaries to git

```bash
rm -rf cashboxNet/bin cashboxNext/binCore cashboxNet/obj cashboxNet/src/obj/ && git checkout .
```

First build using `Terminal -> Run Task... -> cashbox build debug`

Now add the required binaries to git:

For windows:
```bash
git add -f \
cashboxNet/bin/Debug/net9.0/cashboxNet.dll \
cashboxNet/bin/Debug/net9.0/cashboxNet.exe \
cashboxNet/bin/Debug/net9.0/cashboxNet.runtimeconfig.json \
cashboxNet/bin/Debug/net9.0/CommandLine.dll \
cashboxNet/bin/Debug/net9.0/CSScriptLib.dll \
cashboxNet/bin/Debug/net9.0/Microsoft.CodeAnalysis.CSharp.dll \
cashboxNet/bin/Debug/net9.0/Microsoft.CodeAnalysis.dll \
cashboxNet/bin/Debug/net9.0/Microsoft.Extensions.Logging.Abstractions.dll \
cashboxNet/bin/Debug/net9.0/MigraDoc.DocumentObjectModel.dll \
cashboxNet/bin/Debug/net9.0/PdfSharp.dll
```

## Install Windows

```bash
dotnet --list-sdks
...
9.0.306 [C:\Program Files\dotnet\sdk]
```

https://dotnet.microsoft.com/en-us/download/dotnet/thank-you/sdk-9.0.306-windows-x64-installer

This will download `dotnet-sdk-9.0.306-win-x64.exe`. Install it!

## Install Ubuntu

```bash
dotnet --list-sdks
...
9.0.111 [/usr/lib/dotnet/sdk]
```

```bash
sudo apt-get install -y dotnet-sdk-9.0
```

## Install Muh2-Extension for VSCode

Download [muh2-0.0.1.vsix](https://github.com/hmaerki/maerki_util_cashbox_muh2/releases/download/v0.0.1/muh2-0.0.1.vsix).

In VSCode `Extensions symbol on the left -> ... -> Install from VSIX`. Now select `muh2-0.0.1.vsix` and install.
