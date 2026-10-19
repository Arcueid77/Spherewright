using Spherewright.Bridge.Core.Safety;
using Xunit;

namespace Spherewright.Bridge.Core.Tests;

public sealed class VoyagerLocalActionBudgetPolicyTests
{
    private static readonly DateTimeOffset Now = new DateTimeOffset(2025, 1, 1, 0, 0, 0, TimeSpan.Zero);

    [Fact]
    public void InstantConfigurationPreservesLockedFloorWithoutRequiringDepartureCoreRecharge()
    {
        var f = Good();
        f.WorkGameTicks = 1;
        f.AvailableLoadedEnergyJoules = 5_400_000_000d;
        Assert.True(VoyagerLocalActionBudgetPolicy.Evaluate(f).Proved);
        // Deliberately no core minimum in the local policy: native charging can
        // continue locally, while the unchanged flight prepare still gates core.
    }

    [Fact]
    public void FullWorkAndTailAreReservedInBothGameAndWallTime()
    {
        var f = Good();
        f.MaximumGameTicks = f.SpentGameTicks + f.CurrentGameTick - f.LastLandedGameTick!.Value
            + f.WorkGameTicks + f.TailGameTicks + f.ReturnReserveGameTicks;
        Assert.True(VoyagerLocalActionBudgetPolicy.Evaluate(f).Proved);
        f.MaximumGameTicks--;
        Assert.False(VoyagerLocalActionBudgetPolicy.Evaluate(f).Proved);
        f = Good();
        f.JourneyDeadlineUtc = Now.AddSeconds((f.WorkGameTicks + f.TailGameTicks + f.ReturnReserveGameTicks) / 60d);
        Assert.False(VoyagerLocalActionBudgetPolicy.Evaluate(f).Proved);
    }

    [Theory]
    [InlineData("binding")]
    [InlineData("idle")]
    [InlineData("landed_unknown")]
    [InlineData("time_regressed")]
    [InlineData("negative_spent")]
    [InlineData("missing_warper")]
    [InlineData("consumed_extra")]
    [InlineData("expired")]
    [InlineData("loaded_unknown")]
    [InlineData("floor_unknown")]
    [InlineData("power_unknown")]
    [InlineData("power_nan")]
    [InlineData("power_infinite")]
    [InlineData("tail_negative")]
    [InlineData("work_excess")]
    [InlineData("energy_overflow")]
    public void MissingOrChangedProofFailsClosed(string change)
    {
        var f = Good();
        switch (change)
        {
            case "binding": f.ExactRemoteBinding = false; break;
            case "idle": f.NativePlayerIdle = false; break;
            case "landed_unknown": f.LastLandedGameTick = null; break;
            case "time_regressed": f.CurrentGameTick = 99; break;
            case "negative_spent": f.SpentGameTicks = -1; break;
            case "missing_warper": f.NativeWarperCount = null; break;
            case "consumed_extra": f.NativeWarperCount = 1; break;
            case "expired": f.JourneyDeadlineUtc = Now; break;
            case "loaded_unknown": f.AvailableLoadedEnergyJoules = null; break;
            case "floor_unknown": f.LockedReturnFloorJoules = null; break;
            case "power_unknown": f.WorkPowerUpperWatts = null; break;
            case "power_nan": f.WorkPowerUpperWatts = double.NaN; break;
            case "power_infinite": f.WorkPowerUpperWatts = double.PositiveInfinity; break;
            case "tail_negative": f.TailAndNextTickEnergyJoules = -1; break;
            case "work_excess": f.WorkGameTicks = 7201; break;
            case "energy_overflow": f.WorkPowerUpperWatts = double.MaxValue; break;
        }
        var result = VoyagerLocalActionBudgetPolicy.Evaluate(f);
        Assert.False(result.Proved);
        Assert.False(string.IsNullOrEmpty(result.BlockerCode));
    }

    [Fact]
    public void AnExactFloorCannotBeSpentAndTailCannotBorrowReturnFuel()
    {
        var f = Good();
        f.WorkPowerUpperWatts = 60d;
        f.WorkGameTicks = 1;
        f.TailAndNextTickEnergyJoules = 100d;
        f.AvailableLoadedEnergyJoules = f.LockedReturnFloorJoules + 101d;
        Assert.False(VoyagerLocalActionBudgetPolicy.Evaluate(f).Proved);
        f.AvailableLoadedEnergyJoules++;
        Assert.True(VoyagerLocalActionBudgetPolicy.Evaluate(f).Proved);
    }

    [Fact]
    public void GameTickAdditionCannotWrapIntoAnApparentNewAllowance()
    {
        var f = Good();
        f.SpentGameTicks = long.MaxValue;
        Assert.False(VoyagerLocalActionBudgetPolicy.Evaluate(f).Proved);
    }

    private static VoyagerLocalActionBudgetFacts Good() => new VoyagerLocalActionBudgetFacts
    {
        ExactRemoteBinding = true, NativePlayerIdle = true,
        CurrentGameTick = 200, LastLandedGameTick = 100, SpentGameTicks = 12000,
        MaximumGameTicks = 108000, ReturnReserveGameTicks = 54000, WorkGameTicks = 7200, TailGameTicks = 600,
        NowUtc = Now, JourneyDeadlineUtc = Now.AddHours(1),
        AvailableLoadedEnergyJoules = 19_000_000_000d, LockedReturnFloorJoules = 5_300_000_000d,
        WorkPowerUpperWatts = 2_000_000d, TailAndNextTickEnergyJoules = 10_000_000d,
        NativeWarperCount = 2, OriginalExtraWarperReserve = 1,
    };
}
