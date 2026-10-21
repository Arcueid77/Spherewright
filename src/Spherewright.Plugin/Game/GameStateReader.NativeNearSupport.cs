using System.Reflection;
using Spherewright.Contracts.Players;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Spherewright.Plugin.Game;

internal sealed partial class GameStateReader
{
    // Called only by the existing main-thread admission path. No lifecycle method is invoked.
    internal static string? TryCaptureNativeNearSupport(Player player, string stage, Collider[] currentTerrainSurface,
        out NativeNearGroundSupportSnapshot snapshot, out NativeNearGroundSupportBinding? binding)
    {
        snapshot = new NativeNearGroundSupportSnapshot { CaptureStage = stage, CapturedAtGameTick = GameMain.gameTick };
        binding = null;
        try
        {
            var candidate = new NativeNearGroundSupportBinding(player, currentTerrainSurface, snapshot);
            var blocker = candidate.Capture(player, stage, currentTerrainSurface, true, out snapshot);
            // An empty, exactly bound collection is an initialization witness,
            // not a support owner. Callers still must handle the blocker.
            if (blocker is null || blocker == "remote_resume_near_support_registered_collection_empty") binding = candidate;
            return blocker;
        }
        catch (Exception exception)
        {
            snapshot.ReasonCode = NearSupportReason(exception);
            return snapshot.ReasonCode;
        }
    }

    private static string NearSupportReason(Exception exception) => exception is NativeNearSupportException known
        ? known.Code : "native_near_support_capture_unavailable:" + exception.GetType().Name;

    private sealed class NativeNearSupportException : Exception
    {
        public NativeNearSupportException(string code) { Code = "remote_resume_near_support_" + code; }
        public string Code { get; }
    }

    /// <summary>Live references stay on the Unity thread; only detached snapshots leave this binding.</summary>
    internal sealed class NativeNearGroundSupportBinding
    {
        private const int MaximumRegisteredColliders = 4096;
        private const int MaximumColliderChain = 256;
        private const int MaximumFreePoolCapacity = 65536;
        private readonly int _threadId = System.Threading.Thread.CurrentThread.ManagedThreadId;
        private readonly GameData _world;
        private readonly Player _player;
        private readonly PlanetData _planet;
        private readonly PlanetFactory _factory;
        private readonly PlanetPhysics _physics;
        private readonly NearColliderLogic _near;
        private readonly ColliderPool _colliderPool;
        private readonly ColliderContainer[] _chunks;
        private readonly Dictionary<int, ColliderObject> _registered;
        private readonly PlanetSimulator _simulator;
        private readonly Collider[] _nativeTerrain;
        private readonly Collider[] _terrain;
        private readonly Mesh[] _terrainMeshes;
        private Collider? _support;
        private ColliderObject? _registeredObject;
        private NativeNearSupportPose? _supportPose;
        private ColliderData _supportData;
        private int _supportId;
        private int _objectId;
        private EObjectType _objectType;
        private Array? _objectPool;
        private int _chainRoot;
        private List<NativeNearSupportCell>? _chain;

