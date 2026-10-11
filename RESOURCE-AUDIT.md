# Pomodoro Bar Resource Audit

## Measured Baseline — October 11, 2026

- Measure installed Prod **1.1.5**, commit `b2cd4a8596ca9712a5b432fae41ae832c582175c`, on an eight-logical-processor Windows machine.
- Observe the existing app without changing its timer, settings, visibility, or update state: paused focus, both windows present, no active update or alarm.
- Sample the same installed process 60 times across **61.58 seconds**, using a hidden, unpackaged observer to avoid Codex process isolation.
- Record **152.6 MiB average resident working set**, ranging from **148.8 to 154.4 MiB**.
- Record **68.3 MiB private committed memory**, 15 threads, and 693 handles at the first sample.
- Record **2.25 CPU seconds**, equivalent to **0.457% of total machine CPU** or **3.65% of one core**.
- Interpret resident working set as physical pages currently mapped into the process, including shared pages; private commit is a different measure, not additional resident RAM. Do not add the two numbers.
- Treat this as a short baseline, not a leak test or a guaranteed result on another computer. CPU excludes GPU work, Desktop Window Manager, and Windows audio-service costs; no battery-power measurements were taken.

## Controlled Comparisons

- Build an isolated Release harness against the same app code; use silent fake audio, no settings path, and `services:false` to exclude updater, tray, and singleton IPC.
- Warm each scenario for ten seconds, then take thirty samples, recording actual elapsed time rather than assuming every sample lands exactly on a second.
- Change only the disposable harness's private dispatcher interval for the experimental cases; leave installed Prod untouched.
- Retain harness source and raw readings in local ignored `artifacts/resource-audit/`, including `installed.json` and `bin/Release/net10.0-windows/scenarios.json`.
- Interpret scenario differences cautiously: sequential tests share one process, memory is retained between cases, visibility/foreground scheduling varies, and compilation/regression checks overlapped the early cases. This is directional evidence, not a statistically repeated benchmark or an additive per-component attribution.

| Isolated scenario | Sample seconds | Total CPU | Resident MiB | Private commit MiB | Allocation KiB/sec |
| --- | ---: | ---: | ---: | ---: | ---: |
| Paused, both visible, current 10 Hz | 30.97 | 1.627% | 124.2 | 55.7 | 280.7 |
| Running, bar only, current 10 Hz | 42.37 | 1.189% | 126.2 | 53.8 | 374.2 |
| Running, both hidden, current 10 Hz | 31.26 | 0.625% | 126.1 | 52.7 | 333.2 |
| Paused, both hidden, refresh stopped | 30.94 | 0.057% | 126.4 | 52.7 | 5.2 |
| Running, bar only, experimental 1 Hz | 30.35 | 0.097% | 126.6 | 52.8 | 79.2 |
| Paused, both visible, refresh stopped | 31.60 | 0.031% | 126.5 | 52.4 | 5.1 |

- Compare running bar-only cases: CPU fell about **92%** and allocation rate about **79%** in this run. Confirm with repeated production-equivalent runs before promising those savings.
- Compare paused visible cases: eliminating routine redraws removed most measured CPU work while retaining the visible windows; this experiment intentionally freezes the start-now end preview, so a real implementation must still schedule its minute-boundary changes.
- Observe substantial hidden-window work at 10 Hz: hiding alone did not stop editor syncing or its allocations.
- Observe only **2.8–4.3 MiB average managed heap** across the isolated cases, despite roughly 126 MiB resident memory. This argues against optimizing the tiny timer state first; it does not precisely attribute all remaining memory to WPF. Native allocations, runtime/code pages, graphics, and shared mappings were not individually profiled.
- Observe no large RAM reduction when dispatching stops; retained UI/framework memory remains. The isolated process excludes production services and has different lifetimes/geometry/visibility, so its 124–127 MiB is not a replacement for the installed 153 MiB baseline.
- Include the measurement harness's own asynchronous sampling and JSON bookkeeping in these process metrics; the approximately 5 KiB/sec stopped-refresh allocation floor is not purely application allocation.

## Baseline Root Causes

- Find unconditional **100 ms dispatching** in `src/AppHost.cs`: `Tick()` calls `Render()` even when `Timer.Tick()` has nothing to do because the timer is paused.
- Find unconditional refresh of both windows in `Render()`: invalidate the bar and call `Editor.Sync()` regardless of visibility or changed values.
- Find repeated strings, color brushes, duration labels, checkbox assignments, and end-time calculations in `src/EditorWindow.cs`. The nearest-minute end preview is recomputed ten times per second; valid drafts are also reparsed every refresh.
- Find repeated text shaping/measurement, pens, transforms, triangles, and progress geometry in `src/BarWindow.cs`; even a paused countdown is rebuilt for each redraw.
- Find retained editor controls: closing hides the editor, while `AppHost` keeps a strong reference and continues syncing it.
- Separate the small timer model from framework overhead: the engine already uses monotonic deadlines, so it does not need ten polls per second to calculate elapsed time correctly.
- Rule out an idle audio loop: `Audio` starts native asynchronous playback only on events; completion looping is intentional until acknowledgement.
- Rule out frequent updater polling: update checks run at launch, manually, and every four hours; IPC normally awaits an asynchronous pipe connection. Those can cause occasional spikes but do not explain the continuous UI work.

