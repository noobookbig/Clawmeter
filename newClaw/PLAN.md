# Plan: Clawmeter C# rewrite

## Scope (host-side only)

| Component | Current | Target |
|---|---|---|
| **ESP32 firmware** (CYD) | C++ / Arduino | **Keep C++** (not rewriting) |
| **BLE protocol** (firmware ↔ host) | JSON over NimBLE-GATT | **Keep same** (drop-in) |
| **MiniMax API polling** | Python httpx | **C# HttpClient** |
| **Daemon** (polling + BLE writer) | Python asyncio | **C# Windows Service** |
| **Tray GUI** (start/stop, brightness, provider) | Python tkinter / pystray | **C# WPF** (Hardcodet.NotifyIcon.Wpf) |
| **Launcher** | .cmd + PowerShell + VBS | **C# ServiceInstaller** (sc.exe) |
| **Auto-restart on crash** | none | **Windows Service** recovery |
| **Logging** | daemon.log (text) | **Serilog** structured (rolling file) |

## Architecture

```
┌─────────────────────────────────────────────────────────────────┐
│                      Windows 10/11 host                         │
│                                                                 │
│  ┌────────────────────┐  IPC  ┌──────────────────────────────┐  │
│  │  Clawmeter.UI      │◄────►│   Clawmeter.Service            │  │
│  │  (WPF)             │named  │   (BackgroundService)        │  │
│  │                    │pipe + │                               │  │
│  │  • tray icon       │TCP    │   • BLE scanner + connector  │  │
│  │  • brightness      │rest   │   • MiniMax HTTPS poller     │  │
│  │  • provider menu   │       │   • writes JSON to firmware  │  │
│  │  • service control │       │   • brightness control       │  │
│  │  • preview pane    │       │   • config persistence      │  │
│  └────────────────────┘       │   • MiniMax creds (DPAPI)    │  │
│                                └──────────────┬────────────────┘  │
└───────────────────────────────────────┼────────────────────────┘
                                        │ BLE
                                ┌───────┴────────┐
                                │ Clawmeter      │
                                │ (ESP32-CYD)    │
                                │ firmware v3+   │
                                └────────────────┘
```

## Solution layout (`newClaw/`)

```
newClaw/
├── Clawmeter.sln
├── src/
│   ├── Clawmeter.Service/                 (Windows Service, .NET 8)
│   │   ├── Clawmeter.Service.csproj
│   │   ├── Program.cs                     (entrypoint, --service / --console)
│   │   ├── Services/
│   │   │   ├── ClawmeterHostedService.cs   (BackgroundService — main loop)
│   │   │   ├── BleLinkService.cs           (BLE scanner/connector/writer)
│   │   │   ├── ProviderPollerService.cs    (per-provider poll logic)
│   │   │   ├── BrightnessController.cs    (NVS + idle hook)
│   │   │   └── ConfigService.cs            (JSON file + DPAPI)
│   │   ├── Models/
│   │   │   ├── ProviderId.cs               (enum: minimax, claude, codex, …)
│   │   │   ├── UsagePayload.cs
│   │   │   └── ServiceConfig.cs
│   │   ├── Infra/
│   │   │   ├── DpapiSecretStore.cs         (Windows DPAPI for API keys)
│   │   │   ├── NamedPipeServer.cs          (UI ↔ service IPC)
│   │   │   └── LogConfig.cs
│   │   └── appsettings.json
│   │
│   ├── Clawmeter.UI/                      (WPF app, .NET 8-windows)
│   │   ├── Clawmeter.UI.csproj             (TargetFramework=net8.0-windows, UseWPF=true)
│   │   ├── App.xaml / App.xaml.cs
│   │   ├── MainWindow.xaml / .cs          (preview + status)
│   │   ├── Views/
│   │   │   ├── TrayIcon.xaml               (Hardcodet.NotifyIcon.Wpf)
│   │   │   ├── BrightnessSlider.xaml
│   │   │   ├── ProviderPicker.xaml
│   │   │   ├── MinMaxCredentialsDialog.xaml
│   │   │   └── LogViewerWindow.xaml
│   │   ├── Services/
│   │   │   ├── ServiceController.cs        (start/stop/install via sc.exe)
│   │   │   └── ServiceClient.cs            (named-pipe client)
│   │   └── Themes/                         (Dark / Light / Neon)
│   │
│   └── Clawmeter.Shared/                   (class library, .NET 8)
│       ├── Clawmeter.Shared.csproj
│       ├── BleProtocol.cs                  (the JSON shape both sides agree on)
│       ├── ProviderId.cs
│       ├── UsagePayload.cs
│       └── IpcContracts.cs                 (named-pipe request/response records)
│
├── tools/
│   ├── Clawmeter.Installer/
│   │   ├── Clawmeter.Installer.csproj
│   │   └── Program.cs                     (sc create/delete/start/stop)
│   └── BleProvisioner/
│       └── Program.cs                     (writes SSID + API key to NVS over BLE)
│
├── tests/
│   ├── Clawmeter.Tests/
│   │   ├── ProviderPollerServiceTests.cs
│   │   ├── BleProtocolParserTests.cs
│   │   └── DpapiSecretStoreTests.cs
│
├── scripts/
│   ├── dev-cmd.ps1                        (build + watch + run)
│   ├── publish.ps1                        (dotnet publish single-file)
│   └── install-service.ps1
│
├── PLAN.md                                 (this file)
├── README.md
└── .gitignore
```

