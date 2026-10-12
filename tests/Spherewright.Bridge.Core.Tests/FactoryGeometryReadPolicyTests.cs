using Spherewright.Bridge.Core.Factory;
using Spherewright.Bridge.Core.Safety;
using Spherewright.Contracts.Factory;
using Xunit;

namespace Spherewright.Bridge.Core.Tests;

public sealed class FactoryGeometryReadPolicyTests
{
    private const string Hash = "abcdef0123456789abcdef0123456789abcdef0123456789abcdef0123456789";

    [Fact]
    public void AcceptsSignedUniqueObjectIdsAndMaximumBoundedRequest()
    {
        var request = new FactoryGeometryReadRequest
        {
            ObjectIds = Enumerable.Range(1, FactoryGeometryReadPolicy.MaximumObjects).Select(id => -id).ToList(),
            TerrainPoints = Enumerable.Range(0, FactoryGeometryReadPolicy.MaximumTerrainPoints)
                .Select(i => Point(i + 1, 200, 0)).ToList(),
            ExpectedFactoryGeometryHash = Hash,
        };

        Assert.Null(FactoryGeometryReadPolicy.ValidateRequest(request));
        var cut = CompleteCut(request);
        Assert.True(FactoryGeometryReadPolicy.ConfirmsReadOnlyEcho(request, cut, "owned-session", 104));
    }

    [Theory]
    [InlineData("empty")]
    [InlineData("too_many_objects")]
    [InlineData("too_many_terrain_points")]
    [InlineData("duplicate_object_id")]
    [InlineData("zero_object_id")]
    [InlineData("min_int_object_id")]
    [InlineData("positive_slot_overflow")]
    [InlineData("negative_slot_overflow")]
    [InlineData("malformed_hash")]
    [InlineData("null_object_list")]
    [InlineData("null_terrain_list")]
    [InlineData("invalid_terrain_point")]
    public void RejectsInvalidBoundsAndAmbiguousRequestIdentity(string fault)
    {
        var request = new FactoryGeometryReadRequest { ObjectIds = new() { 7 } };
        switch (fault)
        {
            case "empty": request.ObjectIds.Clear(); break;
            case "too_many_objects": request.ObjectIds = Enumerable.Range(1, 257).ToList(); break;
            case "too_many_terrain_points": request.TerrainPoints = Enumerable.Range(1, 65).Select(i => Point(i, 200, 0)).ToList(); break;
            case "duplicate_object_id": request.ObjectIds = new() { -7, 7, -7 }; break;
            case "zero_object_id": request.ObjectIds = new() { 0 }; break;
            case "min_int_object_id": request.ObjectIds = new() { int.MinValue }; break;
            case "positive_slot_overflow": request.ObjectIds = new() { 65536 }; break;
            case "negative_slot_overflow": request.ObjectIds = new() { -65536 }; break;
            case "malformed_hash": request.ExpectedFactoryGeometryHash = "hash"; break;
            case "null_object_list": request.ObjectIds = null!; break;
            case "null_terrain_list": request.TerrainPoints = null!; break;
            case "invalid_terrain_point": request.TerrainPoints = new() { Point(0, 0, 0) }; break;
        }

        Assert.NotNull(FactoryGeometryReadPolicy.ValidateRequest(request));
    }

    [Fact]
    public void AcceptsOnlyExactCompleteIdentityAndOrderedGeometryEcho()
    {
        var request = Request();
        var cut = CompleteCut(request);

        Assert.True(FactoryGeometryReadPolicy.ConfirmsReadOnlyEcho(request, cut, "owned-session", 104));
        Assert.False(cut.Executable);
        Assert.Equal("observed", cut.State);
        Assert.Equal(request.ObjectIds, cut.Objects.Select(row => row.ObjectId));
        Assert.Equal(request.TerrainPoints.Count, cut.Terrain.Count);
        Assert.Empty(cut.Blockers);

        var staleLayout = Request();
        staleLayout.ExpectedFactoryGeometryHash = Hash.Replace('a', 'b');
        Assert.False(FactoryGeometryReadPolicy.ConfirmsReadOnlyEcho(staleLayout, cut, "owned-session", 104));
    }

