// Backdrop.Infrastructure — event-driven PerformanceManager (PERFORMANCE.md §3).
// NO high-frequency polling. Foreground via SetWinEventHook + 500ms debounce;
// 1000ms fallback timer ONLY while a fullscreen candidate is active.
using Backdrop.Core;
using Backdrop.Core.Performance;
using Backdrop.Core.Settings;
using Backdrop.DesktopInterop;
using Backdrop.DesktopInterop.Native;
using Microsoft.Extensions.Logging;
using Microsoft.Win32;

namespace Backdrop.Infrastructure.Performance;

public sealed class PerformanceManager : IPerformanceManager
{
    private readonly Func<BackdropSettings> _settings;
    private readonly IMonitorService _monitors;
    private readonly Microsoft.Extensions.Logging.ILogger<PerformanceManager>? _log;

    private nint _hook;
    private readonly User32.WinEventDelegate _winEventCb;
    private CancellationTokenSource? _debounceCts;
    private System.Threading.Timer? _fallbackTimer; // 1000ms, only while fullscreen candidate active
    private bool _batterySaverOn;
    private bool _rdpActive;
    private bool _locked;
    private bool _displayOff;
    private bool _userPaused;
    private bool _disposed;

    public event EventHandler<PauseReason?>? PauseStateChanged;
    public PauseReason? CurrentPause { get; private set; }

    public PerformanceManager(
        Func<BackdropSettings> settings,
        IMonitorService monitors,
        Microsoft.Extensions.Logging.ILogger<PerformanceManager>? log = null)
    {
        _settings = settings;
        _monitors = monitors;
        _log = log;
        _winEventCb = OnWinEvent;

        SystemEvents.PowerModeChanged += OnPowerMode;
        SystemEvents.SessionSwitch += OnSessionSwitch;

        _hook = User32.SetWinEventHook(User32.EVENT_SYSTEM_FOREGROUND, User32.EVENT_SYSTEM_FOREGROUND,
            0, _winEventCb, 0, 0, User32.WINEVENT_OUTOFCONTEXT);
    }

    public bool UserPaused
    {
        get => _userPaused;
        set { _userPaused = value; _ = EvaluateAsync(); }
    }

    private void OnWinEvent(nint h, uint e, nint hwnd, int o, int c, uint t, uint ms)
    {
        // Debounce foreground re-eval 500ms (PERFORMANCE.md §3).
        _debounceCts?.Cancel();
        _debounceCts = new CancellationTokenSource();
        var ct = _debounceCts.Token;
        _ = Task.Run(async () =>
        {
            try
            {
                await Task.Delay(500, ct);
                await EvaluateAsync();
            }
            catch (OperationCanceledException) { }
        });
    }

    private void OnPowerMode(object sender, PowerModeChangedEventArgs e)
    {
        // Battery saver state is refreshed in Evaluate via SystemInformation; flag display-off here.
        _ = EvaluateAsync();
    }

    private void OnSessionSwitch(object sender, SessionSwitchEventArgs e)
    {
        _locked = e.Reason == SessionSwitchReason.SessionLock;
        if (e.Reason is SessionSwitchReason.RemoteConnect or SessionSwitchReason.RemoteDisconnect)
            _rdpActive = e.Reason == SessionSwitchReason.RemoteConnect;
        _ = EvaluateAsync();
    }

    public void SetDisplayOff(bool off)
    {
        _displayOff = off;
        _ = EvaluateAsync();
    }

    public void SetBatterySaver(bool on)
    {
        _batterySaverOn = on;
        _ = EvaluateAsync();
    }

    public async Task EvaluateAsync()
    {
        if (_disposed)
            return;
        PauseConditions cond;
        try
        {
            var monitors = _monitors.GetMonitors();
            bool fs = ForegroundInspector.IsForegroundFullscreen(monitors);
            bool max = _settings().PauseRules.PauseOnMaximized && ForegroundInspector.IsForegroundMaximized();
            ManageFallbackTimer(fs);
            cond = new PauseConditions(fs, max, _batterySaverOn, _rdpActive, _locked, _displayOff, _userPaused);
        }
        catch (Exception ex)
        {
            _log?.LogWarning(ex, "Evaluate failed");
            return;
        }

        var next = PauseEvaluator.Evaluate(cond, _settings().PauseRules);
        if (next != CurrentPause)
        {
            CurrentPause = next;
            _log?.LogInformation("Pause state -> {Reason}", next?.ToString() ?? "resumed");
            PauseStateChanged?.Invoke(this, next);
        }
        await Task.CompletedTask;
    }

    private void ManageFallbackTimer(bool fullscreenCandidate)
    {
        if (fullscreenCandidate && _fallbackTimer is null)
        {
            _fallbackTimer = new System.Threading.Timer(_ => _ = EvaluateAsync(),
                null, 1000, 1000); // ONLY while candidate active
        }
        else if (!fullscreenCandidate && _fallbackTimer is not null)
        {
            _fallbackTimer.Dispose();
            _fallbackTimer = null;
        }
    }

    public void Dispose()
    {
        if (_disposed)
            return;
        _disposed = true;
        if (_hook != 0)
            User32.UnhookWinEvent(_hook);
        _fallbackTimer?.Dispose();
        _debounceCts?.Dispose();
        SystemEvents.PowerModeChanged -= OnPowerMode;
        SystemEvents.SessionSwitch -= OnSessionSwitch;
    }
}
