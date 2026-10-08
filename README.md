# WinKeyCastr

**English** · [Русский](README.ru.md)

A keystroke visualizer for Windows 11, ported from [KeyCastr](https://github.com/keycastr/keycastr) for macOS.
It shows what you type and click in bezels over the screen, for screencasts, demos and presentations.

The visualizers, preferences, defaults and keystroke formatting follow KeyCastr's source closely.
The bezels can blur the desktop behind them (acrylic).

## Download

Get the latest build from [Releases](https://github.com/nikethebike/WinKeyCastr/releases/latest):

- `WinKeyCastr-win-x64.exe` — self-contained, runs as is.
- `WinKeyCastr-win-x64-framework-dependent.exe` — smaller, needs the [.NET 10 Desktop Runtime](https://dotnet.microsoft.com/download/dotnet/10.0).

Windows SmartScreen may warn about an unsigned app: choose *More info → Run anyway*.

## Requirements

- Windows 11
- .NET 10 SDK (for building) / .NET 10 Desktop Runtime (for running a framework-dependent build)

## Build and run

```powershell
dotnet build
dotnet run --project src/WinKeyCastr
dotnet test
```

Single-file release build:

```powershell
dotnet publish src/WinKeyCastr -c Release -r win-x64 --self-contained false -p:PublishSingleFile=true -o publish
```

## Using it

- KeyCastr lives in the notification area. Click its key-cap icon (with the Windows logo) for **About**, **Preferences…**,
  **Start/Stop Casting** and **Quit**. The icon is filled while casting.
- **Ctrl+Alt+Win+K** toggles casting (KeyCastr's ⌃⌥⌘K). You can change it under *Preferences → General*.
- Drag a bezel to move it. The position is remembered.
- Running the program again opens the preferences of the copy that's already running.
- The interface is in English or Russian: *Preferences → General → Language* (defaults to the system language).

### Visualizers (Preferences → Display)

| Visualizer | What it shows |
| --- | --- |
| **Default** | A stack of bezels in the bottom-left corner. Typing in quick succession shares a bezel; commands, clicks and pauses start a new one. Display mode: *Command Keys Only* (default), *All Modified Keys* or *All Keys*, plus *Apply Modifiers*. Font size, line break delay, linger time, fade duration and colours are adjustable. |
| **Svelte** | A small panel with the last few keystrokes and ⇧ ⌃ ⌥ ⊞ indicators that light up while held. |
| **Minimal** | One bezel showing exactly what is held down right now: modifiers, key and mouse button. |

*Display Mouse Events* adds a ring around the pointer on click, shows clicks in the visualizer, or both.

## How it maps the Mac keyboard to Windows

| macOS | Windows | Shown as |
| --- | --- | --- |
| ⌃ Control | Ctrl | ⌃ |
| ⌥ Option | Alt / AltGr | ⌥ |
| ⇧ Shift | Shift | ⇧ |
| ⌘ Command | Win | ⊞ |

- KeyCastr treats ⌃ and ⌘ chords as *commands*. On Windows, **Alt** chords (Alt+Tab, Alt+F4) count as
  commands too. **AltGr** does not, because it types characters the way the Mac Option key does.
- *Preferences → General → Modifier keys* switches to text labels (`Ctrl+Shift+K`) if your audience
  isn't used to the Mac symbols.
- Keys are shown in the keyboard layout of the window you are typing into, so a Russian layout shows `⌃С`.

## Differences from KeyCastr

- **Acrylic.** Optional blur behind the bezels (*Preferences → Display → Bezel Material*). It uses
  Windows.UI.Composition's host backdrop brush, because the regular Windows 11 acrylic turns solid in
  windows that are not active, and the overlays never are.
- **Notification area / taskbar** instead of menu bar / Dock. With "In the Taskbar" selected, closing
  the preferences window minimizes it to the taskbar, like a Dock icon.
- There is no Sparkle "Update" pane.
- The ⌘ on KeyCastr's key-cap icon is replaced by the Windows logo.
- Windows protects elevated (administrator) windows from non-elevated hooks. To show keystrokes typed
  into elevated apps, run WinKeyCastr as administrator too.

## Project layout

```text
src/WinKeyCastr/
  Input/        global keyboard/mouse hooks, keystroke → text (port of KCEventTransformer)
  Overlay/      always-on-top, non-activating overlay windows and the acrylic backdrop
  Visualizers/  Default, Svelte, Minimal and the mouse-click ring
  Settings/     preferences, saved to %APPDATA%\WinKeyCastr\settings.json
  UI/           preferences and about windows, notification-area icon, colour well, shortcut recorder
tests/WinKeyCastr.Tests/   keystroke formatting tests (ported from KeyCastr) and layout translation tests
```

Debug builds accept `--demo Default|Svelte|Minimal` to play scripted input without touching real
input or saving settings (`--prefs`, `--display` open the preferences).

## Licence

See [THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md) for KeyCastr's BSD licence, which covers the
reused icons.
