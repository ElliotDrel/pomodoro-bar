# Pomodoro Bar

[Download for Windows](https://github.com/ElliotDrel/pomodoro-bar/releases/latest/download/PomodoroBar-win-Setup.exe) · [Release notes](https://github.com/ElliotDrel/pomodoro-bar/releases) · [Report a bug](https://github.com/ElliotDrel/pomodoro-bar/issues/new) · [Resource audit](RESOURCE-AUDIT.md)

A free Windows Pomodoro timer that stays visible while you work. Its small, always-on-top bar shows progress, the countdown, play/pause, skip, and an edit button. The separate editor lets you change the timer and resize the bar; escalating sounds remind you as time runs out.

- Windows 10 or later, 64-bit; the installer includes .NET and needs no administrator rights.
- Start with 25 minutes of focus plus a 5-minute break, or 50 plus 10. Edit the current section directly.
- Scale the complete bar down to 130 × 20 DIPs. Hide either window independently while the timer continues.
- Check, download, and install updates in the background from the editor or command line.

Built with C#/.NET 10 and WPF. Version 1 replaces the Python/Tkinter prototype; Python is no longer needed.

See the [version/channel changelog](CHANGELOG.md) for changes between releases and [request checklist](REQUEST-CHECKLIST.md) for everything to verify, including the editable Focus/Break timeline.

The editor places editable focus and break countdowns above a timeline with editable start, focus-end, and break-end times. Valid input updates related values on every keystroke. Duration edits and clock times round to the nearest minute; seconds remain visible while a countdown runs. Set a future start to schedule automatic focus: the floating bar turns pink and counts down to the start, while the editor keeps both durations visible and shows **Starts in**. A past start shows a red error next to the timeline and resets to now.

The timer refreshes visible countdowns once per displayed second and animates only during alerts. Paused bars stay still; the editor's start-now end estimate updates at its next rounded-minute boundary. Hidden/minimized windows receive no routine UI refresh, and when both are minimized the engine waits for the scheduled start, next reminder, or completion. Reopening or resuming Windows catches the timer up immediately. The app has no tray icon. Closing the last visible window exits the process; minimizing keeps it in the taskbar. The retained native tray helper is unused by the app. See the [resource audit](RESOURCE-AUDIT.md) for measured results and limitations.

Record every change under **Unreleased** before committing. Dev gets those changes when `main` is pulled and rebuilt. When releasing Prod, move the shipped notes into a dated version entry and keep the historical entries. Main/PR checks require a changelog update alongside other changed files; release CI requires the matching version entry and publishes its notes on GitHub. Validate locally with `scripts/changelog.ps1` and `tests/Changelog.Tests.ps1`.

## Install

Download **PomodoroBar-win-Setup.exe** from the [latest release](https://github.com/ElliotDrel/pomodoro-bar/releases/latest). Installation is per user without administrator rights, and includes the .NET runtime. Launch Pomodoro Bar from the Start menu or desktop shortcut. Requires 64-bit Windows 10 or later.

Production uses Velopack, matching Universal Spell Check's release flow. It checks on launch and every four hours. Startup downloads and applies an available update only before you interact with the timer or editor; once you start a session, it stages the update for later. Periodic and manual checks download in the background without restarting. The editor offers **Check for updates** and installation of a verified downloaded update; the editor shows progress and last-checked/last-updated times. Installers are available on the public release page. Restart starts a fresh paused focus period. The bar stays above ordinary windows; exclusive full-screen apps and secure screens can cover it.

## Floating bar

- **Thick outline:** progress through the whole focus-plus-break cycle, replacing the old circle. The unfilled outline blends into the panel; only the thicker completed portion uses the current countdown color, including pink for a scheduled start. The editor timer panel has the same perimeter treatment.
- **Countdown:** current section's remaining time on the left. Focus transitions from white through green, yellow, orange, and red; break stays green until completion, then turns red. When scheduled, it shows time until focus starts in pink.
- **Play / pause:** start or pause; at completion, start the next section. Space does the same when the bar has focus. While scheduled, pause cancels the pending start.
- **Skip:** advance, preserving running or paused state; at completion, start the next section.
- **Pencil:** open the independent editor centered on the primary screen. Floating controls respond only inside their visible button bounds; releasing outside cancels the press.
- **Drag:** move the bar by its digits or background. Right-click for edit, reset, and quit.

Any bar interaction silences audio. Digits and pencil leave completion flashing; play and skip also acknowledge completion. The editor shows an acknowledgement button at completion, including when the bar is hidden. Launching again reopens an individually hidden window while the other remains open. No tray or hidden-icons entry is registered.

## Editor

Pomodoro Bar is one app with two independent windows, each available in the taskbar and Alt+Tab. Startup opens both the time-setting editor and floating bar. Launching the app again reopens both hidden windows and restores minimized windows without resetting the timer. Installation provides one desktop shortcut and one Start Menu shortcut named **Pomodoro Bar**. Both target the same native executable, so application search can identify them as one app. The installation helper retains obsolete duplicate launch shortcuts outside Desktop/Start Menu indexing.

Closing one window hides it while the other stays available. Reopen the editor with the bar's pencil or by launching the app again; show the bar using **Show floating bar** in the editor. Closing the last visible window exits the app and stops audio, timers, updates, and IPC. This supersedes the earlier tray-backed behavior that kept the app running after both windows closed. **Quit** in the bar menu also exits. Minimized windows remain open in the taskbar.

The editor uses a graphite instrument panel with white text, an ice-blue focus track, and a green break track. Its timeline shows distinct focus color blocks (white, green, yellow, orange, red), with small gaps and a focus/break boundary. Break remains solid green until completion, when its text, outline progress, and timeline portion turn red. The scheduled **Starts in** countdown is displayed at twice the old label size above pause. The redundant endpoint footer has been removed. Both large duration fields and all three timeline times are directly editable. Buttons and switches have visible keyboard focus. Scrollbars stay hidden; scroll with the mouse wheel or navigate controls with Tab when the window is small.

Both large duration fields are editable: focus is slightly larger, with play/pause and skip between focus and break. Enter minutes (`15`), `MM:SS` (`12:34`), or `HH:MM:SS` (`1:02:03`). Valid input applies immediately, rounded to whole minutes from one minute to 24 hours. Enter, Escape, or clicking away finishes editing; incomplete input restores the current value, while already applied valid changes remain. Running countdown updates do not overwrite the field being typed into. The active section counts down; the other field shows its configured duration.

Editing keeps an ordered set of fields fixed for **8 seconds after the last valid edit**, including when you move between fields or press Enter. The editor displays the order and remaining seconds. Earlier edits take priority: set focus end first, then shorten focus duration to move the start while preserving that end; set break end first, then focus end to adjust the break. Conflicting edits or a derived past start show an error without changing the timer. Wait for the countdown to expire to begin a new order. Presets, additions, and reset clear the order. Focus and break durations are independent; no fixed 5:1 rule applies to custom values. Compact clock input requires four digits so partial typing does not apply an unintended time.

The timeline's **Start**, **Focus ends**, and **Break ends** fields accept clock times (`1000` for `10:00`, `0930` for `9:30`, `5:25 PM`, or `17:25`), `tomorrow 5:25 PM`, or a dated time (`2026-10-15 5:25 PM`). Next-day dates appear under the fields. Editing focus end changes focus and shifts the break estimate; editing break end changes break duration. A future start arms automatic focus. A past start resets to now with a red error. Scheduled starts catch up after sleep or system-clock changes; completion still waits for acknowledgement. Starting the break late moves its actual end beyond the earlier estimate. Scheduled starts are session-only; relaunching the app begins fresh paused focus.

**30 minutes**, **60 minutes**, **+1 minute**, **+5 minutes**, and **+10 minutes** create a fresh paused session. Presets keep 25/5 and 50/10. Additions retain total minutes and reminder percentages, preserving your current focus/break ratio to the nearest whole minute. For example, 20 focus / 10 break plus 30 becomes 40 / 20. Presets still explicitly restore 25/5 or 50/10. Maximum total is 24 hours.

**Show floating bar** controls visibility independently. **Resize using the bar’s edges** enables native sizing; controls and hit targets scale uniformly down to 130 × 20 DIPs horizontally. **Use vertical bar** in the editor switches to an stacked countdown with minutes above seconds (hours appear on a separate line when needed) and controls below, and persists the orientation. **Use horizontal bar** switches back. Horizontal default is 280 × 52, maximum 1000 × 120; vertical default is 64 × 200, minimum 52 × 130, maximum 300 × 1000 DIPs. **Reset size** restores the current orientation's default without moving the bar. Close the editor with **X** or Escape; **Quit** in the bar menu exits the app, as does closing the last visible window.

One **Check for updates** button sits beside the version, status, and last-checked/last-updated times. While checking it reads **Checking for update now** and is disabled. Downloading also disables it and shows progress beside it. When a verified update is ready, the same button becomes **Update and restart**. Failures restore the check action for retry. The floating bar and its right-click menu use the same palette as the editor; the app registers no tray menu. The bar shows its progress outline, countdown, play/pause, skip, and pencil controls. Reminder motion respects the Windows animation preference; completion still requires acknowledgement and its sound loops until silenced.

## Reminders

| Preset | Focus / break | Remaining focus percentages | Remaining focus time |
| --- | --- | --- | --- |
| 30 minutes | 25 / 5 minutes | 40%, 20% | 10:00, 05:00 |
| 60 minutes | 50 / 10 minutes | 60%, 20%, 10% | 30:00, 10:00, 05:00 |

Reminders throb for four seconds. Sounds are assigned backward: third-last is one softer beep, second-last is a rising two-note sound, last is a stronger three-note sound. The 30-minute preset uses the final two sounds; the 60-minute preset adds the single beep first. Each section's completion loops its beep and flashes at zero until acknowledged. Waiting never consumes the next section's time.

Playback is asynchronous. After sleep or a delayed tick, only the most urgent crossed reminder plays; completion takes precedence. Focus colors interpolate: white at 100%, green at 80%, yellow at 40%, orange at 20%, red at 5%. Break stays green for its whole countdown and turns red at completion. The timeline uses discrete focus blocks rather than a gradient.

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

`scripts/dev.ps1` starts Dev. Dev and Prod run side by side with separate instance locks, IPC, settings and titles. Dev uses the same logo on a yellow icon background in the executable and windows. Dev never auto-updates and rejects update/restart commands; pull, rebuild, and relaunch manually. Tests exercise real WPF windows with silent fake audio plus the engine, parser, colors, layout, migration, and updater with fake packages/process helpers. They also activate redesigned preset/switch controls, verify the single update action through checking, downloading, ready, and retry states, and scroll the minimum-size editor. They require a Windows desktop session, never edit real settings, and never play alarms. Append `-- --capture` for ignored screenshots in `artifacts`, including normal/tiny/vertical bars, scheduled-start previews, and editor focus, break, completion, update-state, and small-window states.

Run the silent, settings-free resource comparison harness with `dotnet run --project tests/PomodoroBar.Tests.csproj -c Release -- --resource-audit --output artifacts/resource-audit/optimized.json`. It measures seven warmed UI states, registers no tray icon, disables updates/IPC, and includes its own sampling overhead. It creates disposable test windows; do not overlap benchmarks with builds. Retain the raw readings when comparing versions.

Use `--scenario "bar only"` to measure one state with scheduler/render counts and per-thread CPU; append `--software` only to experiment with software rendering. Normal app rendering stays unchanged because the software experiment traded lower RAM for higher CPU on the tested machine.

## Releases

Routine production publishing has three steps:

1. Set the next version and dated changelog entry, commit, and push `main` with the matching semver tag. Wait for successful [release CI](https://github.com/ElliotDrel/pomodoro-bar/actions/workflows/release.yml) and the public release. CI runs tests, self-contained packaging, startup smoke, and installer/shortcut checks.
2. Run the installed `PomodoroBar.exe --restart` once from an ordinary Windows session. The flag only restarts; normal startup handles updates. Avoid interacting with the app until updating finishes.
3. Verify the expected version is running using its fresh version-stamped `started` log. Leave Prod running.

Do not repeat local packaging/smoke checks, reinstall the app, or repair shortcuts for routine updates. First installation and confirmed installation damage are separate tasks. Codex can redirect AppData paths, so run the installed command and local verification outside its packaged environment. See [deployment instructions](.agents/skills/deploy/SKILL.md).

Installed `PomodoroBar.exe --restart` requests a graceful Velopack restart, then the startup check downloads/applies any newer release; `--update` is an alias, matching Universal Spell Checker. `--check-for-updates` checks and downloads without restarting; it starts the app in manual-check mode if none is running. Commands are forwarded to the existing instance, and a second instance never applies updates before forwarding. Exit zero acknowledges delivery; verify fresh logs and the installed version to confirm completion. `--quit` gracefully closes that channel's existing instance, and succeeds without starting one if none is running. `--startup-smoke` constructs both native windows without a tray entry with silent audio and exits without reading/writing user settings or enabling IPC/updates.

```powershell
& "$env:LOCALAPPDATA\PomodoroBar\current\PomodoroBar.exe" --check-for-updates
& "$env:LOCALAPPDATA\PomodoroBar\current\PomodoroBar.exe" --update
```
