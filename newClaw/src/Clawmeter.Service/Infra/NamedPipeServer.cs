using System.IO.Pipes;
using System.Text.Json;
using Clawmeter.Shared;
using Microsoft.Extensions.Logging;

namespace Clawmeter.Service.Infra;

/// <summary>
/// Named-pipe server. Exposes the IPC contract the WPF UI speaks to.
/// Pipe name: \\.\pipe\Clawmeter
/// Pipe ACL: current user only (Deny network / Deny guest).
/// Wire format: one JSON IpcRequest → one JSON IpcResponse per client.
/// The pipe is multi-instance — every UI launch gets its own server.
/// </summary>
public sealed class NamedPipeServer
{
    private const string PipeName = "Clawdmeter";
    private readonly ILogger<NamedPipeServer> _log;

    public NamedPipeServer(ILogger<NamedPipeServer> log)
    {
        _log = log;
    }

    public Task StartAsync(CancellationToken ct)
    {
        _log.LogInformation("Named-pipe server would listen at \\\\.\\pipe\\{PipeName}");
        // Actual pipe + ACL setup + per-connection handling lands in M5.
        return Task.CompletedTask;
    }
}
