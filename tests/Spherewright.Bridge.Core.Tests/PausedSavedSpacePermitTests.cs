using Spherewright.Bridge.Core.Safety;
using Spherewright.Contracts.Actions;
using Spherewright.Contracts.Errors;
using Spherewright.Contracts.Sessions;
using Spherewright.Plugin.Game;
using Xunit;

namespace Spherewright.Bridge.Core.Tests
{
 public sealed class PausedSavedSpacePermitTests
 {
  [Fact]
  public void FrozenResidentSailPermitCannotBorrowAnotherPlanetOrBecomeGenericMovementPermission()
  {
   var s=Session();s.LocalPlanetId=101;
   var p=new PausedSavedSpacePermit(s.SessionId!,s.WriteQuarantineActionId!,"source-open-only",1000,101);
   Assert.True(p.Matches(s));s.LocalPlanetId=102;Assert.False(p.Matches(s));
   s.LocalPlanetId=null;Assert.False(p.Matches(s));
  }
  [Fact]
  public void ActualCommitEntryAllowsOnlyBoundPausedInventoryWithoutClearingGlobalQuarantine()
  {
   var s=Session();var permit=new PausedSavedSpacePermit(s.SessionId!,s.WriteQuarantineActionId!,"source-open-only",1000);
   Assert.True(permit.Matches(s));
   Assert.Null(NormalGameActionCoordinator.PausedSpaceInventoryCommitForTest(s,permit,NormalActionKinds.SupplyMechaWarpers,true));
   Assert.NotNull(NormalGameActionCoordinator.PausedSpaceInventoryCommitForTest(s,permit,NormalActionKinds.Move,true));
   Assert.NotNull(NormalGameActionCoordinator.PausedSpaceInventoryCommitForTest(s,permit,NormalActionKinds.SupplyMechaWarpers,false));
   Assert.Equal(WriteHealthStates.Quarantined,s.WriteHealth);Assert.Single(s.WriteBlockers);
  }
  [Theory]
  [InlineData("tick")][InlineData("world")][InlineData("session")][InlineData("quarantine")][InlineData("reason")][InlineData("extra-blocker")]
  public void PermitCannotBorrowAnotherWorldProgressBoundaryOrSuppressAnotherBlocker(string fault)
  {
   var s=Session();var permit=new PausedSavedSpacePermit(s.SessionId!,s.WriteQuarantineActionId!,"source-open-only",1000);
   switch(fault){case "tick":s.GameTick++;break;case "world":s.OwnedBySpherewright=false;break;
    case "session":s.SessionId="other";break;case "quarantine":s.WriteQuarantineActionId=Guid.NewGuid().ToString();break;
    case "reason":s.WriteBlockers[0].Message="other";break;
    case "extra-blocker":s.WriteBlockers.Add(new WriteBlocker {Code=BridgeErrorCodes.WritesDisabled,Message="disabled"});break;}
   Assert.False(permit.Matches(s));Assert.NotNull(NormalGameActionCoordinator.PausedSpaceInventoryCommitForTest(s,permit,NormalActionKinds.SupplyMechaWarpers,true));
  }
  private static SessionState Session()
  {
   var s=new SessionState {GameLoaded=true,OwnedBySpherewright=true,SessionId="synthetic-owned-session",
    GameTick=1000,LocalPlanetId=null,WriteHealth=WriteHealthStates.Quarantined,WriteQuarantineActionId=Guid.NewGuid().ToString()};
   s.WriteBlockers.Add(new WriteBlocker {Code=BridgeErrorCodes.WriteSubsystemQuarantined,Message="source-open-only"});return s;
  }
 }
}

namespace Spherewright.Plugin.Game
{
 internal sealed partial class NormalGameActionCoordinator
 {
  internal sealed partial class NormalActionPlanPayload
  {
   public bool MechaWarperUiTopUp {get;set;}
   public PausedSavedSpacePermit? PausedSpaceInventoryPermit {get;set;}
   public StoppedSpaceHomeBinding? StoppedSpaceHomeReturn {get;set;}
  }
  internal sealed class StoppedSpaceHomeBinding {public PausedSavedSpacePermit Permit=null!;}
  internal static BridgeError? PausedSpaceInventoryCommitForTest(SessionState s,PausedSavedSpacePermit permit,string kind,bool ui)
   =>ValidateCommitCommon(s,new NormalActionPlanPayload {ActionKind=kind,SessionId=s.SessionId!,PlanetId=0,
    MechaWarperUiTopUp=ui,PausedSpaceInventoryPermit=permit},new CommitNormalActionRequest {SessionId=s.SessionId!,PlanetId=0});
 }
}
