"""Floating Pomodoro bar. Standard-library-only, Python 3.10+ / Tk."""
import ctypes
import hashlib
import json
import math
import os
from pathlib import Path
import time
import tkinter as tk
from tkinter import ttk

DEFAULTS = dict(focus=25.0, rest=5.0, thresholds=[40.0, 20.0], width=340,
                height=52, font_size=22, background="#171c28", foreground="#f4f6fc",
                focus_color="#9bafff", break_color="#66dbb7", alert_color="#ffbc70",
                opacity=1.0, sound=False, x=80, y=80, gradient=True,
                timer_white="#f4f6fc", timer_green="#66dbb7", timer_yellow="#ffe082",
                timer_orange="#ffae66", timer_red="#ff5b6e")
SETTINGS = Path(__file__).with_name("settings.json")


def countdown_color(fraction, colors):
    """Interpolate Tk RGB colors by fraction of the current section remaining."""
    stops = [1.0, .8, .4, .2, .05, 0.0]
    colors = list(colors) + [colors[-1]]
    fraction = min(1.0, max(0.0, fraction))
    for index in range(len(stops) - 1):
        upper, lower = stops[index:index+2]
        if lower <= fraction <= upper:
            t = (upper - fraction) / (upper - lower)
            a, b = colors[index:index+2]
            return "#" + "".join(f"{round((x*(1-t)+y*t)/257):02x}" for x, y in zip(a, b))


class Timer:
    """Deadline-based clock; completion waits for explicit acknowledgement."""
    def __init__(self, focus, rest, thresholds, clock=time.perf_counter):
        self.clock = clock
        self.focus, self.rest = focus * 60, rest * 60
        self.thresholds = sorted(set(thresholds), reverse=True)
        self.phase = "focus"
        self.running = False
        self.waiting = False
        self.remaining = self.focus
        self.deadline = None
        self.fired = set()

    @property
    def duration(self):
        return self.focus if self.phase == "focus" else self.rest

    @property
    def progress(self):
        elapsed = self.duration - self.remaining
        if self.phase == "break":
            elapsed += self.focus
        return min(1, max(0, elapsed / (self.focus + self.rest)))

    def tick(self):
        if not self.running:
            return []
        self.remaining = max(0, self.deadline - self.clock())
        events = []
        if self.phase == "focus":
            for pct in self.thresholds:
                if pct not in self.fired and self.remaining <= self.focus * pct / 100:
                    self.fired.add(pct)
                    events.append("milestone")
        if self.remaining == 0:
            self.running = False
            self.waiting = True
            events.append("complete")
        return events

    def toggle(self):
        if self.waiting:
            self.advance(True)
        elif self.running:
            self.tick()
            self.running = False
        else:
            self.deadline = self.clock() + self.remaining
            self.running = True

    def advance(self, run=None):
        if run is None:
            run = self.running or self.waiting
        self.phase = "break" if self.phase == "focus" else "focus"
        self.remaining = self.duration
        self.waiting = False
        self.fired.clear()
        self.running = run
        self.deadline = self.clock() + self.remaining if run else None


