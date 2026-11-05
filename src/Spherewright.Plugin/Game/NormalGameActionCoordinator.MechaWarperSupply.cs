using Spherewright.Bridge.Core.Safety;
using Spherewright.Contracts.Actions;
using Spherewright.Contracts.Errors;
using Spherewright.Contracts.Sessions;

namespace Spherewright.Plugin.Game;

internal sealed partial class NormalGameActionCoordinator
{
    public GameCallResult<PreparedNormalAction> PrepareSupplyMechaWarpersOnMainThread(
        string? requestedSessionId, PrepareSupplyMechaWarpersRequest request)
    {
        var common = request.UiTopUp && request.PlanetId==0
            ? ValidatePausedSpaceInventoryPrepare(requestedSessionId,request.StateHashVersion)
            : ValidatePrepareCommon(requestedSessionId, request.PlanetId, request.StateHashVersion);
        if (common.Error is not null) return GameCallResult<PreparedNormalAction>.Failed(common.Error);
        var observed = _reader.GetPlayerStateOnMainThread(requestedSessionId, new LocalPlanetRequest { PlanetId = request.PlanetId });
        if (!observed.Success || observed.Value is null) return GameCallResult<PreparedNormalAction>.Failed(observed.Error!);
        if (!string.Equals(request.ExpectedPlayerStateHash, observed.Value.StateHash, StringComparison.Ordinal))
            return StalePlan("The inspected player package or mecha state changed before warper supply preparation.");
        if (!TryCaptureMechaWarperSupply(requestedSessionId, request.PlanetId, null, null, out var binding, out var facts))
            return NotReadyPlan("The exact owned native player, package or mecha warper chamber is unavailable.");
        var decision = EvaluateMechaWarperSupply(facts!, request.Count,request.UiTopUp);
        if (!decision.Allowed) return InvalidPlan(decision.Message ?? "The bounded native empty-chamber supply is unavailable.");
        var playerHash = CanonicalStateHash.PlayerAction(observed.Value);
        var expectedHash = MechaWarperSupplyPlanHash(requestedSessionId!, request.PlanetId, playerHash,
            request.Count, facts!, binding!.Player,request.UiTopUp);
        var payload = NormalActionPlanPayload.SupplyMechaWarpers(requestedSessionId!, request.PlanetId,
            expectedHash, playerHash, request.Count, binding!,request.UiTopUp,
            request.PlanetId==0 && _sessions.TryGetPausedSavedSpacePermit(out var permit)?permit:null);
        var prepared = AddPreparedPlan(payload, common.Session!, 1,
            request.UiTopUp ? "One bounded native UI TakeItem/AddItemStacked transfer tops up the exact partial chamber; real stock and count/inc are conserved, with no UseWarper call."
            : "Exactly one native Mecha.AutoReplenishWarper call fills the empty native chamber; bilateral count/inc are conserved and the automatic setting is unchanged.");
        if (prepared.Value is not null) prepared.Value.PlannedMechaWarperUiTopUp=request.UiTopUp;
        if (prepared.Value is not null)
            prepared.Value.ItemBudget.Add(new ActionItemBudget { ItemId = MechaWarperSupplyPolicy.WarperItemId,
                Name = LDB.items.Select(MechaWarperSupplyPolicy.WarperItemId)?.name ?? string.Empty,
                Count = request.Count, Direction = "player-to-mecha-warper" });
        return prepared;
    }

