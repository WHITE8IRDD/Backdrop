// Backdrop.Infrastructure — startup (registry Run key; MSIX uses StartupTask instead — INSTALLER.md §2).
using Backdrop.Core;
using Microsoft.Win32;

namespace Backdrop.Infrastructure.Startup;

public sealed class StartupService : IStartupService
{
    private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "Backdrop";

    public bool IsEnabled()
    {
        using var key = Registry.CurrentUser.OpenSubKey(RunKey, false);
        return key?.GetValue(ValueName) is not null;
    }

    public Result SetEnabled(bool enabled)
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKey, writable: true)
                ?? Registry.CurrentUser.CreateSubKey(RunKey);
            if (key is null)
                return Result.Fail("Cannot open Run key.");
            if (enabled)
            {
                var exe = Environment.ProcessPath ?? string.Empty;
                key.SetValue(ValueName, $"\"{exe}\" --minimized", RegistryValueKind.String);
            }
            else
            {
                key.DeleteValue(ValueName, throwOnMissingValue: false);
            }
            return Result.Ok();
        }
        catch (Exception ex)
        {
            return Result.Fail(ex.Message);
        }
    }
}
