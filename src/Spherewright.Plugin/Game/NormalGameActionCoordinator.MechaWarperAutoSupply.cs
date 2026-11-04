using Spherewright.Bridge.Core.Factory;
using Spherewright.Bridge.Core.Safety;
using Spherewright.Contracts.Actions;
using Spherewright.Contracts.Errors;
using Spherewright.Contracts.Sessions;

namespace Spherewright.Plugin.Game;

internal sealed partial class NormalGameActionCoordinator
{
    private GameCallResult<PreparedNormalAction> PrepareMechaWarperAutoSupplyOnMainThread(
        string? sessionId, PrepareConfigureBuildingRequest request, SessionState session)
    {
        var inspected = _reader.GetPlayerStateOnMainThread(sessionId, new LocalPlanetRequest { PlanetId=request.PlanetId });
        if (!inspected.Success || inspected.Value is null) return GameCallResult<PreparedNormalAction>.Failed(inspected.Error!);
        if (!TryCaptureMechaWarperSupply(sessionId, request.PlanetId, null, null, out var binding, out var facts))
            return NotReadyPlan("The exact native player, package and warper chamber are unavailable.");
        if (MechaWarperAutoSupplyPolicy.Validate(request, inspected.Value, facts!) is {} reason) return StalePlan(reason);
        var fingerprint = MechaWarperSupplyPolicy.Fingerprint(facts!);
        var payload = NormalActionPlanPayload.ConfigureMechaWarperAutoSupply(sessionId!, request.PlanetId,
            CanonicalStateHash.Combine("mecha-warper-auto-supply-v1", sessionId, request.PlanetId,
                inspected.Value.StateHash, fingerprint, CapturePlayerPackageState(binding!.Player)),
            inspected.Value.StateHash, binding!, fingerprint);
        if (RevalidateMechaWarperAutoSupplyOnMainThread(payload) is {} nativeError)
            return GameCallResult<PreparedNormalAction>.Failed(nativeError);
        var result = AddPreparedPlan(payload, session, 1,
            "Enables the normal native mecha warper auto-supply checkbox once; package and chamber contents remain unchanged at the setting-write boundary. No replenishment or UseWarper call is made.");
        if (result.Success && result.Value is not null) result.Value.PlannedMechaWarperAutoSupplyEnabled = true;
        return result;
    }

    private BridgeError? RevalidateMechaWarperAutoSupplyOnMainThread(NormalActionPlanPayload plan, string? ignoredActionId=null)
    {
        var inspected = _reader.GetPlayerStateOnMainThread(plan.SessionId, new LocalPlanetRequest { PlanetId=plan.PlanetId });
        if (!inspected.Success || inspected.Value is null) return inspected.Error;
        var request = new PrepareConfigureBuildingRequest { PlanetId=plan.PlanetId, EntityId=0, RecipeId=0,
            Mode=BuildingConfigurationModes.EnableMechaWarperAutoSupply, ExpectedFactoryStateHash=plan.FactoryStateHash };
        if (!TryCaptureMechaWarperSupply(plan.SessionId, plan.PlanetId, ignoredActionId,
                plan.MechaWarperSupplyBinding, out var binding, out var facts) || facts is null || binding is null
            || plan.MechaWarperSupplyBinding is null
            || MechaWarperSupplyPolicy.Fingerprint(facts) != plan.MechaWarperAutoSupplyFingerprint)
            return Stale("Native warper setting, package, chamber, controller or bound world changed.");
        if (MechaWarperAutoSupplyPolicy.Validate(request, inspected.Value, facts) is {} reason) return Stale(reason);
        var expectedHash = CanonicalStateHash.Combine("mecha-warper-auto-supply-v1", plan.SessionId, plan.PlanetId,
            inspected.Value.StateHash, MechaWarperSupplyPolicy.Fingerprint(facts), CapturePlayerPackageState(binding.Player));
        if (expectedHash != plan.ExpectedStateHash) return Stale("The complete native package binding changed after preparation.");
        if (_flightCheckpoints.VoyagerJourney is {} journey && !IsVoyagerJourneyClosed(journey))
            return Stale("Warper auto-supply setting requires a closed journey and a fresh subsequent route.");
        return GameMain.mainPlayer is {} player && ConstructionPlayerIdle(player)
            ? null : Stale("The native player is no longer strictly idle.");
    }