    private BridgeError? RevalidateSupplyMechaWarpersOnMainThread(NormalActionPlanPayload plan, string? executingActionId = null)
    {
        if (plan.MechaWarperSupplyAttempted) return Stale("This native warper supply was already attempted; reconcile its original action.");
        var common = plan.PausedSpaceInventoryPermit is not null
            ? ValidatePausedSpaceInventoryPrepare(plan.SessionId,StateHashVersion)
            : ValidatePrepareCommon(plan.SessionId, plan.PlanetId, StateHashVersion);
        if (common.Error is not null) return common.Error;
        var observed = _reader.GetPlayerStateOnMainThread(plan.SessionId, new LocalPlanetRequest { PlanetId = plan.PlanetId });
        if (!observed.Success || observed.Value is null) return observed.Error;
        var playerHash = CanonicalStateHash.PlayerAction(observed.Value);
        if (!string.Equals(playerHash, plan.PlayerStateHash, StringComparison.Ordinal)
            || plan.MechaWarperSupplyBinding is null
            || !TryCaptureMechaWarperSupply(plan.SessionId, plan.PlanetId, executingActionId,
                plan.MechaWarperSupplyBinding, out var binding, out var facts)
            || !EvaluateMechaWarperSupply(facts!, plan.Count,plan.MechaWarperUiTopUp).Allowed
            || !string.Equals(MechaWarperSupplyPlanHash(plan.SessionId, plan.PlanetId, playerHash,
                plan.Count, facts!, binding!.Player,plan.MechaWarperUiTopUp), plan.ExpectedStateHash, StringComparison.Ordinal))
            return Stale("The exact owned grounded/idle player, package/chamber count/inc, native chamber or automatic setting changed after prepare.");
        return null;
    }

    private void ExecuteSupplyMechaWarpersOnMainThread(ActionRecord action)
    {
        var plan = action.Plan;
        var rejection = RevalidateSupplyMechaWarpersOnMainThread(plan, action.ActionId);
        if (rejection is not null || plan.MechaWarperSupplyBinding is null
            || !TryCaptureMechaWarperSupply(plan.SessionId, plan.PlanetId, action.ActionId,
                plan.MechaWarperSupplyBinding, out var binding, out var before)
            || !EvaluateMechaWarperSupply(before!, plan.Count,plan.MechaWarperUiTopUp).Allowed)
            throw new InvalidOperationException("Native warper supply lost its exact fresh pre-write proof.");
        var beforeFacts = before!;
        var nativeBinding = binding!;
        var readback = new MechaWarperSupplyReadback { CapturedAtGameTick = GameMain.gameTick,
            RequestedCount = plan.Count, PackageCountBefore = beforeFacts.PackageCount, PackageIncBefore = beforeFacts.PackageInc,
            ChamberCountBefore = beforeFacts.ChamberCount, ChamberIncBefore = beforeFacts.ChamberInc,
            AutoReplenishWarperBefore = beforeFacts.AutoReplenishWarper };
        if(plan.MechaWarperUiTopUp)readback.EvidenceScope="synchronous_native_ui_partial_warper_chamber_topup";
        action.MechaWarperSupplyReadback = readback;
        // Latch before the sole native call. False, throw or incomplete proof remain the original
        // accepted action's unknown outcome; the shared coordinator quarantines writes on throw.
        plan.MechaWarperSupplyAttempted = true;
        bool? nativeReturnedSuccess = null;
        Exception? nativeFailure = null;
        try { nativeReturnedSuccess = plan.MechaWarperUiTopUp
            ? ApplyNativeUiWarperTopUp(nativeBinding,plan.Count) : nativeBinding.Mecha.AutoReplenishWarper(); }
        catch (Exception exception) { nativeFailure = exception; }
        readback.NativeReturnedSuccess = nativeReturnedSuccess;
        MechaWarperSupplyVerification? verification = null;
        if (TryCaptureMechaWarperSupply(plan.SessionId, plan.PlanetId, action.ActionId,
                nativeBinding, out _, out var after))
        {
            var afterFacts = after!;
            readback.ReadbackComplete = true;
            readback.CapturedAtGameTick = GameMain.gameTick;
            readback.PackageCountAfter = afterFacts.PackageCount;
            readback.PackageIncAfter = afterFacts.PackageInc;
            readback.ChamberCountAfter = afterFacts.ChamberCount;
            readback.ChamberIncAfter = afterFacts.ChamberInc;
            readback.AutoReplenishWarperAfter = afterFacts.AutoReplenishWarper;
            verification = plan.MechaWarperUiTopUp ? MechaWarperUiTopUpPolicy.Verify(beforeFacts,afterFacts,plan.Count,nativeReturnedSuccess)
                : MechaWarperSupplyPolicy.VerifyReadback(beforeFacts, afterFacts, plan.Count, nativeReturnedSuccess);
            readback.ActualStored = verification.ActualStored;
            readback.ConservationVerified = verification.Succeeded;
        }
        if (nativeFailure is not null)
            throw new InvalidOperationException("The original native warper-supply call threw; retain its readback and do not replay.", nativeFailure);
        if (verification?.Succeeded != true)
            throw new InvalidOperationException("Native warper supply did not prove its exact prepared count, bilateral count/inc and unchanged setting; retain the original action.");
        action.TargetItemId = MechaWarperSupplyPolicy.WarperItemId;
        action.BeforeTargetAmount = beforeFacts.ChamberCount;
        action.AfterTargetAmount = readback.ChamberCountAfter;
        Complete(action, $"One native warper supply stored exactly {plan.Count} item(s); count/inc were conserved and automatic replenishment was unchanged.");
        action.AfterStateHash = CanonicalStateHash.Combine(NormalActionKinds.SupplyMechaWarpers,
            readback.PackageCountBefore, readback.PackageCountAfter, readback.PackageIncBefore, readback.PackageIncAfter,
            readback.ChamberCountBefore, readback.ChamberCountAfter, readback.ChamberIncBefore, readback.ChamberIncAfter,
            readback.AutoReplenishWarperBefore, readback.AutoReplenishWarperAfter, plan.Count);
    }

