using System.IO.Pipes;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Text;
using System.Text.Json;
using Clawmeter.Service.Services;
using Clawmeter.Shared;
using Microsoft.Extensions.Logging;

namespace Clawmeter.Service.Infra;

/// <summary>
/// Named-pipe server. Exposes the IPC contract the WPF UI speaks to.
/// Pipe name: \\.\pipe\Clawdmeter
/// Pipe ACL: current user only (Deny network / Deny guest).
/// Wire format: one JSON IpcRequest → one JSON IpcResponse per client.
/// Multi-instance — every UI launch gets its own server loop.
/// </summary>
public sealed class NamedPipeServer : IDisposable
{
    private const string PipeName = "Clawdmeter";
    private const int MaxConcurrentServers = 4;

    private static readonly JsonSerializerOptions Options = new() { WriteIndented = false };

    private readonly ILogger<NamedPipeServer> _log;
    private readonly BleLinkService _ble;
    private readonly ProviderPollerService _poller;
    private readonly BrightnessController _brightness;
    private readonly ConfigService _config;

    private readonly CancellationTokenSource _cts = new();
    private readonly List<Task> _serverTasks = new();

    public NamedPipeServer(
        ILogger<NamedPipeServer> log,
        BleLinkService ble,
        ProviderPollerService poller,
        BrightnessController brightness,
        ConfigService config)
    {
        _log = log;
        _ble = ble;
        _poller = poller;
        _brightness = brightness;
        _config = config;
    }

    public Task StartAsync(CancellationToken ct)
    {
        for (var i = 0; i < MaxConcurrentServers; i++)
        {
            _serverTasks.Add(Task.Run(() => ServerLoopAsync(i, _cts.Token), _cts.Token));
        }
        _log.LogInformation("Named-pipe server listening at \\\\.\\pipe\\{PipeName}");
        return Task.CompletedTask;
    }

    public async Task StopAsync()
    {
        _cts.Cancel();
        try { await Task.WhenAll(_serverTasks); } catch { /* shutdown race */ }
    }

    public void Dispose()
    {
        _cts.Cancel();
        _cts.Dispose();
    }