    [Fact]
    public void AcceptsTheMaximumSixteenUniqueNativeConnectionSlots()
    {
        var request = Request();
        var cut = CompleteCut(request);
        cut.Objects[0].Connections = Enumerable.Range(0, 16)
            .Select(slot => new FactoryConnectionSnapshot
            {
                Slot = slot,
                IsOutput = slot % 2 == 0,
                OtherObjectId = 100 + slot,
                OtherSlot = 0,
            })
            .ToList();

        Assert.True(FactoryGeometryReadPolicy.ConfirmsReadOnlyEcho(request, cut, "owned-session", 104));
        Assert.Equal(16, cut.Objects[0].Connections.Count);
    }

    [Fact]
    public void AcceptsObservedRuntimeBeltColliderBoundsThatDifferFromEntityPose()
    {
        var request = Request();
        request.ObjectIds = new() { 42 };
        var cut = CompleteCut(request);
        var belt = cut.Objects[0];
        belt.GeometrySource = "native_runtime_belt_collider_world_bounds";
        belt.NativeHasBuildCollider = true;
        belt.Position = Point(10, 200, 0);
        belt.Position2 = Point(11, 200, 0);
        belt.Colliders = new()
        {
            new FactoryBuildColliderSnapshot
            {
                NativeShape = "Box",
                Centre = Point(15, 200, 0),
                AxisX = Point(1, 0, 0),
                AxisY = Point(0, 1, 0),
                AxisZ = Point(0, 0, 1),
                HalfExtents = Point(4, .5f, .5f),
            },
        };

        Assert.True(FactoryGeometryReadPolicy.ConfirmsReadOnlyEcho(request, cut, "owned-session", 104));
        Assert.Equal("native_runtime_belt_collider_world_bounds", belt.GeometrySource);
        Assert.NotEqual(belt.Position.X, belt.Colliders[0].Centre.X);
        Assert.NotEqual(Math.Abs(belt.Position2.X - belt.Position.X) / 2, belt.Colliders[0].HalfExtents.X);
        Assert.Equal(15, belt.Colliders[0].Centre.X);
        Assert.Equal(4, belt.Colliders[0].HalfExtents.X);
    }

    [Theory]
    [InlineData(2001)]
    [InlineData(2002)]
    [InlineData(2003)]
    public void RequiresRuntimeColliderWorldBoundsForObservedBuiltBelts(int beltItemId)
    {
        var request = Request();
        request.ObjectIds = new() { 42 };
        var cut = CompleteCut(request);
        cut.Objects[0].ItemId = beltItemId;
        cut.Objects[0].GeometrySource = "native_build_collider_world_bounds";

        Assert.False(FactoryGeometryReadPolicy.ConfirmsReadOnlyEcho(request, cut, "owned-session", 104));
    }

    [Theory]
    [InlineData("nonfinite_runtime_centre")]
    [InlineData("nonunit_runtime_axis")]
    [InlineData("nonpositive_runtime_extent")]
    public void RejectsInvalidRuntimeBeltColliderWorldBounds(string fault)
    {
        var request = Request();
        request.ObjectIds = new() { 42 };
        var cut = CompleteCut(request);
        var collider = cut.Objects[0].Colliders[0];
        switch (fault)
        {
            case "nonfinite_runtime_centre": collider.Centre.X = float.NaN; break;
            case "nonunit_runtime_axis": collider.AxisX.X = 2; break;
            case "nonpositive_runtime_extent": collider.HalfExtents.X = 0; break;
        }

        Assert.False(FactoryGeometryReadPolicy.ConfirmsReadOnlyEcho(request, cut, "owned-session", 104));
    }

