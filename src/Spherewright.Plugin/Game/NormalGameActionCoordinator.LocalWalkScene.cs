using Spherewright.Bridge.Core.Safety;
using UnityEngine;

namespace Spherewright.Plugin.Game;

internal sealed partial class NormalGameActionCoordinator
{
    private static NativeLocalWalkSceneBinding? CaptureNativeLocalWalkScene()
    {
        var planet = GameMain.localPlanet;
        if (planet is null || planet.type == EPlanetType.Gas || planet.physics is null || planet.gameObject == null
            || !planet.loaded || planet.loading || !planet.factoryLoaded || planet.factoryLoading) return null;
        var simulator = planet.gameObject.GetComponent<PlanetSimulator>();
        if (simulator == null || !ReferenceEquals(simulator.planetData, planet) || simulator.surfaceCollider is null
            || simulator.surfaceCollider.Length < 1 || simulator.surfaceCollider.Length > 256) return null;
        return new NativeLocalWalkSceneBinding(planet, planet.physics, simulator);
    }

    private static bool NativeLocalWalkSceneProved(Player player, NativeLocalWalkSceneBinding? expected,
        bool finiteWaterCostWithNativePause = false)
    {
        var b = expected ?? CaptureNativeLocalWalkScene();
        if (b is null || !b.MatchesCurrent() || !ReferenceEquals(player.planetData, b.Planet)
            || !ReferenceEquals(player.factory, b.Planet.factory)
            || !player.gameObject.scene.IsValid() || !player.gameObject.scene.isLoaded
            || !player.gameObject.scene.GetPhysicsScene().Equals(UnityEngine.Physics.defaultPhysicsScene)
            || !VoyagerLocalWalkReservePolicy.WaterRayFitsQueriedBall(b.Radius, player.position.magnitude)) return false;
        // This alternative does not certify dry terrain or a stop-position
        // region. Its callers must pay the entire possible Walk/Drift tick,
        // including thrust, and freeze future Native energy on an unproved
        // tail with Pause. Successful completion still requires actual Walk.
        if (finiteWaterCostWithNativePause) return true;
        // The Native local planet's own water collider is either absent or
        // ice layer9. Refuse an ordinary layer4 ocean even while disabled;
        // its normal local-planet lifecycle can enable it later. An Ice name
        // or water item alone is never this proof.
        if (b.Ocean != null && (b.IceFlag <= 0 || b.Ocean.gameObject.layer != 9)) return false;
        // Same defaultPhysicsScene and trigger semantics as Drift's water ray.
        // Repeat at every pre-player/tail observation; no loaded-null shortcut,
        // position prediction, scene mutation, scan or Native update occurs.
        return !Physics.CheckSphere(Vector3.zero, b.Radius + 40f, 16, QueryTriggerInteraction.Collide);
    }

    private static double? NativeLocalGravityUpperBound(Player player)
    {
        var star = GameMain.localStar;
        if (star?.planets is null || star.planetCount < 1 || star.planetCount > 64
            || star.planets.Length < star.planetCount || !ReferenceEquals(player.planetData?.star, star)
            || !FiniteNativePosition(player.uPosition) || !FiniteNativePosition(star.uPosition)) return null;
        var radii = new double[star.planetCount];
        for (var i = 0; i < radii.Length; i++)
        {
            var planet = star.planets[i];
            if (planet is null || !ReferenceEquals(planet.star, star) || !FiniteNativePosition(planet.uPosition)) return null;
            radii[i] = planet.realRadius;
        }
        return VoyagerLocalWalkReservePolicy.CalculateNativeGravityUpperBound(radii, star.mass, star.physicsRadius);
    }

    private static bool FiniteNativePosition(VectorLF3 p) =>
        !double.IsNaN(p.x) && !double.IsInfinity(p.x) && Math.Abs(p.x) <= 1e15d
        && !double.IsNaN(p.y) && !double.IsInfinity(p.y) && Math.Abs(p.y) <= 1e15d
        && !double.IsNaN(p.z) && !double.IsInfinity(p.z) && Math.Abs(p.z) <= 1e15d;