## Projects & dependencies

| Project | TargetFramework | Key packages |
|---|---|---|
| Clawmeter.Service | `net8.0-windows` | `Microsoft.Extensions.Hosting` (BackgroundService), `Microsoft.Windows.SDK.BuildTools`, `System.IO.Pipes`, `Serilog` |
| Clawmeter.UI | `net8.0-windows` (UseWPF) | `Hardcodet.NotifyIcon.Wpf` (tray), `MahApps.Metro` (theme), `System.IO.Pipes` (client) |
| Clawmeter.Shared | `net8.0` | `System.Text.Json` |
| Clawmeter.Installer | `net8.0-windows` | `System.Diagnostics.Process` (sc.exe wrapper) |
| BleProvisioner | `net8.0` | `System.IO.Pipes` (or BLE direct) |
| Clawmeter.Tests | `net8.0` | `xunit`, `Moq` |

## Build commands

```powershell
# Restore
dotnet restore newClaw/Clawmeter.sln

# Build (Release)
dotnet build newClaw/Clawmeter.sln -c Release

# Run UI in dev (talks to running service)
dotnet run --project newClaw/src/Clawmeter.UI

# Run service in console (for debugging)
dotnet run --project newClaw/src/Clawmeter.Service -- --console

# Publish (single-file, self-contained)
dotnet publish newClaw/src/Clawmeter.Service -c Release -r win-x64 --self-contained -o newClaw/dist/service
dotnet publish newClaw/src/Clawmeter.UI      -c Release -r win-x64 --self-contained -o newClaw/dist/ui
```

## Milestones

| # | Deliverable | Est. |
|---|---|---|
| **M1** | `Clawmeter.Shared` — `BleProtocol` parser, `ProviderId` enum, `UsagePayload` DTOs | 0.5 d |
| **M2** | `Clawmeter.Service` — BLE scanner + connector + JSON writer (port Python `ble_link.py`) | 2 d |
| **M3** | `Clawmeter.Service` — MiniMax poller (port Python `build_minimax_usage_payload`) | 1 d |
| **M4** | `Clawmeter.Service` — Brightness control (port Python `brightness.py`) + DPAPI secret store | 0.5 d |
| **M5** | `Clawmeter.Service` — Named-pipe IPC server + ScStartServiceCtrlDispatcher | 1 d |
| **M6** | `Clawmeter.UI` — Tray icon, brightness slider, provider picker, MinMax creds dialog, log viewer, service control | 2 d |
| **M7** | `Clawmeter.Installer` — `sc create` / `delete` / `start` / `stop` wrapper + scheduled-task for auto-restart | 0.5 d |
| **M8** | BleProvisioner — CLI to write SSID + API key to firmware NVS over BLE (Phase 2-4 of WiFi plan) | 1 d |
| **M9** | Tests + README + publish scripts | 1 d |
| | **Total** | **~9.5 d** |

## Mapping old Python → new C#

| Python (current) | C# (new) |
|---|---|
| `daemon/claude_usage_daemon_windows.py` (1700 LOC) | `Clawmeter.Service/` (~1500 LOC across multiple files) |
| `daemon/tray_windows.py` (1170 LOC) | `Clawmeter.UI/` (~800 LOC WPF) |
| `daemon/payloads.py` | `Clawmeter.Shared/BleProtocol.cs` + per-provider poller |
| `daemon/plugins/minimax` | `ProviderPollerService.cs` (MiniMax strategy) |
| `daemon/autostart_windows.py` (HKCU\Run) | `Clawmeter.Installer/` (sc.exe Windows Service) |
| `daemon/config.py` | `ConfigService.cs` (JSON file + DPAPI for API keys) |
| `~/.config/clawdmeter/brightness` file | BrightnessController writes via BLE char (same as today) |
| Bleak (`bleak` Python lib) | `Windows.Devices.Bluetooth` (built-in .NET 8 on Win 10+) |
| `httpx` (async HTTP) | `HttpClient` (async/await) |
| `pystray` (tray icon) | `Hardcodet.NotifyIcon.Wpf` |
| `tkinter` (dialogs) | WPF `Window` + `ContentDialog` (MahApps) |

