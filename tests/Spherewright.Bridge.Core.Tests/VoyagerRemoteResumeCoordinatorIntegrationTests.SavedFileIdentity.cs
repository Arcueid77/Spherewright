using Spherewright.Bridge.Core.Safety;
using Spherewright.Contracts.Sessions;
using Spherewright.Plugin.Game;
using Spherewright.Plugin.RuntimeDescriptor;
using Xunit;

namespace Spherewright.Bridge.Core.Tests;

public sealed partial class VoyagerRemoteResumeCoordinatorIntegrationTests
{
    [Fact]
    public void ExplicitLossCanUseTheLatestCompleteExitBelowTheCredentialObservedProgressWithoutUsingOlderPrimary()
    {
        using var s=new Scenario(expiredJourney:true,currentNative:true);
        var r=s.PrimaryRecoveryRequest(18);
        s.ReplaceCurrentTicket(t=>t.MinimumGameTick=12532);s.ReopenPrimaryCredentials();
        r.AcceptUnstoredProgressLoss=true;r.MinimumRecoveryGameTick=12532;r.ExpectedRecoveryGameTick=12528;
        var p=s.Prepare(r);Assert.Equal(12528,p.CandidateGameTick);Assert.Equal(4,p.DiscardedUnstoredProgressGameTicks);
        var accepted=s.Commit(p);Assert.True(accepted.Success,accepted.Error?.Message);Assert.Equal(GameSave.LastExit,DSPGame.LoadedName);
    }
    [Fact]
    public void ExplicitApprovedLossUsesTheLatestCompleteExitAndChargesTheDisclosedMissingProgress()
    {
        using var s=new Scenario(expiredJourney:true,currentNative:true);
        var r=s.PrimaryRecoveryRequest(18);r.AcceptUnstoredProgressLoss=true;
        r.MinimumRecoveryGameTick=12532;r.ExpectedRecoveryGameTick=12528;
        var p=s.Prepare(r);Assert.Equal(12528,p.CandidateGameTick);
        Assert.Equal(4,p.DiscardedUnstoredProgressGameTicks);Assert.Equal(4,p.ConservedObservedProgressDebitGameTicks);
        var committed=s.Commit(p);Assert.True(committed.Success,committed.Error?.Message);Assert.Equal(GameSave.LastExit,DSPGame.LoadedName);
    }
    [Fact]
    public void FullyLoadedFailedPausedPrimaryReleasesTheRealExitFileLeaseWithoutAdmittingWork()
    {
        using var s=new Scenario(expiredJourney:true,currentNative:true);
        var r=s.PrimaryRecoveryRequest(18);r.AcceptUnstoredProgressLoss=false;r.MinimumRecoveryGameTick=r.ExpectedRecoveryGameTick=null;
        var p=s.Prepare(r);Assert.True(s.Commit(p).Success);s.Sessions.BindFailedPrimaryExitForTest();
        Assert.Throws<IOException>(()=>{using var file=new FileStream(GameSave.SavePath(GameSave.LastExit),FileMode.Open,FileAccess.ReadWrite,FileShare.None);});
        s.Sessions.ReleaseFailedPrimaryExitForTest();
        using(var file=new FileStream(GameSave.SavePath(GameSave.LastExit),FileMode.Open,FileAccess.ReadWrite,FileShare.None))Assert.True(file.CanWrite);
        Assert.True(s.Sessions.FailedExitStillQuarantinedForTest);Assert.False(s.Sessions.ContactAdmittedForTest);
    }
    [Theory]
    [InlineData("loading")] [InlineData("journal")] [InlineData("unpaused")] [InlineData("other_world")] [InlineData("other_action")]
    public void IncompleteOrChangedFailedLoadKeepsItsRealSourceLease(string fault)
    {
        using var s=new Scenario(expiredJourney:true,currentNative:true);
        var r=s.PrimaryRecoveryRequest(18);r.AcceptUnstoredProgressLoss=false;r.MinimumRecoveryGameTick=r.ExpectedRecoveryGameTick=null;
        var p=s.Prepare(r);Assert.True(s.Commit(p).Success);s.Sessions.BindFailedPrimaryExitForTest(fault);
        s.Sessions.ReleaseFailedPrimaryExitForTest();
        Assert.Throws<IOException>(()=>{using var file=new FileStream(GameSave.SavePath(GameSave.LastExit),FileMode.Open,FileAccess.ReadWrite,FileShare.None);});
        Assert.False(s.Sessions.ContactAdmittedForTest);
    }
    [Fact]
    public void ExplicitExactNewerNormalExitKeepsNewProgressAndNeverReplaysTheConsumedResume()
    {
        using var s=new Scenario(expiredJourney:true,currentNative:true);
        var r=s.PrimaryRecoveryRequest(18);r.AcceptUnstoredProgressLoss=false;r.MinimumRecoveryGameTick=r.ExpectedRecoveryGameTick=null;
        var first=s.Prepare(r);s.Sessions.FailBeforeRemoteLeaseHandoffForTest=true;Assert.False(s.Commit(first).Success);
        s.Sessions.FailBeforeRemoteLeaseHandoffForTest=false;
        var originals=Directory.GetFiles(s.Runtime,"owned-world-*").Concat(Directory.GetFiles(s.Handoff,"owned-world-*")).ToDictionary(p=>p,File.ReadAllBytes);
        s.WriteSave(GameSave.LastExit,12529);
        using(var file=new FileStream(GameSave.SavePath(GameSave.LastExit),FileMode.Open,FileAccess.Write))
        {file.Position=44;using var writer=new BinaryWriter(file);writer.Write(DateTime.UtcNow.Ticks);}
        r.MinimumRecoveryGameTick=r.ExpectedRecoveryGameTick=12529;
        var next=s.Prepare(r);Assert.Equal(12529,next.CandidateGameTick);Assert.Equal(12529,next.MinimumGameTick);
        Assert.Equal(0,next.DiscardedUnstoredProgressGameTicks);Assert.Equal(0,DSPGame.LoadCalls);
        Assert.True(s.Commit(next).Success);Assert.Equal(GameSave.LastExit,DSPGame.LoadedName);
        foreach(var pair in originals)Assert.Equal(pair.Value,File.ReadAllBytes(pair.Key));
    }
    [Fact]
    public void ExplicitNewerIdentityStillRejectsAnOldExitEvenWhenTheTickMatches()
    {
        using var s=new Scenario(expiredJourney:true,currentNative:true);
        var r=s.PrimaryRecoveryRequest(18);r.AcceptUnstoredProgressLoss=false;r.MinimumRecoveryGameTick=r.ExpectedRecoveryGameTick=null;
        var first=s.Prepare(r);s.Sessions.FailBeforeRemoteLeaseHandoffForTest=true;Assert.False(s.Commit(first).Success);
        s.Sessions.FailBeforeRemoteLeaseHandoffForTest=false;s.WriteSave(GameSave.LastExit,12529);
        Assert.False(s.Tickets.TryGetFreshRemoteFileAccessProvenance(s.Token,out _,out _,out _,12529));
        Assert.Equal(0,DSPGame.LoadCalls);
    }
    [Fact]
    public void FailedPreDispatchConsumptionCanDeriveFileIdentityWithoutRevivingItsExecution()
    {
        using var s = new Scenario(expiredJourney: true, currentNative: true);
        var r = s.PrimaryRecoveryRequest(18); r.AcceptUnstoredProgressLoss = false;
        r.MinimumRecoveryGameTick = r.ExpectedRecoveryGameTick = null;
        var first = s.Prepare(r); var parent = s.Token;
        s.Sessions.FailBeforeRemoteLeaseHandoffForTest = true;
        Assert.False(s.Commit(first).Success); Assert.Equal(0, DSPGame.LoadCalls);
        s.Sessions.FailBeforeRemoteLeaseHandoffForTest = false;
        Assert.False(s.Tickets.TryGetSavedWorldOpenProvenance(parent, out _, out _, out _));
        var originals = Directory.GetFiles(s.Runtime, "owned-world-*").Concat(Directory.GetFiles(s.Handoff, "owned-world-*"))
            .ToDictionary(path => path, File.ReadAllBytes);
        var next = s.Prepare(r);
        Assert.True(VoyagerRemotePrimaryRecoveryPolicy.HasMatchingEcho(r, next));
        Assert.Equal(12528, next.CandidateGameTick); Assert.Equal(0, next.DiscardedUnstoredProgressGameTicks);
        Assert.True(s.Tickets.TryGetFreshRemoteFileAccessProvenance(parent, out var fileAccess, out _, out _));
        Assert.NotEqual(parent, fileAccess!.ResumeToken); Assert.True(fileAccess.SavedWorldOpenOnly);
        Assert.Equal(12528, fileAccess.ObservedProgressGameTickFloor);
        Assert.False(s.Tickets.TryGetSavedWorldOpenProvenance(parent, out _, out _, out _));
        foreach (var pair in originals) Assert.Equal(pair.Value, File.ReadAllBytes(pair.Key));
        Assert.True(s.Commit(next).Success); Assert.Equal(GameSave.LastExit, DSPGame.LoadedName);
        Assert.False(s.Tickets.TryGetFreshRemoteFileAccessProvenance(parent, out _, out _, out _));
        foreach (var pair in originals) Assert.Equal(pair.Value, File.ReadAllBytes(pair.Key));
    }

