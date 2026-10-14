using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Runtime.InteropServices;
using System.Windows.Interop;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using PomodoroBar;
using PomodoroBar.Core;

internal static class Tests
{
    [DllImport("user32.dll", CharSet=CharSet.Unicode)]
    private static extern IntPtr SendMessage(IntPtr window,uint message,IntPtr wParam,IntPtr lParam);
    [DllImport("user32.dll",EntryPoint="RegisterWindowMessageW",CharSet=CharSet.Unicode)]
    private static extern uint RegisterWindowMessage(string name);
    [DllImport("user32.dll")] private static extern IntPtr GetShellWindow();
    private static int count;
    private static void Check(bool value,string reason) { if (!value) throw new Exception(reason); count++; }
    private static void Near(double a,double b,string reason) => Check(Math.Abs(a-b)<.001,reason);
    private static void Reject(Action action,string reason) { try { action(); } catch (ArgumentException) { count++; return; } throw new Exception(reason); }
    [STAThread]
    private static int Main(string[] args)
    {
        if (args.Contains("--resource-audit")) return ResourceAudit.Run(args);
        try {
            double now = 0;
            var t = new TimerModel(1500,300,[40,20],()=>now);
            Check(!t.Running && t.IsFocus,"starts paused focus");
            Check(double.IsPositiveInfinity(t.SecondsUntilNextEvent()),"paused engine needs no polling"); t.Toggle();
            Near(t.SecondsUntilNextEvent(),900,"schedule first focus milestone directly");
            now = 900; Check(t.Tick()=="second-last","30-minute first reminder at 10 left");
            Near(t.SecondsUntilNextEvent(),300,"schedule next unfired milestone");
            now = 1200; Check(t.Tick()=="last","last reminder at 5 left");
            now = 1500; Check(t.Tick()=="complete" && t.Waiting && !t.Running,"completion blocks");
            Check(double.IsPositiveInfinity(t.SecondsUntilNextEvent()),"acknowledgement waiting needs no engine polling");
            now = 2000; Check(t.Tick() is null && t.IsFocus,"completion waits for acknowledgement");
            t.Toggle(); Check(!t.IsFocus && t.Running && t.Remaining == 300,"ack starts break");
            Near(t.Progress,5d/6,"total cycle progress");
            now += 10; t.Tick(); t.Toggle(); now += 1000; t.Toggle(); now += 5; t.Tick(); Near(t.Remaining,285,"pause excludes paused time");
            t.Advance(false); Check(t.IsFocus && !t.Running,"skip remains paused when asked");
            now = 0; t = new TimerModel(3000,600,[60,20,10],()=>now); t.Toggle();
            now = 1200; Check(t.Tick()=="earlier","60-minute first is single beep at 30 left");
            now = 2400; Check(t.Tick()=="second-last","60-minute second at 10 left");
            now = 2700; Check(t.Tick()=="last","60-minute last at 5 left");
            now = 0; t = new TimerModel(3000,600,[60,20,10],()=>now); t.Toggle(); now = 2750;
            Check(t.Tick()=="last" && t.FiredCount==3,"resume coalesces overdue reminders");
            Check(t.Tick() is null,"no repeated reminder"); now=5000; Check(t.Tick()=="complete","resume completes without queued alarms");
            t.SetTime(7); Check(!t.Waiting && !t.Running && t.Remaining==7,"edit clears completion");
            var wall = new DateTimeOffset(2026,10,11,14,0,0,TimeSpan.Zero);
            Check(TimerModel.EstimatedEnd(wall,89)==wall.AddMinutes(1),"end preview rounds down below 30 seconds");
            Check(TimerModel.EstimatedEnd(wall,90)==wall.AddMinutes(2),"end preview rounds half a minute forward");
            Check(TimerModel.EstimatedEnd(wall.AddSeconds(20),70)==wall.AddMinutes(2),"round the resulting clock time rather than duration");
            Check(TimerModel.EstimatedEnd(wall.AddHours(9).AddMinutes(59),40).Date==wall.Date.AddDays(1),"minute rounding carries into the next day");
            t.Toggle(); now+=2; t.Tick(); t.SetTime(20); now+=3; t.Tick(); Near(t.Remaining,17,"running edit resets deadline");
            now=0;t=new TimerModel(1,1,[40,20],()=>now);t.Toggle();now=1;
            Check(t.Toggle()=="complete" && t.Waiting,"pause at deadline delivers completion");
            foreach (var (input,seconds) in new[] {("30",1800d),("12:34",754d),("1:02:03",3723d),("00:01",1d),("24:00:00",86400d)}) Near(TimerModel.ParseTime(input),seconds,"time parser "+input);
            foreach (string input in new[] {"0","00:60","1:60:00","1:2:3:4","-1","abc","１２","9999999999999999","1441"}) Reject(()=>TimerModel.ParseTime(input),"reject "+input);
            Check(TimerModel.FormatTime(59.1)=="01:00" && TimerModel.FormatTime(86400)=="24:00:00","format rounds up and shows hours");
            Check(VisualMath.CountdownColor(1)==new Rgb(244,246,252),"white start");
            Check(VisualMath.CountdownColor(.8)==new Rgb(102,219,183),"green");
            Check(VisualMath.CountdownColor(.4)==new Rgb(255,224,130),"yellow");
            Check(VisualMath.CountdownColor(.2)==new Rgb(255,174,102),"orange");
            Check(VisualMath.CountdownColor(.05)==new Rgb(255,91,110),"red finish");
            var l = BarLayout.Fit(130,20,86);
            Check(l.Scale > 0 && l.LogicalWidth*l.Scale<=130 && 52*l.Scale<=20,"tiny bar fits uniformly");
            Check(l.Hit(l.Left+(12+86+16)*l.Scale,l.Top+26*l.Scale)==2,"scaled play hit target");
            foreach(var size in new[] {(130d,120d),(1000d,20d),(280d,52d)}) {
                var fit=BarLayout.Fit(size.Item1,size.Item2,86);
                for(int button=0;button<3;button++) Check(fit.Hit(fit.Left+(12+86+16+button*32)*fit.Scale,fit.Top+26*fit.Scale)==button+2,"all controls scale together");
                Check(fit.Hit(fit.Left+22*fit.Scale,fit.Top+26*fit.Scale)==1,"countdown drag region scales");
            }
            var vertical = BarLayout.Fit(110,180,86,true);
            Check(vertical.LogicalWidth*vertical.Scale<=110 && vertical.LogicalHeight*vertical.Scale<=180,"vertical layout fits upright countdown");
            for (int button=0;button<3;button++) Check(vertical.Hit(vertical.Left+vertical.LogicalWidth/2*vertical.Scale,vertical.Top+(68+button*36)*vertical.Scale)==button+2,"vertical hit targets match stacked buttons");
            now=0;var scheduled=new TimerModel(1500,300,[40,20],()=>now);scheduled.Schedule(600);
            Near(scheduled.SecondsUntilNextEvent(),600,"engine schedules exact future start");
            now=605;Check(scheduled.Tick() is null && scheduled.Running,"scheduled focus auto-starts");Near(scheduled.Remaining,1495,"scheduled late tick catches up");
            now=0;scheduled.Schedule(60);now=2000;Check(scheduled.Tick()=="complete" && scheduled.Waiting && scheduled.IsFocus,"sleep across scheduled session still waits for acknowledgement");
            now=0;scheduled.Schedule(600);scheduled.Toggle();Check(!scheduled.Scheduled && !scheduled.Running,"pause cancels scheduled start");
            scheduled.Schedule(600);scheduled.RebaseScheduledStart(-100);scheduled.Tick();Near(scheduled.Remaining,1400,"wall-clock change rebases pending deadline and catches up");
            Near(TimerModel.RoundDuration(89),60,"duration rounds down");Near(TimerModel.RoundDuration(90),120,"duration ties round forward");
            Check(ClockTime.Parse("tomorrow 2:25 PM",wall)==wall.AddDays(1).AddMinutes(25),"explicit tomorrow time parsing");
            Reject(()=>ClockTime.Parse("2:",wall),"partial clock time rejected");
            Check(ClockTime.Parse("tomorrow 2:25 PM",wall.AddDays(1),wall)==wall.AddDays(1).AddMinutes(25),"explicit tomorrow does not add an extra day when editing tomorrow's endpoint");
            now=0;var endpointTimer=new TimerModel(1500,300,[40,20],()=>now);endpointTimer.Toggle();now=100;endpointTimer.Tick();endpointTimer.SetEndpoint(true,1440);
            Near(endpointTimer.Remaining,1440,"endpoint preserves exact deadline seconds");Near(endpointTimer.Focus,1560,"endpoint edit preserves elapsed focus when rounding total duration");
            uint flags=0;string? played=null;
            using(var nativeAudio=new Audio((file,bits)=>{played=file;flags=bits;return true;})) {
                nativeAudio.Play("last");Check(flags==0x20003 && played!.EndsWith("last.wav"),"audio uses asynchronous filename flags");
                nativeAudio.Play("complete",true);Check(flags==0x2000b,"completion adds looping flag");
                nativeAudio.Stop();Check(flags==0 && played is null,"stop cancels native audio");
            }
            foreach(string sound in new[] {"earlier","second-last","last","complete"}) {
                byte[] wav=File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory,"sounds",sound+".wav"));
                Check(System.Text.Encoding.ASCII.GetString(wav,0,4)=="RIFF" && BitConverter.ToUInt16(wav,22)==1 && BitConverter.ToUInt32(wav,24)==22050 && BitConverter.ToUInt16(wav,34)==16,"valid bundled PCM sound "+sound);
            }
            string scratch = Path.Combine(Path.GetTempPath(),"PomodoroBar.Tests-"+Guid.NewGuid()); Directory.CreateDirectory(scratch);
            try {
                string path = Path.Combine(scratch,"settings.json");
                var settings = new Settings { Focus=1380,Width=180,Height=24,X=-500,Y=30 };
                settings.Save(path); var loaded=Settings.Load(path); Near(loaded.Focus,1380,"settings persist"); Near(loaded.X,-500,"multi-monitor position persists");
                File.WriteAllText(Path.Combine(scratch,"legacy.json"),"{\"focus\":23,\"rest\":5,\"thresholds\":[40,20],\"width\":280,\"height\":52,\"x\":54,\"y\":968}");
                Near(Settings.LoadLegacy(Path.Combine(scratch,"legacy.json")).Focus,1380,"legacy minutes migrate to seconds");
                var dpi = Settings.LoadLegacy(Path.Combine(scratch,"legacy.json"),1.5);
                Near(dpi.Width,280/1.5,"legacy physical width converts to DIPs");Near(dpi.X,36,"legacy position converts to DIPs");
                File.WriteAllText(path,"{\"Thresholds\":null}");
                var corrupt = StartupSettings.Load(path);Check(corrupt.Warning is not null && corrupt.CanSave,"corrupt startup recovers");
                Check(Directory.GetFiles(scratch,"settings.json.backup-*").Length==1,"corrupt original is backed up");
                File.Delete(path);
                var missing = StartupSettings.Load(path,Path.Combine(scratch,"missing.json"));Check(missing.Warning is not null && !missing.CanSave,"missing import does not crash or overwrite");
                File.WriteAllText(Path.Combine(scratch,"badlegacy.json"),"{}");
                Check(StartupSettings.Load(path,Path.Combine(scratch,"badlegacy.json")).Warning is not null,"malformed legacy import recovers");
                Reject(()=>(settings with { Focus=double.NaN }).Validate(),"reject corrupt duration");
                Reject(()=>(settings with { Width=129 }).Validate(),"reject invalid geometry");
                UpdateFlowTests.Run(scratch);
                Gui(scratch,args.Contains("--capture"));
            } finally { Directory.Delete(scratch,true); }
            Console.WriteLine($"PASS: {count} assertions; timer, layout, migration, editor, audio and window behavior."); return 0;
        } catch (Exception ex) { Console.Error.WriteLine(ex); return 1; }
    }
    private static void Pump()
    {
        var frame=new DispatcherFrame(); Dispatcher.CurrentDispatcher.BeginInvoke(DispatcherPriority.Background,new Action(()=>frame.Continue=false)); Dispatcher.PushFrame(frame);
    }
    private static void Gui(string scratch,bool capture)
    {
        var app=new Application { ShutdownMode=ShutdownMode.OnExplicitShutdown };
        double now=0; var audio=new FakeAudio();
        var wallStart=DateTimeOffset.Now.Date.AddHours(14);
        using var host=new AppHost(new Settings(),Path.Combine(scratch,"gui.json"),audio,()=>now,services:false,wallClock:()=>new DateTimeOffset(wallStart).AddSeconds(now));
        Pump();
        Check(!typeof(AppHost).GetFields(System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance).Any(f=>f.FieldType==typeof(NativeTray)),"app host has no tray registration or tray-owned lifetime");
        Check(MonitorAreas.WorkingAreas().Count>0,"native monitor work areas available without WinForms");
        Check(!AppDomain.CurrentDomain.GetAssemblies().Any(a=>a.GetName().Name=="System.Windows.Forms"),"native windows and tray do not load a second UI framework");
        Check(host.Bar.IsVisible && host.Editor.IsVisible,"startup opens both windows by default");
        Check(host.Editor.FocusEndCard.Text==wallStart.AddMinutes(25).ToString("t"),"paused editor previews focus end if started now");
        Check(host.CanAutoRestart,"default editor visibility does not count as user interaction");
        Near(host.WakeInterval.TotalSeconds,30,"paused visible editor schedules rounded end-preview boundary");
        Check(!host.AnimationScheduled,"paused bar does not animate");
        int renders=host.Bar.Surface.RenderCount;
        host.Render();Pump();host.Render();Pump();
        Check(host.Bar.Surface.RenderCount==renders,"unchanged paused state does not redraw the bar");
        host.Bar.Hide();host.Editor.Hide();
        Check(!host.WakeScheduled,"paused hidden app has no recurring wakeups");
        int syncs=host.Editor.SyncCount;now=65;host.Tick();
        Check(host.Editor.SyncCount==syncs,"hidden editor does not receive timer sync");
        host.ShowApp();now=0;host.Reset();Pump();
        host.Toggle();host.Bar.Hide();host.Editor.Hide();
        Near(host.WakeInterval.TotalSeconds,900,"hidden running app waits for next reminder instead of display polling");
        now=905;host.Tick();
        Check(audio.Last=="second-last" && !host.AnimationScheduled,"hidden delayed reminder sounds without animating hidden windows");
        Near(host.WakeInterval.TotalSeconds,295,"hidden reminder reschedules following milestone");
        host.ShowApp();
        Check(host.Editor.TimeCard.Text=="09:55" && host.WakeInterval.TotalSeconds<=1,"reopening catches up visible countdown and resumes second updates");
        now=1500;host.Tick();host.Bar.Hide();host.Editor.Hide();
        Check(audio.Loop && !host.WakeScheduled && !host.AnimationScheduled,"hidden completion loops audio without UI polling");
        host.ShowApp();
        Check(host.Editor.AlarmButton.Visibility==Visibility.Visible,"reopened completion remains acknowledgeable");
        now=0;host.Reset();Pump();
        wallStart=wallStart.Date.AddHours(23).AddMinutes(50);host.Render();
        Check(host.Editor.FocusEndCard.Text==wallStart.Date.AddDays(1).AddMinutes(15).ToString("t"),"editor labels next-day end times");
        host.SetTime(1200);host.Toggle();host.Tick();
        Check(host.FocusEnd.Date==host.Now.Date.AddDays(1),"running cross-midnight end starts with tomorrow label");
        now=600;host.Tick();
        Check(host.Editor.FocusEndCard.Text==wallStart.Date.AddDays(1).AddMinutes(10).ToString("t"),"cached running end label changes when midnight passes");
        now=0;host.SetSession(30,[40,20]);
        wallStart=wallStart.Date.AddHours(14);host.Render();
        host.OpenEditor(); Pump();
        double width=host.Bar.Width,height=host.Bar.Height;
        Check(host.Bar.Topmost && host.Bar.WindowStyle==WindowStyle.None,"native always-on-top borderless window");
        Check(host.Editor.IsVisible && host.Bar.Width==width && host.Bar.Height==height,"editor independent of bar geometry");
        Check(host.Bar.ShowInTaskbar && host.Editor.ShowInTaskbar && host.Bar.Owner is null && host.Editor.Owner is null,"both windows are independently available in the taskbar");
        foreach(var window in new Window[] {host.Bar,host.Editor}) {
            Check(ReferenceEquals(window.Icon,AppIcon.Source),"window uses the bundled Pomodoro icon");
            var hwnd=new WindowInteropHelper(window).Handle;
            Check(SendMessage(hwnd,0x007f,new IntPtr(0),IntPtr.Zero)!=IntPtr.Zero,"Windows receives small taskbar icon");
            Check(SendMessage(hwnd,0x007f,new IntPtr(1),IntPtr.Zero)!=IntPtr.Zero,"Windows receives large window icon");
        }
        Check(!host.CanAutoRestart,"opening editor defers startup updates");
        host.SetSession(60,[60,20,10]); Near(host.Timer.Focus,3000,"60 preset"); Near(host.Timer.Rest,600,"60 break");
        host.AddMinutes(1);host.AddMinutes(5);host.AddMinutes(10); Near(host.Timer.Focus+host.Timer.Rest,76*60,"add buttons preserve total minutes"); Near(host.Timer.Focus,63*60,"additions use nearest whole-minute focus split"); Near(host.Timer.Rest,13*60,"break receives remaining whole minutes");
        host.SetSession(30,[40,20]); host.Toggle(); now=900; host.Tick(); Check(audio.Last=="second-last" && !audio.Loop,"native host reminder dispatch");
        now=1500; host.Tick(); Check(audio.Last=="complete" && audio.Loop,"completion loops");
        host.Bar.Hide();host.Render();Check(host.Editor.AlarmButton.Visibility==Visibility.Visible,"hidden-bar completion is acknowledgeable");
        Check(host.Editor.ShowInTaskbar,"hiding bar preserves editor taskbar entry");
        host.Bar.Show();
        Check(host.Bar.ShowInTaskbar && host.Editor.ShowInTaskbar,"showing bar preserves both taskbar windows");
        host.Silence(); Check(audio.Last is null && host.Timer.Waiting,"interaction silences without automatically advancing");
        host.Skip(); Check(!host.Timer.IsFocus && host.Timer.Running,"skip acknowledgement");
        now+=299;host.Tick();Check(host.CountdownBrush==UiTheme.Rest && host.Editor.BreakCard.Foreground==UiTheme.Rest,"break countdown stays green until its final second");
        now+=1;host.Tick();Check(host.Timer.Waiting && host.CountdownBrush==UiTheme.Complete,"break countdown turns red only at completion");
        Check(host.Editor.TimerPanel.ProgressColor==UiTheme.Complete,"completed break outline uses the same red");
        host.SetTime(300);host.Toggle();
        host.Bar.Hide();host.Render();Check(host.Timer.Running && host.Editor.IsVisible && host.Editor.ShowBar.IsChecked==false,"hidden bar independent of editor and clock");
        host.Bar.Show();host.Bar.SetResize(true);Check(host.Bar.ResizeMode==ResizeMode.CanResize,"native resize enabled");
        host.Bar.Width=130;host.Bar.Height=20;Pump();host.Render();Pump();
        Check(host.Bar.Surface.Layout.Scale<.5,"whole bar scales down");
        host.Bar.SetResize(false);Check(host.Bar.WindowStyle==WindowStyle.None && host.Bar.ResizeMode==ResizeMode.NoResize,"resize disables frame");
        host.Bar.ResetSize();Near(host.Bar.Width,280,"reset width");Near(host.Bar.Height,52,"reset height");
        host.SetSession(30,[40,20]); host.Toggle();
        void Edit(TextBox field,string text) {
            Keyboard.ClearFocus(); Pump(); field.Focus(); Keyboard.Focus(field); Pump();
            field.RaiseEvent(new KeyboardFocusChangedEventArgs(Keyboard.PrimaryDevice,0,host.Editor,field) { RoutedEvent=Keyboard.GotKeyboardFocusEvent });
            field.Text = text;
        }
        Edit(host.Editor.TimeCard,"07:42");
        Near(host.Timer.Remaining,480,"typing updates focus immediately and rounds to nearest minute");
        Check(host.Editor.FocusEndCard.Text==TimerModel.EstimatedEnd(host.Now,480).ToString("t"),"end field updates on keystroke");
        Check(host.Editor.TimeCard.Foreground==host.CountdownBrush && host.Editor.TimerPanel.ProgressColor==host.CountdownBrush,"running editor text and perimeter use the same changing color");
        now+=3;host.Tick();Check(host.Editor.TimeCard.Text=="07:42","live sync protects edited input");
        Check(host.Editor.FinishEdit(),"finish live duration edit");
        Near(host.Timer.Remaining,477,"finishing a live edit does not reset the running deadline");
        Edit(host.Editor.BreakCard,"06:29"); Near(host.Timer.Rest,360,"break duration independently editable and rounded");
        host.Editor.CancelEdit(); host.Save(); Near(Settings.Load(Path.Combine(scratch,"gui.json")).Rest,360,"custom break saves");
        Edit(host.Editor.TimeCard,"99:99");
        Check(!host.Editor.FinishEdit(),"invalid partial edit is rejected without changing timer");
        Near(host.Timer.Focus,480,"invalid draft preserves last valid focus duration");
        host.Editor.TimeCard.RaiseEvent(new KeyboardFocusChangedEventArgs(Keyboard.PrimaryDevice,0,host.Editor.TimeCard,host.Editor) { RoutedEvent=Keyboard.LostKeyboardFocusEvent });
        Check(!host.Editor.Editing && host.Editor.TimeCard.Text==TimerModel.FormatTime(host.Timer.Remaining),"invalid blurred input restores current duration");
        host.SetSession(30,[40,20]); now=0; wallStart=wallStart.Date.AddHours(14);
        Edit(host.Editor.StartTimeCard,"2:10 PM");
        Check(host.Timer.Scheduled && !host.Timer.Running,"future start schedules focus automatically");
        Near(host.Timer.StartRemaining,600,"scheduled countdown measures time to start");
        Check(host.Editor.TimeCard.Text=="25:00" && host.Editor.FocusEndCard.Text=="2:35 PM" && host.Editor.BreakEndCard.Text=="2:40 PM","scheduled editor shows durations and both planned endpoints");
        Check(host.Editor.StartCountdown.FontSize>=24 && host.Editor.StartCountdown.Visibility==Visibility.Visible,"scheduled countdown is at least twice the old label size");
        Check(host.Editor.TimerPanel.ProgressColor==host.CountdownBrush && host.Editor.TimeCard.Foreground==host.CountdownBrush,"editor countdown and progress border share scheduled color");
        Check(UiTheme.FocusTimelineBands.Length==5 && UiTheme.FocusTimelineBands.All(b=>b.Color is SolidColorBrush),"focus timeline has five distinct solid color blocks");
        host.Editor.CancelEdit(); host.Bar.Hide();host.Editor.Hide();
        Near(host.WakeInterval.TotalSeconds,600,"hidden scheduled timer sleeps until start deadline");
        now=605;host.Tick(); Check(host.Timer.Running && !host.Timer.Scheduled,"late wake starts scheduled focus");
        Near(host.Timer.Remaining,1495,"late scheduled start accounts for elapsed focus time");
        host.ShowApp(); host.SetSession(30,[40,20]); now=0;
        Edit(host.Editor.StartTimeCard,"1:59 PM");
        Check(!host.Timer.Scheduled && !host.Timer.Running && host.Editor.StartTimeCard.Text=="2:00 PM","past start auto-corrects to now");
        Check(host.Editor.ErrorVisible,"past start displays a red error");
        Edit(host.Editor.FocusEndCard,"2:24 PM"); Near(host.Timer.Remaining,1440,"editing focus endpoint adjusts focus live");
        Check(host.Editor.BreakEndCard.Text=="2:29 PM","focus endpoint shifts break endpoint preserving break duration");
        Edit(host.Editor.BreakEndCard,"2:31 PM"); Near(host.Timer.Rest,420,"editing final endpoint adjusts break live");
        host.Editor.CancelEdit();
        host.Editor.OrientationButton.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent)); Pump();
        Check(host.Bar.Vertical && host.Bar.Surface.Layout.Vertical,"orientation button selects vertical bar");
        host.Save(); Check(Settings.Load(Path.Combine(scratch,"gui.json")).Vertical,"vertical orientation and geometry persist");
        host.Bar.SetVertical(false); host.Reset();Check(audio.Last is null,"reset stops audio");
        var controls = Descendants<Button>(host.Editor).ToArray();
        controls.Single(b => System.Windows.Automation.AutomationProperties.GetName(b).StartsWith("60 minutes:")).RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent));
        Near(host.Timer.Focus,3000,"redesigned 60-minute control invokes its preset");
        controls.Single(b => b.Content is string label && label=="+5 min").RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent));
        Near(host.Timer.Focus+host.Timer.Rest,65*60,"redesigned addition control adjusts the active session");
        host.Editor.ShowBar.IsChecked=false; host.Editor.ShowBar.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent));
        Check(!host.Bar.IsVisible && host.Editor.IsVisible,"visibility switch changes only the floating window");
        host.Editor.ResizeBar.IsChecked=true; host.Editor.ResizeBar.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent));
        Check(host.Bar.IsVisible && host.Bar.ResizeEnabled,"resize switch reveals and unlocks the native bar");
        host.Bar.SetResize(false);host.SetSession(30,[40,20]);host.Editor.CancelEdit();Pump();
        void UpdateState(UpdatePhase phase,bool available=true,bool pending=false) => host.Editor.UpdateDetails(new(phase,phase.ToString()),available,pending,DateTimeOffset.Now,DateTimeOffset.Now);
        string UpdateLabel() => ((TextBlock)host.Editor.UpdateButton.Content).Text;
        UpdateState(UpdatePhase.Checking);Check(!host.Editor.UpdateButton.IsEnabled && UpdateLabel()=="Checking for update now","checking greys out the single update action");
        UpdateState(UpdatePhase.Downloading);Check(!host.Editor.UpdateButton.IsEnabled && UpdateLabel()=="Downloading update…","download remains busy without enabling an unverified install");
        UpdateState(UpdatePhase.Ready,pending:true);Check(host.Editor.UpdateButton.IsEnabled && UpdateLabel()=="Update and restart","verified download transforms the same button into install");
        UpdateState(UpdatePhase.Ready);Check(UpdateLabel()=="Check for updates","ready-looking status without a verified package cannot expose install");
        UpdateState(UpdatePhase.Failed);Check(host.Editor.UpdateButton.IsEnabled && UpdateLabel()=="Check for updates","failure restores a retryable check action");
        UpdateState(UpdatePhase.Unavailable,available:false);Check(!host.Editor.UpdateButton.IsEnabled,"Dev or unpackaged app cannot trigger updates");
        UpdateState(UpdatePhase.Current);Check(host.Editor.UpdateButton.IsEnabled && UpdateLabel()=="Check for updates","up-to-date state remains checkable");
        double editorWidth=host.Editor.Width,editorHeight=host.Editor.Height;
        host.Editor.Width=host.Editor.MinWidth;host.Editor.Height=host.Editor.MinHeight;Pump();
        var scroll=(ScrollViewer)host.Editor.Content;
        scroll.ScrollToEnd();Pump();
        Check(scroll.VerticalOffset>0 && scroll.ComputedVerticalScrollBarVisibility==Visibility.Collapsed,"small editor scrolls to lower controls without a visible scrollbar");
        scroll.ScrollToTop();host.Editor.Width=editorWidth;host.Editor.Height=editorHeight;Pump();
        if(capture) {
            host.SetSession(30,[40,20]);host.Editor.CancelEdit();Pump();
            string root=Path.GetFullPath("artifacts");Directory.CreateDirectory(root);
            Capture(host.Editor,Path.Combine(root,"editor.png")); Capture(host.Bar,Path.Combine(root,"bar.png"));
            host.Bar.Width=130;host.Bar.Height=20;Pump();Capture(host.Bar,Path.Combine(root,"bar-tiny.png"));
            host.Bar.ResetSize();host.Bar.SetVertical(true);Pump();Capture(host.Bar,Path.Combine(root,"bar-vertical.png"));
            host.Bar.SetVertical(false);host.SetStart(host.Now.AddMinutes(5));Pump();
            Capture(host.Editor,Path.Combine(root,"editor-scheduled.png"));Capture(host.Bar,Path.Combine(root,"bar-scheduled.png"));
            host.Bar.SetVertical(true);Pump();Capture(host.Bar,Path.Combine(root,"bar-vertical-scheduled.png"));
            host.Bar.SetVertical(false);host.SetSession(30,[40,20]);Pump();
            UpdateState(UpdatePhase.Checking);Pump();Capture(host.Editor,Path.Combine(root,"editor-checking.png"));
            UpdateState(UpdatePhase.Ready,pending:true);Pump();Capture(host.Editor,Path.Combine(root,"editor-update-ready.png"));
            UpdateState(UpdatePhase.Current);host.Editor.Height=Math.Min(1000,SystemParameters.WorkArea.Height-40);Pump();Capture(host.Editor,Path.Combine(root,"editor-updates.png"));
            host.Editor.Width=host.Editor.MinWidth;host.Editor.Height=host.Editor.MinHeight;Pump();scroll.ScrollToEnd();Pump();Capture(host.Editor,Path.Combine(root,"editor-small-bottom.png"));
            scroll.ScrollToTop();host.SetTime(86400);Pump();Capture(host.Editor,Path.Combine(root,"editor-small-top.png"));
            host.Editor.Width=editorWidth;host.Editor.Height=editorHeight;host.Bar.ResetSize();host.SetSession(30,[40,20]);host.Skip();Pump();Capture(host.Editor,Path.Combine(root,"editor-break.png"));
            host.SetSession(30,[40,20]);host.Toggle();now+=1500;host.Tick();Pump();Capture(host.Editor,Path.Combine(root,"editor-complete.png"));
            host.SetSession(30,[40,20]);Pump();
        }
        int calls=0;
        string testPipe = "PomodoroBar.Tests." + Guid.NewGuid().ToString("N");
        using(var server=new InstanceServer(command=> {
            calls++; if(calls==1) throw new InvalidOperationException("simulated connection handler failure");
            return Task.FromResult(command=="show");
        },testPipe)) {
            bool Send(string command) {
                var request=InstanceServer.Send(command,testPipe);
                var deadline=DateTime.UtcNow.AddSeconds(10);
                while(!request.IsCompleted && DateTime.UtcNow<deadline) Pump();
                Check(request.IsCompleted,"IPC bounded completion");return request.GetAwaiter().GetResult();
            }
            Check(!Send("show"),"handler failure rejects first connection");
            Check(Send("show"),"listener survives handler exception");
            Check(!Send("unexpected"),"unknown IPC command rejected");
        }
        host.Toggle();
        host.Editor.Close();Pump();
        Check(!host.Closing && host.Bar.IsVisible && !host.Editor.IsVisible && host.Timer.Running,"closing editor leaves bar and timer running");
        host.OpenEditor();host.Bar.Close();Pump();
        Check(!host.Closing && !host.Bar.IsVisible && host.Editor.IsVisible && host.Timer.Running,"closing bar leaves editor and timer running");
        Check(host.Editor.ShowInTaskbar,"closing bar preserves editor taskbar access");
        host.ShowApp();Pump();
        Check(host.Editor.IsVisible && host.Bar.IsVisible,"relaunch reopens both windows");
        host.Editor.WindowState=WindowState.Minimized;host.Bar.WindowState=WindowState.Minimized;
        host.ShowApp();Pump();
        Check(host.Editor.WindowState==WindowState.Normal && host.Bar.WindowState==WindowState.Normal,"relaunch restores both minimized windows");
        host.Bar.Close();Pump();Check(!host.Closing && host.Editor.IsVisible,"closing bar retains visible editor");
        host.Editor.Close();Pump();Check(host.Closing,"closing last window exits app instead of leaving a background process");
        Check(!host.WakeScheduled && !host.AnimationScheduled && audio.Last is null,"last-window close stops wakeups, animations, and audio");
        host.Dispose();app.Shutdown();
    }
    private static void Capture(Window window,string path)
    {
        var bitmap=new RenderTargetBitmap((int)window.ActualWidth,(int)window.ActualHeight,96,96,PixelFormats.Pbgra32);bitmap.Render(window);
        var encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(bitmap));using var file=File.Create(path);encoder.Save(file);
    }
    private static IEnumerable<T> Descendants<T>(DependencyObject root) where T:DependencyObject
    {
        for(int i=0;i<VisualTreeHelper.GetChildrenCount(root);i++) {
            var child=VisualTreeHelper.GetChild(root,i);
            if(child is T match) yield return match;
            foreach(var nested in Descendants<T>(child)) yield return nested;
        }
    }
    private sealed class FakeAudio:IAudio {
        public string? Last;public bool Loop;
        public void Play(string name,bool loop=false) { Last=name;Loop=loop; }
        public void Stop() { Last=null;Loop=false; }public void Dispose()=>Stop();
    }
}
