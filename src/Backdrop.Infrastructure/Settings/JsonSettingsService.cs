// Backdrop.Infrastructure — atomic JSON settings (ARCHITECTURE.md §4).
using Backdrop.Core;
using System.Text.Json;

namespace Backdrop.Infrastructure.Settings;

public sealed class JsonSettingsService<T> : Core.ISettingsService<T>, IDisposable where T : class, new()
{
    private readonly string _path;
    private readonly FileSystemWatcher? _watcher;
    private readonly JsonSerializerOptions _json = new() { WriteIndented = true };
    private bool _suppressWatch;

    public T Current { get; private set; } = new();
    public event EventHandler<T>? Changed;

    public JsonSettingsService(string path, bool watch = true)
    {
        _path = path;
        _ = ReloadAsync();
        if (watch)
        {
            var dir = Path.GetDirectoryName(path);
            if (dir is not null)
            {
                _watcher = new FileSystemWatcher(dir, Path.GetFileName(path))
                {
                    NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.Size
                };
                _watcher.Changed += async (_, _) =>
                {
                    if (_suppressWatch)
                        return;
                    await Task.Delay(100);
                    await ReloadAsync();
                };
                _watcher.EnableRaisingEvents = true;
            }
        }
    }

    public async Task<Result> SaveAsync()
    {
        try
        {
            var dir = Path.GetDirectoryName(_path);
            if (dir is not null)
                Directory.CreateDirectory(dir);
            var tmp = _path + ".tmp";
            var json = JsonSerializer.Serialize(Current, _json);
            _suppressWatch = true;
            try
            {
                await File.WriteAllTextAsync(tmp, json);
                File.Move(tmp, _path, overwrite: true); // atomic tmp -> move
            }
            finally
            {
                await Task.Delay(150);
                _suppressWatch = false;
            }
            return Result.Ok();
        }
        catch (Exception ex)
        {
            return Result.Fail(ex.Message);
        }
    }

    public async Task<Result> ReloadAsync()
    {
        try
        {
            if (!File.Exists(_path))
            {
                Current = new T();
                return Result.Ok();
            }
            string json;
            // Retry: writer may hold the file briefly (atomic move is fast but AV can interfere).
            for (int i = 0; ; i++)
            {
                try
                {
                    json = await File.ReadAllTextAsync(_path);
                    break;
                }
                catch (IOException) when (i < 5)
                {
                    await Task.Delay(50);
                }
            }
            var loaded = JsonSerializer.Deserialize<T>(json);
            if (loaded is not null)
            {
                Current = loaded;
                Changed?.Invoke(this, Current);
            }
            return Result.Ok();
        }
        catch (Exception ex)
        {
            return Result.Fail(ex.Message);
        }
    }

    public void Dispose() => _watcher?.Dispose();
}
