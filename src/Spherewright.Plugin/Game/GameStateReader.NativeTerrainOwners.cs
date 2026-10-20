using System.Runtime.CompilerServices;
using Spherewright.Bridge.Core.Safety;
using Spherewright.Contracts.Players;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Spherewright.Plugin.Game;

internal sealed partial class GameStateReader
{
    // Only the already registered local-star array is inspected. No scene search,
    // resource lookup, instance-id export or Native lifecycle call occurs here.
    private static RegisteredTerrainCapture? CaptureRegisteredTerrainFootOwners(Player player,
        NativePlanetTerrainPhysicsSnapshot terrain, Collider[] hits, int count)
    {
        terrain.RegisteredTerrainOwnersComplete = false;
        terrain.FootOverlapsCurrentOrRegisteredSurfaceOnly = false;
        try
        {
            var capture = new RegisteredTerrainCapture(player);
            terrain.RegisteredSimulatorCount = capture.Simulators.Length;
            if (terrain.FootHits is null || count < 1 || count >= hits.Length || terrain.FootHits.Count != count) return null;
            var selected = new List<RegisteredTerrainOwner>();
            var currentHits = 0; var otherHits = 0; var all = true;
            for (var i = 0; i < count; i++)
            {
                var hit = hits[i]; var row = terrain.FootHits[i];
                var matches = capture.Owners.SelectMany(o => o.Surface.Select((c, index) => new { Owner = o, Collider = c, Index = index }))
                    .Where(v => ReferenceEquals(v.Collider, hit)).ToArray();
                row.RegisteredSurfaceReferenceMatchCount = matches.Length;
                row.RegisteredSurfaceUnityMatchCount = capture.Owners.Sum(o => o.Surface.Count(c => c != null && hit != null && c == hit));
                row.IsOtherRegisteredSurface = false;
                if (matches.Length != 1 || row.RegisteredSurfaceUnityMatchCount != 1)
                { all = false; row.ReasonType = matches.Length > 1 ? "ambiguous_registered_surface" : "registered_surface_reference_unproved"; continue; }
                var owner = matches[0].Owner;
                row.UniqueRegisteredSimulatorOrdinal = owner.Ordinal;
                row.UniqueRegisteredSurfaceIndex = matches[0].Index;
                if (ReferenceEquals(owner.Planet, capture.CurrentPlanet))
                {
                    if (row.IsCurrentSurface != true) { all = false; continue; }
                    currentHits++;
                }
                else
                {
                    var observation = owner.Observe();
                    var blocker = NativeRegisteredTerrainOwnerPolicy.OwnerPreflightBlocker(observation, capture.Simulators.Length);
                    if (blocker is not null) { all = false; row.ReasonType = blocker; continue; }
                    row.IsOtherRegisteredSurface = true; row.UnknownOwner = false; row.State = "observed"; row.ReasonType = null;
                    otherHits++;
                    if (!selected.Contains(owner)) selected.Add(owner);
                }
                if (hit == null || hit is not MeshCollider mesh || mesh.sharedMesh == null || !hit.enabled
                    || !hit.gameObject.activeInHierarchy || hit.isTrigger || hit.gameObject.layer != 31 || !TerrainDefaultScene(hit.gameObject)) all = false;
            }
            capture.ConditionalOwners = selected.ToArray();
            terrain.FootCurrentSurfaceHitCount = currentHits; terrain.FootRegisteredSurfaceHitCount = otherHits;
            terrain.RegisteredTerrainOwners = selected.Select(o => o.Observe()).ToList();
            terrain.RegisteredTerrainOwnersComplete = capture.BindingsStillMatch();
            terrain.FootOverlapsCurrentOrRegisteredSurfaceOnly = all && currentHits > 0 && currentHits + otherHits == count
                && terrain.RegisteredTerrainOwnersComplete == true;
            terrain.FootHitDetailsComplete = terrain.FootHits.All(h => h.State == "observed");
            return capture;
        }
        catch (Exception exception)
        { terrain.RegisteredTerrainOwnersComplete = false; terrain.ReasonCode = "native_registered_terrain_capture_unavailable:" + exception.GetType().Name; return null; }
    }