## Recommended First Pass

1. **Make refresh scheduling depend on state.** Stop regular countdown redraws while paused. Keep the visible paused editor's start-now end estimate current at the next rounded-minute boundary, and update immediately on editing or user actions. Refresh a running visible countdown once per second; use short animation bursts only for reminders and completion flashing.
2. **Do no hidden UI work.** Skip hidden editor syncing and hidden bar drawing; refresh when reopened. When both windows are hidden, schedule the next reminder or completion instead of polling the display. Preserve sleep/resume recovery and coalesced delayed reminders.
3. **Update only changed content.** Rebuild session labels when the session changes, countdown text when its displayed second changes, and end-time text when its rounded result changes. Parse a draft on text changes, not every tick. Preserve unsaved draft protection and immediate preview.
4. **Cache drawing resources.** Reuse stable pens and button geometry, cache countdown text until text/font/DPI changes, and freeze immutable WPF brushes/geometries. Keep dynamic color updates to the requested once-per-second cadence. [Microsoft's WPF object-performance guidance](https://learn.microsoft.com/en-us/dotnet/desktop/wpf/advanced/optimizing-performance-object-behavior) supports freezing reusable graphics objects.
5. **Re-measure before making structural changes.** Aim for CPU near the measurement floor when paused/hidden and substantially lower running CPU. Do not promise a RAM percentage: reducing redraw churn does not unload WPF or the .NET runtime.

## More Radical Options

- **Replace WPF with native Win32 windows and GDI/DirectWrite drawing in Rust or C++.** Keep one installed app, its two independent windows, timer engine semantics, shortcuts, and update flow. This is the strongest architectural candidate for lower resident memory because it removes the managed/WPF UI stack; no native rewrite was built or measured, so a specific MiB target would be speculation. Budget for DPI handling, accessibility, keyboard navigation, hit testing, installer/updater integration, and regression testing.
- **Use a tiny native timer/bar with a disposable editor process.** Launch the richer editor when needed and let it exit when closed so its runtime can actually unload. Present one installed product and one shortcut; startup must still open both requested windows. This introduces two processes, IPC, and synchronization complexity, and may recreate the confusion the user previously wanted removed. Prefer a single native process if doing a full rewrite.
- **Allow a sleep-until-reminder mode when both windows are hidden.** Persist the deadline and let Windows launch a helper at the next event, eliminating a resident app between events. This changes tray/reopening behavior and complicates sleep/resume, alarm acknowledgement, and updates. It cannot provide a live floating bar while using zero app RAM. A waitable timer is the simpler in-process alternative; it avoids polling but retains memory. [Microsoft's waitable-timer API](https://learn.microsoft.com/en-us/windows/win32/api/synchapi/nf-synchapi-setwaitabletimer) documents scheduling and resume behavior.
- **Experiment with EcoQoS only after removing wasted work.** Restrict it to suitable background work and measure alarm responsiveness. It can influence efficient core selection and frequency; it does not remove allocations or guarantee a lower CPU percentage. [Microsoft's QoS guidance](https://learn.microsoft.com/en-us/windows/win32/procthread/quality-of-service) describes these tradeoffs.
- **Replace the WinForms tray wrapper with direct `Shell_NotifyIcon` interop.** Test whether avoiding a second UI framework reduces the installed baseline. Expect a smaller opportunity than removing WPF, and count the implementation/accessibility cost; no isolated memory saving was measured.

## Avoid Cosmetic Optimizations

- Avoid periodic forced garbage collections or working-set trimming as the primary fix: they do not remove the unconditional refresh or framework baseline. Emptying the working set can merely trade apparent RAM savings for later page faults. [Microsoft's working-set API documentation](https://learn.microsoft.com/en-us/windows/win32/api/memoryapi/nf-memoryapi-setprocessworkingsetsize) explains this paging behavior.
- Avoid treating a smaller installer, compressed executable, or a single-file publish as evidence of lower runtime memory.
- Avoid proposing a simple Native AOT toggle for this app: Native AOT requires trimming, while WPF and Windows Forms trimming support is disabled in the SDK. [Native AOT requirements](https://learn.microsoft.com/en-us/dotnet/core/deploying/native-aot/) and [Windows trimming incompatibilities](https://learn.microsoft.com/en-us/dotnet/core/deploying/trimming/incompatibilities) describe the limitation.
- Avoid reducing reminder accuracy or dropping completion acknowledgement to save a small amount of CPU; preserve those behaviors while scheduling work more selectively.

## Verification Before Any Optimization Release

- Compare paused both-visible, running bar-only, running both-visible, all-hidden, active draft, reminder pulse, and completion-alarm states with repeated warmed runs and no overlapping builds.
- Record resident and private-resident memory separately from commit and managed heap; include longer retention/handle checks and GPU/DWM observations before claiming a leak fix or total-system savings.
- Confirm correct alerts after sleep/resume, backward-assigned sounds, completion looping and acknowledgement, protected drafts, live rounded end times, DPI changes, tiny resize, and independent window reopening.
- Keep startup/update spikes separate from steady-state timer costs; measure downloaded-update staging independently.
- Preserve current installation identity, Dev/Prod isolation, one shortcut, and two startup windows.

## Initial Audit Deliverables

- Add this measured report and record the audit in the versioned changelog.
- Leave application source, version, installed Prod, settings, and release assets unchanged.
- Pass existing native and changelog regressions and Dev/Release builds before committing the documentation.

## Implemented WPF Optimizations

- Keep WPF as selected; retain one installed product and two independent windows.
- Replace unconditional 10 Hz dispatching with one-shot scheduling for visible countdown seconds, hidden milestones, and rounded paused/draft end-preview changes.
- Refresh on window restoration, Windows resume, and wall-clock changes; preserve monotonic deadlines and coalesced delayed reminders.
- Animate only the visible bar during reminder/completion warnings; keep hidden completion audio independent of UI scheduling.
- Cache formatted bar text, layout transforms, pens, progress/button geometry, editor duration labels, color brushes, and parsed draft values.
- Replace WinForms tray and monitor enumeration with native Windows shell/monitor APIs. Use the same embedded channel icon and release owned native icons/menus/windows on disposal.
- Preserve the original audit tables above as historical evidence; the remaining radical options are alternatives, not implemented product features.

### Matched Measurements

- Compare pre-optimization commit `8e7aac4` with the optimized WPF build using the same tracked `tests/ResourceAudit.cs` harness.
- Run the old build's seven scenarios first, then the optimized build's seven scenarios, with ten-second warmups and thirty samples each. Do not overlap these passes with builds.
- Include a real tray and both window classes; disable updater/IPC, user-settings access, and actual sound playback with `smoke:true`, no settings path, and fake audio.
- Retain raw readings locally at `artifacts/resource-audit/matched-baseline.json` and `matched-optimized.json`.

| Scenario | CPU before → after | Resident MiB before → after | Allocation KiB/sec before → after |
| --- | ---: | ---: | ---: |
| Paused, both visible | 0.522% → 0.129% | 134.8 → 105.0 | 242.8 → 6.4 |
| Running, bar only | 0.380% → 0.548% | 147.1 → 112.5 | 289.8 → 18.5 |
| Running, both visible | 0.373% → 0.399% | 150.5 → 120.7 | 289.0 → 36.0 |
| Running, both hidden | 0.142% → 0.077% | 142.5 → 114.2 | 289.7 → 5.3 |
| Paused, both hidden | 0.288% → 0.067% | 142.8 → 112.1 | 239.7 → 5.0 |
| Running, active draft | 0.428% → 0.441% | 145.9 → 120.1 | 367.6 → 81.7 |
| Completion, both visible, silent audio | 0.810% → 0.246% | 146.7 → 117.8 | 438.7 → 54.2 |

- Observe roughly **18–24% lower resident memory** across these cases and **78–98% lower allocation churn**.
- Observe lower paused, hidden, and completion CPU; retain the visible-running increases in the table rather than claiming every state improved.
- Treat the sampling/JSON harness overhead, sequential process aging, foreground policy, driver behavior, and background system activity as limitations. These are short runs on one machine, not a total-system power measurement or a production leak test.
- Confirm the optimized harness does not load `System.Windows.Forms` or `System.Drawing.Common`; do not attribute the entire RAM difference to any single change.
- Keep actual audio/update costs outside these silent steady-state comparisons. Their behavior remains covered by separate regressions.

### Follow-up: Running-Bar CPU and Renderer

- Repeat the bar-only comparison with per-thread CPU and scheduler/render counters. Record zero for counters unavailable in the old build; do not interpret those old-build zeros as zero refreshes.
- Record old/default rendering at **0.762% total CPU**, **126.9 MiB resident**, and **286.1 KiB/sec allocation** over 30.78 seconds.
- Record optimized/default rendering at **0.612% total CPU**, **107.8 MiB resident**, and **20.4 KiB/sec allocation** over 30.33 seconds.
- Confirm **30 scheduler wakes**, **30 bar renders**, and **zero hidden-editor syncs** in the optimized run.
- Record UI-thread CPU of **0.6875 seconds before** versus **0.15625 seconds after**, approximately **77% lower**. Other existing threads consumed about 1.20 versus 1.34 CPU seconds; their modules could not be resolved, so do not claim exact driver/runtime attribution.
- Test optimized/software rendering: **0.729% total CPU**, **87.6 MiB resident**, **20.2 KiB/sec allocation**, and 30 wakes/renders. Keep default rendering because this run showed a CPU tradeoff despite additional RAM savings.
- Retain the three `repeat-*.json` files in local ignored `artifacts/resource-audit/`. Treat these as repeated short runs, with clear visible-running variability rather than a guaranteed CPU percentage.
- Use the documented [WPF process render-mode API](https://learn.microsoft.com/en-us/dotnet/api/system.windows.media.renderoptions.processrendermode?view=windowsdesktop-10.0) only for controlled experiments; it changes the app's renderer, not global Windows settings.
