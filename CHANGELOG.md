# Changelog

All changes are recorded here before committing. Entries describe the user-visible result, why it changed, and verification; old released entries remain intact.

**Dev:** unreleased changes on `main`, available after pulling and rebuilding Dev. **Prod:** dated, tagged releases below. Documentation/tooling changes can stay Unreleased without changing the installed app version.

See [the request checklist](REQUEST-CHECKLIST.md) for the complete feature review and pending proposals.

## [Unreleased]

## [1.2.1] - 2026-10-14

Channel: Prod. [Release](https://github.com/ElliotDrel/pomodoro-bar/releases/tag/v1.2.1).

- Allow 60 seconds for cold Windows CI to compile the real console-window probe, and wait for any timed-out child to exit before deleting its working directory. This fixes the release test cleanup failure without changing app behavior.

- Match floating-button click targets to their visible bounds in horizontal, tiny, and vertical layouts. Keep adjacent space draggable without invoking controls; releasing outside the pressed button cancels the action.

- Stack vertical countdown components on separate lines (minutes above seconds, or hours/minutes/seconds) and narrow the default to 64 × 200 DIPs. Adjust text caching, layout, and button hit targets for both two- and three-line timers; migrate only the old exact 110 × 180 vertical default, preserving custom geometry.

- Blend unfilled perimeter tracks into the panel instead of starting with a white outline; only completed outline progress uses the changing countdown color. Supersede the earlier fully colored outline treatment.
- Replace the timeline gradient with five distinct solid focus color blocks separated by small gaps. Keep break text, progress color, and timeline green until completion, then red. Verify 200 native assertions, 27 updater assertions, and updated normal/break captures.

- Move the scheduled-start countdown above pause at twice the old label size and remove the redundant endpoint footer. Show the exact countdown color progression on both timeline phases.
- Share a countdown-colored, thicker perimeter renderer between the floating bar and editor timer panel. Keep the whole outline in the timer’s current color and distinguish completed progress with a thicker stroke.
- Remove app tray registration entirely. Closing one window retains the other; closing the last visible window disposes timers, audio, updates, and IPC and exits. This supersedes the earlier hide-both-and-keep-running behavior; minimized windows remain open. Verify 197 native and 27 updater assertions plus eight changelog assertions and updated UI captures. Verify actual Dev process exit after closing both windows in each order; the first window alone does not exit the app.

- Replace the floating progress circle with a thicker perimeter progress outline; keep countdown at the left with play/pause, skip, and edit. Add a persisted vertical orientation button in the editor with upright countdown and stacked controls.
- Build the requested session timeline with a larger focus duration, slightly smaller break duration, play/pause and skip between them, and editable start/focus-end/break-end fields. Apply valid edits per keystroke without overwriting input; show next-day labels separately and fit long durations in the narrow editor.
- Schedule automatic future focus starts with a pink bar countdown and editor status; reject past starts with a red inline error and reset to now. Schedule the start deadline directly, catch up after sleep/clock changes, and keep completion acknowledgement and coalesced reminders.
- Round edited durations and clock times to minutes, with exact selected endpoint deadlines. Presets/additions keep their total and reminder percentages using a whole-minute approximation of 5:1, superseding fractional-minute additions; 30 + 1 now yields 26/5. Preserve independent windows, channel-isolated settings, and Dev's disabled auto-update policy.
- Add native scheduling, live editing, past-start correction, date parsing, orientation persistence, and hit-target regressions plus normal/scheduled/vertical/narrow layout captures. Update the README, file map, and request checklist; promote the verified local Dev review into this production release.
- Verify 195 native assertions, 27 updater assertions, eight changelog assertions, Dev/Release builds, and layout captures. NuGet vulnerability-feed lookup was unavailable (`NU1900`); compilation and tests passed using the existing dependencies.

- Improve GitHub discovery with a clear product description, Windows/productivity topics, and a working download website link. Add direct install, release, support, and measurement links plus a concise user overview to the README. Documentation only; the app binary is unchanged.

## [1.2.0] - 2026-10-14

Channel: Unpublished release attempt.

- Preserve the immutable v1.2.0 tag; its CI run failed during temporary updater-test cleanup and uploaded no public release. Superseded by 1.2.1 with the console-probe timeout and termination fix.

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
