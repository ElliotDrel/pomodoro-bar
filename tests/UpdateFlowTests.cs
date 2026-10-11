using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using PomodoroBar;
using Velopack;
using Velopack.Locators;
using Velopack.Sources;

internal static class UpdateFlowTests
{
    private static int count;
    private static void Check(bool ok,string why) { if(!ok) throw new Exception(why); count++; }
    public static void Run(string root)
    {
        var directory=Path.Combine(root,"updates");Directory.CreateDirectory(directory);
        var hidden = BackgroundUpdateProcess.StartInfo("helper.exe", ["apply", "path with spaces", "--silent"], directory);
        Check(!hidden.UseShellExecute && hidden.CreateNoWindow && hidden.WindowStyle==System.Diagnostics.ProcessWindowStyle.Hidden,"helper explicitly suppresses shell and console windows");
        Check(hidden.ArgumentList.SequenceEqual(new[]{"apply","path with spaces","--silent"}) && hidden.WorkingDirectory==directory,"background helper preserves separate arguments and working directory");
        // A real console executable must receive no console, including no inherited console.
        var probe=Path.Combine(directory,"console.txt");
        var probeScript=Path.Combine(directory,"console.ps1");
        File.WriteAllText(probeScript,"Add-Type -MemberDefinition '[System.Runtime.InteropServices.DllImport(\"kernel32.dll\")] public static extern System.IntPtr GetConsoleWindow();' -Name ConsoleProbe -Namespace Probe; [IO.File]::WriteAllText('"+probe.Replace("'","''")+"',[Probe.ConsoleProbe]::GetConsoleWindow().ToInt64().ToString())");
        var powershell=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System),"WindowsPowerShell","v1.0","powershell.exe");
        using(var child=System.Diagnostics.Process.Start(BackgroundUpdateProcess.StartInfo(powershell,["-NoProfile","-NonInteractive","-File",probeScript],directory))!) {
            if(!child.WaitForExit(15000)){child.Kill();throw new Exception("console probe timed out");}
            Check(child.ExitCode==0 && File.ReadAllText(probe)=="0","real background helper has no console window");
        }
        var locator=new RecordingLocator(directory);
        File.WriteAllText(locator.UpdateExePath!,"fixture; never executed");
        VelopackApp.Build().SetLocator(locator).SetAutoApplyOnStartup(false).Run();
        var manager=new FakeManager(directory,locator);
        using var service=new UpdateService(manager,directory);
        int installs=0;service.ApplyRequested+=()=>installs++;
        service.CheckAsync(UpdateTrigger.Launch).GetAwaiter().GetResult();
        Check(service.State.Phase==UpdatePhase.Current && installs==0,"current launch does not restart");
        Check(!service.PrepareRestart(true) && service.State.Phase==UpdatePhase.Current,"install with no package preserves status");
        Check(service.LastCheckedAt is not null && service.LastUpdatedAt is not null,"update timestamps recorded");
        Check(service.PrepareRestart(false),"pure restart succeeds");
        Check(!locator.Recorder.ShowWindow,"restart requests a hidden helper");
        Check(locator.Recorder.Arguments.Contains("start") && !locator.Recorder.Arguments.Contains("--restart"),"restart waits for exit without replaying command");
        manager.Target=new UpdateInfo(VelopackAssetFeed.FromJson("""
            {"Assets":[{"PackageId":"Test","Version":"2.0.0","Type":"Full","FileName":"Test-2.0.0-full.nupkg","Size":1}]}
            """).Assets.Single(),false);
        service.CheckAsync(UpdateTrigger.Periodic).GetAwaiter().GetResult();
        Check(service.HasPending && installs==0,"periodic download waits for install");
        service.CheckAsync(UpdateTrigger.Manual).GetAwaiter().GetResult();
        Check(service.HasPending && installs==0,"manual download waits for install");
        var delay=new TaskCompletionSource<UpdateInfo?>(TaskCreationOptions.RunContinuationsAsynchronously);
        manager.Delay=delay.Task;
        var launch=service.CheckAsync(UpdateTrigger.Launch);
        Check(SpinWait.SpinUntil(()=>service.State.Phase==UpdatePhase.Checking,5000),"launch check begins");
        var checks=manager.Checks;
        Check(service.PrepareRestart(false),"pure restart does not wait behind an active check");
        service.CheckAsync(UpdateTrigger.Manual).GetAwaiter().GetResult();
        Check(checks==manager.Checks,"overlapping checks do not duplicate downloads");
        delay.SetResult(manager.Target);launch.GetAwaiter().GetResult();
        Check(installs==1 && service.HasPending,"launch requests one verified installation");
        locator.Recorder.Fail=true;
        Check(!service.PrepareRestart(true) && service.HasPending,"failed install helper keeps verified download retryable");
        locator.Recorder.Fail=false;
        Check(service.PrepareRestart(true),"verified download can install");
        Check(!locator.Recorder.ShowWindow,"apply requests a hidden helper");
        Check(locator.Recorder.Arguments.Contains("apply") && locator.Recorder.Arguments.Contains("--waitPid") && locator.Recorder.Arguments.Contains("--silent"),"apply is silent and waits for graceful exit");
        manager.Delay=Task.FromException<UpdateInfo?>(new IOException("check failed"));
        service.CheckAsync(UpdateTrigger.Launch).GetAwaiter().GetResult();
        Check(service.State.Phase==UpdatePhase.Failed && !service.HasPending && installs==1,"check failure clears stale pending update");
        manager.Delay=null;manager.DownloadFails=true;
        service.CheckAsync(UpdateTrigger.Launch).GetAwaiter().GetResult();
        Check(service.State.Phase==UpdatePhase.Failed && !service.HasPending && installs==1,"download failure cannot install");
        Check(!service.PrepareRestart(true),"stale update cannot apply after failure");
        locator.Recorder.Fail=true;
        Check(!service.PrepareRestart(false),"missing helper leaves app running");
        locator.Recorder.Fail=false;manager.DownloadFails=false;
        using(var restored=new UpdateService(manager,directory)) {
            restored.CheckAsync(UpdateTrigger.Manual).GetAwaiter().GetResult();
            Check(restored.LastCheckedAt>=service.LastCheckedAt && restored.LastUpdatedAt==service.LastUpdatedAt,"timestamps survive restart");
        }
        var shutdown=new TaskCompletionSource<UpdateInfo?>(TaskCreationOptions.RunContinuationsAsynchronously);manager.Delay=shutdown.Task;
        var active=service.CheckAsync(UpdateTrigger.Launch);
        Check(SpinWait.SpinUntil(()=>service.State.Phase==UpdatePhase.Checking,5000),"shutdown fixture begins");
        int late=0;service.StateChanged+=_=>late++;service.ApplyRequested+=()=>late++;
        service.Dispose();shutdown.SetResult(manager.Target);active.GetAwaiter().GetResult();
        var after=manager.Checks;service.CheckAsync(UpdateTrigger.Manual).GetAwaiter().GetResult();
        Check(late==0 && after==manager.Checks,"disposed updater suppresses callbacks and new checks");
        Check(locator.Recorder.Exits==0,"updater never force-exits before app cleanup");
        Console.WriteLine($"PASS: {count} updater assertions; launch, periodic, manual, overlap, failures, history, shutdown.");
    }
    private sealed class FakeManager : UpdateManager
    {
        public UpdateInfo? Target; public Task<UpdateInfo?>? Delay; public bool DownloadFails; public int Checks;
        public FakeManager(string directory,RecordingLocator locator):base(new SimpleFileSource(new DirectoryInfo(directory)),locator:locator) { }
        public override Task<UpdateInfo?> CheckForUpdatesAsync(){Interlocked.Increment(ref Checks);return Delay ?? Task.FromResult(Target);}
        public override Task DownloadUpdatesAsync(UpdateInfo info,Action<int>? progress=null,CancellationToken cancelToken=default){ if(DownloadFails) throw new IOException("download failed");progress?.Invoke(50);progress?.Invoke(100);return Task.CompletedTask; }
    }
    private sealed class RecordingLocator : TestVelopackLocator
    {
        public RecordingProcess Recorder { get; }=new(); public override IProcessImpl Process=>Recorder;
        public RecordingLocator(string directory):base("Test","1.0.0",directory,directory,directory,Path.Combine(directory,"Update.exe"),processPath:Environment.ProcessPath!) { }
    }
    private sealed class RecordingProcess : IProcessImpl
    {
        public string[] Arguments=[];public bool Fail;public int Exits;public bool ShowWindow;
        public string GetCurrentProcessPath()=>Environment.ProcessPath!;public uint GetCurrentProcessId()=>(uint)Environment.ProcessId;
        public void Exit(int code)=>Exits++;
        public void StartProcess(string exe,IEnumerable<string> args,string workDir,bool showWindow){if(Fail) throw new IOException("missing helper");Arguments=args.ToArray();ShowWindow=showWindow;}
    }
}
