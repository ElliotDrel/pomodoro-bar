# Changelog and request checklist

## Latest release: 1.1.4

- Open the [public release](https://github.com/ElliotDrel/pomodoro-bar/releases/tag/v1.1.4) for the installer, portable build, and update files.
- Run the finished native app with the redesigned editor, floating bar, switches, and menus.
- Check the implementation with 158 regression assertions, Dev/Release builds, rendered UI captures, and a self-contained startup smoke test.

## Recent changes

- **1.1.4:** Fix the local installer helper that rewrote only the Desktop shortcut to the root launcher while the Start Menu shortcut retained the native executable target. Match both to Velopack's native target; verify both locations and retain confirmed duplicate launch shortcuts outside search indexing. Gate publishing on a clean Windows installation test that verifies the installer-created shortcuts before any repair. The timer behavior is unchanged.
- **1.1.3:** Enforce hidden, no-console process launches for update installation and restart; launch local release verification through a hidden script host instead of visibly opening PowerShell. Add a real console-process probe and assertions for both updater paths.
- **1.1.2:** Replace the update dropdown and separate buttons with one update action beside its status information; remove **Quit app** and **Done** from the editor.
- **1.1.1:** Remove the installer download button from the installed app; keep installer downloads on the public release page.
- **1.1.0:** Rebuild the native UI with a graphite palette, prominent editable countdown, focus/break timeline, descriptive presets, switches, matching menus, and visible keyboard focus.
- **1.0.5:** Make checkbox labels readable and hide scrollbars while preserving scrolling.
- **1.0.4:** Match the Universal Spell Checker update sequence, add editor update controls, and give Dev the same logo on a yellow background.
- **1.0.3:** Open both independent windows on startup and relaunch; restore minimized windows.
- **1.0.2:** Present one installed Pomodoro Bar app and let each window close independently.
- **Installation follow-up:** Guard physical installation and shortcut verification against redirected AppData before the one-app cleanup release.
- **1.0.1:** Supply the app icon to native window/taskbar entries and isolate regression-test IPC.
- **1.0.0:** Replace the Python prototype with native C#/.NET Windows UI, separate Dev/Prod channels, and GitHub Actions/Velopack releases.
- **Prototype:** Add presets, timer colors, normalized reminders, sound escalation, independent editing, uniform resizing, direct countdown editing, and size reset; retain these features in the native app.

## Check the latest UI requests

- [ ] Check, download, install, and restart without a terminal appearing; progress stays in the editor.
- [ ] Inspect the editor and bar for the new stopwatch-inspired design, readable text, and matching controls.
- [ ] Resize the editor smaller and scroll with the wheel; confirm no scrollbar appears.
- [ ] Find **Check for updates** directly beside version, update status, last checked, and last updated; confirm there is no update dropdown.
- [ ] Click **Check for updates** and confirm the button says **Checking for update now** and becomes disabled.
- [ ] Observe download progress beside the disabled button if a newer release exists.
- [ ] Confirm the same button becomes **Update and restart** only after a verified update has downloaded; a failed check returns to the check action.
- [ ] Confirm there is no **Download installer**, **Quit app**, or **Done** button in the editor.
- [ ] Close the editor with **X** or Escape; reopen it with the pencil or tray icon.
- [ ] Quit the whole app using **Quit** in the tray or floating-bar menu.

## Check the earlier app requests

### Native app, project, installation, and icons

- [ ] Use the native Windows app without Python or a Chrome extension; the Windows approach replaced the earlier Chrome/prototype option.
- [ ] Find the main checkout under `All Coding/Pomodoro-Bar/pomodoro-bar` and the [public repository](https://github.com/ElliotDrel/pomodoro-bar); changes are committed and pushed to `main`.
- [ ] Use the retained prototype backup if needed; cleanup preserved backups and the compatibility launcher instead of deleting historical work.
- [ ] Install the release per user under `%LocalAppData%\PomodoroBar`, with the .NET runtime bundled and no separate runtime installation required.
- [ ] Keep settings outside the installer-owned folder under `%LocalAppData%\PomodoroBar.Data`; updates preserve configured durations and bar geometry.
- [ ] Find one desktop shortcut named **Pomodoro Bar** and one installed production app, with two windows belonging to that app.
- [ ] Inspect the desktop and taskbar icons; both native windows use the embedded app icon, and the shortcut points to the stable installed launcher and current executable icon.
- [ ] Run installation verification from an ordinary Windows process; the physical-path preflight detects packaged-process AppData redirection, which caused the earlier misleading installation/shortcut paths.

### Windows and editing

- [ ] Launch from the desktop or installed app entry and confirm both the editor and always-on-top floating bar open.
- [ ] Find both independent windows in the taskbar and Alt+Tab.
- [ ] Close either window with **X** and confirm the other window and timer keep working.
- [ ] Launch again and confirm hidden/minimized windows reopen without creating a second production instance.
- [ ] Click the pencil to open the editor centered on the primary screen; the earlier expanding-bar/down-arrow editor was superseded by this separate window.
- [ ] Toggle **Show floating bar** in the editor without closing or hiding the editor.
- [ ] Edit the large formatted countdown directly; enter minutes, `MM:SS`, or `HH:MM:SS`, then press Enter to save or Escape to cancel.
- [ ] Keep an unsaved draft while the timer runs and confirm live countdown updates do not overwrite it.

### Floating bar and sizing

- [ ] Find the cycle-progress circle, current countdown, play/pause, skip, and pencil on the compact bar.
- [ ] Confirm the progress circle measures the whole focus-plus-break cycle.
- [ ] Inspect the tight spacing between the countdown and play button.
- [ ] Enable **Resize using the bar’s edges**, then drag native window edges/corners to resize the bar.
- [ ] Shrink the bar to `130 × 20` DIPs and confirm the circle, digits, buttons, and hit targets scale together.
- [ ] Click **Reset size** and confirm the bar returns to `280 × 52` DIPs without changing its position or timer.
- [ ] Confirm appearance is automatic; there are no color-picker settings, as requested after the initial customization proposal.

### Presets, colors, reminders, and audio

- [ ] Select **30 minutes** and confirm `25:00` focus plus `05:00` break.
- [ ] Select **60 minutes** and confirm `50:00` focus plus `10:00` break.
- [ ] Use `+1`, `+5`, or `+10` minutes and confirm the total increases; these current preset/addition controls use the original `5:1` focus/break split.
- [ ] Watch countdown colors change continuously from white through green, yellow, orange, and red as time runs low.
- [ ] Check the 30-minute preset reminders at 10 and 5 minutes of focus remaining, corresponding to 40% and 20% of focus duration.
- [ ] Check the 60-minute preset reminders at 30, 10, and 5 minutes of focus remaining, corresponding to 60%, 20%, and 10% of focus duration.
- [ ] Hear escalating, distinct reminder sounds assigned backward: the last reminder always uses the same strongest sound, the second-last uses the same rising sound, and the additional third-last uses the single softer beep.
- [ ] Reach zero and confirm completion waits for acknowledgement and loops its alarm; any interaction on the bar silences audio, while circle/play/skip also start the next section.
- [ ] Hide the bar at completion and use the editor's acknowledgement button or tray controls.
- [ ] Respect the Windows animation preference: when animations are disabled, the reminder warning stays still while audio and acknowledgement behavior remain active.
- [ ] Resume after a delayed tick/sleep and confirm overdue reminders coalesce rather than playing in a queue.

### Development, updates, and release flow

- [ ] Run Dev with `dotnet run --project src/PomodoroBar.csproj -c Dev`; confirm its yellow-background icon uses the same logo.
- [ ] Run Dev beside Prod with separate settings, locks, IPC, and titles; confirm Dev never auto-updates.
- [ ] Check for updates on production startup and every four hours, matching the Universal Spell Checker flow.
- [ ] Apply a startup update before interaction; stage periodic/manual updates for the single **Update and restart** action.
- [ ] Preserve your configured settings across an update; restarting begins a fresh paused focus period.
- [ ] Use installed `PomodoroBar.exe --update` or `--restart` for restart/startup-update behavior.
- [ ] Use `--check-for-updates` to check/download without restarting and `--quit` to close the production instance.
- [ ] Publish through semver tags and GitHub Actions native tests, self-contained build, smoke test, Velopack packaging, and public upload.
- [ ] Retain the three completed Opus 5.5 adversarial reviews from the port/updater work; this UI pass used regression tests and visual critique without adding further review runs.

## Proposed next change: separate Focus and Break inputs

- Present two editable durations together: **Focus `25 min`** and **Break `5 min`**.
- Change the ratio by editing either duration; set **Focus `23 min`** and **Break `7 min`** for the example requested.
- Keep the live countdown distinct from the configured durations so new users know what each number does.
- Let presets set both durations, and let additions preserve the chosen ratio once this feature is implemented.
- Mark this proposal as **not implemented in 1.1.2**: the current countdown editor changes the active section, and preset/addition buttons still apply the original `5:1` split.