## IPC: UI ↔ Service

**Named pipe** `\\.\pipe\Clawmeter` for low-latency control + status.

```csharp
public record ServiceCommand(string Action, object? Payload);
public record ServiceResponse(bool Ok, string? Error, object? Data);

// UI → Service
"start"             → ServiceControl.StartAsync()
"stop"              → ServiceControl.StopAsync()
"set-brightness"    → BrightnessController.SetPct(pct)
"set-provider"      → ConfigService.SetProvider(id)
"poll-now"          → ProviderPollerService.PollNowAsync()
"get-status"        → returns { connected, provider, brightness, last_sync, daemon_live }

// Service → UI (push via pipe async)
BrightnessChanged(int pct)
StatusChanged(ServiceStatus)
LogLineEmitted(string line)
```

For the initial milestone we use a single named pipe + JSON messages. If we later need pub/sub across processes, swap to gRPC over `localhost:50051` (also in-process during dev).

## Secrets storage

API keys stored encrypted with **DPAPI** (Windows Data Protection API):
```csharp
public byte[] Protect(byte[] data);  // ProtectedData.Protect (CurrentUser scope)
```

Stored path: `%APPDATA%\Clawmeter\secrets\minimax.key` (binary, DPAPI-encrypted).

## Logging

`Serilog` with:
- Rolling file: `%APPDATA%\Clawmeter\logs\daemon-.log` (daily, 14-day retention)
- Windows Event Log (Service channel: `Clawmeter`)
- `ILogger<T>` injected into all services

## Firmware contract (UNCHANGED)

The C# service must produce **byte-for-byte** the same JSON wire format that
`daemon/payloads.py:build_minimax_usage_payload` does today. The ESP32 firmware
already parses this — no firmware changes required.

The C# side can additionally write `BRIGHTNESS` via the same daemon BLE characteristic (UUID matches the Python daemon's).

## When to use what

| Use case | Tool |
|---|---|
| Brightness tweaks | WPF UI slider OR tray menu |
| Provider change | WPF picker OR `--setup` flag on installer |
| API key rotation | WPF dialog (DPAPI-encrypted) OR env var on first run |
| Firmware SSID setup | `BleProvisioner` CLI (Phase 2-4 of WiFi plan) |
| Auto-start on login | `sc.exe create ClawmeterSvc start= auto` (Windows Service) |
| Crash recovery | Service Control Manager (auto-restart on failure) |

## Phase 0: cleanup of `Start Clawdmeter.cmd`

While we wait for the C# rewrite, also fix the host-side cmd that the user
just reported. Option B (VBS as primary entry point) shipped in commit
`8032dd9` and is the closest working path. The C# UI will replace this
entirely once M6 lands.

## Open questions (please confirm before M1)

1. **Service vs UI bundled**: same executable, or two separate?
   - Default: two (Service has no UI, UI is purely a tray + dialogs)
2. **Auth on the named pipe**: any security needed?
   - Local-only pipe, ACLs restrict to current user
   - For multi-user (RDP) → add named-pipe server-side ACL + token
3. **Auto-update**: ship a built-in updater, or rely on `dotnet` updates?
   - Default: `dotnet` for v1, evaluate Squirrel.Windows later
4. **C# language version**: .NET 8 LTS (default) or .NET 9 (newer)?
   - Default: .NET 8 LTS
5. **Single-file publish**: yes (no .NET runtime install needed) or framework-dep (smaller)?
   - Default: single-file self-contained (~75 MB)
6. **Code signing**: needed for production but skip for now?
   - Default: unsigned for dev, sign when shipping

## File-by-file deliverables

After approval I'll generate:
- `newClaw/Clawmeter.sln` (8 projects)
- `newClaw/PLAN.md` (this file)
- `newClaw/README.md`
- `newClaw/.gitignore`
- All csproj files
- `Program.cs` entrypoints
- Skeleton `BleProtocol.cs`, `ProviderId.cs`, `UsagePayload.cs`

Then proceed to M1 in a new branch `feature/csharp-rewrite`.

## Out of scope

- Firmware (C++) — keep as-is
- ESP32 toolchain (esptool) — replaced by `BleProvisioner` over BLE
- `daemon/` Python code — kept for fallback; C# replaces over time
- `sketches/` HTML — kept for design reference; the WPF UI replaces
- `firmware/.pio` — unchanged
- macOS/Linux daemon ports — covered by separate M3 in future
