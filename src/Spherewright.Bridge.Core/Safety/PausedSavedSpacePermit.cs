using Spherewright.Contracts.Errors;
using Spherewright.Contracts.Sessions;

namespace Spherewright.Bridge.Core.Safety;

/// <summary>Narrow paused-source inventory permit; never a generic healthy-world admission.</summary>
public sealed class PausedSavedSpacePermit
{
    public string SessionId {get;}
    public string OpenActionId {get;}
    public string QuarantineReason {get;}
    public long SavedTick {get;}
    public PausedSavedSpacePermit(string sessionId,string openActionId,string reason,long tick)
    {
        if(string.IsNullOrEmpty(sessionId)||!Guid.TryParse(openActionId,out _)||string.IsNullOrEmpty(reason)||tick<0)
            throw new ArgumentException("Exact paused saved-world identity is required.");
        SessionId=sessionId;OpenActionId=openActionId;QuarantineReason=reason;SavedTick=tick;
    }
    public bool Matches(SessionState s)=>s.GameLoaded && s.OwnedBySpherewright && s.SessionId==SessionId
        && s.LocalPlanetId is null && s.GameTick==SavedTick && s.WriteHealth==WriteHealthStates.Quarantined
        && s.WriteQuarantineActionId==OpenActionId && s.WriteBlockers.Count==1
        && s.WriteBlockers[0].Code==BridgeErrorCodes.WriteSubsystemQuarantined && s.WriteBlockers[0].Message==QuarantineReason;
    public IReadOnlyList<WriteBlocker> RemainingBlockers(SessionState s)=>Matches(s)?Array.Empty<WriteBlocker>():s.WriteBlockers;
}