        internal NativeNearGroundSupportBinding(Player player, Collider[] surface, NativeNearGroundSupportSnapshot initial)
        {
            _world = GameMain.data ?? throw new NativeNearSupportException("world_unavailable");
            _player = player ?? throw new NativeNearSupportException("player_unavailable");
            _planet = GameMain.localPlanet ?? throw new NativeNearSupportException("planet_unavailable");
            Require(ReferenceEquals(GameMain.mainPlayer, player)
                && _planet.type != EPlanetType.Gas && _planet.loaded && !_planet.loading
                && _planet.factoryLoaded && !_planet.factoryLoading && _planet.factory is not null
                && ReferenceEquals(player.planetData, _planet) && ReferenceEquals(player.factory, _planet.factory), "context_unproved");
            _factory = _planet.factory!;
            _physics = _planet.physics ?? throw new NativeNearSupportException("physics_unavailable");
            Require(ReferenceEquals(_factory.planet, _planet) && ReferenceEquals(_physics.planet, _planet)
                && _physics.nearColliderLogic is not null, "physics_binding_unproved");
            _near = _physics.nearColliderLogic!;
            _colliderPool = ColliderPool.instance ?? throw new NativeNearSupportException("collider_pool_unavailable");
            Require(ReferenceEquals(_near.planet, _planet), "pool_binding_unproved");
            _chunks = _physics.colChunks;
            Require(_chunks is { Length: 600 } && ReferenceEquals(ReadPrivate<ColliderContainer[]>(_near, "colChunks"), _chunks), "chunk_table_unproved");
            _registered = ReadPrivate<Dictionary<int, ColliderObject>>(_near, "colliderObjs");
            initial.RegisteredColliderCount = _registered.Count;
            Require(_registered.Count <= MaximumRegisteredColliders, "registered_collection_limit_exceeded");
            _simulator = _planet.gameObject == null ? null! : _planet.gameObject.GetComponent<PlanetSimulator>();
            Require(_simulator != null && ReferenceEquals(_simulator.planetData, _planet)
                && surface is { Length: > 0 and <= 256 } && _simulator.surfaceCollider is { } native && native.Length == surface.Length,
                "terrain_binding_unproved");
            _nativeTerrain = _simulator!.surfaceCollider; _terrain = surface!.ToArray();
            _terrainMeshes = new Mesh[_terrain.Length];
            for (var i = 0; i < _terrain.Length; i++)
            {
                Require(_terrain[i] is MeshCollider && _terrain[i] != null
                    && ReferenceEquals(_terrain[i], _nativeTerrain[i])
                    && !_terrain.Take(i).Any(c => c == _terrain[i]), "terrain_membership_unproved");
                _terrainMeshes[i] = ((MeshCollider)_terrain[i]).sharedMesh;
                Require(_terrainMeshes[i] != null, "terrain_mesh_unavailable");
            }
            Require(ContextMatches(player, surface), "context_unproved");
        }

        internal string? Recheck(Player player, string stage, Collider[] currentTerrainSurface,
            out NativeNearGroundSupportSnapshot snapshot) => Capture(player, stage, currentTerrainSurface, false, out snapshot);

        internal bool TryObserveInitialization(Player player, Collider[] surface, out int? count)
        {
            count = null;
            if (System.Threading.Thread.CurrentThread.ManagedThreadId != _threadId || !ContextMatches(player, surface)) return false;
            count = _registered.Count;
            return true;
        }

