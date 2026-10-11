using System;
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
    private static int count;
    private static void Check(bool value,string reason) { if (!value) throw new Exception(reason); count++; }
    private static void Near(double a,double b,string reason) => Check(Math.Abs(a-b)<.001,reason);
    private static void Reject(Action action,string reason) { try { action(); } catch (ArgumentException) { count++; return; } throw new Exception(reason); }
    [STAThread]
    private static int Main(string[] args)
    {
        try {
            double now = 0;
            var t = new TimerModel(1500,300,[40,20],()=>now);
            Check(!t.Running && t.IsFocus,"starts paused focus"); t.Toggle();
            now = 900; Check(t.Tick()=="second-last","30-minute first reminder at 10 left");
            now = 1200; Check(t.Tick()=="last","last reminder at 5 left");
            now = 1500; Check(t.Tick()=="complete" && t.Waiting && !t.Running,"completion blocks");
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
            Check(l.Hit(l.Left+(40+86+16)*l.Scale,l.Top+26*l.Scale)==2,"scaled play hit target");
            foreach(var size in new[] {(130d,120d),(1000d,20d),(280d,52d)}) {
                var fit=BarLayout.Fit(size.Item1,size.Item2,86);
                for(int button=0;button<3;button++) Check(fit.Hit(fit.Left+(40+86+16+button*32)*fit.Scale,fit.Top+26*fit.Scale)==button+2,"all controls scale together");
                Check(fit.Hit(fit.Left+22*fit.Scale,fit.Top+26*fit.Scale)==0,"ring hit scales");
            }
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
        using var host=new AppHost(new Settings(),Path.Combine(scratch,"gui.json"),audio,()=>now,services:false);
        host.OpenEditor(); Pump();
        double width=host.Bar.Width,height=host.Bar.Height;
        Check(host.Bar.Topmost && host.Bar.WindowStyle==WindowStyle.None,"native always-on-top borderless window");
        Check(host.Editor.IsVisible && host.Bar.Width==width && host.Bar.Height==height,"editor independent of bar geometry");
        foreach(var window in new Window[] {host.Bar,host.Editor}) {
            Check(ReferenceEquals(window.Icon,AppIcon.Source),"window uses the bundled Pomodoro icon");
            var hwnd=new WindowInteropHelper(window).Handle;
            Check(SendMessage(hwnd,0x007f,new IntPtr(0),IntPtr.Zero)!=IntPtr.Zero,"Windows receives small taskbar icon");
            Check(SendMessage(hwnd,0x007f,new IntPtr(1),IntPtr.Zero)!=IntPtr.Zero,"Windows receives large window icon");
        }
        Check(!host.CanAutoRestart,"opening editor defers startup updates");
        host.SetSession(60,[60,20,10]); Near(host.Timer.Focus,3000,"60 preset"); Near(host.Timer.Rest,600,"60 break");
        host.AddMinutes(1);host.AddMinutes(5);host.AddMinutes(10); Near(host.Timer.Focus+host.Timer.Rest,76*60,"add buttons"); Near(host.Timer.Focus/host.Timer.Rest,5,"5:1 ratio");
        host.SetSession(30,[40,20]); host.Toggle(); now=900; host.Tick(); Check(audio.Last=="second-last" && !audio.Loop,"native host reminder dispatch");
        now=1500; host.Tick(); Check(audio.Last=="complete" && audio.Loop,"completion loops");
        host.Bar.Hide();host.Render();Check(host.Editor.AlarmButton.Visibility==Visibility.Visible,"hidden-bar completion is acknowledgeable");
        host.Bar.Show();
        host.Silence(); Check(audio.Last is null && host.Timer.Waiting,"interaction silences without automatically advancing");
        host.Skip(); Check(!host.Timer.IsFocus && host.Timer.Running,"skip acknowledgement");
        host.Bar.Hide();host.Render();Check(host.Timer.Running && host.Editor.IsVisible && host.Editor.ShowBar.IsChecked==false,"hidden bar independent of editor and clock");
        host.Bar.Show();host.Bar.SetResize(true);Check(host.Bar.ResizeMode==ResizeMode.CanResize,"native resize enabled");
        host.Bar.Width=130;host.Bar.Height=20;Pump();host.Render();Pump();
        Check(host.Bar.Surface.Layout.Scale<.5,"whole bar scales down");
        host.Bar.SetResize(false);Check(host.Bar.WindowStyle==WindowStyle.None && host.Bar.ResizeMode==ResizeMode.NoResize,"resize disables frame");
        host.Bar.ResetSize();Near(host.Bar.Width,280,"reset width");Near(host.Bar.Height,52,"reset height");
        host.Editor.TimeCard.Focus();Keyboard.Focus(host.Editor.TimeCard);Pump();
        host.Editor.TimeCard.Text="07:42"; now+=3;host.Tick();Check(host.Editor.TimeCard.Text=="07:42","live sync protects edited draft");
        Check(host.Editor.FinishEdit(),"commit edited time");Near(host.Timer.Remaining,462,"editor edits active section");
        host.Editor.CancelEdit();now+=1;host.Tick();Check(host.Editor.TimeCard.Text=="07:41","editor and countdown synchronize");
        Keyboard.ClearFocus();Pump();host.Editor.TimeCard.Focus();Keyboard.Focus(host.Editor.TimeCard);Pump();
        host.Editor.TimeCard.RaiseEvent(new KeyboardFocusChangedEventArgs(Keyboard.PrimaryDevice,0,host.Editor,host.Editor.TimeCard) { RoutedEvent=Keyboard.GotKeyboardFocusEvent });
        host.Editor.TimeCard.Text="99:99";
        host.Editor.TimeCard.RaiseEvent(new KeyboardFocusChangedEventArgs(Keyboard.PrimaryDevice,0,host.Editor.TimeCard,host.Editor) { RoutedEvent=Keyboard.LostKeyboardFocusEvent });
        Check(!host.Editor.Editing && host.Editor.TimeCard.Text=="07:41",$"invalid blurred draft cancels instead of trapping editor: editing={host.Editor.Editing} text={host.Editor.TimeCard.Text}");
        host.Save(); Near(Settings.Load(Path.Combine(scratch,"gui.json")).Rest,462,"custom break saves");
        host.Reset();Check(audio.Last is null,"reset stops audio");
        if(capture) {
            host.SetSession(30,[40,20]);host.Editor.CancelEdit();Pump();
            string root=Path.GetFullPath("artifacts");Directory.CreateDirectory(root);
            Capture(host.Editor,Path.Combine(root,"editor.png")); Capture(host.Bar,Path.Combine(root,"bar.png"));
            host.Bar.Width=130;host.Bar.Height=20;Pump();Capture(host.Bar,Path.Combine(root,"bar-tiny.png"));
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
        host.Bar.Close();Pump();Check(host.Closing,"window-manager close exits without recursive close crash");
        host.Dispose();app.Shutdown();
    }
    private static void Capture(Window window,string path)
    {
        var bitmap=new RenderTargetBitmap((int)window.ActualWidth,(int)window.ActualHeight,96,96,PixelFormats.Pbgra32);bitmap.Render(window);
        var encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(bitmap));using var file=File.Create(path);encoder.Save(file);
    }
    private sealed class FakeAudio:IAudio {
        public string? Last;public bool Loop;
        public void Play(string name,bool loop=false) { Last=name;Loop=loop; }
        public void Stop() { Last=null;Loop=false; }public void Dispose()=>Stop();
    }
}
