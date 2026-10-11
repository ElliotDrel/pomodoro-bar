using System;
using System.IO;
using System.IO.Pipes;
using System.Threading;
using System.Threading.Tasks;

namespace PomodoroBar;

internal sealed class InstanceServer : IDisposable
{
    private readonly CancellationTokenSource stop = new();
    private readonly Func<string, Task<bool>> handler;
    public InstanceServer(Func<string, Task<bool>> handler) { this.handler = handler; _ = Listen(); }
    private async Task Listen()
    {
        while (!stop.IsCancellationRequested) {
            try {
                using var pipe = new NamedPipeServerStream(BuildChannel.PipeName, PipeDirection.InOut, 1,
                    PipeTransmissionMode.Byte, PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);
                await pipe.WaitForConnectionAsync(stop.Token);
                using var reader = new StreamReader(pipe, leaveOpen: true);
                using var writer = new StreamWriter(pipe, leaveOpen: true) { AutoFlush = true };
                using var request = CancellationTokenSource.CreateLinkedTokenSource(stop.Token);
                request.CancelAfter(TimeSpan.FromSeconds(5));
                string? command = await reader.ReadLineAsync(request.Token);
                bool accepted = command is "show" or "restart" or "quit" && await handler(command);
                await writer.WriteLineAsync(accepted ? "ok" : "error");
            } catch (OperationCanceledException) { }
            catch (Exception ex) {
                Log.Write("ipc_error " + ex.Message);
                try { await Task.Delay(100,stop.Token); } catch (OperationCanceledException) { }
            }
        }
    }
    public static async Task<bool> Send(string command)
    {
        try {
            using var pipe = new NamedPipeClientStream(".", BuildChannel.PipeName, PipeDirection.InOut, PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            await pipe.ConnectAsync(timeout.Token);
            using var writer = new StreamWriter(pipe, leaveOpen: true) { AutoFlush = true };
            using var reader = new StreamReader(pipe, leaveOpen: true);
            await writer.WriteLineAsync(command);
            return await reader.ReadLineAsync(timeout.Token) == "ok";
        } catch (Exception ex) when (ex is IOException or OperationCanceledException or TimeoutException) { Log.Write("ipc_send_failed " + ex.Message); return false; }
    }
    public void Dispose() => stop.Cancel();
}
