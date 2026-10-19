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

    private static bool NativeLocalWalkSceneProved(Player player, NativeLocalWalkSceneBinding? expected)
    {
        var b = expected ?? CaptureNativeLocalWalkScene();
        if (b is null || !b.MatchesCurrent() || !ReferenceEquals(player.planetData, b.Planet)
            || !ReferenceEquals(player.factory, b.Planet.factory)
            || !player.gameObject.scene.IsValid() || !player.gameObject.scene.isLoaded
            || !player.gameObject.scene.GetPhysicsScene().Equals(UnityEngine.Physics.defaultPhysicsScene)
            || !VoyagerLocalWalkReservePolicy.WaterRayFitsQueriedBall(b.Radius, player.position.magnitude)) return false;
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
