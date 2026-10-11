import importlib.machinery
import importlib.util
from pathlib import Path
import tempfile
import tkinter as tk
import unittest

APP = Path(__file__).with_name("timer.pyw")
if not APP.exists():
    APP = Path(__file__).resolve().parents[1] / "outputs" / "pomodoro-bar" / "timer.pyw"
loader = importlib.machinery.SourceFileLoader("pomodoro", str(APP))
spec = importlib.util.spec_from_loader(loader.name, loader)
m = importlib.util.module_from_spec(spec)
loader.exec_module(m)


class Clock:
    now = 0.0
    def __call__(self):
        return self.now


class TimerTests(unittest.TestCase):
    def test_countdown_gradient_endpoints_and_interpolation(self):
        def rgb(value):
            return tuple(int(value[i:i+2], 16)*257 for i in (1, 3, 5))
        hexes = [m.DEFAULTS[k] for k in ("timer_white", "timer_green", "timer_yellow", "timer_orange", "timer_red")]
        colors = [rgb(value) for value in hexes]
        for fraction, expected in zip([1, .8, .4, .2, .05], hexes):
            self.assertEqual(m.countdown_color(fraction, colors), expected)
        self.assertEqual(m.countdown_color(0, colors), hexes[-1])
        self.assertNotEqual(m.countdown_color(.6, colors), hexes[1])
        self.assertNotEqual(m.countdown_color(.6, colors), hexes[2])
        self.assertNotEqual(m.countdown_color(.6, colors), m.countdown_color(.6-1/1500, colors))

    def check_profile(self, focus, rest, thresholds, remaining):
        clock = Clock()
        timer = m.Timer(focus, rest, thresholds, clock)
        timer.toggle()
        for seconds in remaining:
            clock.now = focus * 60 - seconds + .01
            self.assertEqual(timer.tick(), ["milestone"])
            self.assertEqual(timer.tick(), [])
        clock.now = focus * 60 + 120
        self.assertEqual(timer.tick(), ["complete"])
        self.assertTrue(timer.waiting)
        self.assertFalse(timer.running)
        self.assertEqual(timer.remaining, 0)
        self.assertAlmostEqual(timer.progress, focus / (focus + rest))
        clock.now += 100
        self.assertEqual(timer.tick(), [])
        timer.toggle()
        self.assertEqual(timer.phase, "break")
        self.assertEqual(timer.remaining, rest * 60)
        self.assertTrue(timer.running)
        clock.now += rest * 60
        self.assertEqual(timer.tick(), ["complete"])
        self.assertEqual(timer.progress, 1)
        timer.toggle()
        self.assertEqual(timer.phase, "focus")
        self.assertEqual(timer.progress, 0)

    def test_30_minute_profile(self):
        self.check_profile(25, 5, [40, 20], [600, 300])

    def test_60_minute_profile(self):
        self.check_profile(50, 10, [60, 20, 10], [1800, 600, 300])

    def test_pause_resume_skip_and_sleep(self):
        clock = Clock()
        timer = m.Timer(25, 5, [40, 20], clock)
        timer.toggle()
        clock.now = 25
        timer.toggle()
        self.assertEqual(timer.remaining, 1475)
        clock.now = 1000
        timer.tick()
        self.assertEqual(timer.remaining, 1475)
        timer.advance()
        self.assertFalse(timer.running)
        timer.toggle()
        clock.now += 301
        self.assertEqual(timer.tick(), ["complete"])
        timer.advance()
        clock.now += 2000
        self.assertEqual(timer.tick(), ["milestone", "milestone", "complete"])

    def test_proportional_custom_duration(self):
        clock = Clock()
        timer = m.Timer(40, 8, [40, 20], clock)
        timer.toggle()
        clock.now = 1440
        self.assertEqual(timer.tick(), ["milestone"])
        self.assertEqual(timer.remaining, 960)


class WindowTests(unittest.TestCase):
    def test_window_settings_and_acknowledgement(self):
        with tempfile.TemporaryDirectory(dir=Path(__file__).parent) as directory:
            root = tk.Tk()
            settings = Path(directory) / "settings.json"
            app = m.App(root, settings)
            root.update()
            self.assertTrue(root.attributes("-topmost"))
            self.assertTrue(root.overrideredirect())
            self.assertEqual(root.winfo_width(), 340)
            app.resize_enabled.set(True)
            app.toggle_resize()
            root.update()
            self.assertFalse(root.overrideredirect())
            root.geometry("420x72")
            root.update()
            self.assertEqual(app.config["width"], 420)
            self.assertEqual(app.config["height"], 72)
            app.resize_enabled.set(False)
            app.toggle_resize()
            root.update()
            self.assertTrue(root.overrideredirect())
            self.assertEqual(root.winfo_width(), 420)
            bar_geometry = root.geometry()
            app.open_editor()
            root.update()
            self.assertTrue(app.panel.winfo_ismapped())
            self.assertTrue(app.editor.winfo_ismapped())
            self.assertEqual(root.geometry(), bar_geometry)
            app.set_session(60, [60, 20, 10])
            self.assertEqual(app.timer.focus, 3000)
            self.assertEqual(app.timer.thresholds, [60, 20, 10])
            app.toggle()
            app.bar_visible.set(False)
            app.toggle_visibility()
            root.update()
            self.assertFalse(root.winfo_ismapped())
            self.assertTrue(app.editor.winfo_ismapped())
            self.assertTrue(app.timer.running)
            app.close_editor()
            root.update()
            self.assertFalse(root.winfo_ismapped())
            app.open_editor()
            app.bar_visible.set(True)
            app.toggle_visibility()
            root.update()
            self.assertTrue(root.winfo_ismapped())
            app.add_minutes(1)
            self.assertAlmostEqual(app.timer.focus + app.timer.rest, 61 * 60)
            self.assertFalse(app.timer.running)
            app.add_minutes(5)
            app.add_minutes(10)
            self.assertAlmostEqual(app.timer.focus + app.timer.rest, 76 * 60)
            self.assertAlmostEqual(app.timer.focus / app.timer.rest, 5)
            app.set_session(60, [60, 20, 10])
            app.toggle()
            self.assertTrue(settings.exists())
            app.close_editor()
            root.update()
            self.assertEqual(root.winfo_width(), 420)
            app.timer.deadline = app.timer.clock() - 1
            root.after_cancel(app.after_id)
            app.update()
            self.assertTrue(app.timer.waiting)
            app.acknowledge()
            self.assertEqual(app.timer.phase, "break")
            self.assertTrue(app.timer.running)
            app.close()
            root = tk.Tk()
            restored = m.App(root, settings)
            root.update()
            self.assertEqual(restored.config["width"], 420)
            self.assertEqual(restored.config["height"], 72)
            self.assertEqual(restored.timer.focus, 3000)
            restored.close()


if __name__ == "__main__":
    unittest.main(verbosity=2)
