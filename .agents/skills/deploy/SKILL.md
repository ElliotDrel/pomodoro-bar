---
name: deploy
description: Run Pomodoro Bar in Dev or publish a native production release with GitHub Actions and Velopack.
---

# Deploy

Adapted from Universal Spell Check's Dev/Prod workflow. Explicit user/session instructions and authorization take precedence.

## Dev

Record every change in `CHANGELOG.md` under Unreleased before committing; preserve released entries. Run `scripts/changelog.ps1` and `tests/Changelog.Tests.ps1`. Push verified committed changes to `origin/main`. Pull and run `dotnet run --project src/PomodoroBar.csproj -c Dev`. Dev has isolated settings and singleton identity and never auto-updates. Rebuild/relaunch native changes. No installer or tag needed.

## Prod

1. **Publish.** Set the next semver and dated changelog entry, commit, and push `main` with its matching tag. Wait for successful release CI and the public release. CI handles tests, packaging, startup smoke, installer checks, and uploads; do not repeat local packaging/smoke checks just to publish.
2. **Restart.** Run the installed `PomodoroBar.exe --restart` once from an ordinary Windows session. Codex's packaged environment can redirect AppData paths. Use the existing installation; do not download/reinstall the installer or repair shortcuts for routine updates.
3. **Verify.** Wait for the expected version to be running, confirmed by its fresh version-stamped `started` log. The restart command's exit code only acknowledges the request. Leave verified Prod running and report the release link/version. If verification fails, inspect the actual error instead of repeatedly launching the app.

`--restart` only relaunches the app. The normal startup sequence checks, downloads, and applies available updates; the restart flag does not request an update directly. Avoid interacting with the app until startup updating finishes, because interaction defers automatic installation. Do not add flags or another updater.

First installation or a confirmed broken installation is a separate installer task, not part of routine publishing. Preserve settings and backups. Stop only session-owned Dev/testing instances and leave the repository clean and pushed, unless the user requests a local-only commit.
