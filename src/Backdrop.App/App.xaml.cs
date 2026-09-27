// Backdrop.App — entry point. DI host, global handlers (ARCHITECTURE.md §7),
// single-instance + minimal CLI (ROADMAP: CLI in V1 minimal).
using Backdrop.Core;
using Backdrop.Core.Settings;
using Backdrop.DesktopInterop;
using Backdrop.Infrastructure.Catalog;
using Backdrop.Infrastructure.Engine;
using Backdrop.Infrastructure.Import;
using Backdrop.Infrastructure.Ipc;
using Backdrop.Infrastructure.Logging;
using Backdrop.Infrastructure.Performance;
using Backdrop.Infrastructure.Settings;
using Backdrop.Infrastructure.Startup;
using Backdrop.Infrastructure.Thumbnails;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.UI.Xaml;

namespace Backdrop.App;

public partial class App : Application
{
    public static IHost Host { get; private set; } = null!;
    public static Window MainWindow { get; private set; } = null!;
    private static Mutex? _single;

    // Synchronous stage tracer: Serilog flushes async and may lose crash context.
    internal static void Trace(string stage)
    {
        try
        {
            Directory.CreateDirectory(BackdropPaths.LogsDir);
            File.AppendAllText(Path.Combine(BackdropPaths.LogsDir, "startup.log"),
                $"{DateTime.Now:HH:mm:ss.fff} [{Environment.ProcessId}] {stage}{Environment.NewLine}");
        }
        catch { }
    }

    public App()
    {
        Trace("App ctor (framework resolved via auto-init/DD or package identity)");
        Trace("InitializeComponent");
        InitializeComponent();
        UnhandledException += (s, e) =>
        {
            Trace("FATAL XAML: " + e.Exception);
            try { Host?.Services.GetService<ILogger<App>>()?.LogCritical(e.Exception, "Unhandled XAML exception"); } catch { }
            e.Handled = true;
        };
        AppDomain.CurrentDomain.UnhandledException += (s, e) =>
        {
            Trace("FATAL domain: " + e.ExceptionObject);
            try { Host?.Services.GetService<ILogger<App>>()?.LogCritical((Exception)e.ExceptionObject, "Unhandled domain exception"); } catch { }
        };
    }

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        Trace("OnLaunched enter");
        _single = new Mutex(true, "Backdrop.App.SingleInstance", out bool first);
        if (!first)
        {
            Trace("second instance -> exit 0");
            Environment.Exit(0); // second instance: CLI pipe handles the command
        }

        Trace(" building host");
        BackdropPaths.EnsureCreated();
        var factory = BackdropLogging.CreateFactory(BackdropPaths.LogsDir);

        Host = Microsoft.Extensions.Hosting.Host.CreateDefaultBuilder()
            .ConfigureServices(services =>
            {
                services.AddSingleton(factory);
                services.AddSingleton(sp => sp.GetRequiredService<ILoggerFactory>().CreateLogger<App>());
                services.AddSingleton(new JsonSettingsService<BackdropSettings>(BackdropPaths.SettingsFile));
                services.AddSingleton(sp => ((JsonSettingsService<BackdropSettings>)sp.GetRequiredService<JsonSettingsService<BackdropSettings>>()).Current);
                // Note: settings snapshot delegate — PerformanceManager reads live via service.
                services.AddSingleton<Func<BackdropSettings>>(sp =>
                    () => sp.GetRequiredService<JsonSettingsService<BackdropSettings>>().Current);
                services.AddSingleton<IMonitorService, MonitorService>();
                services.AddSingleton<IDesktopIntegration, DesktopIntegrationService>();
                services.AddSingleton<IWallpaperCatalog>(sp => new SqliteCatalogService(BackdropPaths.DatabaseFile));
                services.AddSingleton<IThumbnailService, ThumbnailService>();
                services.AddSingleton<IImportService, ImportService>();
                services.AddSingleton<IWallpaperEngine>(sp => new WallpaperEngineService(
                    sp.GetRequiredService<IMonitorService>(),
                    sp.GetRequiredService<IDesktopIntegration>(),
                    () => Path.Combine(AppContext.BaseDirectory, "Backdrop.WallpaperHost.exe"),
                    sp.GetService<ILogger<WallpaperEngineService>>()));
                services.AddSingleton<IPerformanceManager>(sp => new PerformanceManager(
                    sp.GetRequiredService<Func<BackdropSettings>>(),
                    sp.GetRequiredService<IMonitorService>(),
                    sp.GetService<ILogger<PerformanceManager>>()));
                services.AddSingleton<IStartupService, StartupService>();
                services.AddSingleton<Services.TrayService>();
                services.AddSingleton<ViewModels.LibraryViewModel>();
                services.AddSingleton<ViewModels.DisplaysViewModel>();
                services.AddSingleton<ViewModels.PerformanceViewModel>();
                services.AddSingleton<ViewModels.SettingsViewModel>();
            })
            .Build();

        // Wire pause -> engine (PERFORMANCE.md §3 pause/resume flow).
        Trace(" resolving services");
        var perf = (PerformanceManager)Host.Services.GetRequiredService<IPerformanceManager>();
        Trace(" perf ok");
        var engine = Host.Services.GetRequiredService<IWallpaperEngine>();
        Trace(" engine ok");
        perf.PauseStateChanged += async (_, reason) =>
        {
            if (reason is null)
                await engine.ResumeAllAsync();
            else
                await engine.PauseAllAsync(reason.Value);
        };

        // Named-pipe CLI/API with token auth (SECURITY.md §4).
        var settings = Host.Services.GetRequiredService<JsonSettingsService<BackdropSettings>>().Current;
        var pipe = new PipeServer("backdrop-cli", settings.CliToken, cmd =>
        {
            if (cmd.Action == "pause") { perf.UserPaused = true; return Task.FromResult("{\"ok\":true}"); }
            if (cmd.Action == "resume") { perf.UserPaused = false; return Task.FromResult("{\"ok\":true}"); }
            return Task.FromResult("{\"ok\":false,\"error\":\"unknown action\"}");
        });
        pipe.Start();
        Trace(" pipe started");

        Trace(" creating MainWindow");
        MainWindow = new MainWindow();
        Trace(" activating MainWindow");
        MainWindow.Activate();
        Trace(" activated; showing tray");
        Host.Services.GetRequiredService<Services.TrayService>().Show();
        Trace(" running; host starting");

        _ = Host.StartAsync();
        _ = RestoreAssignmentsAsync(); // Phase 2: Apply survives restart.
        Trace(" OnLaunched exit");
    }

    // Re-apply persisted per-monitor assignments after restart (ARCHITECTURE.md §4).
    private static async Task RestoreAssignmentsAsync()
    {
        try
        {
            await Task.Delay(1500); // let settings/catalog settle
            var svc = Host.Services.GetRequiredService<JsonSettingsService<BackdropSettings>>();
            var catalog = Host.Services.GetRequiredService<IWallpaperCatalog>();
            var engine = Host.Services.GetRequiredService<IWallpaperEngine>();
            foreach (var (monitor, wallId) in svc.Current.MonitorAssignments.ToList())
            {
                var rec = await catalog.GetByIdAsync(wallId);
                if (rec.IsSuccess && rec.Value is not null)
                    await engine.ApplyAsync(rec.Value, monitor, svc.Current.SpawnMode);
                else
                    svc.Current.MonitorAssignments.Remove(monitor); // stale entry
            }
            await svc.SaveAsync();
        }
        catch { /* best effort: app stays usable with empty Library */ }
    }
}