        internal string? Capture(Player player, string stage, Collider[] surface, bool initial,
            out NativeNearGroundSupportSnapshot snapshot)
        {
            snapshot = new NativeNearGroundSupportSnapshot { CaptureStage = stage };
            try
            {
                // Reject a foreign thread before reading any retained Unity object.
                Require(System.Threading.Thread.CurrentThread.ManagedThreadId == _threadId, "thread_changed");
                snapshot.CapturedAtGameTick = GameMain.gameTick;
                Require(ContextMatches(player, surface), "context_changed");
                snapshot.RegisteredColliderCount = _registered.Count;
                Require(_registered.Count <= MaximumRegisteredColliders, "registered_collection_limit_exceeded");
                Require(_registered.Count > 0, "registered_collection_empty");
                var p = player.position;
                Require(Finite(p) && Finite(p.sqrMagnitude) && p.sqrMagnitude > 1f, "player_position_unproved");
                var up = p.normalized;
                Require(Finite(up) && up.sqrMagnitude > 0f, "player_up_unproved");
                // The three-argument overload deliberately mirrors Native UseGlobal query semantics.
                var grounded = Physics.CheckSphere(p + up * .15f, .35f, 15873);
                snapshot.ActualGroundSphereHit = grounded; snapshot.CastAttempted = !grounded;
                if (initial) Require(!grounded, "initial_sphere_already_grounded");
                if (!grounded)
                {
                    var hit = Physics.SphereCast(new Ray(p + up * .35f, -up), .35f, out var info, 20f, 15873);
                    snapshot.CastHit = hit;
                    Require(hit && info.collider != null, "cast_miss_or_collider_unavailable");
                    Require(Finite(info.distance) && info.distance >= 0f && info.distance <= 20f
                        && Finite(info.point) && Finite(info.normal) && Finite(info.normal.sqrMagnitude)
                        && info.normal.sqrMagnitude > 0f, "cast_geometry_unproved");
                    snapshot.CastDistance = info.distance; snapshot.HitPoint = Vector(info.point); snapshot.HitNormal = Vector(info.normal);
                    var dot = Vector3.Dot(info.normal, up); Require(Finite(dot), "normal_dot_unproved"); snapshot.NormalDotUp = dot;
                    if (initial) BindSupport(info.collider!);
                    else Require(ReferenceEquals(info.collider, _support) && info.collider == _support, "cast_support_changed");
                }
                VerifySupport(snapshot);
                var hits = new Collider[257];
                var count = Physics.OverlapSphereNonAlloc(p + up * .15f, .35f, hits, 15873, QueryTriggerInteraction.Ignore);
                snapshot.FootOverlapCount = count; snapshot.FootOverlapComplete = count >= 0 && count < hits.Length;
                Require(snapshot.FootOverlapComplete == true, "foot_query_incomplete");
                var all = true; var supportHit = false; var terrainHit = false;
                for (var i = 0; i < count; i++)
                {
                    var c = hits[i];
                    var sameSupport = c != null && ReferenceEquals(c, _support) && c == _support;
                    var currentTerrain = c is MeshCollider mesh && mesh != null && mesh.sharedMesh != null
                        && c.gameObject.layer == 9 && _terrain.Count(t => ReferenceEquals(t, c) && t == c) == 1;
                    if (c == null || !c.enabled || !c.gameObject.activeInHierarchy || c.isTrigger || !Scene(c.gameObject)
                        || (!sameSupport && !currentTerrain)) all = false;
                    supportHit |= sameSupport; terrainHit |= currentTerrain;
                }
                snapshot.AllFootHitsCurrentSupportOrTerrain = all; snapshot.CurrentSupportFootHit = supportHit; snapshot.TerrainFootHit = terrainHit;
                Require(all && (!grounded || (count > 0 && (supportHit || terrainHit))), "foot_owner_unproved");
                Require(ContextMatches(player, surface), "context_changed_during_query");
                snapshot.State = "observed";
                return null;
            }
            catch (Exception exception)
            {
                snapshot.ReasonCode = NearSupportReason(exception);
                return snapshot.ReasonCode;
            }
        }

        private void BindSupport(Collider hit)
        {
            var matches = Matches(hit);
            Require(matches.Length == 1 && matches[0].Value is not null && matches[0].Value.id == matches[0].Key
                && ReferenceEquals(matches[0].Value.collider, hit), "registered_support_ambiguous");
            _supportId = matches[0].Key; _registeredObject = matches[0].Value; _support = hit;
            var cell = Cell(_supportId);
            Require(_physics.GetColliderData(hit, out var data) && Same(data, cell.Data), "native_pool_lookup_unproved");
            Require(data.idType != 0 && data.objId > 0 && data.usage == EColliderUsage.Physics
                && (data.objType == EObjectType.Vein || data.objType == EObjectType.Vegetable), "support_kind_unproved");
            _supportData = data; _objectType = data.objType; _objectId = data.objId;
            (_objectPool, _chainRoot) = ObjectRoot();
            _chain = Chain(_chainRoot);
            Require(_chain.Count(c => c.Id == _supportId) == 1, "support_not_in_object_chain");
            _supportPose = new NativeNearSupportPose(hit, data, _colliderPool);
        }

