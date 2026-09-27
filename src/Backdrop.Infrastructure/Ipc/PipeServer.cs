// Backdrop.Infrastructure — local named-pipe IPC (SECURITY.md §4).
// PipeOptions.CurrentUserOnly restricts connections to the creating user (= DACL current-user SID),
// local only, no remote. Every command additionally requires the CLI token.
using System.IO.Pipes;
using System.Text.Json;

namespace Backdrop.Infrastructure.Ipc;

public sealed record PipeCommand(string Token, string Action, string? Arg);

public sealed class PipeServer : IDisposable
{
    private readonly string _pipeName;
    private readonly string _token;
    private readonly Func<PipeCommand, Task<string>> _handler;
    private readonly CancellationTokenSource _cts = new();

    public PipeServer(string pipeName, string token, Func<PipeCommand, Task<string>> handler)
    {
        _pipeName = pipeName;
        _token = token;
        _handler = handler;
    }

    public void Start()
    {
        _ = Task.Run(async () =>
        {
            while (!_cts.IsCancellationRequested)
            {
                var server = new NamedPipeServerStream(_pipeName, PipeDirection.InOut, 1,
                    PipeTransmissionMode.Message,
                    PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly, 4096, 4096);
                try
                {
                    await server.WaitForConnectionAsync(_cts.Token);
                    using var reader = new StreamReader(server, leaveOpen: true);
                    using var writer = new StreamWriter(server, leaveOpen: true) { AutoFlush = true };
                    var line = await reader.ReadLineAsync(_cts.Token);
                    if (line is null)
                        continue;
                    var cmd = JsonSerializer.Deserialize<PipeCommand>(line);
                    if (cmd is null || cmd.Token != _token)
                    {
                        await writer.WriteLineAsync(JsonSerializer.Serialize(new { ok = false, error = "unauthorized" }));
                        continue;
                    }
                    var result = await _handler(cmd);
                    await writer.WriteLineAsync(result);
                }
                catch (OperationCanceledException) { break; }
                catch { /* keep serving */ }
                finally
                {
                    server.Dispose();
                }
            }
        });
    }

    public void Dispose()
    {
        _cts.Cancel();
        _cts.Dispose();
    }
}