    [Theory]
    [InlineData("phase")]
    [InlineData("old_v1_phase")]
    [InlineData("session")]
    [InlineData("planet")]
    [InlineData("negative_revision")]
    [InlineData("negative_tick")]
    [InlineData("bad_hash")]
    [InlineData("missing_object")]
    [InlineData("object_order")]
    [InlineData("object_id")]
    [InlineData("negative_built_count")]
    [InlineData("negative_prebuild_count")]
    [InlineData("terrain_index")]
    [InlineData("terrain_position")]
    [InlineData("null_connections")]
    [InlineData("null_colliders")]
    [InlineData("too_many_colliders")]
    [InlineData("observed_with_unknown")]
    [InlineData("unknown_geometry_source")]
    [InlineData("negative_prebuild_runtime_source")]
    [InlineData("missing_runtime_collider")]
    [InlineData("extra_runtime_collider")]
    [InlineData("missing_ground_grid_snap")]
    [InlineData("nonfinite_ground_grid_snap")]
    [InlineData("missing_altitude_grid_snaps")]
    [InlineData("nonfinite_altitude_grid_snap")]
    [InlineData("nan_rotation")]
    [InlineData("nonunit_rotation")]
    [InlineData("nan_rotation2")]
    [InlineData("nonunit_rotation2")]
    [InlineData("nan_tilt")]
    [InlineData("too_many_connections")]
    [InlineData("duplicate_connection_slot")]
    [InlineData("invalid_connection_slot")]
    public void RejectsTamperedOrIncompleteEchoes(string fault)
    {
        var request = Request();
        var cut = CompleteCut(request);
        switch (fault)
        {
            case "phase": cut.Phase = "normal_build"; break;
            case "old_v1_phase": cut.Phase = "read_only_factory_geometry_cut_v1"; break;
            case "session": cut.SessionId = "other-session"; break;
            case "planet": cut.PlanetId = 103; break;
            case "negative_revision": cut.Revision = -1; break;
            case "negative_tick": cut.CapturedAtGameTick = -1; break;
            case "bad_hash": cut.FactoryGeometryHash = "not-a-sha256"; break;
            case "missing_object": cut.Objects.RemoveAt(0); break;
            case "object_order": cut.Objects.Reverse(); break;
            case "object_id": cut.Objects[0].ObjectId++; break;
            case "negative_built_count": cut.BuiltObjectCount = -1; break;
            case "negative_prebuild_count": cut.PrebuildObjectCount = -1; break;
            case "terrain_index": cut.Terrain[0].Index = 1; break;
            case "terrain_position": cut.Terrain[0].RequestedPosition.X += 1; break;
            case "null_connections": cut.Objects[0].Connections = null!; break;
            case "null_colliders": cut.Objects[0].Colliders = null!; break;
            case "too_many_colliders":
                cut.Objects[0].Colliders = Enumerable.Range(0, FactoryGeometryReadPolicy.MaximumColliders + 1)
                    .Select(_ => Collider(conservative: false)).ToList();
                break;
            case "observed_with_unknown":
                cut.Objects[0].Observed = false;
                cut.Objects[0].ReasonCode = "native_geometry_unavailable";
                break;
            case "unknown_geometry_source": cut.Objects[1].GeometrySource = "legacy_prefab_pose_bounds"; break;
            case "negative_prebuild_runtime_source":
                cut.Objects[0].GeometrySource = "native_runtime_belt_collider_world_bounds";
                cut.Objects[0].NativeHasBuildCollider = true;
                break;
            case "missing_runtime_collider":
                cut.Objects[1].GeometrySource = "native_runtime_belt_collider_world_bounds";
                cut.Objects[1].NativeHasBuildCollider = true;
                cut.Objects[1].Colliders.Clear();
                break;
            case "extra_runtime_collider":
                cut.Objects[1].GeometrySource = "native_runtime_belt_collider_world_bounds";
                cut.Objects[1].NativeHasBuildCollider = true;
                cut.Objects[1].Colliders.Add(Collider(conservative: false));
                break;
            case "missing_ground_grid_snap": cut.Terrain[0].GroundGridSnapPosition = null; break;
            case "nonfinite_ground_grid_snap": cut.Terrain[0].GroundGridSnapPosition!.X = float.NaN; break;
            case "missing_altitude_grid_snaps": cut.Terrain[0].AltitudeGridSnapPositions.RemoveAt(0); break;
            case "nonfinite_altitude_grid_snap": cut.Terrain[0].AltitudeGridSnapPositions[2].Z = float.PositiveInfinity; break;
            case "nan_rotation": cut.Objects[0].Rotation.X = float.NaN; break;
            case "nonunit_rotation": cut.Objects[0].Rotation.W = 2; break;
            case "nan_rotation2": cut.Objects[0].Rotation2.Z = float.NaN; break;
            case "nonunit_rotation2": cut.Objects[0].Rotation2.W = 2; break;
            case "nan_tilt": cut.Objects[0].Tilt = float.NaN; break;
            case "too_many_connections":
                cut.Objects[0].Connections = Enumerable.Range(0, 17)
                    .Select(slot => new FactoryConnectionSnapshot { Slot = slot % 16, OtherObjectId = 100 + slot })
                    .ToList();
                break;
            case "duplicate_connection_slot":
                cut.Objects[0].Connections = new()
                {
                    new FactoryConnectionSnapshot { Slot = 3, OtherObjectId = 100 },
                    new FactoryConnectionSnapshot { Slot = 3, OtherObjectId = 101 },
                };
                break;
            case "invalid_connection_slot": cut.Objects[0].Connections[0].Slot = 16; break;
        }

        Assert.False(FactoryGeometryReadPolicy.ConfirmsReadOnlyEcho(request, cut, "owned-session", 104));
    }