class App:
    def __init__(self, root, settings_path=SETTINGS, reopen_event=None):
        self.root, self.settings_path = root, settings_path
        self.reopen_event = reopen_event
        self.config = DEFAULTS.copy()
        try:
            saved = json.loads(settings_path.read_text(encoding="utf-8"))
            candidate = {**self.config, **{key: saved[key] for key in ("focus", "rest", "thresholds", "x", "y", "width", "height") if key in saved}}
            self.validate(candidate)
            self.config = candidate
        except (OSError, ValueError, TypeError, KeyError, tk.TclError):
            pass
        self.timer = Timer(self.config["focus"], self.config["rest"], self.config["thresholds"])
        self.editor_open = False
        self.pulse_until = 0
        self.drag_origin = None
        self.resize_mode = False
        self.resize_save_id = None
        self.hitboxes = []
        root.title("Pomodoro Bar")
        root.overrideredirect(True)
        root.attributes("-topmost", True)
        root.attributes("-alpha", self.config["opacity"])
        root.configure(bg=self.config["background"])
        self.bar = tk.Canvas(root, highlightthickness=0)
        self.bar.pack(fill="both", expand=True)
        root.bind("<Configure>", self.on_resize)
        self.bar.bind("<ButtonPress-1>", self.press)
        self.bar.bind("<B1-Motion>", self.drag)
        self.bar.bind("<ButtonRelease-1>", self.release)
        self.bar.bind("<Motion>", self.hover)
        self.bar.bind("<Button-3>", lambda e: self.menu.tk_popup(e.x_root, e.y_root))
        self.menu = tk.Menu(root, tearoff=False)
        self.menu.add_command(label="Edit settings", command=self.open_editor)
        self.menu.add_command(label="Reset session", command=self.reset)
        self.menu.add_separator()
        self.menu.add_command(label="Quit", command=self.close)
        self.editor = tk.Toplevel(root)
        self.editor.withdraw()
        self.editor.title("Pomodoro Settings")
        self.editor.configure(bg="#222938")
        self.editor.protocol("WM_DELETE_WINDOW", self.close_editor)
        self.editor.bind("<Escape>", lambda e: self.close_editor())
        self.panel_shell = ttk.Frame(self.editor)
        self.panel_shell.pack(fill="both", expand=True)
        self.panel_canvas = tk.Canvas(self.panel_shell, bg="#222938", highlightthickness=0)
        scrollbar = ttk.Scrollbar(self.panel_shell, orient="vertical", command=self.panel_canvas.yview)
        scrollbar.pack(side="right", fill="y")
        self.panel_canvas.pack(side="left", fill="both", expand=True)
        self.panel_canvas.configure(yscrollcommand=scrollbar.set)
        self.panel = ttk.Frame(self.panel_canvas, padding=18)
        self.panel_item = self.panel_canvas.create_window((0, 0), window=self.panel, anchor="nw")
        self.panel.bind("<Configure>", lambda e: self.panel_canvas.configure(scrollregion=self.panel_canvas.bbox("all")))
        self.panel_canvas.bind("<Configure>", lambda e: self.panel_canvas.itemconfigure(self.panel_item, width=e.width))
        self.editor.bind("<MouseWheel>", self.scroll_settings)
        self.make_panel()
        self.layout(initial=True)
        self.make_taskbar_window()
        root.bind("<space>", self.space)
        root.protocol("WM_DELETE_WINDOW", self.close)
        self.update()

    def make_taskbar_window(self):
        """Keep the borderless Tk window discoverable as a normal Windows app."""
        if os.name != "nt":
            return
        user32 = ctypes.windll.user32
        user32.GetParent.argtypes = [ctypes.c_void_p]
        user32.GetParent.restype = ctypes.c_void_p
        user32.GetWindowLongPtrW.argtypes = [ctypes.c_void_p, ctypes.c_int]
        user32.GetWindowLongPtrW.restype = ctypes.c_ssize_t
        user32.SetWindowLongPtrW.argtypes = [ctypes.c_void_p, ctypes.c_int, ctypes.c_ssize_t]
        user32.SetWindowLongPtrW.restype = ctypes.c_ssize_t
        hwnd = user32.GetParent(self.root.winfo_id())
        if hwnd:
            exstyle = user32.GetWindowLongPtrW(hwnd, -20)
            user32.SetWindowLongPtrW(hwnd, -20, (exstyle & ~0x80) | 0x40000)
            self.root.withdraw()
            self.root.after_idle(self.root.deiconify)

    def validate(self, cfg):
        for name in ("focus", "rest"):
            value = float(cfg[name])
            if not math.isfinite(value) or not .05 <= value <= 1440:
                raise ValueError("Timer lengths must be between 0.05 and 1440 minutes.")
        for name, low, high in (("width", 260, 1000), ("height", 36, 120), ("font_size", 12, 48)):
            if not low <= int(cfg[name]) <= high:
                raise ValueError(f"{name.replace('_', ' ').title()} must be {low}–{high}.")
        if not .3 <= float(cfg["opacity"]) <= 1:
            raise ValueError("Opacity must be between 0.3 and 1.")
        if any(not math.isfinite(float(p)) or not 0 < float(p) < 100 for p in cfg["thresholds"]):
            raise ValueError("Alert percentages must be greater than 0 and less than 100.")
        for name in ("background", "foreground", "focus_color", "break_color", "alert_color", "timer_white", "timer_green", "timer_yellow", "timer_orange", "timer_red"):
            self.root.winfo_rgb(cfg[name])

    def make_panel(self):
        style = ttk.Style(self.root)
        style.theme_use("clam")
        style.configure("TFrame", background="#222938")
        style.configure("TLabel", background="#222938", foreground="#eef1fa", font=("Segoe UI", 10))
        style.configure("TCheckbutton", background="#222938", foreground="#eef1fa")
        style.configure("TButton", font=("Segoe UI", 11), padding=9)
        self.status = tk.StringVar()
        self.session_label = tk.StringVar()
        ttk.Label(self.panel, text="Study session", font=("Segoe UI", 16, "bold")).pack(anchor="w")
        ttk.Label(self.panel, textvariable=self.session_label, font=("Segoe UI", 12)).pack(anchor="w", pady=(8, 2))
        ttk.Label(self.panel, textvariable=self.status).pack(anchor="w", pady=(0, 16))
        presets = ttk.Frame(self.panel)
        presets.pack(fill="x", pady=(0, 12))
        ttk.Button(presets, text="30 minutes", command=lambda: self.set_session(30, [40, 20])).pack(side="left", expand=True, fill="x", padx=(0, 6))
        ttk.Button(presets, text="60 minutes", command=lambda: self.set_session(60, [60, 20, 10])).pack(side="left", expand=True, fill="x", padx=(6, 0))
        additions = ttk.Frame(self.panel)
        additions.pack(fill="x", pady=(0, 16))
        for minutes in (1, 5, 10):
            ttk.Button(additions, text=f"+{minutes} minute" + ("s" if minutes != 1 else ""), command=lambda n=minutes: self.add_minutes(n)).pack(side="left", expand=True, fill="x", padx=3)
        self.bar_visible = tk.BooleanVar(value=True)
        ttk.Checkbutton(self.panel, text="Show floating bar", variable=self.bar_visible, command=self.toggle_visibility).pack(anchor="w", pady=(0, 12))
        self.resize_enabled = tk.BooleanVar(value=False)
        ttk.Checkbutton(self.panel, text="Resize floating bar · drag its edges", variable=self.resize_enabled, command=self.toggle_resize).pack(anchor="w", pady=(0, 12))
        self.note = tk.StringVar(value="Choose a duration, then press play on the bar.")
        ttk.Label(self.panel, textvariable=self.note, wraplength=410).pack(anchor="w", pady=(0, 16))
        actions = ttk.Frame(self.panel)
        actions.pack(fill="x")
        ttk.Button(actions, text="Quit", command=self.close).pack(side="left")
        ttk.Button(actions, text="Done", command=self.close_editor).pack(side="right")
        self.describe_session()

    def describe_session(self):
        total = self.config["focus"] + self.config["rest"]
        self.session_label.set(f"{total:g} minutes · {self.format_time(self.config['focus']*60)} focus + {self.format_time(self.config['rest']*60)} break")

    def set_session(self, total, thresholds=None):
        if total > 1440:
            self.note.set("Maximum session length is 24 hours.")
            return
        self.config["focus"] = total * 5 / 6
        self.config["rest"] = total / 6
        if thresholds is not None:
            self.config["thresholds"] = thresholds
        self.reset()
        self.describe_session()
        if self.save():
            self.note.set("Saved. New session is paused; press play to begin.")

    def add_minutes(self, minutes):
        self.set_session(self.config["focus"] + self.config["rest"] + minutes)

    def save(self):
        self.config["x"], self.config["y"] = self.root.winfo_x(), self.root.winfo_y()
        try:
            temp = self.settings_path.with_suffix(".tmp")
            temp.write_text(json.dumps({key: self.config[key] for key in ("focus", "rest", "thresholds", "x", "y", "width", "height")}, indent=2), encoding="utf-8")
            temp.replace(self.settings_path)
            return True
        except OSError as exc:
            self.note.set(f"Could not save settings: {exc}")
            return False

    def layout(self, initial=False):
        w = self.config["width"]
        h = self.config["height"]
        self.bar.configure(width=w, height=h)
        self.root.update_idletasks()
        sw, sh = self.root.winfo_screenwidth(), self.root.winfo_screenheight()
        x, y = (self.config["x"], self.config["y"]) if initial else (self.root.winfo_x(), self.root.winfo_y())
        # Keep restored settings reachable; preserve intentional multi-monitor moves during use.
        if initial:
            x, y = max(0, min(int(x), sw - w)), max(0, min(int(y), sh - self.config["height"]))
        self.root.geometry(f"{w}x{h}{int(x):+d}{int(y):+d}")
        self.render()

    def open_editor(self):
        if not self.editor_open:
            self.editor.update_idletasks()
            width = 480
            height = min(self.panel.winfo_reqheight(), self.root.winfo_screenheight() - 100)
            # Center the editor on the primary screen, independently of the bar.
            x = max(0, (self.root.winfo_screenwidth() - width) // 2)
            y = max(0, (self.root.winfo_screenheight() - height) // 2 - 20)
            self.editor.geometry(f"{width}x{height}+{x}+{y}")
            self.editor.minsize(480, 310)
            self.editor.deiconify()
            self.editor_open = True
        self.editor.lift()
        self.editor.focus_force()

    def close_editor(self):
        self.editor.withdraw()
        self.editor_open = False

    def toggle_visibility(self):
        if self.bar_visible.get():
            self.root.deiconify()
            self.root.attributes("-topmost", True)
        else:
            self.root.withdraw()

    def toggle_resize(self):
        self.resize_mode = self.resize_enabled.get()
        self.root.overrideredirect(not self.resize_mode)
        self.root.resizable(self.resize_mode, self.resize_mode)
        self.root.minsize(260, 36)
        self.root.maxsize(1000, 120)
        if self.resize_mode:
            self.bar_visible.set(True)
            self.toggle_visibility()
            self.root.lift()
            self.note.set("Drag the bar's window edges. Turn resize off when finished.")
        else:
            self.make_taskbar_window()
            if not self.bar_visible.get():
                self.root.after_idle(self.root.withdraw)
            self.save()
            self.note.set("Bar size saved.")

    def on_resize(self, event):
        if event.widget is self.root and self.resize_mode:
            self.config["width"] = max(260, min(1000, event.width))
            self.config["height"] = max(36, min(120, event.height))
            self.render()
            if self.resize_save_id:
                self.root.after_cancel(self.resize_save_id)
            self.resize_save_id = self.root.after(300, self.save_resize)

    def save_resize(self):
        self.resize_save_id = None
        self.save()

    def scroll_settings(self, event):
        if self.editor_open:
            self.panel_canvas.yview_scroll(-int(event.delta/120), "units")

    def reset(self):
        self.timer = Timer(self.config["focus"], self.config["rest"], self.config["thresholds"])
        self.pulse_until = 0
        self.render()

    def toggle(self):
        self.timer.toggle()
        self.pulse_until = 0
        self.render()

    def skip(self):
        self.timer.advance()
        self.pulse_until = 0
        self.render()

    def acknowledge(self):
        if self.timer.waiting:
            self.timer.advance(True)
        self.pulse_until = 0
        self.render()

    def space(self, event):
        if not isinstance(self.root.focus_get(), (ttk.Entry, tk.Entry)):
            self.toggle()
            return "break"

    @staticmethod
    def format_time(seconds):
        seconds = max(0, math.ceil(seconds - 1e-7))
        return f"{seconds // 60:02d}:{seconds % 60:02d}"

    def render(self):
        c, cfg, timer = self.bar, self.config, self.timer
        w = cfg["width"]
        h, fg = cfg["height"], cfg["foreground"]
        pulsing = timer.waiting or time.perf_counter() < self.pulse_until
        strength = (.5 + .5 * math.sin(time.perf_counter() * math.pi * 2)) * .55 if pulsing else 0
        def blend(a, b, t):
            aa, bb = self.root.winfo_rgb(a), self.root.winfo_rgb(b)
            return "#" + "".join(f"{int((x*(1-t)+y*t)/257):02x}" for x, y in zip(aa, bb))
        bg = blend(cfg["background"], cfg["alert_color"], strength)
        c.configure(bg=bg)
        c.delete("all")
        c.create_rectangle(0, 0, w-1, h-1, outline=blend(bg, fg, .22))
        cy, radius = h/2, min(16, h/2-7)
        cx = radius + 15
        color = cfg["focus_color"] if timer.phase == "focus" else cfg["break_color"]
        if timer.waiting:
            color = cfg["alert_color"]
        c.create_oval(cx-radius, cy-radius, cx+radius, cy+radius, outline=blend(bg, fg, .2), width=3)
        progress = timer.progress
        if progress > 0:
            if progress >= .9999:
                c.create_oval(cx-radius, cy-radius, cx+radius, cy+radius, outline=color, width=3)
            else:
                c.create_arc(cx-radius, cy-radius, cx+radius, cy+radius, start=90, extent=-360*progress, style="arc", outline=color, width=3)
        if timer.waiting:
            c.create_text(cx, cy, text="!", fill=fg, font=("Segoe UI", max(10, int(radius)), "bold"))
        else:
            c.create_oval(cx-3, cy-3, cx+3, cy+3, outline="", fill=color)
        # Reserve control space and shrink oversized type to keep small bars usable.
        font_size = min(48, round(cfg["font_size"]*h/52), int(h*.49), int((w-165)/3.2))
        digits = countdown_color(timer.remaining / timer.duration, [self.root.winfo_rgb(cfg[key]) for key in ("timer_white", "timer_green", "timer_yellow", "timer_orange", "timer_red")]) if cfg["gradient"] else fg
        c.create_text(cx+radius+17, cy, text=self.format_time(timer.remaining), anchor="w", fill=digits, font=("Consolas", font_size, "bold"))
        self.hitboxes = [(cx-radius-5, 0, cx+radius+5, h, self.acknowledge)]
        centers = [w-99, w-61, w-23]
        for x, action in zip(centers, (self.toggle, self.skip, self.open_editor)):
            self.hitboxes.append((x-17, 0, x+17, h, action))
        x = centers[0]
        if timer.running:
            for offset in (-5, 3):
                c.create_rectangle(x+offset, cy-7, x+offset+3, cy+7, fill=fg, outline="")
        else:
            c.create_polygon(x-5, cy-8, x-5, cy+8, x+7, cy, fill=fg)
        x = centers[1]
        c.create_polygon(x-7, cy-7, x-7, cy+7, x+3, cy, fill=fg)
        c.create_line(x+6, cy-7, x+6, cy+7, fill=fg, width=2)
        x = centers[2]
        # Pencil, drawn without depending on an installed icon font.
        c.create_polygon(x-7, cy+4, x+3, cy-6, x+7, cy-2, x-3, cy+8, fill="", outline=fg, width=2)
        c.create_line(x+1, cy-4, x+5, cy, fill=fg, width=2)
        c.create_polygon(x-7, cy+4, x-8, cy+9, x-3, cy+8, fill=fg)
        state = "Click the ring or play to begin the next section" if timer.waiting else ("Running" if timer.running else "Paused")
        self.status.set(f"{timer.phase.title()} · {self.format_time(timer.remaining)} · {state}")

    def press(self, event):
        for x1, y1, x2, y2, action in self.hitboxes:
            if x1 <= event.x <= x2 and y1 <= event.y <= y2:
                action()
                return
        self.drag_origin = (event.x_root, event.y_root, self.root.winfo_x(), self.root.winfo_y())

    def drag(self, event):
        if self.drag_origin:
            sx, sy, x, y = self.drag_origin
            self.root.geometry(f"{x+event.x_root-sx:+d}{y+event.y_root-sy:+d}")

    def release(self, event):
        if self.drag_origin:
            self.drag_origin = None
            self.save()

    def hover(self, event):
        clickable = any(a <= event.x <= b and y <= event.y <= z for a, y, b, z, _ in self.hitboxes)
        self.bar.configure(cursor="hand2" if clickable else "fleur")

    def update(self):
        if self.reopen_event:
            kernel, event = self.reopen_event
            if kernel.WaitForSingleObject(event, 0) == 0:
                self.open_editor()
        events = self.timer.tick()
        if events:
            self.pulse_until = time.perf_counter() + 4
            if self.config["sound"]:
                self.root.bell()
        self.render()
        self.after_id = self.root.after(80, self.update)

    def close(self):
        self.save()
        self.root.after_cancel(self.after_id)
        if self.resize_save_id:
            self.root.after_cancel(self.resize_save_id)
        self.root.destroy()


def main():
    mutex = None
    event = None
    if os.name == "nt":
        kernel = ctypes.WinDLL("kernel32", use_last_error=True)
        kernel.CreateMutexW.argtypes = [ctypes.c_void_p, ctypes.c_bool, ctypes.c_wchar_p]
        kernel.CreateMutexW.restype = ctypes.c_void_p
        kernel.CloseHandle.argtypes = [ctypes.c_void_p]
        kernel.CreateEventW.argtypes = [ctypes.c_void_p, ctypes.c_bool, ctypes.c_bool, ctypes.c_wchar_p]
        kernel.CreateEventW.restype = ctypes.c_void_p
        kernel.SetEvent.argtypes = [ctypes.c_void_p]
        kernel.WaitForSingleObject.argtypes = [ctypes.c_void_p, ctypes.c_ulong]
        name = "Local\\PomodoroBar-" + hashlib.sha256(str(Path(__file__).resolve()).lower().encode()).hexdigest()[:20]
        mutex = kernel.CreateMutexW(None, False, name)
        existing = ctypes.get_last_error() == 183
        event = kernel.CreateEventW(None, False, False, name + "-Editor")
        if existing:
            kernel.SetEvent(event)
            kernel.CloseHandle(event)
            kernel.CloseHandle(mutex)
            return
        try:
            ctypes.windll.shcore.SetProcessDpiAwareness(1)
        except (AttributeError, OSError):
            pass
    try:
        root = tk.Tk()
        App(root, reopen_event=(kernel, event) if event else None)
        root.mainloop()
    finally:
        if mutex:
            kernel.CloseHandle(mutex)
        if event:
            kernel.CloseHandle(event)


if __name__ == "__main__":
    main()
