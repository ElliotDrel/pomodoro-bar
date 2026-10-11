using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using PomodoroBar;
using PomodoroBar.Core;

// Deliberately silent and settings-free. Sampling overhead is included in results.
internal static class ResourceAudit
{
    [DllImport("kernel32.dll")] private static extern uint GetCurrentThreadId();
    private sealed class SilentAudio : IAudio { public void Play(string name,bool loop=false) {} public void Stop() {} public void Dispose() {} }
    internal static int Run(string[] args)
    {
        int outputIndex = Array.IndexOf(args,"--output");
        string output = outputIndex >= 0 && outputIndex+1 < args.Length ? Path.GetFullPath(args[outputIndex+1]) : Path.GetFullPath("artifacts/resource-audit/optimized.json");
        Directory.CreateDirectory(Path.GetDirectoryName(output)!);
        int exit = 0;
        int scenarioIndex = Array.IndexOf(args,"--scenario");
        string? filter = scenarioIndex >= 0 && scenarioIndex+1 < args.Length ? args[scenarioIndex+1] : null;
        bool software = args.Contains("--software");
        if (software) System.Windows.Media.RenderOptions.ProcessRenderMode = System.Windows.Interop.RenderMode.SoftwareOnly;
        var app = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
        app.Startup += async (_,_) => {
            try {
                using var host = new AppHost(new Settings(),null,new SilentAudio(),services:true,smoke:true);
                host.Bar.Title = "Resource audit · disposable test bar"; host.Editor.Title = "Resource audit · disposable test editor";
                var results = new List<object>();
                async Task Sample(string name,bool running,bool bar,bool editor,Action? prepare = null) {
                    if (filter is not null && !name.Contains(filter,StringComparison.OrdinalIgnoreCase)) return;
                    host.Reset(); if (running) host.Toggle();
                    if (bar) host.Bar.Show(); else host.Bar.Hide(); if (editor) host.Editor.Show(); else host.Editor.Hide();
                    prepare?.Invoke();
                    await Task.Delay(10000);
                    using var process = Process.GetCurrentProcess(); process.Refresh();
                    long Counter(object target,string property) => Convert.ToInt64(target.GetType().GetProperty(property,System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance)?.GetValue(target) ?? 0);
                    long wakes=Counter(host,"WakeCallbackCount"), syncs=Counter(host.Editor,"SyncCount"), renders=Counter(host.Bar.Surface,"RenderCount");
                    var threads = process.Threads.Cast<ProcessThread>().ToDictionary(t=>t.Id,t=>t.TotalProcessorTime.TotalSeconds);
                    double cpu = process.TotalProcessorTime.TotalSeconds;
                    long bytes = GC.GetTotalAllocatedBytes(true); int gc0 = GC.CollectionCount(0);
                    var watch = Stopwatch.StartNew(); var rows = new List<object>();
                    for (int i=0;i<30;i++) {
                        await Task.Delay(1000); process.Refresh();
                        rows.Add(new { Seconds=watch.Elapsed.TotalSeconds, WorkingSetMiB=process.WorkingSet64/1048576d,
                            PrivateCommitMiB=process.PrivateMemorySize64/1048576d, ManagedHeapMiB=GC.GetTotalMemory(false)/1048576d,
                            Threads=process.Threads.Count, Handles=process.HandleCount });
                    }
                    double elapsed = watch.Elapsed.TotalSeconds, used = process.TotalProcessorTime.TotalSeconds-cpu;
                    string Module(ProcessThread thread) {
                        long address=thread.StartAddress.ToInt64();
                        return process.Modules.Cast<ProcessModule>().FirstOrDefault(m=>address>=m.BaseAddress.ToInt64() && address<m.BaseAddress.ToInt64()+m.ModuleMemorySize)?.ModuleName ?? "unknown";
                    }
                    var threadDeltas = process.Threads.Cast<ProcessThread>().Where(t=>threads.ContainsKey(t.Id)).Select(t=>new {Id=t.Id,CpuSeconds=t.TotalProcessorTime.TotalSeconds-threads[t.Id],Module=Module(t)}).ToArray();
                    results.Add(new { Scenario=name, DurationSeconds=elapsed, CpuSeconds=used,
                        OneCorePercent=used/elapsed*100, MachineCpuPercent=used/elapsed*100/Environment.ProcessorCount,
                        AllocatedKiBPerSecond=(GC.GetTotalAllocatedBytes(true)-bytes)/1024d/elapsed,
                        Gen0Collections=GC.CollectionCount(0)-gc0, WakeCallbacks=Counter(host,"WakeCallbackCount")-wakes,
                        EditorSyncs=Counter(host.Editor,"SyncCount")-syncs, BarRenders=Counter(host.Bar.Surface,"RenderCount")-renders,
                        GuiThreadId=GetCurrentThreadId(),ThreadCpu=threadDeltas,Samples=rows });
                    File.WriteAllText(output,JsonSerializer.Serialize(new { LogicalProcessors=Environment.ProcessorCount,
                        Version=BuildChannel.Version, Renderer=software ? "SoftwareOnly" : "Default", LoadedAssemblies=AppDomain.CurrentDomain.GetAssemblies().Select(a=>a.GetName().Name).Order().ToArray(), Scenarios=results },new JsonSerializerOptions { WriteIndented=true }));
                }
                await Sample("Paused, both visible",false,true,true);
                await Sample("Running, bar only",true,true,false);
                await Sample("Running, both visible",true,true,true);
                await Sample("Running, both hidden",true,false,false);
                await Sample("Paused, both hidden",false,false,false);
                await Sample("Running, active draft",true,true,true,() => { host.Editor.TimeCard.Focus(); System.Windows.Input.Keyboard.Focus(host.Editor.TimeCard); host.Editor.TimeCard.Text="23:07"; });
                host.Editor.CancelEdit();
                await Sample("Completion, both visible, silent audio",false,true,true,() => { host.SetTime(1); host.Toggle(); });
            } catch (Exception ex) { exit=1; File.WriteAllText(output+".error.txt",ex.ToString()); }
            app.Shutdown();
        };
        app.Run(); return exit;
    }
}
