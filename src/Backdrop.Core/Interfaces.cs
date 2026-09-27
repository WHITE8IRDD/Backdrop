// Backdrop.Core — service contracts (ARCHITECTURE.md §3, §5).
namespace Backdrop.Core;

public interface IWallpaperPlayer : IAsyncDisposable
{
    Task<Result> LoadAsync(WallpaperRecord wall, CancellationToken ct);
    Task<Result> PlayAsync();
    Task<Result> PauseAsync();
    Task<Result> ResumeAsync();
    Task<Result> StopAsync();
    Task<Result> SetVolumeAsync(double vol); // 0..1
    Task<Result> SetMuteAsync(bool mute);
    Task<Result> SeekAsync(TimeSpan position);
    PlaybackState State { get; }
}

public interface IWallpaperEngine
{
    Task<Result> ApplyAsync(WallpaperRecord wall, string monitorDeviceId, SpawnMode mode, CancellationToken ct = default);
    Task<Result> ApplySpanAsync(WallpaperRecord wall, IReadOnlyList<string> monitors, CancellationToken ct = default);
    Task<Result> PauseAllAsync(PauseReason reason);
    Task<Result> ResumeAllAsync();
    Task<Result> RemoveAsync(Guid wallpaperId);
    IReadOnlyList<WallpaperInstance> ActiveInstances { get; }
}

public interface IMonitorService
{
    IReadOnlyList<MonitorInfo> GetMonitors();
    event EventHandler? MonitorsChanged;
}

public interface IDesktopIntegration
{
    Result<nint> InitializeWorkerW();
    Result<nint> GetWorkerW();
    Result AttachHost(nint hostHwnd, RectPx bounds);
    RectPx GetVirtualScreen();
}

public interface IPerformanceManager : IDisposable
{
    event EventHandler<PauseReason?>? PauseStateChanged; // null = resume
    PauseReason? CurrentPause { get; }
    Task EvaluateAsync();
}

public interface IThumbnailService
{
    Task<Result<string>> GenerateAsync(WallpaperRecord wall, CancellationToken ct = default);
}

public interface ISettingsService<T> where T : class, new()
{
    T Current { get; }
    Task<Result> SaveAsync();
    Task<Result> ReloadAsync();
    event EventHandler<T>? Changed;
}

public interface ITrayService
{
    void Show();
    void Hide();
    void SetPaused(bool paused);
}

public interface IStartupService
{
    bool IsEnabled();
    Result SetEnabled(bool enabled);
}

public interface IWallpaperCatalog
{
    Task<Result<WallpaperRecord>> AddAsync(WallpaperRecord record, CancellationToken ct = default);
    Task<Result<WallpaperRecord?>> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<Result<WallpaperRecord?>> GetByHashAsync(string sha256, CancellationToken ct = default);
    Task<IReadOnlyList<WallpaperRecord>> ListAsync(CancellationToken ct = default);
    Task<Result> RemoveAsync(Guid id, CancellationToken ct = default);
    Task<Result> TouchUsedAsync(Guid id, CancellationToken ct = default);
    Task<Result> UpdateAsync(WallpaperRecord record, CancellationToken ct = default);
}

public interface IImportService
{
    Task<Result<WallpaperRecord>> ImportAsync(string sourcePathOrUrl, CancellationToken ct = default);
}