    private bool TryCaptureMechaWarperSupply(string? sessionId, int planetId, string? ignoredActionId,
        MechaWarperSupplyBinding? expected, out MechaWarperSupplyBinding? binding, out MechaWarperSupplyFacts? facts)
    {
        binding = null;
        facts = null;
        var data = GameMain.data;
        var player = GameMain.mainPlayer;
        var controller = player?.controller;
        var mecha = player?.mecha;
        var package = player?.package;
        var chamber = mecha?.warpStorage;
        if (!_sessions.IsCurrentSessionOwned || !string.Equals(_sessions.SessionId, sessionId, StringComparison.Ordinal)
            || data is null || player is null || controller is null || mecha is null || !ReferenceEquals(mecha.player, player)
            || package?.grids is null || chamber?.grids is null || ReferenceEquals(package, chamber)
            || package.size <= 0 || package.grids.Length != package.size || chamber.size != 1 || chamber.grids.Length != 1)
            return false;
        if (expected is not null && (!ReferenceEquals(expected.World, data) || !ReferenceEquals(expected.Player, player)
            || !ReferenceEquals(expected.Controller, controller) || !ReferenceEquals(expected.Mecha, mecha)
            || !ReferenceEquals(expected.Package, package) || !ReferenceEquals(expected.Chamber, chamber)
            || !ReferenceEquals(expected.PackageGrids, package.grids) || !ReferenceEquals(expected.ChamberGrids, chamber.grids)))
            return false;
        binding = new MechaWarperSupplyBinding { World = data, Player = player, Controller = controller, Mecha = mecha,
            Package = package, Chamber = chamber, PackageGrids = package.grids, ChamberGrids = chamber.grids };
        var packageCount = package.GetItemCount(MechaWarperSupplyPolicy.WarperItemId, out var packageInc);
        var chamberCount = chamber.GetItemCount(MechaWarperSupplyPolicy.WarperItemId, out var chamberInc);
        var grid = chamber.grids[0];
        facts = new MechaWarperSupplyFacts { OwnedBindingVerified = true,
            CurrentPlanetVerified = data.localPlanet is not null && data.localPlanet.id == planetId && player.planetId == planetId,
            PlayerStateHashFresh = true, IsAlive = player.isAlive,
            IsGrounded = player.movementState == EMovementState.Walk && !player.warping,
            ControllerEnabled = !data.disableController, NativeGuideRunning = data.guideRunning,
            IsIdle = player.currentOrder is null && !float.IsNaN(player.speed) && !float.IsInfinity(player.speed)
                && (planetId==0 ? player.uVelocity.magnitude<=.5d : player.speed >= 0f && player.speed <= 0.1f) && BuildUiIsIdle(player)
                && !_actions.ActiveValues.Any(candidate => !candidate.Terminal && candidate.ActionId != ignoredActionId),
            ChamberIsFiltered = chamber.type == EStorageType.Filtered && !chamber.isPlayerInventory,
            ChamberSlotCount = chamber.size, ChamberGridCount = chamber.grids.Length, ChamberBannedGridCount = chamber.bans,
            ChamberItemId = grid.itemId, ChamberFilterItemId = grid.filter, ChamberStackSize = grid.stackSize,
            PackageCount = packageCount, PackageInc = packageInc, ChamberCount = chamberCount, ChamberInc = chamberInc,
            AutoReplenishWarper = mecha.autoReplenishWarper,
            PausedSpaceSourceAuthorized=planetId==0 && _sessions.TryGetPausedSavedSpacePermit(out _),
            SpaceStopSpeed=planetId==0?player.uVelocity.magnitude:null };
        return true;
    }

