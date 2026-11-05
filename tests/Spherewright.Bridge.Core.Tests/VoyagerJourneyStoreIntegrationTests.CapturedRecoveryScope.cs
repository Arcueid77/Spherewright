using System.Text.Json;
using Spherewright.Bridge.Core.Safety;
using Xunit;

namespace Spherewright.Bridge.Core.Tests;

public sealed partial class VoyagerJourneyStoreIntegrationTests
{
    [Fact]
    public void RealRecoveredHomeThenNewLandingReplaysThroughProductionScopeAndChargeBudget()
    {
        using var data = JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory,
            "Fixtures", "recovered-home-next-voyage-charge.json")));
        var cut = data.RootElement;
        Assert.True(cut.GetProperty("remotePrimaryRecovery").GetBoolean());
        Assert.True(cut.GetProperty("sameLoadedSession").GetBoolean());
        Assert.True(cut.GetProperty("differentJourney").GetBoolean());
        Assert.True(cut.GetProperty("priorReturnSucceeded").GetBoolean());
        Assert.False(cut.GetProperty("firstLandingProved").GetBoolean());
        Assert.False(cut.GetProperty("independentDebitsExcluded").GetBoolean());
        Assert.True(cut.GetProperty("independentDebitsBudgeted").GetBoolean());
        Assert.False(cut.GetProperty("physicalIdleAfterPauseProved").GetBoolean());
        Assert.Equal("energy_and_journey", cut.GetProperty("firstLandingCaptureStage").GetString());
        Assert.Equal("remote_idle_core_stop_reserve_insufficient", cut.GetProperty("firstLandingBlocker").GetString());
        double Number(string key) => cut.GetProperty(key).GetDouble();
        long Tick(string key) => cut.GetProperty(key).GetInt64();
        var spent = Tick("projectedSpentGameTicks");
        var now = new DateTimeOffset(2025, 1, 1, 0, 0, 0, TimeSpan.Zero);
        using var f = new Fixture(maximumRoundTripWallSeconds: 43200);
        using var old = BindAccountingRecovery(f, out _);
        var next = Copy(old.Journey, r => { r.JourneyId = Guid.NewGuid().ToString("N");
            r.MaximumGameTicks = Tick("maximumGameTicks"); r.ReturnReserveGameTicks = Tick("returnReserveGameTicks");
            r.IssuedAtUtc = now; r.DeadlineUtc = now.AddSeconds(Tick("explicitWallSeconds")); });
        var budget = new VoyagerLocalActionBudgetFacts
        {
            // Capture reaches energy_and_journey only AFTER the production
            // reader proves binding, actual Walk/zero tails and idle drones.
            // The later paused preservation is explicitly NOT idle evidence.
            ExactRemoteBinding = true, NativePlayerIdle = true,
            CurrentGameTick = spent, LastLandedGameTick = spent,
            SpentGameTicks = f.Sessions.EffectiveVoyagerSpentGameTicks(next, spent) ?? -1,
            MaximumGameTicks = f.Sessions.EffectiveVoyagerMaximumGameTicks(next),
            ReturnReserveGameTicks = next.ReturnReserveGameTicks, WorkGameTicks = 1,
            TailGameTicks = VoyagerLocalWalkReservePolicy.StopGameTicks,
            NowUtc = now, JourneyDeadlineUtc = f.Sessions.EffectiveVoyagerDeadline(next),
            AvailableLoadedEnergyJoules = Number("loadedEnergyJoules"), LockedReturnFloorJoules = Number("returnEnergyFloorJoules"),
            WorkPowerUpperWatts = Number("perNativeTickEnergyUpperJoules") * 60,
            TailAndNextTickEnergyJoules = Number("perNativeTickEnergyUpperJoules") * VoyagerLocalWalkReservePolicy.StopGameTicks,
            NativeWarperCount = cut.GetProperty("nativeWarperCount").GetInt32(), OriginalExtraWarperReserve = 1,
        };
        Assert.Equal(spent, budget.SpentGameTicks);
        Assert.Null(VoyagerRecoveryWindowPolicy.ChargeBudgetBlocker(budget, now, next.DeadlineUtc));
        Assert.True(VoyagerLocalActionBudgetPolicy.Evaluate(budget).Proved);
        // Budget repair does not award core energy or admit the captured low
        // core as idle; Native must still perform its bounded recharge.
        Assert.True(Number("coreEnergyJoules") < budget.TailAndNextTickEnergyJoules);
        budget.SpentGameTicks = -1; // Exact old session-wide debit symptom.
        var evidence = new NativeResumeDecisionEvidence("captured_landing_budget", spent, now, true);
        var blocker = VoyagerRecoveryWindowPolicy.ChargeBudgetBlocker(budget, now, next.DeadlineUtc, evidence);
        Assert.Equal("remote_resume_charge_original_budget_unproved", blocker);
        Assert.Equal("charge_spent_valid", evidence.Failure(blocker!).FirstFailedPredicate);
    }
}
