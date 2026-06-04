# Simple Notepad

A faithful, lightweight clone of the classic Windows Notepad — built with
**.NET 10** and **Avalonia (XAML)** so it runs natively on **Linux**, Windows,
and macOS. It follows your system light/dark theme and ships with a desktop
entry and icon.

> There is also a Windows-only **.NET Framework / WinForms** version kept for
> reference in [`winforms-dotnet-framework/`](winforms-dotnet-framework/). It
> only builds on Windows.

## Features

Everything the original Notepad does, and nothing it doesn't:

- **File** — New, Open, Save, Save As, Exit, with unsaved-changes prompts and an
  `Untitled` default document.
- **Edit** — Undo, Redo, Cut, Copy, Paste, Delete, Find, Find Next, Replace,
  Go To line, Select All, and Time/Date (F5).
- **Format** — Word Wrap toggle and a Font picker.
- **View** — Zoom in/out/reset and a toggleable status bar showing
  `Ln / Col`, zoom level, line endings, and encoding.
- **Help** — About box.
- Standard keyboard shortcuts (Ctrl+N/O/S, Ctrl+F/H/G, F3, F5, Ctrl + +/−/0).
- Follows the system theme (light/dark).
- Opens a file passed on the command line.

## Install (Linux)

**One line — installs the .NET runtime and the app:**

```bash
curl -fsSL https://raw.githubusercontent.com/billsecond/simple-notepad-linux/main/get.sh | bash
```

This clones the repo and runs `install.sh`, which automatically installs the
.NET 10 SDK into `~/.dotnet` if it isn't already present, then builds and
installs Simple Notepad.

**Or via APT** (true `apt install`, with `apt update` upgrades):

```bash
sudo install -d /etc/apt/keyrings
curl -fsSL https://billsecond.github.io/simple-notepad-linux/pubkey.gpg \
  | sudo tee /etc/apt/keyrings/wdnotepad.gpg >/dev/null
echo "deb [signed-by=/etc/apt/keyrings/wdnotepad.gpg] https://billsecond.github.io/simple-notepad-linux ./" \
  | sudo tee /etc/apt/sources.list.d/wdnotepad.list
sudo apt update
sudo apt install wdnotepad
```

**Or from a local checkout:**

```bash
./install.sh          # per-user install into ~/.local (no root needed)
sudo ./install.sh     # system-wide install into /usr/local
```

After installing, run `notepad` (or `wdnotepad`) from a terminal or launch
"Simple Notepad" from your application menu.

The installer publishes a **self-contained** build (so the result runs without
needing the .NET SDK/runtime installed), then:

- installs the app under `~/.local/share/simple-notepad/`,
- creates four launchers on your `PATH` — `notepad`, `Notepad`, `notepad.exe`,
  `Notepad.exe` — each of which works with or without a filename argument,
- registers a desktop entry + icon (`simple-notepad.desktop`), and
- **prompts** whether to make Notepad the default app for common text files.

Then just run, for example:

```bash
notepad                 # empty "Untitled" document
notepad.exe notes.txt   # open a file
Notepad ~/todo          # any capitalization, any/no extension
```

> If the installer warns that `~/.local/bin` isn't on your `PATH`, add
> `export PATH="$HOME/.local/bin:$PATH"` to your `~/.bashrc` and restart the shell.

To remove everything:

```bash
./uninstall.sh          # or: sudo ./uninstall.sh for a system install
```

## Building / running manually

Requires the .NET 10 SDK (`dotnet`).

```bash
dotnet run -c Release                 # build & run
dotnet run -c Release -- notes.txt    # open a file
dotnet build -c Release               # just build -> bin/Release/net10.0/
```

## Project layout

| File | Purpose |
| --- | --- |
| `Program.cs` | Avalonia entry point. |
| `App.axaml(.cs)` | Application setup, theme, command-line file argument. |
| `MainWindow.axaml(.cs)` | The editor window — menus, status bar, all commands. |
| `FindReplaceWindow.cs` | Modeless Find / Replace tool window. |
| `Dialogs.cs` | Message box, Go To, and Font dialogs. |
| `Assets/notepad.png` | Application / window icon. |
| `install.sh` / `uninstall.sh` | Linux installer and uninstaller. |
| `Notepad.csproj` | Build configuration (.NET 10 + Avalonia). |
| `winforms-dotnet-framework/` | Reference Windows-only WinForms version. |