    private static bool TerrainDefaultScene(GameObject o) => o != null && o.scene.IsValid() && o.scene.isLoaded
        && o.scene.GetPhysicsScene().Equals(Physics.defaultPhysicsScene);
    private static NativeTerrainColliderPhysicsSnapshot ObserveTerrainCollider(Collider c) => new()
    {
        Type = c is MeshCollider ? "mesh" : c is SphereCollider ? "sphere" : "other", Enabled = c.enabled,
        ActiveInHierarchy = c.gameObject.activeInHierarchy, Layer = c.gameObject.layer, IsTrigger = c.isTrigger,
        SharedMeshPresent = c is MeshCollider mesh ? mesh.sharedMesh != null : (bool?)null,
        SameDefaultPhysicsScene = TerrainDefaultScene(c.gameObject), WorldPosition = TerrainVector(c.transform.position),
        WorldRotation = TerrainRotation(c.transform.rotation), WorldLossyScale = TerrainVector(c.transform.lossyScale),
    };

    // This temporary capture is retained only inside the existing FirstFrameSurfaceState.
    // Exact managed identity grants membership; Unity == is only an additional disagreement diagnostic.
    internal sealed class RegisteredTerrainCapture
    {
        private readonly UniverseSimulator _universe;
        private readonly StarData _star;
        private readonly PlanetData[] _planets;
        private readonly PlanetSimulator[] _slots;
        private readonly PlanetData?[] _slotPlanets;
        public PlanetData CurrentPlanet { get; }
        public PlanetSimulator[] Simulators { get; }
        public RegisteredTerrainOwner[] Owners { get; }
        public RegisteredTerrainOwner[] ConditionalOwners { get; set; } = Array.Empty<RegisteredTerrainOwner>();

        public RegisteredTerrainCapture(Player player)
        {
            _universe = GameMain.universeSimulator; _star = GameMain.localStar; CurrentPlanet = GameMain.localPlanet;
            if (_universe == null || _star is null || CurrentPlanet is null || !ReferenceEquals(player.planetData, CurrentPlanet)
                || !ReferenceEquals(CurrentPlanet.star, _star) || !ReferenceEquals(player.factory, CurrentPlanet.factory)
                || _star.planets is null || _universe.planetSimulators is not { Length: > 0 and <= NativeRegisteredTerrainOwnerPolicy.MaximumRegisteredSimulators } sims
                || _star.planetCount != sims.Length || _star.planets.Length != sims.Length)
                throw new InvalidOperationException("registered_context");
            Simulators = sims; _slots = sims.ToArray(); _planets = _star.planets;
            _slotPlanets = new PlanetData?[sims.Length];
            var owners = new List<RegisteredTerrainOwner>();
            var seenSimulators = new HashSet<PlanetSimulator>(ExactReferenceComparer<PlanetSimulator>.Instance);
            var seenPlanets = new HashSet<PlanetData>(ExactReferenceComparer<PlanetData>.Instance);
            var seenColliders = new HashSet<Collider>(ExactReferenceComparer<Collider>.Instance);
            for (var i = 0; i < sims.Length; i++)
            {
                var sim = sims[i];
                if (ReferenceEquals(sim, null)) continue;
                if (sim == null || !seenSimulators.Add(sim) || sim.planetData is null || !seenPlanets.Add(sim.planetData))
                    throw new InvalidOperationException("registered_duplicate_or_destroyed");
                var planet = sim.planetData;
                _slotPlanets[i] = planet;
                if (!ReferenceEquals(planet.star, _star) || planet.index != i || !ReferenceEquals(_planets[i], planet))
                    throw new InvalidOperationException("registered_slot");
                // A normal unmodelled/gas slot is not an eligible owner. Its unknown
                // geometry cannot excuse any hit; exact matching below still rejects it.
                if (planet.type == EPlanetType.Gas || !planet.loaded || planet.loading || planet.factoryLoading) continue;
                var owner = new RegisteredTerrainOwner(sim, _star, _planets, i, CurrentPlanet);
                foreach (var collider in owner.Surface)
                    if (!seenColliders.Add(collider)) throw new InvalidOperationException("registered_duplicate_surface");
                owners.Add(owner);
            }
            Owners = owners.ToArray();
            if (Owners.Count(o => ReferenceEquals(o.Planet, CurrentPlanet)) != 1) throw new InvalidOperationException("registered_current_missing");
        }

        public bool BindingsStillMatch()
        {
            try
            {
                return _universe != null && ReferenceEquals(GameMain.universeSimulator, _universe)
                    && ReferenceEquals(GameMain.localStar, _star) && ReferenceEquals(GameMain.localPlanet, CurrentPlanet)
                    && ReferenceEquals(_star.planets, _planets) && _star.planetCount == Simulators.Length
                    && ReferenceEquals(_universe.planetSimulators, Simulators) && Simulators.Length == _slots.Length
                    && Simulators.Select((s, i) => ReferenceEquals(s, _slots[i]) && (ReferenceEquals(s, null)
                        ? _slotPlanets[i] is null : s != null && ReferenceEquals(s.planetData, _slotPlanets[i])
                            && ReferenceEquals(s.planetData.star, _star) && s.planetData.index == i
                            && ReferenceEquals(_planets[i], s.planetData))).All(v => v)
                    && Owners.All(o => o.BindingsStillMatch());
            }
            catch (Exception) { return false; }
        }

