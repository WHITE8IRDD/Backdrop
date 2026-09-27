// Backdrop.App — Library VM (UI spec §2: filter chips, sort, cards, drag-drop, preview).
using Backdrop.Core;
using Backdrop.Infrastructure.Settings;
using Backdrop.Core.Settings;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Backdrop.App.ViewModels;

public enum LibraryFilter { All, Video, Gif, Html }
public enum LibrarySort { LastUsed, DateAdded, Name }

public partial class LibraryViewModel : ObservableObject
{
    private readonly IWallpaperCatalog _catalog;
    private readonly IImportService _import;
    private readonly IWallpaperEngine _engine;
    private readonly IMonitorService _monitors;
    private readonly JsonSettingsService<BackdropSettings> _settings;
    private readonly IThumbnailService _thumbs;
    private bool _backfilling;

    [ObservableProperty] private LibraryFilter filter = LibraryFilter.All;
    [ObservableProperty] private LibrarySort sort = LibrarySort.LastUsed;
    [ObservableProperty] private string search = string.Empty;
    [ObservableProperty] private bool isImporting;
    [ObservableProperty] private string? infoMessage;
    [ObservableProperty] private WallpaperRecord? selected;

    public List<WallpaperRecord> Items { get; private set; } = [];

    public LibraryViewModel(IWallpaperCatalog catalog, IImportService import,
        IWallpaperEngine engine, IMonitorService monitors,
        JsonSettingsService<BackdropSettings> settings, IThumbnailService thumbs)
    {
        _catalog = catalog;
        _import = import;
        _engine = engine;
        _monitors = monitors;
        _settings = settings;
        _thumbs = thumbs;
    }

    public IEnumerable<WallpaperRecord> FilteredItems()
    {
        IEnumerable<WallpaperRecord> q = Items;
        q = Filter switch
        {
            LibraryFilter.Video => q.Where(x => x.Type == WallpaperType.Video),
            LibraryFilter.Gif => q.Where(x => x.Type == WallpaperType.Gif),
            LibraryFilter.Html => q.Where(x => x.Type is WallpaperType.HtmlLocal or WallpaperType.HtmlRemoteUrl),
            _ => q,
        };
        if (!string.IsNullOrWhiteSpace(Search))
            q = q.Where(x => x.Title.Contains(Search, StringComparison.OrdinalIgnoreCase));
        q = Sort switch
        {
            LibrarySort.DateAdded => q.OrderByDescending(x => x.AddedAt),
            LibrarySort.Name => q.OrderBy(x => x.Title),
            _ => q.OrderByDescending(x => x.LastUsedAt),
        };
        return q;
    }

    [RelayCommand]
    public async Task RefreshAsync()
    {
        Items = (await _catalog.ListAsync()).ToList();
        OnPropertyChanged(nameof(FilteredItems));
        // Heal rows imported before real thumbnails existed (tiny/missing thumb files).
        if (!_backfilling && Items.Any(NeedsThumbnail))
        {
            _backfilling = true;
            try { await BackfillThumbnailsAsync(); } finally { _backfilling = false; }
        }
    }

    private static bool NeedsThumbnail(WallpaperRecord w)
    {
        if (w.Type is not (WallpaperType.Video or WallpaperType.Gif))
            return false;
        try
        {
            return string.IsNullOrEmpty(w.ThumbnailPath)
                || !File.Exists(w.ThumbnailPath)
                || new FileInfo(w.ThumbnailPath).Length < 4096;
        }
        catch { return true; }
    }

    private async Task BackfillThumbnailsAsync()
    {
        bool changed = false;
        foreach (var w in Items.Where(NeedsThumbnail).ToList())
        {
            var t = await _thumbs.GenerateAsync(w);
            if (t.IsSuccess && !string.IsNullOrEmpty(t.Value))
            {
                await _catalog.UpdateAsync(w with { ThumbnailPath = t.Value! });
                changed = true;
            }
        }
        if (changed)
        {
            Items = (await _catalog.ListAsync()).ToList();
            OnPropertyChanged(nameof(FilteredItems));
        }
    }

    [RelayCommand]
    public async Task ImportAsync(string path)
    {
        IsImporting = true;
        try
        {
            int before = Items.Count;
            var r = await _import.ImportAsync(path);
            App.Trace($"ImportAsync result success={r.IsSuccess} err={r.Error} itemsBefore={before}");
            if (!r.IsSuccess)
            {
                InfoMessage = $"Import failed: {r.Error}";
                return;
            }
            await RefreshAsync();
            App.Trace($"ImportAsync after refresh: items={Items.Count} filter={Filter} sort={Sort}");
            bool visible = Items.Any(x => x.Sha256 == r.Value!.Sha256);
            InfoMessage = visible
                ? $"Imported “{r.Value!.Title}” ({Items.Count} in library)."
                : $"Imported “{r.Value!.Title}” but it is not listed — restart the app.";
        }
        finally { IsImporting = false; }
    }

    [RelayCommand]
    public async Task ApplyAsync(WallpaperRecord? wall)
    {
        if (wall is null)
            return;
        var primary = _monitors.GetMonitors().FirstOrDefault(m => m.IsPrimary)?.DeviceId
            ?? _monitors.GetMonitors().FirstOrDefault()?.DeviceId;
        if (primary is null)
        {
            InfoMessage = "No monitors found.";
            return;
        }
        var r = await _engine.ApplyAsync(wall, primary, SpawnMode.PerMonitor);
        if (r.IsSuccess)
        {
            // Persist so Apply survives restart (Phase 2 criteria).
            _settings.Current.SpawnMode = SpawnMode.PerMonitor;
            _settings.Current.MonitorAssignments[primary] = wall.Id;
            await _settings.SaveAsync();
            await _catalog.TouchUsedAsync(wall.Id);
        }
        InfoMessage = r.IsSuccess ? $"Applied to {primary}." : $"Apply failed: {r.Error}";
    }

    [RelayCommand]
    public async Task RemoveAsync(WallpaperRecord? wall)
    {
        if (wall is null)
            return;
        await _engine.RemoveAsync(wall.Id); // Remove requires confirmation dialog (UI spec §3) — view shows it.
        var r = await _catalog.RemoveAsync(wall.Id);
        if (r.IsSuccess)
        {
            foreach (var key in _settings.Current.MonitorAssignments
                         .Where(kv => kv.Value == wall.Id).Select(kv => kv.Key).ToList())
                _settings.Current.MonitorAssignments.Remove(key);
            await _settings.SaveAsync();
        }
        InfoMessage = r.IsSuccess ? "Removed." : $"Remove failed: {r.Error}";
        await RefreshAsync();
    }
}
