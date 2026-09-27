// Backdrop.WallpaperHost — MediaFoundation player (WALLPAPER_ENGINE.md §2, TECH_STACK.md).
// Uses Windows.Media.Playback.MediaPlayer: HW accel, loop, mute/volume.
// Runs ONLY in the host process — main app never touches MF.
using Backdrop.Core;
#if WINDOWS10_0_22621_0_OR_GREATER
using Windows.Media.Core;
using Windows.Media.Playback;
#endif

namespace Backdrop.WallpaperHost.Players;

public sealed class MediaFoundationPlayer : IWallpaperPlayer
{
    private WallpaperRecord? _wall;
#if WINDOWS10_0_22621_0_OR_GREATER
    private MediaPlayer? _mp;
#endif
    public PlaybackState State { get; private set; } = PlaybackState.Stopped;

    public Task<Result> LoadAsync(WallpaperRecord wall, CancellationToken ct)
    {
        try
        {
            _wall = wall;
            State = PlaybackState.Buffering;
#if WINDOWS10_0_22621_0_OR_GREATER
            _mp?.Dispose();
            _mp = new MediaPlayer
            {
                IsLoopingEnabled = true,
                IsMuted = true, // wallpapers start muted (UI spec)
                // HW decode via MediaFoundation is on by default; no opt-in flag on MediaPlayer.
            };
            _mp.Source = MediaSource.CreateFromUri(new Uri(wall.LibraryPath, UriKind.RelativeOrAbsolute));
            _mp.MediaOpened += (_, _) => State = PlaybackState.Playing;
            _mp.MediaFailed += (_, _) => State = PlaybackState.Error;
#else
            State = PlaybackState.Error;
            return Task.FromResult(Result.Fail("MediaFoundation unavailable on this target."));
#endif
            State = PlaybackState.Buffering;
            return Task.FromResult(Result.Ok());
        }
        catch (Exception ex)
        {
            State = PlaybackState.Error;
            return Task.FromResult(Result.Fail(ex.Message));
        }
    }

    public Task<Result> PlayAsync()
    {
        try
        {
#if WINDOWS10_0_22621_0_OR_GREATER
            _mp?.Play();
#endif
            State = PlaybackState.Playing;
            return Task.FromResult(Result.Ok());
        }
        catch (Exception ex) { return Task.FromResult(Result.Fail(ex.Message)); }
    }

    public Task<Result> PauseAsync()
    {
        try
        {
#if WINDOWS10_0_22621_0_OR_GREATER
            _mp?.Pause();
            // Yield GPU: lowering priority + pausing gives 0% CPU/GPU (PERFORMANCE.md §5).
            try { System.Diagnostics.Process.GetCurrentProcess().PriorityClass = System.Diagnostics.ProcessPriorityClass.Idle; } catch { }
#endif
            State = PlaybackState.Paused;
            return Task.FromResult(Result.Ok());
        }
        catch (Exception ex) { return Task.FromResult(Result.Fail(ex.Message)); }
    }

    public Task<Result> ResumeAsync() => PlayAsync();

    public Task<Result> StopAsync()
    {
        try
        {
#if WINDOWS10_0_22621_0_OR_GREATER
            _mp?.Pause();
            _mp?.Dispose();
            _mp = null;
#endif
            State = PlaybackState.Stopped;
            return Task.FromResult(Result.Ok());
        }
        catch (Exception ex) { return Task.FromResult(Result.Fail(ex.Message)); }
    }

    public Task<Result> SetVolumeAsync(double vol)
    {
        try
        {
#if WINDOWS10_0_22621_0_OR_GREATER
            if (_mp is not null)
                _mp.Volume = Math.Clamp(vol, 0, 1);
#endif
            return Task.FromResult(Result.Ok());
        }
        catch (Exception ex) { return Task.FromResult(Result.Fail(ex.Message)); }
    }

    public Task<Result> SetMuteAsync(bool mute)
    {
        try
        {
#if WINDOWS10_0_22621_0_OR_GREATER
            if (_mp is not null)
                _mp.IsMuted = mute;
#endif
            return Task.FromResult(Result.Ok());
        }
        catch (Exception ex) { return Task.FromResult(Result.Fail(ex.Message)); }
    }

    public Task<Result> SeekAsync(TimeSpan position) => Task.FromResult(Result.Ok());

    public ValueTask DisposeAsync()
    {
#if WINDOWS10_0_22621_0_OR_GREATER
        _mp?.Dispose();
        _mp = null;
#endif
        return ValueTask.CompletedTask;
    }
}

/// <summary>GIF goes through the MF pipeline (WALLPAPER_ENGINE.md §2).</summary>
public sealed class GifPlayer : IWallpaperPlayer
{
    private readonly MediaFoundationPlayer _inner = new();
    public PlaybackState State => _inner.State;
    public Task<Result> LoadAsync(WallpaperRecord w, CancellationToken c) => _inner.LoadAsync(w, c);
    public Task<Result> PlayAsync() => _inner.PlayAsync();
    public Task<Result> PauseAsync() => _inner.PauseAsync();
    public Task<Result> ResumeAsync() => _inner.ResumeAsync();
    public Task<Result> StopAsync() => _inner.StopAsync();
    public Task<Result> SetVolumeAsync(double v) => _inner.SetVolumeAsync(v);
    public Task<Result> SetMuteAsync(bool m) => _inner.SetMuteAsync(m);
    public Task<Result> SeekAsync(TimeSpan p) => _inner.SeekAsync(p);
    public ValueTask DisposeAsync() => _inner.DisposeAsync();
}