    private static MechaWarperSupplyDecision EvaluateMechaWarperSupply(MechaWarperSupplyFacts facts,int count,bool ui) =>
        ui ? MechaWarperUiTopUpPolicy.Evaluate(facts,count) : MechaWarperSupplyPolicy.Evaluate(facts,count);

    private CommonPrepareResult ValidatePausedSpaceInventoryPrepare(string? sessionId,int version)
    {
        var s=_sessions.CaptureOnMainThread();
        return version==StateHashVersion && sessionId==s.SessionId && _sessions.TryGetPausedSavedSpacePermit(out var p)
            && p!.Matches(s) ? CommonPrepareResult.Succeeded(s)
            : CommonPrepareResult.Failed(BridgeError.Create(BridgeErrorCodes.ActionRejected,
                "Only an exact newly opened paused saved-space source permits this scoped inventory operation.",false,
                "Keep generic writes stopped and inspect current source provenance."));
    }

    private static bool ApplyNativeUiWarperTopUp(MechaWarperSupplyBinding binding,int count)
    {
        // UIStorageGrid uses TakeItemFromGrid/AddItemStacked and returns any
        // unaccepted remainder. Native package TakeItem selects that same real
        // zero-inc stock; no buffer is assigned and no item is synthesized.
        var taken=binding.Package.TakeItem(1210,count,out var inc);
        if(taken<=0)return false;
        var stored=binding.Chamber.AddItemStacked(1210,taken,inc,out var remainderInc);
        if(stored<taken)
        {
            var restored=binding.Package.AddItemStacked(1210,taken-stored,remainderInc,out var rejectedInc);
            if(restored!=taken-stored || rejectedInc!=0)throw new InvalidOperationException("Native UI transfer remainder was not conserved; retain the original action.");
        }
        return taken==count && stored==count;
    }

    private static string MechaWarperSupplyPlanHash(string sessionId, int planetId, string playerHash,
        int count, MechaWarperSupplyFacts facts, Player player,bool uiTopUp=false) => CanonicalStateHash.Combine(
            NormalActionKinds.SupplyMechaWarpers, sessionId, planetId, playerHash, count,
            MechaWarperSupplyPolicy.Fingerprint(facts), CapturePlayerPackageState(player),uiTopUp);

    private sealed class MechaWarperSupplyBinding
    {
        public GameData World = null!;
        public Player Player = null!;
        public PlayerController Controller = null!;
        public Mecha Mecha = null!;
        public StorageComponent Package = null!;
        public StorageComponent Chamber = null!;
        public StorageComponent.GRID[] PackageGrids = null!;
        public StorageComponent.GRID[] ChamberGrids = null!;
    }

    private sealed partial class NormalActionPlanPayload
    {
        public MechaWarperSupplyBinding? MechaWarperSupplyBinding { get; private set; }
        public bool MechaWarperSupplyAttempted { get; set; }
        public bool MechaWarperUiTopUp {get;private set;}
        public PausedSavedSpacePermit? PausedSpaceInventoryPermit {get;private set;}
        public static NormalActionPlanPayload SupplyMechaWarpers(string sessionId, int planetId, string expectedHash,
            string playerHash, int count, MechaWarperSupplyBinding binding,bool uiTopUp=false,PausedSavedSpacePermit? spacePermit=null) => new NormalActionPlanPayload
            {
                ActionKind = NormalActionKinds.SupplyMechaWarpers, SessionId = sessionId, PlanetId = planetId,
                ExpectedStateHash = expectedHash, PlayerStateHash = playerHash, Count = count,
                MechaWarperSupplyBinding = binding, MechaWarperUiTopUp=uiTopUp,PausedSpaceInventoryPermit=spacePermit, EstimatedTicks = 1,
            };
    }
}
