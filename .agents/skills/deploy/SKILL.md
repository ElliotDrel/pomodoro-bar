---
name: deploy
description: Run Pomodoro Bar in Dev or publish a native production release with GitHub Actions and Velopack.
---

# Deploy

Adapted from Universal Spell Check's Dev/Prod workflow. Explicit user/session instructions and authorization take precedence.

## Dev

Push verified committed changes to `origin/main`. Pull and run `dotnet run --project src/PomodoroBar.csproj -c Dev`. Dev has isolated settings and singleton identity and never auto-updates. Rebuild/relaunch native changes. No installer or tag needed.

## Prod

1. Inspect status, branch, origin, tags, and fetch `origin/main`. Reconcile without destructive resets or force pushes.
2. Pass native regressions, Dev/Release builds, and `git diff --check`. Publish locally and run bounded `--startup-smoke`. Inspect rendered UI and fix acceptance failures before shipping.
3. Commit changes and choose a strictly higher semver. Present the concrete release version/commit; obtain explicit release authorization if not already supplied in this session. A Dev push does not authorize a production release.
4. Push `main` and `vX.Y.Z` together. CI runs tests → self-contained publish → startup smoke → `vpk pack` → `vpk upload github --publish`. Never bypass CI with manual production packaging/upload.
5. Verify remote tag and matching completed successful workflow. Diagnose failures; do not claim queued/running CI is done.
6. Verify public `isDraft=false` release, version, installer, package, and update feed. Only a fully uploaded stuck draft may be published manually.
7. First install: run the published setup and import legacy settings if needed. Subsequent updates: installed `PomodoroBar.exe --restart`, then verify binary version and fresh `started` log. Accepted IPC alone does not prove installation. Leave verified Prod running.

## Closeout

- Every session change committed/pushed; local HEAD matches remote main and release tag.
- Session-owned Dev/testing processes stopped and listeners cleared.
- Disposable scratch cleaned; preserve backups, user settings, retained evidence/tooling, and unrelated state.
- Report release/CI links, installed version/PID, and anything incomplete. Do not finish publishing with an unverified release.
