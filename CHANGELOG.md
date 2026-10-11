# Changelog

All changes are recorded here before committing. Entries describe the user-visible result, why it changed, and verification; old released entries remain intact.

**Dev:** unreleased changes on `main`, available after pulling and rebuilding Dev. **Prod:** dated, tagged releases below. Documentation/tooling changes can stay Unreleased without changing the installed app version.

See [the request checklist](REQUEST-CHECKLIST.md) for the complete feature review and pending proposals.

## [Unreleased]

- No pending changes.

## [1.1.6] - 2026-10-11

Channel: Prod. [Release](https://github.com/ElliotDrel/pomodoro-bar/releases/tag/v1.1.6) · [Changes since 1.1.5](https://github.com/ElliotDrel/pomodoro-bar/compare/v1.1.5...v1.1.6).

- Record the installed 1.1.5 CPU/RAM baseline and isolated refresh scenarios in `RESOURCE-AUDIT.md` before making optimizations; preserve the original measurements and their limitations.
- Replace continuous 10 Hz UI polling with state-aware countdown, milestone, and rounded end-preview scheduling; skip hidden/minimized UI and animate only visible alerts. Catch up on Windows resume and clock changes.
- Cache countdown layout/text, immutable drawing resources, session labels, and parsed drafts. Preserve immediate draft previews, completion acknowledgement, independent windows, and uniformly scaled controls.
- Replace WinForms tray/monitor dependencies with native shell and monitor interop, using the embedded channel icon, native menu actions, keyboard activation, and Explorer restart recovery.
- Add scheduling/tray regressions and a reproducible silent seven-state resource harness; measure old and optimized builds sequentially before publishing performance claims.
- Verify 191 native assertions, eight changelog assertions, clean Dev/Release builds, self-contained Release and Dev startup smoke, and normal/tiny/editor UI captures. Record 18–24% lower resident RAM and 78–98% lower allocation churn in the matched seven-state runs; preserve visible-running CPU variability and the rejected software-rendering tradeoff in the audit.

## [1.1.5] - 2026-10-11

Channel: Prod. [Release](https://github.com/ElliotDrel/pomodoro-bar/releases/tag/v1.1.5) · [Changes since 1.1.4](https://github.com/ElliotDrel/pomodoro-bar/compare/v1.1.4...v1.1.5).

### Added

- Show the current section's estimated end time beneath the editor countdown, updating immediately as a valid duration is typed and as presets/countdown change. Round the resulting clock time to the nearest minute, with 30-second ties rounded forward and next-day dates labeled.
- Distinguish paused start-now estimates and unsaved running drafts from the actual running timer's end; keep existing draft protection and show a prompt for invalid/incomplete durations.
- Set up this versioned Dev/Prod changelog and preserve the full request checklist separately.
- Require changelog updates in main/PR checks and a matching dated entry before packaging a production release.
- Generate release notes from this log for GitHub releases, keeping the published notes and repository history consistent; backfill the notes for 1.0.0 through 1.1.4.

### Verification

- Pass eight changelog regression assertions, all 168 native assertions, Dev/Release builds, and editor UI captures. Cover rounding boundaries, next-day rollover, immediate draft preview, invalid drafts, and stable running countdown estimates.

## [1.1.4] - 2026-10-11

Channel: Prod. [Release](https://github.com/ElliotDrel/pomodoro-bar/releases/tag/v1.1.4) · [Changes since 1.1.3](https://github.com/ElliotDrel/pomodoro-bar/compare/v1.1.3...v1.1.4).

### Fixed

- Match Desktop and Start Menu shortcuts to the same native executable. The local install helper previously changed only Desktop to the root launcher, producing duplicate application search results.
- Retain confirmed duplicate launch shortcuts outside Desktop/Start Menu indexing; preserve custom command shortcuts and backups.

### Added

- Block release upload unless a clean Windows installation passes physical-path, shortcut-target, icon, and duplicate-shortcut checks before any repair helper runs.

### Verification

- 158 native regression assertions, Dev/Release builds, startup smoke, real duplicate detection/retirement and repeat-repair checks, clean installer CI, and local installed-version/shortcut verification.

## [1.1.3] - 2026-10-11

Channel: Prod. [Release](https://github.com/ElliotDrel/pomodoro-bar/releases/tag/v1.1.3) · [Changes since 1.1.2](https://github.com/ElliotDrel/pomodoro-bar/compare/v1.1.2...v1.1.3).

### Fixed

- Explicitly suppress shell execution and console windows for update apply/restart helpers.
- Launch local installation and release verification through a hidden script host instead of showing PowerShell; preserve exit codes.

### Verification

- 158 native assertions, including a real PowerShell console-window probe; Dev/Release builds, startup smoke, CI, installed update, and restart verification.

## [1.1.2] - 2026-10-11

Channel: Prod. [Release](https://github.com/ElliotDrel/pomodoro-bar/releases/tag/v1.1.2) · [Changes since 1.1.1](https://github.com/ElliotDrel/pomodoro-bar/compare/v1.1.1...v1.1.2).

### Changed

- Replace the update dropdown with one action beside version, status, and update timestamps. It disables while checking/downloading and becomes Update and restart when a verified update is ready.
- Remove Quit app and Done from the editor; keep independent window close behavior and tray/bar Quit.

### Verification

- 153 native assertions, Dev/Release builds, UI captures, startup smoke, CI, and installed production verification.

## [1.1.1] - 2026-10-11

Channel: Prod. [Release](https://github.com/ElliotDrel/pomodoro-bar/releases/tag/v1.1.1) · [Changes since 1.1.0](https://github.com/ElliotDrel/pomodoro-bar/compare/v1.1.0...v1.1.1).

### Removed

- Remove the redundant installer-download button from the installed app. Installers remain available on the public release page.

## [1.1.0] - 2026-10-11

Channel: Prod. [Release](https://github.com/ElliotDrel/pomodoro-bar/releases/tag/v1.1.0) · [Changes since 1.0.5](https://github.com/ElliotDrel/pomodoro-bar/compare/v1.0.5...v1.1.0).

### Changed

- Rebuild the native UI with a graphite palette, prominent editable countdown, focus/break timeline, descriptive presets, switches, matching menus, and visible keyboard focus.
- Keep the floating bar compact, uniformly resizable, and separate from the editor.

## [1.0.5] - 2026-10-11

Channel: Prod. [Release](https://github.com/ElliotDrel/pomodoro-bar/releases/tag/v1.0.5) · [Changes since 1.0.4](https://github.com/ElliotDrel/pomodoro-bar/compare/v1.0.4...v1.0.5).

### Fixed

- Give editor checkbox labels readable contrast and hide scrollbars while preserving scrolling in smaller windows.

## [1.0.4] - 2026-10-11

Channel: Prod. [Release](https://github.com/ElliotDrel/pomodoro-bar/releases/tag/v1.0.4) · [Changes since 1.0.3](https://github.com/ElliotDrel/pomodoro-bar/compare/v1.0.3...v1.0.4).

### Added

- Match Universal Spell Check's startup, periodic, manual, and command-line update/restart sequence through one updater flow; show editor progress and timestamps.
- Give Dev the same app logo on a yellow background, with isolated settings/identity and no auto-updates.

## [1.0.3] - 2026-10-11

Channel: Prod. [Release](https://github.com/ElliotDrel/pomodoro-bar/releases/tag/v1.0.3) · [Changes since 1.0.2](https://github.com/ElliotDrel/pomodoro-bar/compare/v1.0.2...v1.0.3).

### Fixed

- Show both independent windows on startup and relaunch, restore minimized windows, and keep both represented in the taskbar and Alt+Tab.

## [1.0.2] - 2026-10-11

Channel: Prod. [Release](https://github.com/ElliotDrel/pomodoro-bar/releases/tag/v1.0.2) · [Changes since 1.0.1](https://github.com/ElliotDrel/pomodoro-bar/compare/v1.0.1...v1.0.2).

### Fixed

- Present one Pomodoro Bar app with independent windows; closing either hides only that window instead of exiting both.
- Guard local installation/shortcut verification against packaged-process AppData redirection; preserve user settings outside the install root.

## [1.0.1] - 2026-10-11

Channel: Prod. [Release](https://github.com/ElliotDrel/pomodoro-bar/releases/tag/v1.0.1) · [Changes since 1.0.0](https://github.com/ElliotDrel/pomodoro-bar/compare/v1.0.0...v1.0.1).

### Fixed

- Supply the embedded app icon to both native window/taskbar entries and isolate regression-test IPC.

## [1.0.0] - 2026-10-11

Channel: Prod. [Release](https://github.com/ElliotDrel/pomodoro-bar/releases/tag/v1.0.0).

### Added

- Rewrite the prototype as a native C#/.NET Windows app with WPF windows, a WinForms tray, bundled .NET, Velopack installation/updates, and tagged GitHub Actions releases.
- Preserve the compact always-on-top bar, separate pencil-opened editor, uniformly scaled controls, editable countdown, presets/additions, size reset, timer colors, normalized reminders, and escalating/completion sounds.
- Separate Dev/Prod settings, locks, IPC, and channel identity. Keep user settings outside installer-owned files.

## Before 1.0.0 - prototype

- Build the floating timer, progress circle, play/pause and skip controls, separate editor, live formatted countdown, presets, resizing down to 130 × 20 DIPs, automatic colors, and escalating reminder sounds.
- Set up the public project on main and preserve the prototype backup during the native migration.
