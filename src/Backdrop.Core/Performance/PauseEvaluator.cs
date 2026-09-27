// Backdrop.Core — pure pause evaluation (PERFORMANCE.md §2/§3). Fully unit-testable.
using Backdrop.Core.Settings;

namespace Backdrop.Core.Performance;

/// <summary>Snapshot of OS conditions; evaluation has no side effects.</summary>
public sealed record PauseConditions(
    bool FullscreenAppActive,
    bool MaximizedAppActive,
    bool BatterySaverOn,
    bool RemoteDesktopActive,
    bool SessionLocked,
    bool DisplayOff,
    bool UserPaused);

public static class PauseEvaluator
{
    /// <returns>First matching <see cref="PauseReason"/>, or null to resume.</returns>
    public static PauseReason? Evaluate(PauseConditions c, PauseRules rules)
    {
        if (c.UserPaused)
            return PauseReason.User;
        if (c.SessionLocked && rules.PauseOnLockScreen)
            return PauseReason.LockScreen;
        if (c.DisplayOff && rules.PauseOnDisplayOff)
            return PauseReason.DisplayOff;
        if (c.RemoteDesktopActive && rules.PauseOnRemoteDesktop)
            return PauseReason.RemoteDesktop;
        if (c.BatterySaverOn && rules.PauseOnBatterySaver)
            return PauseReason.BatterySaver;
        if (c.FullscreenAppActive && rules.PauseOnFullscreen)
            return PauseReason.FullscreenApp;
        if (c.MaximizedAppActive && rules.PauseOnMaximized)
            return PauseReason.MaximizedApp;
        return null;
    }
}
