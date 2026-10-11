using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Velopack;
using Velopack.Locators;
using Velopack.Sources;

namespace PomodoroBar;
internal enum UpdateTrigger { Launch, Periodic, Manual }
internal enum UpdatePhase { Idle, Checking, Downloading, Ready, Current, Failed, Unavailable }
internal sealed record UpdateStatus(UpdatePhase Phase, string Text);
internal sealed class UpdateService : IDisposable
{
    private readonly UpdateManager? manager;
    private readonly SemaphoreSlim gate = new(1,1);
    private readonly CancellationTokenSource stop = new();
    private readonly System.Threading.Timer? periodic;
    private readonly string stateDirectory;
    private UpdateInfo? pending;
    private volatile bool disposed;
    private bool historyLoaded;
    public event Action<UpdateStatus>? StateChanged;
    public event Action? ApplyRequested;
    public UpdateStatus State { get; private set; } = new(UpdatePhase.Idle,"Ready to check for updates");
    public DateTimeOffset? LastCheckedAt { get; private set; }
    public DateTimeOffset? LastUpdatedAt { get; private set; }
    public bool CanUpdate => !BuildChannel.IsDev && manager?.IsInstalled == true;
    public bool HasPending => State.Phase == UpdatePhase.Ready && pending is not null;
    public bool IsBusy => State.Phase is UpdatePhase.Checking or UpdatePhase.Downloading;
    public UpdateService(UpdateManager? manager = null, string? stateDirectory = null)
    {
        this.stateDirectory = stateDirectory ?? BuildChannel.DataDirectory;
        if (BuildChannel.IsDev) { State = new(UpdatePhase.Unavailable,"Dev updates are disabled"); return; }
        try {
            this.manager = manager ?? new UpdateManager(new GithubSource("https://github.com/ElliotDrel/pomodoro-bar", null, false));
            if (!this.manager.IsInstalled) { State = new(UpdatePhase.Unavailable,"Install the released app to enable updates"); return; }
            periodic = new System.Threading.Timer(_ => _ = CheckAsync(UpdateTrigger.Periodic), null, TimeSpan.FromHours(4),TimeSpan.FromHours(4));
        } catch (Exception ex) { Log.Write("update_init_failed " + ex.Message); State = new(UpdatePhase.Failed,"Could not initialize updates"); }
    }
    // Network, download and history I/O runs off the WPF dispatcher.
    public Task CheckAsync(UpdateTrigger trigger) => Task.Run(() => CheckCoreAsync(trigger));
    private async Task CheckCoreAsync(UpdateTrigger trigger)
    {
        if (disposed || !CanUpdate || !await gate.WaitAsync(0).ConfigureAwait(false)) return;
        try {
            stop.Token.ThrowIfCancellationRequested();
            if (!historyLoaded) { LoadHistory(); historyLoaded = true; }
            pending = null;
            Log.Write($"update_check_start trigger={trigger}"); SetState(UpdatePhase.Checking,"Checking for updates…");
            var info = await manager!.CheckForUpdatesAsync().ConfigureAwait(false);
            stop.Token.ThrowIfCancellationRequested();
            if (info is null) { SetState(UpdatePhase.Current,"Up to date"); Log.Write("update_check_done result=up_to_date"); return; }
            var version = info.TargetFullRelease.Version.ToString();
            SetState(UpdatePhase.Downloading,"Downloading " + version);
            await manager.DownloadUpdatesAsync(info, progress: percent => {
                if (!stop.IsCancellationRequested && State.Phase == UpdatePhase.Downloading) SetState(UpdatePhase.Downloading,$"Downloading {version} · {percent}%");
            }, cancelToken: stop.Token).ConfigureAwait(false);
            stop.Token.ThrowIfCancellationRequested(); pending = info;
            Log.Write("update_download_done version=" + version);
            SetState(UpdatePhase.Ready,$"Version {version} downloaded · ready to install");
        } catch (OperationCanceledException) when (stop.IsCancellationRequested) { }
        catch (Exception ex) { pending = null; Log.Write("update_check_failed " + ex.Message); SetState(UpdatePhase.Failed,"Update check or download failed · try again"); }
        finally {
            if (!disposed) {
                LastCheckedAt = DateTimeOffset.Now;
                TryHistory(() => File.WriteAllText(Path.Combine(stateDirectory,"last-update-check.txt"),LastCheckedAt.Value.ToString("O")));
                SetState(State.Phase,State.Text);
            }
            gate.Release();
            if (!disposed && trigger == UpdateTrigger.Launch && HasPending) {
                try { ApplyRequested?.Invoke(); } catch (Exception ex) { Log.Write("update_subscriber_failed " + ex.Message); }
            }
        }
    }
    public bool PrepareRestart(bool applyUpdate, Func<bool>? canRestart = null)
    {
        if (disposed || !CanUpdate) return false;
        // A pure restart must not wait behind a potentially long download.
        if (!applyUpdate) {
            try {
                if (canRestart?.Invoke() == false) return false;
                Log.Write("restart_requested"); UpdateExe.Start(VelopackLocator.Current,(uint)Environment.ProcessId,[]); return true;
            } catch (Exception ex) { Log.Write("restart_failed " + ex.Message); SetState(UpdatePhase.Failed,"Restart failed · the current app is still running"); return false; }
        }
        if (!HasPending) return false;
        gate.Wait();
        try {
            if (disposed || !HasPending || canRestart?.Invoke() == false) return false;
            if (applyUpdate) {
                if (!HasPending) throw new InvalidOperationException("No verified update is ready to install.");
                Log.Write("update_apply_now version=" + pending!.TargetFullRelease.Version);
                manager!.WaitExitThenApplyUpdates(pending, silent:true, restart:true, restartArgs:[]);
            } else { Log.Write("restart_requested"); UpdateExe.Start(VelopackLocator.Current,(uint)Environment.ProcessId,[]); }
            return true;
        } catch (Exception ex) { Log.Write("restart_failed " + ex.Message); SetState(UpdatePhase.Ready,"Could not start updater · retry Update and restart"); return false; }
        finally { gate.Release(); }
    }
    private void SetState(UpdatePhase phase,string text)
    {
        if (disposed) return;
        State = new(phase,text);
        try { StateChanged?.Invoke(State); } catch (Exception ex) { Log.Write("update_subscriber_failed " + ex.Message); }
    }
    private void LoadHistory()
    {
        TryHistory(() => {
            var check = Path.Combine(stateDirectory,"last-update-check.txt");
            if (File.Exists(check) && DateTimeOffset.TryParse(File.ReadAllText(check),out var checkedAt)) LastCheckedAt = checkedAt;
            var installed = Path.Combine(stateDirectory,"installed-version.txt");
            var lines = File.Exists(installed) ? File.ReadAllLines(installed) : [];
            if (lines.Length == 2 && lines[0] == BuildChannel.Version && DateTimeOffset.TryParse(lines[1],out var updatedAt)) LastUpdatedAt = updatedAt;
            else { LastUpdatedAt = DateTimeOffset.Now; File.WriteAllLines(installed,[BuildChannel.Version,LastUpdatedAt.Value.ToString("O")]); }
        });
    }
    private void TryHistory(Action action)
    {
        try { Directory.CreateDirectory(stateDirectory); action(); } catch (Exception ex) { Log.Write("update_history_failed " + ex.Message); }
    }
    public void Dispose() { if (disposed) return; disposed = true; stop.Cancel(); periodic?.Dispose(); }
}
