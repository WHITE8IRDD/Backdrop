// Backdrop.Infrastructure — main-process engine: owns host processes (ARCHITECTURE.md §6).
// Spawns Backdrop.WallpaperHost.exe x N, heartbeats every 5s, restarts dead hosts,
// falls back to solid color + toast on crash. Debounces rapid monitor changes 500ms.
using Backdrop.Core;
using Backdrop.DesktopInterop;
using Microsoft.Extensions.Logging;
using System.Diagnostics;

namespace Backdrop.Infrastructure.Engine;

public sealed class WallpaperEngineService : IWallpaperEngine, IDisposable
{
    private readonly IMonitorService _monitors;
    private readonly IDesktopIntegration _desktop;
    private readonly Func<string> _hostExePath;
    private readonly ILogger<WallpaperEngineService>? _log;
    private readonly Dictionary<string, HostHandle> _hosts = new(); // monitor -> host
    private readonly System.Threading.Timer _heartbeat;
    private CancellationTokenSource? _debounceCts;
    private bool _disposed;

    private sealed record HostHandle(Process Process, WallpaperRecord Wall, SpawnMode Mode);

    /// <summary>Locate Backdrop.WallpaperHost.exe across publish, dev, and installed layouts.
    /// Dev base (bin\Release\TFM\win-x64) is 5 levels below src\Backdrop.App, hence the depth.</summary>
    public static string ResolveHostExePath()
    {
        string @base = AppContext.BaseDirectory;
        var candidates = new[]
        {
            // 1. Same folder as the app (publish / MSIX / self-contained layout).
            Path.Combine(@base, "Backdrop.WallpaperHost.exe"),
            // 2. Dev builds: sibling project output (Release/Debug, with/without RID folder).
            Path.GetFullPath(Path.Combine(@base, @"..\..\..\..\..\Backdrop.WallpaperHost\bin\Release\net8.0-windows10.0.22621.0\win-x64\Backdrop.WallpaperHost.exe")),
            Path.GetFullPath(Path.Combine(@base, @"..\..\..\..\..\Backdrop.WallpaperHost\bin\Debug\net8.0-windows10.0.22621.0\win-x64\Backdrop.WallpaperHost.exe")),
            Path.GetFullPath(Path.Combine(@base, @"..\..\..\..\..\Backdrop.WallpaperHost\bin\Release\net8.0-windows10.0.22621.0\Backdrop.WallpaperHost.exe")),
            Path.GetFullPath(Path.Combine(@base, @"..\..\..\..\..\Backdrop.WallpaperHost\bin\Debug\net8.0-windows10.0.22621.0\Backdrop.WallpaperHost.exe")),
            // 3. Per-user install layout.
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Backdrop", "Backdrop.WallpaperHost.exe"),
        };
        foreach (var p in candidates)
        {
            if (File.Exists(p))
                return p;
        }
        throw new FileNotFoundException(
            "Backdrop.WallpaperHost.exe not found. Probed:" + Environment.NewLine +
            string.Join(Environment.NewLine, candidates));
    }

    public WallpaperEngineService(IMonitorService monitors, IDesktopIntegration desktop,
        Func<string> hostExePath, ILogger<WallpaperEngineService>? log = null)
    {
        _monitors = monitors;
        _desktop = desktop;
        _hostExePath = hostExePath;
        _log = log;
        _monitors.MonitorsChanged += OnMonitorsChanged;
        _heartbeat = new System.Threading.Timer(_ => CheckHeartbeats(), null, 5000, 5000);
    }

    public IReadOnlyList<WallpaperInstance> ActiveInstances => _hosts.Select(kv =>
        new WallpaperInstance(kv.Value.Wall.Id, kv.Key, kv.Value.Process.Id,
            kv.Value.Process.HasExited ? PlaybackState.Error : PlaybackState.Playing, null)).ToList();

    public async Task<Result> ApplyAsync(WallpaperRecord wall, string monitorDeviceId, SpawnMode mode, CancellationToken ct = default)
    {
        try
        {
            // Debounce rapid monitor changes 500ms (WALLPAPER_ENGINE.md §3).
            _debounceCts?.Cancel();
            _debounceCts = new CancellationTokenSource();
            await Task.Delay(500, _debounceCts.Token).ContinueWith(_ => { }, ct);

            StopHost(monitorDeviceId);
            var start = StartHost(wall, monitorDeviceId);
            if (!start.IsSuccess)
                return start;
            return Result.Ok();
        }
        catch (OperationCanceledException)
        {
            return Result.Fail("Superseded by newer apply.");
        }
        catch (Exception ex)
        {
            _log?.LogError(ex, "Apply failed");
            return Result.Fail(ex.Message);
        }
    }