        private void VerifySupport(NativeNearGroundSupportSnapshot snapshot)
        {
            Require(_support != null && _registeredObject is not null && _supportPose is not null && _chain is not null,
                "original_support_unavailable");
            var matches = Matches(_support!);
            Require(matches.Length == 1 && matches[0].Key == _supportId && ReferenceEquals(matches[0].Value, _registeredObject)
                && ReferenceEquals(_registeredObject!.collider, _support) && _registeredObject.id == _supportId,
                "registered_support_changed");
            var supportCell = Cell(_supportId);
            Require(Same(supportCell.Data, _supportData) && _physics.GetColliderData(_support!, out var data) && Same(data, _supportData),
                "support_pool_cell_changed");
            var (pool, root) = ObjectRoot();
            Require(ReferenceEquals(pool, _objectPool) && root == _chainRoot, "live_object_changed");
            var current = Chain(root);
            Require(current.Count == _chain!.Count && current.Zip(_chain!, (a, b) => a.Matches(b)).All(v => v), "collider_chain_changed");
            Require(_supportPose!.Matches(_support!, _supportData, _colliderPool), "support_geometry_changed");
            Require(NotInFreePool(_support!), "support_in_free_pool");
            snapshot.Layer = _support!.gameObject.layer; snapshot.Shape = _support is CapsuleCollider ? "capsule" : _support is SphereCollider ? "sphere" : "box";
            snapshot.IsTrigger = _support.isTrigger; snapshot.Enabled = _support.enabled;
            snapshot.ActiveInHierarchy = _support.gameObject.activeInHierarchy; snapshot.SameDefaultPhysicsScene = Scene(_support.gameObject);
            snapshot.OwnerType = _objectType == EObjectType.Vein ? "vein" : "vegetable"; snapshot.ObjectId = _objectId;
            snapshot.CurrentPoolReference = true; snapshot.LiveObject = true; snapshot.ColliderChain = true;
            // This mark is not object existence: Native501 intentionally clears it.
            snapshot.NativeLiveFlag = _registeredObject!.live;
        }

        private bool ContextMatches(Player player, Collider[] surface)
        {
            if (!ReferenceEquals(GameMain.data, _world) || !ReferenceEquals(GameMain.mainPlayer, _player) || !ReferenceEquals(player, _player)
                || !ReferenceEquals(GameMain.localPlanet, _planet) || _planet.type == EPlanetType.Gas || !_planet.loaded || _planet.loading
                || !_planet.factoryLoaded || _planet.factoryLoading || !ReferenceEquals(_planet.factory, _factory)
                || !ReferenceEquals(_factory.planet, _planet)
                || !ReferenceEquals(player.planetData, _planet) || !ReferenceEquals(player.factory, _factory)
                || !ReferenceEquals(_planet.physics, _physics) || !ReferenceEquals(_physics.planet, _planet)
                || !ReferenceEquals(_physics.nearColliderLogic, _near) || !ReferenceEquals(_near.planet, _planet)
                || _colliderPool == null || !ReferenceEquals(ColliderPool.instance, _colliderPool)
                || !ReferenceEquals(_physics.colChunks, _chunks) || !ReferenceEquals(ReadPrivate<ColliderContainer[]>(_near, "colChunks"), _chunks)
                || !ReferenceEquals(ReadPrivate<Dictionary<int, ColliderObject>>(_near, "colliderObjs"), _registered)
                || _simulator == null || !ReferenceEquals(_simulator.planetData, _planet)
                || _planet.gameObject == null || !ReferenceEquals(_planet.gameObject.GetComponent<PlanetSimulator>(), _simulator)
                || !ReferenceEquals(_simulator.surfaceCollider, _nativeTerrain) || surface is null || surface.Length != _terrain.Length
                || player.gameObject == null || !Scene(player.gameObject)) return false;
            for (var i = 0; i < _terrain.Length; i++)
            {
                if (!ReferenceEquals(surface[i], _terrain[i]) || !ReferenceEquals(_nativeTerrain[i], _terrain[i])
                    || _terrain[i] is not MeshCollider mesh || mesh == null || !mesh.enabled || !mesh.gameObject.activeInHierarchy
                    || mesh.isTrigger || (mesh.gameObject.layer != 31 && mesh.gameObject.layer != 9)
                    || !ReferenceEquals(mesh.sharedMesh, _terrainMeshes[i]) || !Scene(mesh.gameObject)) return false;
            }
            return true;
        }

        private KeyValuePair<int, ColliderObject>[] Matches(Collider hit) => _registered
            .Where(e => e.Value is not null && e.Value.collider != null && e.Value.collider == hit).ToArray();

