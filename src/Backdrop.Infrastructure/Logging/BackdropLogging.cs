// Backdrop.Infrastructure — file logging (ARCHITECTURE.md §4: Logs\backdrop-*.log, 10MB roll).
using Microsoft.Extensions.Logging;
using Serilog;

namespace Backdrop.Infrastructure.Logging;

public static class BackdropLogging
{
    public static ILoggerFactory CreateFactory(string logsDir)
    {
        Directory.CreateDirectory(logsDir);
        var serilog = new LoggerConfiguration()
            .MinimumLevel.Information()
            .WriteTo.File(Path.Combine(logsDir, "backdrop-.log"),
                rollingInterval: RollingInterval.Day,
                fileSizeLimitBytes: 10 * 1024 * 1024, // 10 MB roll
                retainedFileCountLimit: 7,
                rollOnFileSizeLimit: true)
            .CreateLogger();
        return LoggerFactory.Create(b => b.AddSerilog(serilog));
    }
}
