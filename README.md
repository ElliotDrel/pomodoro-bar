# Pomodoro Bar

A native Windows app with a small, always-on-top timer, a separate editor, and escalating reminders. Built with C#/.NET 10 and WPF. Version 1 replaces the Python/Tkinter prototype; Python is no longer needed.

## Install

Download **PomodoroBar-win-Setup.exe** from the [latest release](https://github.com/ElliotDrel/pomodoro-bar/releases/latest). Installation is per user without administrator rights, and includes the .NET runtime. Launch Pomodoro Bar from the Start menu or desktop shortcut. Requires 64-bit Windows 10 or later.

Production uses Velopack, matching Universal Spell Check's release flow. It checks on launch and every four hours. Startup downloads and applies an available update only before you interact with the timer or editor; once you start a session, it stages the update for later. Periodic and manual checks download in the background without restarting. The editor and tray offer **Check for updates** and installation of a verified downloaded update; the editor shows progress and last-checked/last-updated times. **Download installer** opens the latest public installer download. Restart starts a fresh paused focus period. The bar stays above ordinary windows; exclusive full-screen apps and secure screens can cover it.

## Floating bar

- **Circle:** progress through the whole focus-plus-break cycle; purple is focus, green is break. Click to dismiss a reminder pulse. At completion, click to start the next section.
- **Countdown:** current section's time, transitioning from white through green, yellow, orange, and red.
- **Play / pause:** start or pause; at completion, start the next section. Space does the same when the bar has focus.
- **Skip:** advance, preserving running or paused state; at completion, start the next section.
- **Pencil:** open the independent editor centered on the primary screen.
- **Drag:** move the bar by its digits or background. Right-click for edit, reset, and quit.

Any bar interaction silences audio. Digits and pencil leave completion flashing; circle, play, and skip also acknowledge completion. The editor shows an acknowledgement button at completion, including when the bar is hidden; the tray also offers **Silence alarm**. Launching again reopens both windows; double-clicking the tray icon reopens the editor even when both windows are hidden.

## Editor

Pomodoro Bar is one app with two independent windows, each available in the taskbar and Alt+Tab. Startup opens both the time-setting editor and floating bar. Launching the app again reopens both hidden windows and restores minimized windows without resetting the timer. Installation provides one desktop shortcut named **Pomodoro Bar** and removes the obsolete **Pomodoro Timer** shortcut when it targets this app.

Closing either window hides only that window and keeps the timer running. Reopen the bar from the tray's **Show floating bar** menu; reopen the editor by launching Pomodoro Bar again or double-clicking its tray icon. **Quit** exits the entire app.

The large time card mirrors the countdown. Click the digits and enter minutes (`15`), `MM:SS` (`12:34`), or `HH:MM:SS` (`1:02:03`). Enter or clicking away saves; Escape cancels. Values range from one second to 24 hours. Saving sets the current section's length and remaining time, preserving running/paused state. The other section keeps its length. The bar keeps counting while the card protects your draft.

**30 minutes**, **60 minutes**, **+1 minute**, **+5 minutes**, and **+10 minutes** create a fresh paused session. Plus buttons preserve a 5:1 focus/break ratio and current reminder percentages. Adding one minute to the 30-minute preset gives 25:50 focus and 05:10 break. Presets/additions allow a maximum 24-hour total.

**Show floating bar** controls visibility independently of the editor; hidden timers keep running. Closing the editor preserves visibility. **Resize floating bar** enables native Windows sizing edges. Drag any edge/corner and turn it off to lock the borderless bar. All content and hit targets scale together. Default is 280 × 52, minimum 130 × 20, maximum 1000 × 120, in Windows device-independent pixels. **Reset bar size** restores the default without moving the bar or changing the timer. **Done** closes the editor; **Quit** exits. Appearance and reminders are handled by code.

## Reminders

| Preset | Focus / break | Remaining focus percentages | Remaining focus time |
| --- | --- | --- | --- |
| 30 minutes | 25 / 5 minutes | 40%, 20% | 10:00, 05:00 |
| 60 minutes | 50 / 10 minutes | 60%, 20%, 10% | 30:00, 10:00, 05:00 |

Reminders throb for four seconds. Sounds are assigned backward: third-last is one softer beep, second-last is a rising two-note sound, last is a stronger three-note sound. The 30-minute preset uses the final two sounds; the 60-minute preset adds the single beep first. Each section's completion loops its beep and flashes at zero until acknowledged. Waiting never consumes the next section's time.

Playback is asynchronous. After sleep or a delayed tick, only the most urgent crossed reminder plays; completion takes precedence. Colors interpolate per section: white at 100%, green at 80%, yellow at 40%, orange at 20%, red at 5%.

## Data and migration

Prod settings: `%LocalAppData%\PomodoroBar.Data\settings.json`. Dev settings: `%LocalAppData%\PomodoroBar.Dev\settings.json`. Shared diagnostic logs: `%LocalAppData%\PomodoroBar.Data\logs`, stamped with channel/version/PID. User settings stay outside the installer-owned `%LocalAppData%\PomodoroBar` directory and survive updates. Reopening starts fresh paused focus with the bar visible.

Import the prototype's durations and geometry on first launch:

```powershell
& "$env:LOCALAPPDATA\PomodoroBar\current\PomodoroBar.exe" --import-settings 'C:\path\to\old\settings.json'
```

Import runs only if native settings do not exist and leaves the source intact. `Launch Pomodoro.vbs` is a compatibility launcher for installed Prod and imports adjacent prototype settings on first launch.

## Development

Install the .NET 10 SDK on Windows, then:

```powershell
dotnet run --project src/PomodoroBar.csproj -c Dev
dotnet run --project tests/PomodoroBar.Tests.csproj -c Release
dotnet build src/PomodoroBar.csproj -c Release
git diff --check
```

`scripts/dev.ps1` starts Dev. Dev and Prod run side by side with separate instance locks, IPC, settings, titles, and tray labels. Dev uses the same logo on a yellow icon background in the executable, windows, and tray. Dev never auto-updates and rejects update/restart commands; pull, rebuild, and relaunch manually. Tests exercise real WPF windows with silent fake audio plus the engine, parser, colors, layout, migration, and updater with fake packages/process helpers. They require a Windows desktop session, never edit real settings, and never play alarms. Append `-- --capture` for ignored screenshots in `artifacts`.

## Releases

Finish verification and commit on `main`, then push `main` and the approved semver tag together. The [release workflow](.github/workflows/release.yml) runs tests, publishes self-contained `win-x64`, runs a bounded silent startup smoke test, packs with Velopack `1.2.161`, then uploads/publishes the GitHub release. Do not manually package production to bypass CI.

Verify successful CI, a public non-draft release and installer/update assets, then install and verify local Prod. Leave Prod running, stop verification Dev instances, and verify every session commit is pushed. See [deployment instructions](.agents/skills/deploy/SKILL.md).

Local installation must run from an ordinary Windows session outside a packaged app such as Codex. Packaged-process AppData virtualization can redirect an otherwise valid installer into the host's private cache. Run `scripts/verify-installation.ps1 -PreflightOnly` before installation, use `scripts/install-prod.ps1 -InstallerPath <published-setup.exe> -ExpectedVersion <version>`, and verify the physical installation and shortcut paths. The GitHub build and native window-icon checks alone do not establish desktop installation correctness.

Installed `PomodoroBar.exe --restart` requests a graceful Velopack restart, then the startup check downloads/applies any newer release; `--update` is an alias, matching Universal Spell Checker. `--check-for-updates` checks and downloads without restarting; it starts the app in manual-check mode if none is running. Commands are forwarded to the existing instance, and a second instance never applies updates before forwarding. Exit zero acknowledges delivery; verify fresh logs and the installed version to confirm completion. `--quit` gracefully closes that channel's existing instance, and succeeds without starting one if none is running. `--startup-smoke` constructs native UI including the tray with silent audio and exits without reading/writing user settings or enabling IPC/updates.

```powershell
& "$env:LOCALAPPDATA\PomodoroBar\current\PomodoroBar.exe" --check-for-updates
& "$env:LOCALAPPDATA\PomodoroBar\current\PomodoroBar.exe" --update
```
