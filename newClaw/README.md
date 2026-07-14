# Clawmeter — C# rewrite (Windows host)

C# / WPF / Windows Service rewrite of the host-side daemon + tray app.
Firmware (ESP32-CYD) stays C++/Arduino and is **unchanged** — the wire
protocol is byte-for-byte identical to the Python daemon's today.

## Quick start

```powershell
# 1. Build everything (debug)
.\scripts\dev-cmd.ps1

# 2. Run the service in foreground (for dev)
.\scripts\dev-cmd.ps1 service    # has --console flag

# 3. Run the UI
.\scripts\dev-cmd.ps1 ui

# 4. Publish self-contained single-file
.\scripts\publish.ps1
# → dist\service\Clawmeter.Service.exe  (~75 MB)
# → dist\ui\Clawmeter.UI.exe           (~75 MB)
```

## Architecture

```
Windows host
├─ Clawmeter.Service     (.NET 8 BackgroundService)  ─BLE─► ESP32-CYD firmware
│   ├─ BleLinkService             (scanner, connector, JSON writer)
│   ├─ ProviderPollerService      (per-provider HTTPS polling)
│   ├─ BrightnessController       (NVS write hook)
│   ├─ ConfigService              (DPAPI-protected secrets)
│   └─ NamedPipeServer            (\\.\pipe\Clawmeter)
│
└─ Clawmeter.UI          (.NET 8 WPF + Hardcodet.NotifyIcon.Wpf)
    ├─ TrayIcon                  (provider menu, brightness menu)
    ├─ BrightnessSlider
    ├─ ProviderPicker
    ├─ MinMaxCredentialsDialog   (DPAPI-encrypted key)
    ├─ LogViewerWindow
    └─ ServiceClient              (named-pipe client → Service)
```

## Solution layout

```
newClaw/
├── Clawmeter.sln
├── src/
│   ├── Clawmeter.Shared/    ← BleProtocol, ProviderId, UsagePayload, IpcContracts
│   ├── Clawmeter.Service/   ← BackgroundService + BleLink + Poller + Brightness + Config + DPAPI + Pipe
│   └── Clawmeter.UI/        ← WPF + MahApps.Metro + Hardcodet tray
├── tools/
│   ├── Clawmeter.Installer/ ← sc create/delete/start/stop wrapper
│   └── BleProvisioner/      ← CLI: BLE-push SSID + MiniMax key
├── tests/Clawmeter.Tests/   ← xunit round-trips (BleProtocol)
├── scripts/
│   ├── dev-cmd.ps1          ← build / test / run-ui / run-service
│   └── publish.ps1          ← dotnet publish self-contained
├── PLAN.md                  ← full architecture + milestones
└── README.md                ← (you are here)
```

## Key dependencies

| Project | Packages |
|---|---|
| Shared | `System.Text.Json` (built-in) |
| Service | `Microsoft.Extensions.Hosting` (BackgroundService), `Serilog`, `Windows.Devices.Bluetooth` (built-in) |
| UI | `Hardcodet.NotifyIcon.Wpf`, `MahApps.Metro` |

## Mapping old Python → new C#

| Python (current daemon) | C# (new) |
|---|---|
| `daemon/claude_usage_daemon_windows.py` | `Clawmeter.Service/` |
| `daemon/tray_windows.py` | `Clawmeter.UI/` |
| `daemon/payloads.py:build_minimax_usage_payload` | `ProviderPollerService.PollMinimaxAsync` (M3) |
| `daemon/ble.py` | `BleLinkService` (M2) |
| `daemon/brightness.py` | `BrightnessController` (M4) |
| `daemon/config.py` | `ConfigService` (M4) |
| `daemon/autostart_windows.py` (HKCU\Run) | `Clawmeter.Installer` (sc.exe, M7) |
| `~/.config/clawdmeter/brightness` | Same file path, but written via BLE |
| `daemon/plugins/minimax` | `ProviderPollerService.PollMinimaxAsync` (M3) |

## Firmware contract — UNCHANGED

The C# service produces **byte-for-byte** the same JSON wire format as
the Python daemon's `build_windowed_payload()` — the ESP32 firmware
already parses this. No firmware changes required.

## Milestones (≈9.5 days)

| # | Deliverable | Status |
|---|---|---|
| **M1** | Clawmeter.Shared skeleton | ✅ scaffolded (record types, BleProtocol) |
| **M2** | Clawmeter.Service — BLE | skeleton; Windows.Devices.Bluetooth wiring next |
| **M3** | MiniMax poller | skeleton; HTTP + scoring next |
| **M4** | BrightnessController + DPAPI | skeleton; DpapiSecretStore next |
| **M5** | NamedPipeServer + IPC | skeleton; pipe + ACL next |
| **M6** | Clawmeter.UI — WPF | skeleton; tray, dialogs, slider next |
| **M7** | Clawmeter.Installer | ✅ skeleton (sc.exe wrapper) |
| **M8** | BleProvisioner CLI | ✅ skeleton |
| **M9** | Tests + publish scripts | ✅ xunit + ps1 in place |

## IPC contract (UI ↔ Service)

Named pipe `\\.\pipe\Clawmeter`, JSON over the wire:

```jsonc
// request
{ "action": "set-brightness", "payload": 75 }

// response
{ "ok": true, "error": null, "data": null }
```

Actions: `start` `stop` `set-brightness` `set-provider` `poll-now` `get-status`
(see `IpcActions` in `Clawmeter.Shared/IpcContracts.cs`).

## Decisions taken (M0)

1. **Service + UI** — separate exes
2. **Named-pipe auth** — local-only, current-user ACLs
3. **Auto-update** — rely on `dotnet` for v1
4. **.NET** — 8 LTS
5. **Publish** — single-file self-contained (~75 MB)
6. **Code signing** — unsigned for dev; sign on release

## Out of scope

- Firmware (C++) — keep as-is
- ESP32 toolchain — replaced by `BleProvisioner` over BLE
- macOS / Linux daemon ports — separate M3 in future
- `daemon/` Python — kept for fallback until C# covers all flows

## See also

- [PLAN.md](./PLAN.md) — full architecture + risk analysis + open questions
- Parent [CLAUDE.md](../CLAUDE.md) and [AGENTS.md](../AGENTS.md) for firmware context
