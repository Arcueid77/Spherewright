using Spherewright.Contracts.Actions;

namespace Spherewright.Bridge.Core.Safety;

// Only serialization with an independently verified paused-save binding may
// avoid an unrelated live idle-controller requirement. This never resumes the
// simulation or clears quarantine. Returning to menu still separately proves
// its own saved-file and Native EndGame conditions.
public static class VoyagerPausedSavePolicy
{
    public static bool UsesBoundPreservation(string? actionKind, bool hasPausedSaveBinding) =>
        actionKind is not null && NormalActionKinds.IsSaveLifecycle(actionKind) && hasPausedSaveBinding;
}