    [Fact]
    public void PreservesUnavailableAndConservativeGeometryAsReadEvidenceNotClearance()
    {
        var request = Request();
        var cut = CompleteCut(request);
        var objectGeometry = cut.Objects[0];
        objectGeometry.Observed = false;
        objectGeometry.ReasonCode = "native_shape_is_conservative";
        objectGeometry.NativeHasBuildCollider = true;
        objectGeometry.Colliders.Add(Collider(conservative: true));
        cut.State = "unavailable";
        cut.Blockers.Add("geometry_is_incomplete");

        Assert.True(FactoryGeometryReadPolicy.ConfirmsReadOnlyEcho(request, cut, "owned-session", 104));
        Assert.Equal("unavailable", cut.State);
        Assert.False(objectGeometry.Observed);
        Assert.True(objectGeometry.NativeHasBuildCollider);
        Assert.Contains(objectGeometry.Colliders, collider => collider.ConservativeEnclosure);
        Assert.False(cut.Executable);
        Assert.Null(typeof(FactoryGeometryCutSnapshot).GetProperty("NativePathCheckPassed"));
        Assert.Null(typeof(FactoryGeometryCutSnapshot).GetProperty("ClearForBuild"));
    }

    [Fact]
    public void UnobservedTerrainKeepsGridSnapCandidatesUnknown()
    {
        var request = Request();
        var cut = CompleteCut(request);
        var terrain = cut.Terrain[0];
        terrain.Observed = false;
        terrain.ReasonCode = "terrain_grid_snap_unavailable";
        terrain.GroundRayHit = false;
        terrain.GroundRadius = null;
        terrain.NativeModifiedSurfaceRadius = null;
        terrain.MaximumNeighbourGradient = null;
        terrain.GroundGridSnapPosition = null;
        terrain.AltitudeGridSnapPositions.Clear();
        cut.State = "unavailable";
        cut.Blockers.Add("terrain_grid_snap_unavailable");

        Assert.True(FactoryGeometryReadPolicy.ConfirmsReadOnlyEcho(request, cut, "owned-session", 104));
        Assert.Equal("unavailable", cut.State);
        Assert.False(terrain.Observed);
        Assert.Null(terrain.GroundGridSnapPosition);
        Assert.Empty(terrain.AltitudeGridSnapPositions);
        Assert.False(cut.Executable);
    }

    [Fact]
    public void ObservedTerrainEchoContainsFiniteGroundAndFourAltitudeGridSnaps()
    {
        var request = Request();
        var cut = CompleteCut(request);
        var terrain = cut.Terrain[0];

        Assert.True(FactoryGeometryReadPolicy.ConfirmsReadOnlyEcho(request, cut, "owned-session", 104));
        Assert.True(terrain.Observed);
        Assert.NotNull(terrain.GroundGridSnapPosition);
        Assert.True(FactoryGeometryReadPolicy.ValidPoint(terrain.GroundGridSnapPosition));
        Assert.Equal(4, terrain.AltitudeGridSnapPositions.Count);
        Assert.All(terrain.AltitudeGridSnapPositions, point => Assert.True(FactoryGeometryReadPolicy.ValidPoint(point)));
    }