    private void ExecuteMechaWarperAutoSupplyOnMainThread(ActionRecord action)
    {
        var plan = action.Plan;
        if (plan.MechaWarperAutoSupplyAttempted) throw new InvalidOperationException("The original checkbox write must never be repeated.");
        if (RevalidateMechaWarperAutoSupplyOnMainThread(plan, action.ActionId) is {} error)
            throw new InvalidOperationException(error.Message);
        if (!TryCaptureMechaWarperSupply(plan.SessionId, plan.PlanetId, action.ActionId,
                plan.MechaWarperSupplyBinding, out var binding, out var before) || before is null || binding is null)
            throw new InvalidOperationException("The exact pre-write native binding is unavailable.");
        var packageBefore = CapturePlayerPackageState(binding.Player);
        var readback = new MechaWarperAutoSupplyReadback { CapturedAtGameTick=GameMain.gameTick,
            EnabledBefore=before.AutoReplenishWarper, PackageCountBefore=before.PackageCount,
            PackageIncBefore=before.PackageInc, ChamberCountBefore=before.ChamberCount, ChamberIncBefore=before.ChamberInc };
        action.MechaWarperAutoSupplyReadback = readback;
        plan.MechaWarperAutoSupplyAttempted = true;
        // DSP29151 UIMechaWindow.OnAutoReplenishButtonClick, case4. The native
        // checkbox only toggles this setting; do not invoke AutoReplenishWarper,
        // top up a partial chamber, remove stock or change any reserve here.
        binding.Mecha.autoReplenishWarper = !binding.Mecha.autoReplenishWarper;
        if (!TryCaptureMechaWarperSupply(plan.SessionId, plan.PlanetId, action.ActionId,
                binding, out _, out var after) || after is null)
            throw new InvalidOperationException("The original setting write has incomplete readback; retain its action.");
        readback.EnabledAfter = after.AutoReplenishWarper;
        readback.PackageCountAfter = after.PackageCount;
        readback.PackageIncAfter = after.PackageInc;
        readback.ChamberCountAfter = after.ChamberCount;
        readback.ChamberIncAfter = after.ChamberInc;
        readback.PackageContentsPreserved = packageBefore == CapturePlayerPackageState(binding.Player);
        readback.ReadbackComplete = true;
        readback.ConservationVerified = MechaWarperAutoSupplyPolicy.VerifyReadback(before, after, readback.PackageContentsPreserved);
        if (!readback.ConservationVerified)
            throw new InvalidOperationException("Native setting or exact package/chamber conservation was not proved; retain the original action.");
        action.AfterStateHash = CanonicalStateHash.Combine("mecha-warper-auto-supply-v1",
            MechaWarperSupplyPolicy.Fingerprint(before), MechaWarperSupplyPolicy.Fingerprint(after), packageBefore);
        Complete(action, "Native warper auto-supply enabled once; no package or chamber stock moved. Subsequent normal replenishment and flight need fresh independent proof.");
    }

    private sealed partial class NormalActionPlanPayload
    {
        public string MechaWarperAutoSupplyFingerprint { get; private set; } = string.Empty;
        public bool MechaWarperAutoSupplyAttempted { get; set; }
        public static NormalActionPlanPayload ConfigureMechaWarperAutoSupply(string sessionId, int planetId,
            string expectedHash, string playerHash, MechaWarperSupplyBinding binding, string fingerprint)
        {
            var plan = Configure(sessionId, planetId, expectedHash, playerHash, 0, 0,
                BuildingConfigurationModes.EnableMechaWarperAutoSupply, 0, 0, string.Empty, -1, -1, -1, 0, 0, 0, "none", "none", 0);
            plan.MechaWarperSupplyBinding = binding;
            plan.MechaWarperAutoSupplyFingerprint = fingerprint;
            return plan;
        }
    }
}
