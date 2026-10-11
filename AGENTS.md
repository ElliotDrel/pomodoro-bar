# AGENTS.md

## Project

Native C#/.NET 10 Windows app: WPF windows, WinForms tray, Velopack installer/updater. This supersedes Python/Tkinter. Keep the floating bar separate from the editor. Release installers bundle .NET.

## Preserve

- Uniformly scale circle, countdown, controls, and hit targets down to 130 × 20 DIPs.
- Mirror the countdown in the editor without overwriting a draft.
- Presets: 30 total = 25/5; 60 total = 50/10. Additions preserve 5:1 ratio and reminder percentages.
- Assign sounds backward; completion loops until a bar interaction silences audio. Completion waits for acknowledgement; coalesce delayed reminders.
- Audio/update work must not block the dispatcher.
- Channel identity, paths, locks, IPC belong in `BuildChannel.cs`. Dev never auto-updates.
- Settings live in channel-specific LocalAppData outside the installer-owned root; preserve them on updates.
- One update flow: `UpdateService.CheckAsync`; launch applies, periodic stages.

## Files

| Path | Purpose |
| --- | --- |
| src/Core/ | Deadline engine, parser, settings, safe startup recovery/migration, color/layout math |
| src/BarWindow.cs | Floating drawing, hit targets, moving, native resizing |
| src/EditorWindow.cs | Independent editor and protected time draft |
| src/AppIcon.cs | Shared embedded icon for both native windows and their taskbar entries |
| src/AppHost.cs | Controller, alerts, tray, lifecycle and settings |
| src/Program.cs | Entry point, Velopack, singleton, migration, smoke mode |
| src/BuildChannel.cs, src/Log.cs | Channel identity, paths and diagnostics |
| src/InstanceServer.cs | Current-user relaunch/restart IPC |
| src/UpdateService.cs | Unified update/download/restart flow |
| src/Audio.cs, sounds/ | Async Windows playback and bundled WAV chimes |
| tests/ | Native engine, GUI, layout, migration and audio regression harness |
| assets/ | App icon |
| scripts/dev.ps1 | Dev run helper |
| scripts/install-prod.ps1, scripts/verify-installation.ps1 | Unpackaged installation and physical-path/shortcut verification |
| .github/workflows/ | Main checks and tagged production releases |
| .agents/skills/deploy/SKILL.md | Dev/release workflow and closeout |
| Launch Pomodoro.vbs | Compatibility launcher for installed native Prod |
| README.md | Install, controls, migration, development, releases |

## Verification and publishing

Run `dotnet run --project tests/PomodoroBar.Tests.csproj -c Release`, Dev and Release builds, and `git diff --check` before committing. Tests require Windows and silent fake audio. Rebuild/relaunch native changes. Update README and the file map for changed controls/files.

Read `.agents/skills/deploy/SKILL.md` when publishing. Use the user-requested branch and meaningful commits. Semver tags trigger CI/Velopack upload; verify public release and installed local Prod before closeout. Stop only session-owned Dev instances/scratch. Never delete backups without explicit per-file permission.

Local deployment must run outside packaged/MSIX processes. AppData writes can be redirected even with elevated execution or absolute paths; verify the physical file path and shell shortcuts from an unpackaged process before claiming success.