    [Fact]
    public void OptionalGeometryObservationDoesNotChangeActionConfigurationOrEndpointHashes()
    {
        var request = Request();
        var entity = new FactoryEntitySnapshot
        {
            SessionId = "owned-session",
            PlanetId = 104,
            ObjectId = 41,
            ItemId = 2011,
            ObjectKind = "entity",
            ComponentKind = "belt",
            Position = Point(20, 200, 0),
            Connections = new() { new FactoryConnectionSnapshot { Slot = 0, IsOutput = true, OtherObjectId = 42, OtherSlot = 1 } },
        };
        var hashesBefore = Hashes(entity);
        entity.GeometryCut = CompleteCut(request);

        Assert.Equal(hashesBefore, Hashes(entity));
    }

    private static string[] Hashes(FactoryEntitySnapshot entity) => new[]
    {
        CanonicalStateHash.Factory(entity),
        CanonicalStateHash.FactoryConfiguration(entity),
        CanonicalStateHash.FactoryEndpoint(entity),
    };

    private static FactoryGeometryReadRequest Request() => new()
    {
        ObjectIds = new() { -41, 42 },
        TerrainPoints = new() { Point(20, 200, 0), Point(22, 200, 0) },
        ExpectedFactoryGeometryHash = Hash,
    };

    private static FactoryGeometryCutSnapshot CompleteCut(FactoryGeometryReadRequest request) => new()
    {
        Phase = "read_only_factory_geometry_cut_v2",
        State = "observed",
        SessionId = "owned-session",
        PlanetId = 104,
        Revision = 22,
        CapturedAtGameTick = 7800,
        FactoryGeometryHash = Hash,
        BuiltObjectCount = 6492,
        PrebuildObjectCount = 0,
        Objects = request.ObjectIds.Select(id => new FactoryObjectGeometrySnapshot
        {
            ObjectId = id,
            ItemId = 2001,
            Name = "belt",
            GeometrySource = id > 0
                ? "native_runtime_belt_collider_world_bounds"
                : "native_build_collider_world_bounds",
            NativeHasBuildCollider = true,
            Observed = true,
            Position = Point(Math.Abs(id), 200, 0),
            Rotation = IdentityRotation(),
            Position2 = Point(Math.Abs(id) + 1, 200, 0),
            Rotation2 = IdentityRotation(),
            Tilt = 0,
            Colliders = new() { Collider(conservative: false) },
            Connections = new() { new FactoryConnectionSnapshot { Slot = 0, IsOutput = true, OtherObjectId = 43, OtherSlot = 1 } },
        }).ToList(),
        Terrain = request.TerrainPoints.Select((point, index) => new FactoryTerrainPointSnapshot
        {
            Index = index,
            RequestedPosition = new Vector3Snapshot { X = point.X, Y = point.Y, Z = point.Z },
            Observed = true,
            GroundRayHit = true,
            GroundRadius = 201,
            NativeModifiedSurfaceRadius = 201,
            MaximumNeighbourGradient = .1,
            WaterRayHit = false,
            GroundGridSnapPosition = Point(MathF.Round(point.X), point.Y, MathF.Round(point.Z)),
            AltitudeGridSnapPositions = Enumerable.Range(0, 4)
                .Select(level => Point(MathF.Round(point.X), point.Y + level * .2f, MathF.Round(point.Z)))
                .ToList(),
        }).ToList(),
    };

    private static FactoryBuildColliderSnapshot Collider(bool conservative) => new()
    {
        NativeShape = conservative ? "Sphere" : "Box",
        ConservativeEnclosure = conservative,
        Centre = Point(20, 200, 0),
        AxisX = Point(1, 0, 0),
        AxisY = Point(0, 1, 0),
        AxisZ = Point(0, 0, 1),
        HalfExtents = Point(.5f, .5f, .5f),
        PaddingMetres = conservative ? .01f : 0,
    };

    private static QuaternionSnapshot IdentityRotation() => new() { W = 1 };

    private static Vector3Snapshot Point(float x, float y, float z) => new() { X = x, Y = y, Z = z };
}
