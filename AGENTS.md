# AGENTS.md

## Project

Pomodoro Bar is a standard-library Python/Tkinter Windows desktop timer. Keep the floating bar separate from the editor window. No third-party runtime dependencies are needed.

## Behavior to preserve

- Scale the circle, countdown, controls, and hit targets together; support the minimum 130 x 20 pixel bar.
- Keep the editor's time card synchronized with the floating bar while protecting text being edited.
- The 30-minute preset is 25 minutes focus plus 5 minutes break; the 60-minute preset is 50 plus 10.
- Assign reminder sounds backward from the last reminder. Completion audio loops until a bar interaction silences it.
- Use asynchronous audio; never block Tk's event loop with playback or sleeping.
- Keep local settings out of commits. Do not overwrite user settings when updating the app.

## Files

| File | Purpose |
| --- | --- |
| timer.pyw | Window UI, countdown engine, settings, and audio playback/synthesis |
| Launch Pomodoro.vbs | Discover Python on PATH and launch without a terminal |
| test_timer.py | Timer, GUI behavior, layout, and audio regression tests |
| sounds/*.wav | Bundled synthesized reminder and completion sounds |
| README.md | Installation, controls, and behavior |
| .gitignore | Excludes machine-local settings and Python caches |

## Verification

Run `python test_timer.py` on an interactive Windows desktop and `git diff --check` before committing. Audio tests use a fake playback backend; do not introduce audible alarms into the test suite. Update the README when user-facing controls or behavior change.
