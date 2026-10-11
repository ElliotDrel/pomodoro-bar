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
4. Push `main` and `vX.Y.Z` together. CI runs tests → self-contained publish → startup smoke → `vpk pack` → clean silent installer/shortcut verification → `vpk upload github --publish`. Never bypass CI with manual production packaging/upload.
5. Verify remote tag and matching completed successful workflow. Diagnose failures; do not claim queued/running CI is done.
6. Verify public `isDraft=false` release, version, installer, package, and update feed. Only a fully uploaded stuck draft may be published manually.
7. Before local installation or update verification, run `scripts/verify-installation.ps1 -PreflightOnly` from the execution process. A packaged/MSIX process must fail closed: even elevated commands and literal AppData paths can be redirected. Perform installation from an unpackaged Windows session; use `scripts/install-prod.ps1` with the published installer and expected version. Preserve/import existing settings. After installation, run `scripts/verify-installation.ps1 -ExpectedVersion X.Y.Z -VerifyShortcuts` to verify the physical path, not only `Test-Path`, environment variables, file version or logical process path. Verify Desktop, Start Menu, and pinned shortcut target and icon paths in that same unpackaged context. Keep Desktop and Start Menu targets identical to the native main executable; never rewrite only Desktop to the root stub. Use `scripts/repair-shortcuts.ps1` to normalize owned shortcuts and retain confirmed duplicate plain launchers outside search indexing. Launch temporary interactive Task Scheduler PowerShell verification through `wscript.exe scripts/background.ps1.vbs` so it stays hidden. This supersedes the previous version-only installation check.
8. Subsequent updates: invoke installed `PomodoroBar.exe --restart`, verify physical installation, binary version and fresh `started` log. Accepted IPC alone does not prove installation. Leave verified Prod running.

## Closeout

- Every session change committed/pushed; local HEAD matches remote main. A production release tag matches the released app commit; later documentation or tooling commits may advance main without republishing unchanged binaries.
- Session-owned Dev/testing processes stopped and listeners cleared.
- Disposable scratch cleaned; preserve backups, user settings, retained evidence/tooling, and unrelated state.
- Report release/CI links, installed version/PID, and anything incomplete. Do not finish publishing with an unverified release.