        public bool MatchesBeforeUniverse() => BindingsStillMatch() && ConditionalOwners.All(o => o.OceanTriggerMatchesBeforeUniverse()
            && NativeRegisteredTerrainOwnerPolicy.OwnerPreflightBlocker(o.Observe(), Simulators.Length) is null);

        public bool ObserveAfterUniverse(NativePlanetTerrainPhysicsSnapshot current)
        {
            try
            {
                var observations = ConditionalOwners.Select(o => o.Observe()).ToList();
                for (var i = 0; i < observations.Count; i++)
                {
                    observations[i].SphereFootClearAfterUniverse = NativeRegisteredTerrainOwnerPolicy.PostUniverseFootBlocker(current) is null;
                    observations[i].PoseMatchesNativeAfterUniverse = ConditionalOwners[i].PoseMatchesNativeAfterUniverse();
                }
                current.RegisteredSimulatorCount = Simulators.Length;
                current.RegisteredTerrainOwners = observations; current.RegisteredTerrainOwnersComplete = BindingsStillMatch();
                return current.RegisteredTerrainOwnersComplete == true && NativeRegisteredTerrainOwnerPolicy.PostUniverseFootBlocker(current) is null
                    && observations.All(o => NativeRegisteredTerrainOwnerPolicy.OwnerPostUniverseBlocker(o, Simulators.Length) is null);
            }
            catch (Exception) { current.RegisteredTerrainOwnersComplete = false; return false; }
        }
    }

    internal sealed class RegisteredTerrainOwner
    {
        private readonly PlanetSimulator _simulator;
        private readonly StarData _star;
        private readonly PlanetData[] _planets;
        private readonly PlanetData _current;
        private readonly GameObject _root, _body;
        private readonly Collider[] _surfaceArray;
        private readonly MeshCollider[] _planetSurfaceArray;
        private readonly Mesh[] _meshArray, _meshes;
        private readonly SphereCollider _sphere;
        private readonly Collider? _ocean;
        private readonly Transform? _rootParent;
        private readonly Vector3[] _surfaceLocalPositions, _surfaceLocalScales;
        private readonly Quaternion[] _surfaceLocalRotations;
        private readonly Vector3 _bodyPosition, _bodyScale, _sphereCenter;
        private readonly Quaternion _bodyRotation;
        private readonly Vector3? _oceanPosition, _oceanScale;
        private readonly Quaternion? _oceanRotation;
        private readonly bool? _oceanTrigger;
        private readonly float _sphereRadius, _radius;
        private readonly int _iceFlag;
        private readonly EPlanetType _type;
        public int Ordinal { get; }
        public PlanetData Planet { get; }
        public Collider[] Surface { get; }