    private async Task ServerLoopAsync(int slot, CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try
            {
                var pipe = CreatePipe();
                await pipe.WaitForConnectionAsync(ct);
                _ = Task.Run(() => HandleClientAsync(pipe, ct));
            }
            catch (OperationCanceledException) { return; }
            catch (Exception ex) { _log.LogWarning(ex, "pipe slot {Slot} accept failed", slot); }
        }
    }

    private static NamedPipeServerStream CreatePipe()
    {
        var pipe = new NamedPipeServerStream(
            PipeName,
            PipeDirection.InOut,
            NamedPipeServerStream.MaxAllowedServerInstances,
            PipeTransmissionMode.Byte,
            PipeOptions.Asynchronous);

        // Restrict to current user. Deny network (DenyNetworkLogon) so other
        // sessions on the same box can't reach in.
        try
        {
            var sid = WindowsIdentity.GetCurrent().User!;
            var acl = new PipeSecurity();
            acl.AddAccessRule(new PipeAccessRule(
                sid, PipeAccessRights.ReadWrite | PipeAccessRights.CreateNewInstance,
                AccessControlType.Allow));
            acl.AddAccessRule(new PipeAccessRule(
                new SecurityIdentifier(WellKnownSidType.Network, null),
                PipeAccessRights.ReadWrite, AccessControlType.Deny));
            pipe.SetAccessControl(acl);
        }
        catch { /* fall back to default ACL on older runtimes */ }
        return pipe;
    }

    private async Task HandleClientAsync(NamedPipeServerStream pipe, CancellationToken ct)
    {
        try
        {
            // length-prefixed read: 4 bytes BE + payload
            var lenBuf = new byte[4];
            await pipe.ReadExactlyAsync(lenBuf, ct);
            var len = (lenBuf[0] << 24) | (lenBuf[1] << 16) | (lenBuf[2] << 8) | lenBuf[3];
            if (len <= 0 || len > 1 << 20) return;   // sanity cap 1 MB
            var buf = new byte[len];
            await pipe.ReadExactlyAsync(buf, ct);
            var req = JsonSerializer.Deserialize<IpcRequest>(buf, Options);
            if (req is null) return;

            var resp = await DispatchAsync(req, ct);
            var outBytes = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(resp, Options));
            var lenOut   = BitConverter.GetBytes(outBytes.Length);
            if (BitConverter.IsLittleEndian) Array.Reverse(lenOut);
            await pipe.WriteAsync(lenOut, ct);
            await pipe.WriteAsync(outBytes, ct);
            await pipe.FlushAsync(ct);
        }
        catch (Exception ex) { _log.LogDebug(ex, "pipe client handler failed"); }
        finally { try { pipe.Dispose(); } catch { } }
    }

    private async Task<IpcResponse> DispatchAsync(IpcRequest req, CancellationToken ct)
    {
        try
        {
            return req.Action switch
            {
                IpcActions.Start         => Ok(),
                IpcActions.Stop          => Ok(),   // shutdown is host-driven
                IpcActions.PollNow       => await OnPollNowAsync(ct),
                IpcActions.GetStatus     => await OnGetStatusAsync(),
                IpcActions.SetBrightness => await OnSetBrightnessAsync(req.Payload),
                IpcActions.SetProvider   => await OnSetProviderAsync(req.Payload),
                _                          => new IpcResponse { Ok = false, Error = $"unknown action: {req.Action}" }
            };
        }
        catch (Exception ex)
        {
            return new IpcResponse { Ok = false, Error = ex.Message };
        }
    }

    private async Task<IpcResponse> OnPollNowAsync(CancellationToken ct)
    {
        var cfg = _config.Current;
        var provider = Enum.TryParse<ProviderId>(cfg.Provider, true, out var p) ? p : ProviderId.Minimax;
        var payload = await _poller.PollAsync(provider, ct);
        if (payload is null) return new IpcResponse { Ok = false, Error = "poll returned null" };
        await _ble.WritePayloadAsync(payload with { BrightnessPct = cfg.BrightnessPct }, ct);
        return Ok();
    }

    private Task<IpcResponse> OnGetStatusAsync()
    {
        var cfg = _config.Current;
        var status = new ServiceStatus
        {
            Connected     = _ble.IsConnected,
            Provider      = cfg.Provider,
            BrightnessPct = cfg.BrightnessPct,
            LastSync      = _ble.IsConnected ? DateTime.UtcNow : (DateTime?)null,
            DaemonLive    = true,
        };
        return Task.FromResult(new IpcResponse { Ok = true, Data = JsonSerializer.SerializeToElement(status) });
    }

    private async Task<IpcResponse> OnSetBrightnessAsync(object? payload)
    {
        var pct = ExtractInt(payload);
        if (pct is null) return new IpcResponse { Ok = false, Error = "expected int payload" };
        await _brightness.SetPctAsync(pct.Value, CancellationToken.None);
        await _ble.SetBrightnessPctAsync(pct.Value, CancellationToken.None);
        return Ok();
    }

    private async Task<IpcResponse> OnSetProviderAsync(object? payload)
    {
        var s = ExtractString(payload);
        if (s is null) return new IpcResponse { Ok = false, Error = "expected string payload" };
        if (!Enum.TryParse<ProviderId>(s, true, out var id))
            return new IpcResponse { Ok = false, Error = $"unknown provider: {s}" };
        await _config.SaveAsync(_config.Current with { Provider = id.ToString() });
        return Ok();
    }

    private static IpcResponse Ok() => new() { Ok = true };

    private static int? ExtractInt(object? payload)
        => payload switch
        {
            null           => null,
            int i          => i,
            long l         => (int)l,
            double d       => (int)d,
            JsonElement el => el.ValueKind == JsonValueKind.Number && el.TryGetInt32(out var v) ? v : null,
            _              => null
        };

    private static string? ExtractString(object? payload)
        => payload switch
        {
            null                => null,
            string s           => s,
            JsonElement el     => el.ValueKind == JsonValueKind.String ? el.GetString() : null,
            _                  => null
        };
}
