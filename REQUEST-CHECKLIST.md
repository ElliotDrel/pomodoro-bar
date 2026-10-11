# Request checklist

See [CHANGELOG.md](CHANGELOG.md) for dated Dev/Prod changes between versions. These unchecked items are things for Elliot to verify, not claims that they are unfinished.

## Check the latest UI requests

- [ ] Edit the countdown and watch the end time update immediately, rounded to the nearest minute. Paused timers show an estimate if started now; running unsaved drafts show an estimate if saved now; next-day times say tomorrow or show the date.
- [ ] Check, download, install, and restart without a terminal appearing; progress stays in the editor.
- [ ] Confirm one Pomodoro Bar Desktop shortcut, one Start Menu shortcut, and identical executable/icon targets; app search should no longer see the native executable and root launcher as separate entries.
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
- [ ] Inspect the desktop and taskbar icons; both native windows use the embedded app icon, and the shortcut targets the native executable and its embedded icon.
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

## Resource optimization

- [ ] Confirm paused bars do not redraw continuously and paused end estimates still update at rounded-minute boundaries.
- [ ] Confirm running visible countdowns/colors update each second, while reminder/completion flashes still animate.
- [ ] Hide/minimize both windows and confirm reminders/completion continue without routine UI work; reopen and confirm the countdown catches up.
- [ ] Resume Windows or change the system clock and confirm the engine/preview recover correctly.
- [ ] Confirm the native tray icon, right-click actions, keyboard activation, update actions, and reopening both windows.
- [ ] Compare the measured results in `RESOURCE-AUDIT.md`; retain WPF as selected before considering a full native UI rewrite.

## Proposed next change: separate Focus and Break inputs

- Present two editable durations together: **Focus `25 min`** and **Break `5 min`**.
- Change the ratio by editing either duration; set **Focus `23 min`** and **Break `7 min`** for the example requested.
- Keep the live countdown distinct from the configured durations so new users know what each number does.
- Let presets set both durations, and let additions preserve the chosen ratio once this feature is implemented.
- Mark this proposal as **not implemented**: the current countdown editor changes the active section, and preset/addition buttons still apply the original `5:1` split.
