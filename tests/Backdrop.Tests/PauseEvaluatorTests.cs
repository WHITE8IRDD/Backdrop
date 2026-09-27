// Backdrop.Tests — pause rules (PERFORMANCE.md §2). Every row in the spec table has a test.
using Backdrop.Core;
using Backdrop.Core.Performance;
using Backdrop.Core.Settings;
using FluentAssertions;

namespace Backdrop.Tests;

public class PauseEvaluatorTests
{
    private static PauseRules Defaults() => new();

    [Fact]
    public void NoTriggers_Resumes()
    {
        PauseEvaluator.Evaluate(new PauseConditions(false, false, false, false, false, false, false), Defaults())
            .Should().BeNull();
    }

    [Fact]
    public void Fullscreen_Pauses_ByDefault()
    {
        PauseEvaluator.Evaluate(new PauseConditions(true, false, false, false, false, false, false), Defaults())
            .Should().Be(PauseReason.FullscreenApp);
    }

    [Fact]
    public void Maximized_Off_ByDefault()
    {
        PauseEvaluator.Evaluate(new PauseConditions(false, true, false, false, false, false, false), Defaults())
            .Should().BeNull();
    }

    [Fact]
    public void Maximized_Pauses_WhenOptedIn()
    {
        var rules = Defaults();
        rules.PauseOnMaximized = true;
        PauseEvaluator.Evaluate(new PauseConditions(false, true, false, false, false, false, false), rules)
            .Should().Be(PauseReason.MaximizedApp);
    }

    [Fact]
    public void BatterySaver_Pauses()
    {
        PauseEvaluator.Evaluate(new PauseConditions(false, false, true, false, false, false, false), Defaults())
            .Should().Be(PauseReason.BatterySaver);
    }

    [Fact]
    public void Rdp_Pauses()
    {
        PauseEvaluator.Evaluate(new PauseConditions(false, false, false, true, false, false, false), Defaults())
            .Should().Be(PauseReason.RemoteDesktop);
    }

    [Fact]
    public void Lock_Pauses()
    {
        PauseEvaluator.Evaluate(new PauseConditions(false, false, false, false, true, false, false), Defaults())
            .Should().Be(PauseReason.LockScreen);
    }

    [Fact]
    public void DisplayOff_Pauses()
    {
        PauseEvaluator.Evaluate(new PauseConditions(false, false, false, false, false, true, false), Defaults())
            .Should().Be(PauseReason.DisplayOff);
    }

    [Fact]
    public void UserPause_Wins_Over_All()
    {
        PauseEvaluator.Evaluate(new PauseConditions(true, true, true, true, true, true, true), Defaults())
            .Should().Be(PauseReason.User);
    }
}