        public RegisteredTerrainOwner(PlanetSimulator sim, StarData star, PlanetData[] planets, int ordinal, PlanetData current)
        {
            _simulator = sim; _star = star; _planets = planets; Ordinal = ordinal; Planet = sim.planetData; _current = current;
            _root = Planet.gameObject; _body = Planet.bodyObject; _sphere = sim.sphereCollider; _ocean = sim.oceanCollider;
            if (!ReferenceEquals(Planet.star, star) || Planet.index != ordinal || !ReferenceEquals(planets[ordinal], Planet)
                || _root == null || _body == null || !ReferenceEquals(_root.GetComponent<PlanetSimulator>(), sim)
                || !TerrainDefaultScene(_root) || !TerrainDefaultScene(_body) || _sphere == null
                || (!ReferenceEquals(_ocean, null) && _ocean == null)
                || sim.surfaceCollider is not { Length: > 0 and <= NativeTerrainObservationPolicy.MaximumSurfaceColliders } surface
                || Planet.meshColliders is null || Planet.meshColliders.Length != surface.Length
                || Planet.meshes is null || Planet.meshes.Length != surface.Length)
                throw new InvalidOperationException("registered_population");
            _surfaceArray = surface; Surface = surface.ToArray(); _planetSurfaceArray = Planet.meshColliders; _meshArray = Planet.meshes;
            _meshes = new Mesh[Surface.Length];
            _surfaceLocalPositions = new Vector3[Surface.Length]; _surfaceLocalRotations = new Quaternion[Surface.Length]; _surfaceLocalScales = new Vector3[Surface.Length];
            _rootParent = _root.transform.parent; _bodyPosition = _body.transform.localPosition;
            _bodyRotation = _body.transform.localRotation; _bodyScale = _body.transform.localScale;
            _sphereCenter = _sphere.center; _sphereRadius = _sphere.radius; _radius = Planet.realRadius; _iceFlag = Planet.iceFlag; _type = Planet.type;
            if (!ReferenceEquals(_body.transform.parent, _root.transform) || !ReferenceEquals(_sphere.gameObject, _body)
                || (_ocean != null && !ReferenceEquals(_ocean.transform.parent, _body.transform)))
                throw new InvalidOperationException("registered_hierarchy");
            for (var i = 0; i < Surface.Length; i++)
            {
                if (Surface[i] == null || Surface[i] is not MeshCollider mesh || mesh.sharedMesh == null
                    || !ReferenceEquals(mesh, _planetSurfaceArray[i]) || !ReferenceEquals(mesh.sharedMesh, _meshArray[i])
                    || !ReferenceEquals(mesh.transform.parent, _body.transform)) throw new InvalidOperationException("registered_mesh");
                _meshes[i] = mesh.sharedMesh; _surfaceLocalPositions[i] = mesh.transform.localPosition;
                _surfaceLocalRotations[i] = mesh.transform.localRotation; _surfaceLocalScales[i] = mesh.transform.localScale;
                _ = ObserveTerrainCollider(mesh); _ = TerrainVector(_surfaceLocalPositions[i]); _ = TerrainRotation(_surfaceLocalRotations[i]);
                var scale = TerrainVector(_surfaceLocalScales[i]); if (scale.X <= 0 || scale.Y <= 0 || scale.Z <= 0) throw new ArithmeticException();
            }
            if (_ocean != null)
            { _oceanPosition = _ocean.transform.localPosition; _oceanRotation = _ocean.transform.localRotation; _oceanScale = _ocean.transform.localScale; _oceanTrigger = _ocean.isTrigger; }
            _ = Observe();
        }

        public bool BindingsStillMatch() => _simulator != null && ReferenceEquals(_simulator.planetData, Planet)
            && ReferenceEquals(Planet.star, _star) && Planet.index == Ordinal && ReferenceEquals(_planets[Ordinal], Planet)
            && _root != null && _body != null && ReferenceEquals(Planet.gameObject, _root) && ReferenceEquals(Planet.bodyObject, _body)
            && ReferenceEquals(_root.GetComponent<PlanetSimulator>(), _simulator) && ReferenceEquals(_root.transform.parent, _rootParent)
            && ReferenceEquals(_body.transform.parent, _root.transform) && _body.transform.localPosition.Equals(_bodyPosition)
            && _body.transform.localRotation.Equals(_bodyRotation) && _body.transform.localScale.Equals(_bodyScale)
            && Planet.realRadius == _radius && Planet.iceFlag == _iceFlag && Planet.type == _type
            && ReferenceEquals(_simulator.surfaceCollider, _surfaceArray) && ReferenceEquals(Planet.meshColliders, _planetSurfaceArray)
            && ReferenceEquals(Planet.meshes, _meshArray) && _surfaceArray.Length == Surface.Length
            && ReferenceEquals(_simulator.sphereCollider, _sphere) && _sphere != null && ReferenceEquals(_sphere.gameObject, _body)
            && _sphere.radius == _sphereRadius && _sphere.center.Equals(_sphereCenter)
            && ReferenceEquals(_simulator.oceanCollider, _ocean)
            && (_ocean == null ? ReferenceEquals(_ocean, null) : ReferenceEquals(_ocean.transform.parent, _body.transform)
                && _ocean.transform.localPosition.Equals(_oceanPosition!.Value) && _ocean.transform.localRotation.Equals(_oceanRotation!.Value)
                && _ocean.transform.localScale.Equals(_oceanScale!.Value))
            && Surface.Select((c, i) => c != null && c is MeshCollider mesh && ReferenceEquals(c, _surfaceArray[i])
                && ReferenceEquals(c, _planetSurfaceArray[i]) && ReferenceEquals(mesh.sharedMesh, _meshes[i])
                && ReferenceEquals(_meshArray[i], _meshes[i]) && ReferenceEquals(c.transform.parent, _body.transform)
                && c.transform.localPosition.Equals(_surfaceLocalPositions[i]) && c.transform.localRotation.Equals(_surfaceLocalRotations[i])
                && c.transform.localScale.Equals(_surfaceLocalScales[i])).All(v => v);

