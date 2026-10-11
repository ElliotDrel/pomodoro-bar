using System;
using System.Threading;
using System.Threading.Tasks;
using Velopack;
using Velopack.Locators;
using Velopack.Sources;

namespace PomodoroBar;

internal enum UpdateTrigger { Launch, Periodic, Manual }

internal sealed class UpdateService : IDisposable
{
    private readonly UpdateManager? manager;
    private readonly SemaphoreSlim gate = new(1,1);
    private readonly CancellationTokenSource stop = new();
    private readonly System.Threading.Timer? periodic;
    private UpdateInfo? pending;
    public event Action<string>? Status;
    public event Action? ApplyRequested;
    public bool CanUpdate => !BuildChannel.IsDev && manager?.IsInstalled == true;
    public UpdateService()
    {
        if (BuildChannel.IsDev) { Log.Write("update_service disabled=dev"); return; }
        try {
            manager = new UpdateManager(new GithubSource("https://github.com/ElliotDrel/pomodoro-bar", null, false));
            if (manager.IsInstalled) periodic = new System.Threading.Timer(_ => _ = CheckAsync(UpdateTrigger.Periodic), null, TimeSpan.FromHours(4),TimeSpan.FromHours(4));
        } catch (Exception ex) { Log.Write("update_init_failed " + ex.Message); }
    }
    public async Task CheckAsync(UpdateTrigger trigger)
    {
        if (!CanUpdate || stop.IsCancellationRequested) { Status?.Invoke(BuildChannel.IsDev ? "Dev updates are disabled" : "Install the released app to enable updates"); return; }
        if (!await gate.WaitAsync(0)) { Status?.Invoke("An update check is already running"); return; }
        try {
            Log.Write($"update_check_start trigger={trigger}"); Status?.Invoke("Checking for updates…");
            var info = await manager!.CheckForUpdatesAsync();
            stop.Token.ThrowIfCancellationRequested();
            if (info is null) { Status?.Invoke("Up to date"); Log.Write("update_check_done result=up_to_date"); return; }
            Status?.Invoke("Downloading " + info.TargetFullRelease.Version);
            await manager.DownloadUpdatesAsync(info, cancelToken: stop.Token);
            stop.Token.ThrowIfCancellationRequested(); pending = info;
            Log.Write("update_download_done version=" + info.TargetFullRelease.Version);
            Status?.Invoke("Update ready · restart to install");
            if (trigger == UpdateTrigger.Launch) ApplyRequested?.Invoke();
        } catch (OperationCanceledException) when (stop.IsCancellationRequested) { }
        catch (Exception ex) { Log.Write("update_check_failed " + ex.Message); Status?.Invoke("Update check failed · try again from the tray"); }
        finally { gate.Release(); }
    }
    public bool PrepareRestart()
    {
        if (!CanUpdate) return false;
        try {
            if (pending is not null) { Log.Write("update_apply_now"); manager!.WaitExitThenApplyUpdates(pending, silent:true, restart:true, restartArgs:[]); }
            else { Log.Write("restart_requested"); UpdateExe.Start(VelopackLocator.Current,(uint)Environment.ProcessId,[]); }
            return true;
        } catch (Exception ex) { Log.Write("restart_failed " + ex.Message); Status?.Invoke("Restart failed · try again"); return false; }
    }
    public void Dispose() { stop.Cancel(); periodic?.Dispose(); }
}
