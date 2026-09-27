// Backdrop.Core — settings model (PERFORMANCE.md §2, UI spec Performance/Settings pages).
namespace Backdrop.Core.Settings;

public sealed class PauseRules
{
    public bool PauseOnFullscreen { get; set; } = true;
    public bool PauseOnMaximized { get; set; } = false; // optional, off by default
    public bool PauseOnBatterySaver { get; set; } = true;
    public bool PauseOnRemoteDesktop { get; set; } = true;
    public bool PauseOnLockScreen { get; set; } = true;
    public bool PauseOnDisplayOff { get; set; } = true;
}

public sealed class BackdropSettings
{
    public int Version { get; set; } = 1;
    public bool RunAtStartup { get; set; } = false;
    public bool Mute { get; set; } = true;
    public double Volume { get; set; } = 0.5;
    public bool Loop { get; set; } = true;
    public FillMode Fill { get; set; } = FillMode.Fill;
    public int FpsLimit { get; set; } = 60; // 30/60/unlimited(0)
    public bool HardwareAcceleration { get; set; } = true;
    public long MaxImportBytes { get; set; } = 500L * 1024 * 1024; // 500 MB default (SECURITY.md)
    public PauseRules PauseRules { get; set; } = new();
    public Dictionary<string, Guid> MonitorAssignments { get; set; } = new(); // DeviceId -> WallpaperId
    public SpawnMode SpawnMode { get; set; } = SpawnMode.PerMonitor;
    public string CliToken { get; set; } = Guid.NewGuid().ToString("N"); // pipe auth (SECURITY.md §4)
}