        private (Array Pool, int Root) ObjectRoot()
        {
            if (_objectType == EObjectType.Vein)
            {
                var pool = _factory.veinPool;
                Require(pool is not null && _objectId > 0 && _objectId < pool.Length && _objectId < _factory.veinCursor
                    && pool[_objectId].id == _objectId && pool[_objectId].colliderId > 0, "vein_object_unproved");
                return (pool!, pool![_objectId].colliderId);
            }
            var vegetables = _factory.vegePool;
            Require(_objectType == EObjectType.Vegetable && vegetables is not null && _objectId > 0 && _objectId < vegetables.Length
                && _objectId < _factory.vegeCursor && vegetables[_objectId].id == _objectId
                && vegetables[_objectId].colliderId > 0, "vegetable_object_unproved");
            return (vegetables!, vegetables![_objectId].colliderId);
        }

        private List<NativeNearSupportCell> Chain(int root)
        {
            var cells = new List<NativeNearSupportCell>(); var seen = new HashSet<int>();
            for (var id = root; id != 0;)
            {
                Require(id > 0 && cells.Count < MaximumColliderChain && seen.Add(id), "collider_chain_incomplete");
                var cell = Cell(id);
                Require(cell.Data.idType != 0 && cell.Data.objId == _objectId && cell.Data.objType == _objectType
                    && cell.Data.link >= 0, "collider_chain_owner_unproved");
                cells.Add(cell); id = cell.Data.link;
            }
            Require(cells.Count > 0, "collider_chain_empty"); return cells;
        }

        private NativeNearSupportCell Cell(int id)
        {
            var chunk = id >> 20; var index = id & 0xFFFFF;
            Require(id > 0 && chunk >= 0 && chunk < _chunks.Length && _chunks[chunk] is not null, "pool_cell_bounds_unproved");
            var container = _chunks[chunk]; var pool = container.colliderPool;
            Require(pool is not null && container.cursor > 0 && container.cursor <= pool.Length
                && index > 0 && index < container.cursor && index < pool.Length, "pool_cell_bounds_unproved");
            var data = pool![index]; Require(Finite(data), "pool_cell_nonfinite");
            return new NativeNearSupportCell(id, container, pool, data);
        }

        private bool NotInFreePool(Collider support)
        {
            string name;
            if (support is CapsuleCollider) name = "cc";
            else if (support is SphereCollider) name = "sc";
            else if (support is BoxCollider) name = "bc";
            else return false;
            var field = typeof(ColliderPool).GetField(name + "Pool", BindingFlags.Instance | BindingFlags.NonPublic);
            var cursorField = typeof(ColliderPool).GetField(name + "Cursor", BindingFlags.Instance | BindingFlags.NonPublic);
            Require(field is not null && cursorField is not null && field.GetValue(_colliderPool) is Array
                && cursorField.GetValue(_colliderPool) is int, "free_pool_unobserved");
            var pool = (Array)field!.GetValue(_colliderPool)!; var cursor = (int)cursorField!.GetValue(_colliderPool)!;
            Require(pool.Length <= MaximumFreePoolCapacity && cursor >= 0 && cursor <= pool.Length, "free_pool_bounds_unproved");
            for (var i = 0; i < cursor; i++)
            {
                Require(pool.GetValue(i) is Collider c && c != null, "free_pool_entry_unproved");
                if ((Collider)pool.GetValue(i)! == support) return false;
            }
            return true;
        }

        private static T ReadPrivate<T>(NearColliderLogic near, string name) where T : class
        {
            var field = typeof(NearColliderLogic).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
            Require(field is not null && field.FieldType == typeof(T) && field.GetValue(near) is T, "native_collection_unobserved");
            return (T)field!.GetValue(near)!;
        }
        private static void Require(bool condition, string reason) { if (!condition) throw new NativeNearSupportException(reason); }
        private static bool Finite(float f) => !float.IsNaN(f) && !float.IsInfinity(f);
        private static bool Finite(Vector3 p) => Finite(p.x) && Finite(p.y) && Finite(p.z);
        private static bool Finite(Quaternion q) => Finite(q.x) && Finite(q.y) && Finite(q.z) && Finite(q.w);
        private static bool Finite(ColliderData d) => Finite(d.pos) && Finite(d.ext) && Finite(d.radius) && d.radius >= 0f && Finite(d.q);
        private static bool Same(ColliderData a, ColliderData b) => Finite(a) && Finite(b) && a.idType == b.idType && a.link == b.link
            && a.pos.Equals(b.pos) && a.ext.Equals(b.ext) && a.radius.Equals(b.radius) && a.q.Equals(b.q);
        private static bool Scene(GameObject o) => o != null && o.scene.IsValid() && o.scene.isLoaded
            && o.scene.GetPhysicsScene().Equals(Physics.defaultPhysicsScene);
        private static Spherewright.Contracts.Factory.Vector3Snapshot Vector(Vector3 p) => new() { X = p.x, Y = p.y, Z = p.z };

