# AGENTS.md

## Project

Native C#/.NET 10 Windows app: WPF windows, direct Windows shell tray, Velopack installer/updater. This supersedes Python/Tkinter. Keep the floating bar separate from the editor. Release installers bundle .NET.

## Preserve

- One app and one desktop shortcut named Pomodoro Bar. Both independent windows appear in the taskbar and Alt+Tab; startup and relaunch show both. Do not suppress either window's taskbar presence.
- Closing either window hides only that window. Quit alone exits the app; both windows can be reopened through tray/relaunch.

- Uniformly scale circle, countdown, controls, and hit targets down to 130 × 20 DIPs.
- Mirror the countdown in the editor without overwriting a draft.
- Presets: 30 total = 25/5; 60 total = 50/10. Additions preserve 5:1 ratio and reminder percentages.
- Assign sounds backward; completion loops until a bar interaction silences audio. Completion waits for acknowledgement; coalesce delayed reminders.
- Audio/update work must not block the dispatcher.
- Schedule visible countdown seconds and hidden milestones directly; do not reintroduce unconditional polling. Keep paused rounded end previews live, skip hidden/minimized UI, and animate alerts only on the visible bar. Catch up on resume/time changes and preserve drafts.
- Channel identity, paths, locks, IPC belong in `BuildChannel.cs`. Dev never auto-updates.
- Settings live in channel-specific LocalAppData outside the installer-owned root; preserve them on updates.
- One update flow: `UpdateService.CheckAsync`; launch applies, periodic stages.
- Every change must update `CHANGELOG.md` before committing: record Dev/tooling work under Unreleased; move released changes into a dated version entry when tagging Prod. Preserve old version history and update `REQUEST-CHECKLIST.md` when a requested behavior changes. CI enforces the log and generates release notes from it.

## Files

| Path | Purpose |
| --- | --- |
| src/Core/ | Deadline engine, parser, settings, safe startup recovery/migration, color/layout math |
| src/BarWindow.cs | Floating drawing, hit targets, moving, native resizing |
| src/EditorWindow.cs | Independent editor, protected time draft, live rounded end-time preview, and focus/break timeline |
| src/UiTheme.cs | Shared palette, keyboard-focus button/switch templates, and menu styling |
| src/AppIcon.cs | Shared embedded window/taskbar icon and owned native tray icon handles |
| src/NativeTray.cs | Windows shell tray registration, native menus, keyboard activation, Explorer recovery and icon cleanup |
| src/MonitorAreas.cs | Native monitor work areas without loading WinForms |
| src/AppHost.cs | Controller, alerts, tray, lifecycle and settings |
| src/Program.cs | Entry point, Velopack, singleton, migration, smoke mode |
| src/BuildChannel.cs, src/Log.cs | Channel identity, paths and diagnostics |
| src/InstanceServer.cs | Current-user relaunch/restart IPC |
| src/UpdateService.cs | Unified update/download/restart flow |
| src/BackgroundUpdateProcess.cs | Explicit no-shell/no-console policy for restart and apply helpers |
| src/Audio.cs, sounds/ | Async Windows playback and bundled WAV chimes |
| tests/ | Native engine, GUI, layout, migration and audio regression harness |
| tests/UpdateFlowTests.cs | Fake-package updater regressions and a harmless real console-window suppression probe |
| tests/ResourceAudit.cs | Silent settings-free warmed CPU/RAM/allocation measurements in seven UI states |
| assets/ | Prod app icon and yellow-background Dev variant |
| scripts/dev.ps1 | Dev run helper |
| scripts/install-prod.ps1, scripts/verify-installation.ps1 | Unpackaged installation and physical-path/shortcut verification |
| scripts/repair-shortcuts.ps1 | Match installer shortcut targets and retire confirmed duplicate plain launchers safely |
| scripts/background.ps1.vbs | Hidden PowerShell launcher for unpackaged installation/verification tasks |
| .github/workflows/ | Main checks and tagged production releases |
| .agents/skills/deploy/SKILL.md | Dev/release workflow and closeout |
| Launch Pomodoro.vbs | Compatibility launcher for installed native Prod |
| README.md | Install, controls, migration, development, releases |
| CHANGELOG.md | Dated version/channel history, Unreleased changes, and source for public release notes |
| REQUEST-CHECKLIST.md | Complete user-request verification checklist and unimplemented proposals |
| RESOURCE-AUDIT.md | Measured CPU/RAM baseline, refresh experiments, and prioritized optimization suggestions |
| scripts/changelog.ps1 | Validate log updates and dated release entries; extract version-specific notes |
| tests/Changelog.Tests.ps1 | Release-note extraction and missing-log/invalid-history regression fixtures |

## Verification and publishing

Run `dotnet run --project tests/PomodoroBar.Tests.csproj -c Release`, Dev and Release builds, and `git diff --check` before committing. Tests require Windows and silent fake audio. Rebuild/relaunch native changes. Update README and the file map for changed controls/files.

Read `.agents/skills/deploy/SKILL.md` when publishing. Use the user-requested branch and meaningful commits. Semver tags trigger CI/Velopack upload; verify public release and installed local Prod before closeout. Stop only session-owned Dev instances/scratch. Never delete backups without explicit per-file permission.

Local deployment must run outside packaged/MSIX processes. AppData writes can be redirected even with elevated execution or absolute paths; verify the physical file path and shell shortcuts from an unpackaged process before claiming success.
