using System.IO.Pipes;
using System.Text.Json;
using Clawmeter.Shared;

namespace Clawmeter.UI.Services;

/// <summary>
/// Named-pipe client. The WPF UI's only IPC path — never talk to BLE or the
/// firmware directly. Talks to Clawmeter.Service over \\.\pipe\Clawmeter.
///
/// Wire format: one JSON IpcRequest → one JSON IpcResponse. Connection is
/// created on demand for each call (cheap, the OS keeps the pipe around
/// for a few seconds). Reconnect on broken pipe.
/// </summary>
public sealed class ServiceClient : IDisposable
{
    private const string PipeName = "Clawdmeter";
    private static readonly JsonSerializerOptions Options = new() { WriteIndented = false };

    public async Task<IpcResponse> SendAsync(string action, object? payload = null)
    {
        using var pipe = new NamedPipeClientStream(".", PipeName, PipeDirection.InOut,
            PipeOptions.Asynchronous);

        await pipe.ConnectAsync(2000).ConfigureAwait(false);

        var req = new IpcRequest { Action = action, Payload = payload };
        var json = JsonSerializer.Serialize(req, Options);
        var bytes = System.Text.Encoding.UTF8.GetBytes(json);
        await pipe.WriteAsync(bytes).ConfigureAwait(false);
        await pipe.FlushAsync().ConfigureAwait(false);

        // length-prefixed read (4 bytes BE + payload)
        var lenBuf = new byte[4];
        await pipe.ReadExactlyAsync(lenBuf).ConfigureAwait(false);
        var len = (lenBuf[0] << 24) | (lenBuf[1] << 16) | (lenBuf[2] << 8) | lenBuf[3];

        var buf = new byte[len];
        await pipe.ReadExactlyAsync(buf).ConfigureAwait(false);

        return JsonSerializer.Deserialize<IpcResponse>(buf, Options)
            ?? new IpcResponse { Ok = false, Error = "null response" };
    }

    public async Task<ServiceStatus> GetStatusAsync()
    {
        var resp = await SendAsync(IpcActions.GetStatus);
        if (!resp.Ok || resp.Data is null)
            return new ServiceStatus { Connected = false, Provider = "?" };

        var data = ((JsonElement)resp.Data).Deserialize<ServiceStatus>();
        return data ?? new ServiceStatus();
    }

    public void Dispose() { /* no-op — pipes are per-call */ }
}