    [Theory]
    [InlineData("other_exit")] [InlineData("older_exit")] [InlineData("newer_primary")]
    [InlineData("changed_attempt")] [InlineData("missing_tombstone")]
    [InlineData("changed_journal")]
    public void SavedFileIdentityDerivationRejectsOtherBoundariesOrIncompleteOriginalProof(string fault)
    {
        using var s = new Scenario(expiredJourney: true, currentNative: true);
        var r = s.PrimaryRecoveryRequest(18); r.AcceptUnstoredProgressLoss = false;
        r.MinimumRecoveryGameTick = r.ExpectedRecoveryGameTick = null;
        var plan = s.Prepare(r);
        s.Sessions.FailBeforeRemoteLeaseHandoffForTest = true;
        Assert.False(s.Commit(plan).Success); s.Sessions.FailBeforeRemoteLeaseHandoffForTest = false;
        switch (fault)
        {
            case "other_exit": s.WriteSave(GameSave.LastExit, 12529); break;
            case "older_exit": s.WriteSave(GameSave.LastExit, 12527); break;
            case "newer_primary": s.WriteSave(Scenario.Identity, 12529); break;
            case "changed_attempt": File.AppendAllText(Directory.GetFiles(s.Runtime,"owned-world-reauthorization-attempt-*.json").Single(), " "); break;
            case "missing_tombstone": File.Delete(Directory.GetFiles(s.Runtime,"owned-world-resume-consumed-*.json").Single()); break;
            case "changed_journal": File.AppendAllText(s.JournalPath, "broken-synthetic-journal"); break;
        }
        Assert.False(s.Tickets.TryGetFreshRemoteFileAccessProvenance(s.Token,out _,out _,out _));
        Assert.Equal(0,DSPGame.LoadCalls);
    }
}