        public NativeRegisteredTerrainOwnerPhysicsSnapshot Observe()
        {
            var t = new NativePlanetTerrainPhysicsSnapshot
            {
                State = "observed", Complete = true, RootLayer = _root.layer, BodyLayer = _body.layer,
                RootWorldPosition = TerrainVector(_root.transform.position), RootWorldRotation = TerrainRotation(_root.transform.rotation),
                RootWorldLossyScale = TerrainVector(_root.transform.lossyScale), BodyWorldPosition = TerrainVector(_body.transform.position),
                BodyWorldRotation = TerrainRotation(_body.transform.rotation), BodyWorldLossyScale = TerrainVector(_body.transform.lossyScale),
                SurfaceColliders = Surface.Select(ObserveTerrainCollider).ToList(),
            };
            CaptureNativeTerrainRootLocalPose(_root.transform, t);
            return new()
            {
                SimulatorOrdinal = Ordinal, State = "observed", SameLocalStar = ReferenceEquals(Planet.star, GameMain.localStar),
                SlotMatchesPlanet = Planet.index == Ordinal && ReferenceEquals(_planets[Ordinal], Planet),
                SimulatorMatchesPlanet = ReferenceEquals(_simulator.planetData, Planet) && ReferenceEquals(_root.GetComponent<PlanetSimulator>(), _simulator),
                PlanetMeshArrayMatches = Surface.Select((c, i) => ReferenceEquals(c, _planetSurfaceArray[i]) && ReferenceEquals(_meshes[i], _meshArray[i])).All(v => v),
                IsCurrentPlanet = ReferenceEquals(Planet, _current), IsSolid = Planet.type != EPlanetType.Gas,
                Loaded = Planet.loaded, Loading = Planet.loading, FactoryLoading = Planet.factoryLoading, Terrain = t,
                Sphere = ObserveTerrainCollider(_sphere), SphereRadius = _sphere.radius, SphereCenter = TerrainVector(_sphere.center),
                OceanPresent = !ReferenceEquals(_ocean, null), Ocean = _ocean == null ? null : ObserveTerrainCollider(_ocean),
                ExpectedOceanLayerAfterUniverse = _iceFlag > 0 ? 9 : 4,
                ExpectedOceanTriggerAfterUniverse = _ocean == null ? null : _iceFlag > 0 ? false : _oceanTrigger,
            };
        }

        public bool OceanTriggerMatchesBeforeUniverse() => _ocean == null ? ReferenceEquals(_ocean, null) : _ocean.isTrigger == _oceanTrigger;

        public bool PoseMatchesNativeAfterUniverse()
        {
            if (!BindingsStillMatch() || !ReferenceEquals(GameMain.localPlanet, _current) || GameCamera.main == null
                || !NativeVector(_current.uPosition) || !NativeVector(Planet.uPosition)) return false;
            // Exact read-only math and operation order from Native UpdateUniversalPosition.
            // Do not call that mutating method, SetLayers or any simulation/update.
            _ = TerrainRotation(_current.runtimeRotation); _ = TerrainRotation(Planet.runtimeRotation);
            var camera = GameCamera.main.transform.position; _ = TerrainVector(camera);
            var relative = Maths.QInvRotateLF(_current.runtimeRotation, Planet.uPosition - _current.uPosition);
            if (!NativeVector(relative)) return false;
            UniverseSimulator.VirtualMapping(relative.x, relative.y, relative.z, camera, out var position, out var scale);
            _ = TerrainVector(position);
            var rotation = Quaternion.Inverse(_current.runtimeRotation) * Planet.runtimeRotation;
            _ = TerrainRotation(rotation);
            if (!TerrainFinite(scale) || scale <= 0) return false;
            // Mirror Native Vector3/Quaternion comparison semantics, including its
            // no-assignment case for an already equivalent local position/scale.
            return _root.transform.localPosition == position && _root.transform.localRotation == rotation
                && _root.transform.localScale == Vector3.one * scale;
        }

        private static bool NativeVector(VectorLF3 v) => !double.IsNaN(v.x) && !double.IsInfinity(v.x) && Math.Abs(v.x) <= 1e15
            && !double.IsNaN(v.y) && !double.IsInfinity(v.y) && Math.Abs(v.y) <= 1e15
            && !double.IsNaN(v.z) && !double.IsInfinity(v.z) && Math.Abs(v.z) <= 1e15;
    }

    private sealed class ExactReferenceComparer<T> : IEqualityComparer<T> where T : class
    {
        public static ExactReferenceComparer<T> Instance { get; } = new();
        public bool Equals(T? x, T? y) => ReferenceEquals(x, y);
        public int GetHashCode(T obj) => RuntimeHelpers.GetHashCode(obj);
    }
}
