using Spherewright.Bridge.Core.Safety;
using Spherewright.Contracts.Sessions;
using Spherewright.Plugin.Game;
using Xunit;
using System.Text.Json;

namespace Spherewright.Bridge.Core.Tests;
public sealed partial class OwnedWorldAutosaveCoordinatorIntegrationTests
{
    [Fact]
    public void LatestLastExitOpensPausedWithoutSelectingOlderPrimaryOrResumingTasks()
    {
        using var f=new Fixture(OwnedWorldResumeModes.OpenSavedPrimary);
        using var captured=JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory,"Fixtures","saved-open-predispatch-refusal.json")));
        Assert.Equal("ACTION_FAILED",captured.RootElement.GetProperty("error").GetProperty("code").GetString());
        Assert.False(captured.RootElement.GetProperty("session").GetProperty("gameLoaded").GetBoolean());
        Assert.Equal(JsonValueKind.Null,captured.RootElement.GetProperty("session").GetProperty("gameTick").ValueKind);
        var last=Path.Combine(Path.GetDirectoryName(f.PrimaryPath)!,"_lastexit_.dsv");
        Fixture.WriteSave(last,Identity,CandidateTick+100);
        var primary=File.ReadAllBytes(f.PrimaryPath);var exit=File.ReadAllBytes(last);
        var coordinator=f.NewCoordinator();var prepared=coordinator.PrepareOnMainThread(new PrepareOwnedWorldResumeRequest
        {ResumeToken=f.Store.CurrentResumeToken!,RecoveryMode=captured.RootElement.GetProperty("recoveryMode").GetString()!,
            MinimumRecoveryGameTick=CandidateTick+100,ExpectedRecoveryGameTick=CandidateTick+100});
        Assert.True(prepared.Success,prepared.Error?.Message);Assert.True(OwnedLastExitOpenPolicy.ConfirmsEcho(prepared.Value!));
        var accepted=coordinator.CommitOnMainThread(f.Confirmed(prepared.Value!));
        Assert.True(accepted.Success,accepted.Error?.Message);Assert.Equal("_lastexit_",DSPGame.LoadedName);
        Assert.True(f.Sessions.SavedOpenAnyNativeLocation);Assert.True(f.Sessions.SavedOpenObserverBound);
        Assert.Equal(primary,File.ReadAllBytes(f.PrimaryPath));Assert.Equal(exit,File.ReadAllBytes(last));
        // This is the actual production tracker lease guard, not a substitute
        // that silently accepts a new source kind the tracker would reject.
        var ticket=f.Sessions.SavedOpenPreviousTicket!;
        string? Check(bool exact,OwnedSaveRecoveryLease? other,long floor)=>SavedWorldOpenLeaseHandoff.Validate(ticket,
            f.Sessions.SourceLease,f.Sessions.JournalLease,other,OwnedWorldResumeSourceKind.LastExit,true,
            accepted.Value!.ActionId,floor,ticket.GameVersion,exact);
        Assert.Null(Check(true,f.Sessions.PrimaryLease,CandidateTick+100));
        Assert.Equal("saved_world_open_primary_role_mismatch",Check(false,f.Sessions.PrimaryLease,CandidateTick+100));
        Assert.NotNull(Check(true,null,CandidateTick+100));
        Assert.NotNull(Check(true,f.Sessions.PrimaryLease,CandidateTick+101));
    }

    [Fact]
    public void PreDispatchSavedOpenFailureRetainsOneOriginalTerminalHandleAndNeverCallsNativeLoad()
    {
        using var f=new Fixture(OwnedWorldResumeModes.OpenSavedPrimary);
        var last=Path.Combine(Path.GetDirectoryName(f.PrimaryPath)!,"_lastexit_.dsv");
        Fixture.WriteSave(last,Identity,CandidateTick+100);
        var coordinator=f.NewCoordinator();var originalToken=f.Store.CurrentResumeToken!;
        var prepared=coordinator.PrepareOnMainThread(new PrepareOwnedWorldResumeRequest {
            ResumeToken=originalToken,RecoveryMode=OwnedWorldResumeModes.OpenSavedLastExit,
            MinimumRecoveryGameTick=CandidateTick+100,ExpectedRecoveryGameTick=CandidateTick+100});
        Assert.True(prepared.Success,prepared.Error?.Message);
        var request=f.Confirmed(prepared.Value!);f.Sessions.FailBeforeRemoteLeaseHandoffForTest=true;
        var result=coordinator.CommitOnMainThread(request);
        Assert.True(result.Success,result.Error?.Message);Assert.Equal(0,DSPGame.LoadCalls);
        Assert.Equal("action_failed",result.Value!.State);
        Assert.True(coordinator.TryGetActionResultOnMainThread(result.Value.ActionId,out var terminal));
        Assert.True(terminal!.Terminal);Assert.False(terminal.Succeeded);
        Assert.Equal("saved_world_open_pre_dispatch",terminal.NativeFirstFailure!.Stage);
        Assert.Equal("false",terminal.NativeFirstFailure.Predicates["native_load_dispatch_entered"]);
        var same=coordinator.CommitOnMainThread(request);
        Assert.True(same.Success);Assert.True(same.Value!.IdempotentReplay);
        Assert.Equal(result.Value.ActionId,same.Value.ActionId);Assert.Equal(0,DSPGame.LoadCalls);
        var immutable=new[]{f.Runtime,f.Handoff}.SelectMany(Directory.GetFiles)
            .Where(p=>Path.GetFileName(p).StartsWith("owned-world-reauthorization-attempt-")
                || Path.GetFileName(p).StartsWith("owned-world-resume-consumed-"))
            .ToDictionary(p=>p,File.ReadAllBytes);
        Assert.False(f.Store.TryGetSavedWorldOpenProvenance(originalToken,out _,out _,out _));
        Assert.False(f.Store.TryGetConsumedCurrentSavedData(originalToken,CandidateTick+101,out _,out _,out _));
        var fresh=coordinator.PrepareOnMainThread(new PrepareOwnedWorldResumeRequest {
            ResumeToken=originalToken,RecoveryMode=OwnedWorldResumeModes.OpenSavedLastExit,
            MinimumRecoveryGameTick=CandidateTick+100,ExpectedRecoveryGameTick=CandidateTick+100});
        Assert.True(fresh.Success,fresh.Error?.Message);Assert.Equal(0,DSPGame.LoadCalls);
        Assert.NotEqual(originalToken,f.Store.CurrentResumeToken);
        Assert.False(f.Store.TryGetActiveTicket(f.Store.CurrentResumeToken!,out _,out _));
        Assert.True(f.Store.TryGetSavedWorldOpenProvenance(f.Store.CurrentResumeToken!,out var dataOnly,out _,out _));
        Assert.True(dataOnly!.SavedWorldOpenOnly);Assert.Equal(CandidateTick+100,dataOnly.MinimumGameTick);
        Assert.True(coordinator.TryGetActionResultOnMainThread(result.Value.ActionId,out var unchanged));
        Assert.Equal("action_failed",unchanged!.State);Assert.Equal(0,DSPGame.LoadCalls);
        foreach(var original in immutable)Assert.Equal(original.Value,File.ReadAllBytes(original.Key));
    }

    [Theory]
    [InlineData("replica")][InlineData("foreign-source")][InlineData("newer-primary")][InlineData("newer-generation")]
    public void ConsumedSavedDataRejectsChangedReplicaSourceOrNewerProgressWithoutReplacingAnyGeneration(string fault)
    {
        using var f=new Fixture(OwnedWorldResumeModes.OpenSavedPrimary);
        var last=Path.Combine(Path.GetDirectoryName(f.PrimaryPath)!,"_lastexit_.dsv");
        Fixture.WriteSave(last,Identity,CandidateTick+100);var token=f.Store.CurrentResumeToken!;
        Assert.True(f.Store.TryGetSavedWorldOpenProvenance(token,out var prior,out _,out _));
        var coordinator=f.NewCoordinator();var p=coordinator.PrepareOnMainThread(new PrepareOwnedWorldResumeRequest {
            ResumeToken=token,RecoveryMode=OwnedWorldResumeModes.OpenSavedLastExit,
            MinimumRecoveryGameTick=CandidateTick+100,ExpectedRecoveryGameTick=CandidateTick+100});
        Assert.True(p.Success,p.Error?.Message);f.Sessions.FailBeforeRemoteLeaseHandoffForTest=true;
        Assert.True(coordinator.CommitOnMainThread(f.Confirmed(p.Value!)).Success);
        switch(fault){case "replica":File.AppendAllText(Assert.Single(Directory.GetFiles(f.Handoff,"owned-world-resume-consumed-*.json"))," ");break;
            case "foreign-source":Fixture.WriteSave(last,Identity+"-foreign",CandidateTick+100);break;
            case "newer-primary":Fixture.WriteSave(f.PrimaryPath,Identity,CandidateTick+101);break;
            case "newer-generation":f.Store.ArmFromHealthySavedOwnedSession(Identity,"synthetic-newer",101,
                CandidateTick+200,prior!.GameplayJournalCheckpoint!);break;}
        var current=f.Store.CurrentResumeToken;
        Assert.False(f.Store.TryGetConsumedCurrentSavedData(token,CandidateTick+100,out _,out _,out _));
        Assert.Equal(current,f.Store.CurrentResumeToken);Assert.Equal(0,DSPGame.LoadCalls);
    }
}