        private sealed class NativeNearSupportCell
        {
            internal NativeNearSupportCell(int id, ColliderContainer container, ColliderData[] pool, ColliderData data)
            { Id = id; Container = container; Pool = pool; Data = data; }
            internal int Id { get; }
            internal ColliderContainer Container { get; }
            internal ColliderData[] Pool { get; }
            internal ColliderData Data { get; }
            internal bool Matches(NativeNearSupportCell other) => Id == other.Id && ReferenceEquals(Container, other.Container)
                && ReferenceEquals(Pool, other.Pool) && Same(Data, other.Data);
        }

        private sealed class NativeNearSupportPose
        {
            private readonly Vector3 _position, _scale, _center, _size;
            private readonly Quaternion _rotation;
            private readonly Matrix4x4 _matrix;
            private readonly float _radius, _height;
            private readonly int _layer, _direction;
            private readonly Transform _parent;

            internal NativeNearSupportPose(Collider c, ColliderData data, ColliderPool pool)
            {
                if (c == null) throw new NativeNearSupportException("support_collider_unproved");
                Require(c.enabled && c.gameObject.activeInHierarchy && !c.isTrigger && Scene(c.gameObject), "support_collider_unproved");
                _position = c.transform.position; _rotation = c.transform.rotation; _scale = c.transform.lossyScale;
                _matrix = c.transform.localToWorldMatrix; _parent = c.transform.parent;
                _layer = c.gameObject.layer;
                Require(Finite(_position) && Finite(_rotation) && Finite(_scale) && _scale.x > 0 && _scale.y > 0 && _scale.z > 0
                    && _position.Equals(data.pos) && ReferenceEquals(_parent, pool.transform)
                    && _layer == (data.objType == EObjectType.Vegetable ? 10 : 11), "support_pose_unproved");
                for (var i = 0; i < 16; i++) Require(Finite(_matrix[i]), "support_matrix_nonfinite");
                if (c is CapsuleCollider capsule && data.shape == EColliderShape.Capsule)
                { _center = capsule.center; _radius = capsule.radius; _height = capsule.height; _direction = capsule.direction;
                    Require(_radius == data.radius && _height == (data.radius + data.ext.magnitude) * 2f && _direction == 1, "support_capsule_unproved"); }
                else if (c is SphereCollider sphere && data.shape == EColliderShape.Sphere)
                { _center = sphere.center; _radius = sphere.radius; Require(_radius == data.radius, "support_sphere_unproved"); }
                else if (c is BoxCollider box && data.shape == EColliderShape.Box)
                { _center = box.center; _size = box.size; Require(_size.Equals(data.ext * 2f), "support_box_unproved"); }
                else throw new NativeNearSupportException("support_shape_unproved");
                Require(Finite(_center) && Finite(_radius) && Finite(_height) && Finite(_size), "support_shape_nonfinite");
            }

            internal bool Matches(Collider c, ColliderData data, ColliderPool pool)
            {
                var value = new NativeNearSupportPose(c, data, pool);
                return _position.Equals(value._position) && _rotation.Equals(value._rotation) && _scale.Equals(value._scale)
                    && _matrix.Equals(value._matrix) && _layer == value._layer && ReferenceEquals(_parent, value._parent)
                    && _center.Equals(value._center) && _radius.Equals(value._radius) && _height.Equals(value._height)
                    && _direction == value._direction && _size.Equals(value._size);
            }
        }
    }
}