    public async Task<Result> ApplySpanAsync(WallpaperRecord wall, IReadOnlyList<string> monitors, CancellationToken ct = default)
    {
        foreach (var m in monitors)
            StopHost(m);
        var start = StartHost(wall, "::span");
        if (!start.IsSuccess)
            return start;
        await Task.CompletedTask;
        return Result.Ok();
    }

    public async Task<Result> PauseAllAsync(PauseReason reason)
    {
        foreach (var h in _hosts.Values)
        {
            try
            {
                await h.Process.StandardInput.WriteLineAsync("{\"cmd\":\"pause\"}");
                h.Process.PriorityClass = ProcessPriorityClass.Idle; // yield CPU (PERFORMANCE.md §3)
            }
            catch (Exception ex) { _log?.LogWarning(ex, "Pause host failed"); }
        }
        _log?.LogInformation("Paused all ({Reason})", reason);
        return Result.Ok();
    }

    public async Task<Result> ResumeAllAsync()
    {
        foreach (var h in _hosts.Values)
        {
            try
            {
                h.Process.PriorityClass = ProcessPriorityClass.Normal;
                await h.Process.StandardInput.WriteLineAsync("{\"cmd\":\"resume\"}");
            }
            catch (Exception ex) { _log?.LogWarning(ex, "Resume host failed"); }
        }
        return Result.Ok();
    }

    public Task<Result> RemoveAsync(Guid wallpaperId)
    {
        foreach (var key in _hosts.Where(kv => kv.Value.Wall.Id == wallpaperId).Select(kv => kv.Key).ToList())
            StopHost(key);
        return Task.FromResult(Result.Ok());
    }

    private Result StartHost(WallpaperRecord wall, string monitor)
    {
        try
        {
            var exe = _hostExePath();
            if (!File.Exists(exe))
            {
                // Fall back to the multi-layout resolver (dev vs publish outputs).
                try { exe = ResolveHostExePath(); }
                catch (Exception rex) { return Result.Fail(rex.Message); }
            }
            var psi = new ProcessStartInfo(exe,
                $"--monitor \"{monitor}\" --wallpaper-id {wall.Id:N} --library-path \"{wall.LibraryPath}\" --type {wall.Type}")
            {
                UseShellExecute = false,
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                CreateNoWindow = true,
            };
            var p = Process.Start(psi);
            if (p is null)
                return Result.Fail("Failed to start host process.");
            _hosts[monitor] = new HostHandle(p, wall, SpawnMode.PerMonitor);
            return Result.Ok();
        }
        catch (Exception ex)
        {
            return Result.Fail(ex.Message);
        }
    }

    private void StopHost(string monitor)
    {
        if (_hosts.Remove(monitor, out var h))
        {
            try
            {
                if (!h.Process.HasExited)
                {
                    h.Process.StandardInput.WriteLine("{\"cmd\":\"stop\"}");
                    if (!h.Process.WaitForExit(1500))
                        h.Process.Kill();
                }
                h.Process.Dispose();
            }
            catch { }
        }
    }

    private void CheckHeartbeats()
    {
        foreach (var (monitor, h) in _hosts.ToList())
        {
            try
            {
                if (h.Process.HasExited)
                {
                    _log?.LogWarning("Host for {Monitor} died (exit {Code}); restarting", monitor, h.Process.ExitCode);
                    _hosts.Remove(monitor);
                    StartHost(h.Wall, monitor); // restart or UI shows fallback toast
                }
            }
            catch { }
        }
    }

    private void OnMonitorsChanged(object? sender, EventArgs e)
    {
        // Re-parent hosts on hotplug (MULTI_MONITOR.md §3); removed monitor's wallpaper moves to primary.
        var current = _monitors.GetMonitors().Select(m => m.DeviceId).ToHashSet();
        foreach (var key in _hosts.Keys.Where(k => k != "::span" && !current.Contains(k)).ToList())
        {
            var wall = _hosts[key].Wall;
            StopHost(key);
            var primary = _monitors.GetMonitors().FirstOrDefault(m => m.IsPrimary)?.DeviceId
                ?? current.FirstOrDefault();
            if (primary is not null)
                StartHost(wall, primary);
        }
    }

    public void Dispose()
    {
        if (_disposed)
            return;
        _disposed = true;
        _monitors.MonitorsChanged -= OnMonitorsChanged;
        _heartbeat.Dispose();
        foreach (var k in _hosts.Keys.ToList())
            StopHost(k);
    }
}
