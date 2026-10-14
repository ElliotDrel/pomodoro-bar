---
name: deploy
description: Run Pomodoro Bar in Dev or publish a production release.
---

# Deploy

## Dev

Run `dotnet run --project src/PomodoroBar.csproj -c Dev`. Dev uses isolated settings and never auto-updates. Rebuild/relaunch after native changes.

## Prod: publish → restart → verify

1. Set the next version and dated `CHANGELOG.md` entry, commit, and push `main` with its semver tag. Wait for successful release CI and a public release. CI handles testing, packaging, and installer checks.
2. Run the installed `PomodoroBar.exe --restart` once. Normal startup checks and applies updates; the flag only restarts. Leave the app untouched until updating finishes, since interaction defers installation.
3. Confirm the expected version is running using its fresh version-stamped `started` log. Command acceptance alone is insufficient. Leave Prod running and report its version and release link.

Run installed commands and verification from an ordinary Windows session: Codex can redirect AppData paths. Routine updates use the existing installation, without local repackaging or reinstallation. Use `scripts/install-prod.ps1` only for first installation or repair. Deployment needs no new flags or updater code.
