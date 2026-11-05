using Spherewright.Plugin.RuntimeDescriptor;
using Spherewright.Plugin.Transport;
using Xunit;

namespace Spherewright.Bridge.Core.Tests;
public sealed partial class VoyagerJourneyStoreIntegrationTests
{
    [Fact]
    public void SavedSourceContinuationKeepsItsParentImmutableAndDebitsAllElapsedTicksAgainBeforeNewControl()
    {
        using var f=new Fixture();var j=f.Store.VoyagerJourney!;var tick=Math.Max(j.StartedAtGameTick+40,j.LastAccountedGameTick+1);
        var parent=new StoppedSpaceReturnRecord {ActionId=Guid.NewGuid().ToString(),SessionId="original-session",
            SourceFailedActionId=j.LastAccountedFlightActionId,OriginalLedger=PluginJson.Serialize(j),SourceTick=tick,
            PriorSpent=Math.Max(j.SpentGameTicks,tick-j.StartedAtGameTick),MaximumTicks=j.ReturnReserveGameTicks,
            DeadlineUtc=DateTimeOffset.UtcNow.AddHours(4)};
        f.Store.RecordStoppedSpaceReturnStart(parent);var original=PluginJson.Serialize(parent);
        Assert.False(f.Store.TryBindSavedHomeReturnPredecessor(parent.ActionId,tick+2101,false,out _,out _,out _));
        Assert.True(f.Store.TryBindSavedHomeReturnPredecessor(parent.ActionId,tick+2101,true,out var bound,out var left,out var error),error);
        Assert.Equal(parent.MaximumTicks-2101,left);Assert.Equal(original,PluginJson.Serialize(bound));
        var next=new StoppedSpaceReturnRecord {ActionId=Guid.NewGuid().ToString(),SessionId="new-session",
            SourceFailedActionId=parent.SourceFailedActionId,OriginalLedger=parent.OriginalLedger,SourceTick=tick+2101,
            PriorSpent=Math.Max(j.SpentGameTicks,tick+2101-j.StartedAtGameTick),MaximumTicks=left+1,
            DeadlineUtc=DateTimeOffset.UtcNow.AddHours(4),PredecessorActionId=parent.ActionId,PredecessorKnownTerminalFailed=true,
            PredecessorFingerprint=Spherewright.Bridge.Core.Safety.CanonicalStateHash.Combine(original)};
        Assert.Throws<InvalidOperationException>(()=>f.Store.RecordStoppedSpaceReturnStart(next));
        next.MaximumTicks=left;f.Store.RecordStoppedSpaceReturnStart(next);
        Assert.True(f.Store.TryBindSavedHomeReturnPredecessor(next.ActionId,tick+2201,true,out _,out var later,out error),error);
        Assert.Equal(parent.MaximumTicks-2201,later);
        Assert.Equal(parent.OriginalLedger,PluginJson.Serialize(f.Store.VoyagerJourney));
    }

    [Theory]
    [InlineData("prior-spent")][InlineData("expired")][InlineData("unbounded-wall")][InlineData("ticks")]
    public void SpaceReturnAdmissionRejectsChangedAccountingOrNonfiniteScopeBeforeDurableWrite(string fault)
    {
        using var f=new Fixture();var j=f.Store.VoyagerJourney!;
        var tick=Math.Max(j.StartedAtGameTick+40,j.LastAccountedGameTick+1);
        var record=new StoppedSpaceReturnRecord {ActionId=Guid.NewGuid().ToString(),SessionId="synthetic-session",
            SourceFailedActionId=j.LastAccountedFlightActionId,OriginalLedger=PluginJson.Serialize(j),SourceTick=tick,
            PriorSpent=Math.Max(j.SpentGameTicks,tick-j.StartedAtGameTick),MaximumTicks=j.ReturnReserveGameTicks,
            DeadlineUtc=DateTimeOffset.UtcNow.AddHours(4)};
        switch(fault){case "prior-spent":record.PriorSpent++;break;
            case "expired":record.DeadlineUtc=DateTimeOffset.UtcNow.AddSeconds(-1);break;
            case "unbounded-wall":record.DeadlineUtc=DateTimeOffset.UtcNow.AddHours(7);break;
            case "ticks":record.MaximumTicks++;break;}
        Assert.Throws<InvalidOperationException>(()=>f.Store.RecordStoppedSpaceReturnStart(record));
        Assert.Equal(record.OriginalLedger,PluginJson.Serialize(f.Store.VoyagerJourney));
    }

    [Fact]
    public void CurrentSpaceReturnClosesOnlyAfterCoveringNormalHomeSaveWithoutRewritingTheFailedCheckpoint()
    {
        using var f=new Fixture();var j=f.Store.VoyagerJourney!;var checkpoint=File.ReadAllBytes(f.TicketPath);
        var sourceTick=Math.Max(j.StartedAtGameTick+40,j.LastAccountedGameTick+1);
        var record=new StoppedSpaceReturnRecord {ActionId=Guid.NewGuid().ToString(),SessionId="synthetic-new-session",
            SourceFailedActionId=j.LastAccountedFlightActionId,OriginalLedger=PluginJson.Serialize(j),SourceTick=sourceTick,
            PriorSpent=Math.Max(j.SpentGameTicks,sourceTick-j.StartedAtGameTick),MaximumTicks=j.ReturnReserveGameTicks,
            DeadlineUtc=DateTimeOffset.UtcNow.AddHours(4)};
        f.Store.RecordStoppedSpaceReturnStart(record);
        Assert.False(f.Store.CompleteStoppedSpaceHomeReturn(record,sourceTick+10,f.PrimaryPath,out _));
        Fixture.WriteSave(Fixture.Identity,sourceTick+10);
        Assert.True(f.Store.CompleteStoppedSpaceHomeReturn(record,sourceTick+10,f.PrimaryPath,out var error),error);
        Assert.Equal(checkpoint,File.ReadAllBytes(f.TicketPath));
        var done=f.ReopenStore().VoyagerJourney!;Assert.Equal("recovered_closed",done.Phase);
        Assert.Equal(record.PriorSpent+10,done.SpentGameTicks);Assert.Equal(j.ReturnReserveGameTicks,done.ReturnReserveGameTicks);
        Assert.Equal(j.DeadlineUtc,done.DeadlineUtc);Assert.Equal(j.LastAccountedFlightActionId,done.LastAccountedFlightActionId);
        var retired=PluginJson.Deserialize<FlightCheckpointTicket>(File.ReadAllText(f.TicketPath))!;
        Assert.Equal(j.LastAccountedFlightActionId,retired.LastAttemptActionId);
        Assert.Equal(FlightCheckpointLifecycleStates.Retired,retired.LifecycleState);
        Assert.False(f.Store.CompleteStoppedSpaceHomeReturn(record,sourceTick+10,f.PrimaryPath,out _));
    }
}