    private static string FreezeNativeLocalFailureEnergy(bool exactOwnedBinding)
    {
        // Pause is an ordinary DSP control, never a velocity write or a
        // physical-stop certificate. At task501 this LogicFrame may still
        // finish: admission reserves that whole Player/construction tick.
        // Subsequent FixedUpdate calls return before another LogicFrame.
        // Never pause a replacement world or automatically Resume it.
        if (!exactOwnedBinding || !GameMain.isRunning) return string.Empty;
        try
        {
            GameMain.Pause();
            return GameMain.isPaused
                ? " Native Pause is verified; this LogicFrame may finish, then further Native energy is frozen. Physical idle remains unproved; do not automatically Resume or replay."
                : " Native Pause could not be verified; preserve the original failure for intervention.";
        }
        catch (Exception)
        { return " Native Pause failed; preserve the original failure for intervention."; }
    }

    private sealed class NativeLocalWalkSceneBinding
    {
        public NativeLocalWalkSceneBinding(PlanetData planet, PlanetPhysics physics, PlanetSimulator simulator)
        {
            Planet = planet; Physics = physics; Simulator = simulator; Root = planet.gameObject;
            Ocean = simulator.oceanCollider; Surface = simulator.surfaceCollider.ToArray();
            Radius = planet.realRadius; WaterItemId = planet.waterItemId; IceFlag = planet.iceFlag;
        }
        public PlanetData Planet { get; }
        public PlanetPhysics Physics { get; }
        public PlanetSimulator Simulator { get; }
        public GameObject Root { get; }
        public Collider? Ocean { get; }
        public Collider[] Surface { get; }
        public float Radius { get; }
        public int WaterItemId { get; }
        public int IceFlag { get; }
        public bool MatchesCurrent() => ReferenceEquals(GameMain.localPlanet, Planet)
            && ReferenceEquals(GameMain.data?.localPlanet, Planet) && ReferenceEquals(GameMain.data?.localLoadedPlanetFactory, Planet.factory)
            && Planet.loaded && !Planet.loading && Planet.factoryLoaded && !Planet.factoryLoading
            && Planet.type != EPlanetType.Gas && ReferenceEquals(Planet.physics, Physics)
            && Root != null && ReferenceEquals(Planet.gameObject, Root) && Simulator != null
            && ReferenceEquals(Root.GetComponent<PlanetSimulator>(), Simulator) && ReferenceEquals(Simulator.planetData, Planet)
            && ReferenceEquals(Simulator.oceanCollider, Ocean) && Planet.realRadius == Radius
            && Planet.waterItemId == WaterItemId && Planet.iceFlag == IceFlag
            && Root.scene.IsValid() && Root.scene.isLoaded && Root.scene.GetPhysicsScene().Equals(UnityEngine.Physics.defaultPhysicsScene)
            && (Ocean == null || Ocean.gameObject.scene.GetPhysicsScene().Equals(UnityEngine.Physics.defaultPhysicsScene))
            && Root.transform.position.sqrMagnitude == 0f && Root.transform.rotation == Quaternion.identity
            && Root.transform.lossyScale.x == 1f && Root.transform.lossyScale.y == 1f && Root.transform.lossyScale.z == 1f
            && Simulator.surfaceCollider is not null && Surface.Length == Simulator.surfaceCollider.Length
            && Surface.Select((collider, index) => collider != null && collider.enabled
                && ReferenceEquals(collider, Simulator.surfaceCollider[index])).All(valid => valid);
    }

    private sealed partial class NormalActionPlanPayload
    {
        public NativeLocalWalkSceneBinding? VoyagerLocalWalkScene { get; set; }
    }
}
