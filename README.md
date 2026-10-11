# Pomodoro Bar

Double-click **Launch Pomodoro.vbs** to open the small, always-on-top Windows timer. Requires Python with Tkinter, already installed on this computer. Launching it again opens the existing timer's editor.

A **Pomodoro Timer** shortcut is also installed on this computer's desktop. It launches the same app without a terminal window. After a code update, quit the running timer and reopen it to load the new version.

## Floating bar

- **Progress circle:** progress through the entire focus-plus-break cycle. Purple means focus; green means break. Click it to dismiss a milestone flash. At section completion, clicking it acknowledges and starts the next section.
- **Countdown:** time remaining in the current section. Digits continuously fade from white to green, yellow, orange, and red.
- **Play / pause:** start or pause. At completion, starts the next section.
- **Skip:** advance, preserving paused/running state. At completion, acknowledges and starts the next section.
- **Pencil:** open the independent editor centered on the primary screen. Opening the editor does not move or resize the floating bar.
- **Drag the timer digits or empty bar space:** move the bar.
- **Right-click:** edit, reset, or quit.

## Editor

Timing controls are **30 minutes**, **60 minutes**, **+1 minute**, **+5 minutes**, and **+10 minutes**. Choices apply immediately and create a new paused session. Press play on the bar to begin.

The plus buttons increase the total focus-plus-break duration, preserving a 5:1 focus-to-break ratio and the selected preset's alert percentages. Adding one minute to the 30-minute preset gives 25:50 focus and 05:10 break.

**Show floating bar** hides or shows the bar while the timer keeps running. Closing the editor keeps the selected visibility. If both windows are hidden, double-click the launcher again to reopen the editor, then check Show floating bar.

**Done** closes the editor. **Quit** exits. Appearance, colors, and alerts are handled by code; there are no appearance selectors.

**Resize floating bar** enables standard Windows sizing edges around the bar. Drag an edge or corner, then turn resize off to restore the borderless bar. The circle, countdown, buttons, and click targets scale together to fit the window, preserving their proportions. Width and height are saved automatically. You can shrink the bar to 130 × 20 pixels; its maximum size is 1000 × 120 pixels. Extra space is centered when the window has a different aspect ratio.

## Alerts and colors

Milestones refer to **time remaining in focus**, not the whole session.

| Preset | Focus / break | Alert percentages remaining | Time remaining in focus |
| --- | --- | --- | --- |
| 30 minutes | 25 / 5 minutes | 40%, 20% | 10:00, 05:00 |
| 60 minutes | 50 / 10 minutes | 60%, 20%, 10% | 30:00, 10:00, 05:00 |

Milestones throb for four seconds. At the end of either section, the bar pulses at 00:00 until acknowledged. Waiting does not consume time from the next section. Click the circle, play, or skip to acknowledge and start the next section.

Countdown colors interpolate continuously: white at 100%, green at 80%, yellow at 40%, orange at 20%, and red at 5% remaining. This applies separately to focus and break.

Duration and position are saved in settings.json beside the app. Reopening starts a fresh paused focus period with the bar visible. The bar stays above ordinary desktop windows; exclusive full-screen apps and Windows secure screens can cover it.

## Desktop choice

Chrome Document Picture-in-Picture offers an always-on-top window, but the originating page must remain open, the page cannot set window position, and Chrome may limit its size. The desktop app gives this bar direct control over size and placement.

Reference: https://developer.chrome.com/docs/web-platform/document-picture-in-picture
