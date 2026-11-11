using Spherewright.Contracts.Actions;

namespace Spherewright.Bridge.Core.Safety;

public static class NativeSurfaceNavigationPolicy
{
    public const long MaximumGameTicks = 3600;
    public const long LandingReserveGameTicks = 900;

    public static string? ValidateRequest(PrepareMoveRequest request)
    {
        if (!request.UseNativeNavigation) return null;
        if (request.AllowPassiveDrift || request.PreviewWalkingRoute || request.WalkingPreviewPoints is not null
            || request.ExpectedWalkingGeometryHash is not null || !Finite(request.ArrivalTolerance)
            || request.ArrivalTolerance < 3 || request.ArrivalTolerance > 5 || request.Target is null
            || !Finite(request.Target.X) || !Finite(request.Target.Y) || !Finite(request.Target.Z))
            return "Native surface navigation requires exclusive arguments and arrivalTolerance3..5 metres.";
        return null;
    }

    public static bool CanFund(double arc, double flyThreshold, double sailThreshold, double perTick,
        double core, double loaded, double floor)
        => new[] { arc, flyThreshold, sailThreshold, perTick, core, loaded, floor }.All(Finite)
            && flyThreshold >= 20 && sailThreshold > flyThreshold && arc > flyThreshold
            && arc > 20 && arc <= Math.Min(500, sailThreshold) && perTick > 0 && floor >= 0
            && core >= perTick * 2 && loaded >= floor + perTick * MaximumGameTicks
            && Finite(floor + perTick * MaximumGameTicks);

    public static bool HasArrived(NativeSurfaceNavigationReadback readback, float tolerance)
        => readback.EvidenceVersion == 1 && readback.NativeNavigationStarted && readback.NativeFlyObserved
            && readback.NativeArrivalObserved && readback.NativeNavigationReleased
            && readback.MovementState is "Walk" or "Drift" or "Fly"
            && readback.NativeGameTicks >= 0 && readback.NativeGameTicks <= MaximumGameTicks
            && Finite(tolerance) && tolerance >= 3 && tolerance <= 5
            && readback.FinalSurfaceDistanceMetres is { } distance && Finite(distance)
            && distance >= 0 && distance <= tolerance;

    public static string Hash(string sessionId, string playerHash, NativeSurfaceNavigationPlan plan)
        => CanonicalStateHash.Combine("native-surface-navigation-v1", sessionId, playerHash, plan.PlanetId,
            ScalarBits(plan.Target.X), ScalarBits(plan.Target.Y), ScalarBits(plan.Target.Z), ScalarBits(plan.ArcDistanceMetres),
            ScalarBits(plan.NativeFlyThresholdMetres), ScalarBits(plan.NativeSailThresholdMetres), ScalarBits(plan.ArrivalToleranceMetres),
            plan.MaximumGameTicks, plan.LandingReserveGameTicks, ScalarBits(plan.EnergyUpperPerTickJoules),
            ScalarBits(plan.RequiredLoadedEnergyJoules), ScalarBits(plan.ProtectedEnergyFloorJoules));

    public static bool ConfirmsEcho(string sessionId, PrepareMoveRequest request, PreparedNormalAction? prepared)
    {
        var plan = prepared?.NativeSurfaceNavigation;
        if (ValidateRequest(request) is not null || !request.UseNativeNavigation || plan is null || plan.Target is null
            || prepared is not { Prepared: true, CommitAllowedNow: true } || prepared.ActionKind != NormalActionKinds.Move
            || string.IsNullOrEmpty(prepared.PlanToken) || prepared.CommitBlockers is null || prepared.CommitBlockers.Count != 0
            || plan.EvidenceVersion != 1 || plan.NativeOperation != "PlayerNavigation.Navigate"
            || plan.PlanetId != request.PlanetId || plan.MaximumGameTicks != MaximumGameTicks
            || plan.SessionId != sessionId || string.IsNullOrEmpty(plan.PlayerActionStateHash)
            || plan.LandingReserveGameTicks != LandingReserveGameTicks || plan.ArrivalToleranceMetres != request.ArrivalTolerance
            || plan.BindingHash != prepared.ExpectedStateHash || string.IsNullOrEmpty(plan.BindingHash)
            || plan.BindingHash != Hash(sessionId, plan.PlayerActionStateHash, plan)) return false;
        var requested = request.Target;
        if (requested is null || !Finite(requested.X) || !Finite(requested.Y) || !Finite(requested.Z)) return false;
        var a = Math.Sqrt((double)requested.X * requested.X + (double)requested.Y * requested.Y + (double)requested.Z * requested.Z);
        var b = Math.Sqrt((double)plan.Target.X * plan.Target.X + (double)plan.Target.Y * plan.Target.Y + (double)plan.Target.Z * plan.Target.Z);
        if (a <= 1 || b <= 1 || !Finite(a) || !Finite(b)) return false;
        var error = Math.Sqrt(Math.Pow(requested.X / a - plan.Target.X / b, 2)
            + Math.Pow(requested.Y / a - plan.Target.Y / b, 2) + Math.Pow(requested.Z / a - plan.Target.Z / b, 2)) * b;
        return error <= .01 && plan.EnergyUpperPerTickJoules > 0
            && plan.RequiredLoadedEnergyJoules == plan.ProtectedEnergyFloorJoules + plan.EnergyUpperPerTickJoules * MaximumGameTicks
            && CanFund(plan.ArcDistanceMetres, plan.NativeFlyThresholdMetres, plan.NativeSailThresholdMetres,
                plan.EnergyUpperPerTickJoules, plan.EnergyUpperPerTickJoules * 2,
                plan.RequiredLoadedEnergyJoules, plan.ProtectedEnergyFloorJoules);
    }

    private static bool Finite(double value) => !double.IsNaN(value) && !double.IsInfinity(value);

    // Classic Unity/.NET and current MCP runtimes format floating-point text
    // differently. Integer IEEE754 bits bind every scalar across those hosts.
    // JSON may normalize negative zero, whose surface/energy meaning is zero.
    private static long ScalarBits(double value) => value == 0 ? 0 : BitConverter.DoubleToInt64Bits(value);
}
