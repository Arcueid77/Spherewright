using System.Text.Json;
using Spherewright.Contracts.Actions;
using Spherewright.Contracts.Factory;
using Xunit;

namespace Spherewright.Contracts.Tests;

public sealed class ReadOnlyRouteGeometryContractTests
{
    private static readonly JsonSerializerOptions CamelCase = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
    private const string GeometryHash = "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef";

    [Fact]
    public void NativeSegmentBatchRoundTripsExactLayersPointsAndSlopeWithoutPlacementCredit()
    {
        var segment=Segment(Point(2,200,0),Point(8,200,0));
        segment.RoutingMode=BeltPathModes.NativeElevatedGrid;segment.StartAltitudeLevel=1;segment.EndAltitudeLevel=2;
        var request=new FactoryGeometryReadRequest{ExpectedFactoryGeometryHash=GeometryHash,NativeBeltSegments=new(){segment}};
        var restored=JsonSerializer.Deserialize<FactoryGeometryReadRequest>(JsonSerializer.Serialize(request,CamelCase),CamelCase)!;
        Assert.Equal(GeometryHash,restored.ExpectedFactoryGeometryHash);
        var copied=Assert.Single(restored.NativeBeltSegments!);
        Assert.Equal(Coordinates(segment.Start),Coordinates(copied.Start));
        Assert.Equal(Coordinates(segment.End),Coordinates(copied.End));
        Assert.Equal(1,copied.StartAltitudeLevel);Assert.Equal(2,copied.EndAltitudeLevel);
        var row=new FactoryNativeBeltSegmentSnapshot{Observed=true,Segment=segment,NativeGroundRadius=200.2f,
            NativeMaximumSlope=.25f,Points=new(){Point(2,200,0),Point(5,201,0),Point(8,202,0)}};
        var json=JsonSerializer.Serialize(row,CamelCase);
        var result=JsonSerializer.Deserialize<FactoryNativeBeltSegmentSnapshot>(json,CamelCase)!;
        Assert.Equal("read_only_native_belt_segment_points_v1",result.Phase);
        Assert.False(result.Executable);Assert.False(result.NativeConditionsChecked);
        Assert.Equal(.25f,result.NativeMaximumSlope);Assert.Equal(row.Points.Select(Coordinates),result.Points.Select(Coordinates));
        Assert.DoesNotContain("planToken",json,StringComparison.Ordinal);
        Assert.DoesNotContain("prepared",json,StringComparison.Ordinal);
    }

    [Fact]
    public void LegacyGeometryWithoutSegmentsKeepsOrdinaryReadDefaultsAndNoInventedGeneration()
    {
        var request=JsonSerializer.Deserialize<FactoryGeometryReadRequest>("{\"objectIds\":[41],\"terrainPoints\":[]}",CamelCase)!;
        var result=JsonSerializer.Deserialize<FactoryGeometryCutSnapshot>("{\"state\":\"observed\"}",CamelCase)!;
        Assert.Null(request.NativeBeltSegments);Assert.Empty(result.NativeBeltSegments);
        Assert.False(result.Executable);
    }

    [Fact]
    public void NegativeGenerationRetainsReasonAndNoPartialPointOrConditionClaim()
    {
        var row=new FactoryNativeBeltSegmentSnapshot{Index=3,Observed=false,
            ReasonCode="native_belt_segment_requires_exact_snapped_endpoints",Segment=Segment(Point(2,200,0),Point(8,200,0))};
        var result=JsonSerializer.Deserialize<FactoryNativeBeltSegmentSnapshot>(JsonSerializer.Serialize(row,CamelCase),CamelCase)!;
        Assert.Equal(3,result.Index);Assert.False(result.Observed);Assert.False(result.NativeConditionsChecked);
        Assert.Equal(row.ReasonCode,result.ReasonCode);Assert.Empty(result.Points);
        // A malformed wire claim survives decoding so the policy can reject it.
        var claimed=JsonSerializer.Deserialize<FactoryNativeBeltSegmentSnapshot>("{\"nativeConditionsChecked\":true}",CamelCase)!;
        Assert.True(claimed.NativeConditionsChecked);
    }

    [Fact]
    public void BoundedRouteRequestRoundTripsExplicitSegmentsAndMixedSorterBindings()
    {
        var priorGeometryCut = new FactoryGeometryCutSnapshot
        {
            Phase = "read_only_factory_geometry_cut_v2",
            State = "observed",
            SessionId = "owned-session",
            PlanetId = 104,
            Revision = 21,
            CapturedAtGameTick = 499,
            FactoryGeometryHash = GeometryHash,
            Terrain = new()
            {
                TerrainSnap(0, Point(2.12f, 200, .08f), Point(2, 200, 0)),
                TerrainSnap(1, Point(8.09f, 200, -.06f), Point(8, 200, 0)),
                TerrainSnap(2, Point(14.11f, 200, .04f), Point(14, 200, 0)),
            },
        };
        var snappedStart = priorGeometryCut.Terrain[0].GroundGridSnapPosition!;
        var snappedJoin = priorGeometryCut.Terrain[1].GroundGridSnapPosition!;
        var snappedEnd = priorGeometryCut.Terrain[2].GroundGridSnapPosition!;
        var request = new PrepareBuildRequest
        {
            PlanetId = 104,
            BuildingItemId = 2001,
            BeltPathMode = BeltPathModes.NativeRoutePreview,
            ExpectedPlayerStateHash = "player-state-hash",
            StateHashVersion = 1,
            BeltRoutePreview = new BeltRoutePreviewRequest
            {
                ExpectedFactoryGeometryHash = GeometryHash,
                FilterItemId = 1001,
                SourceSorterItemId = 2011,
                DestinationSorterItemId = 2012,
                Source = Binding(41, 3),
                Destination = Binding(43, 4),
                Segments = new()
                {
                    Segment(snappedStart, snappedJoin),
                    Segment(snappedJoin, snappedEnd),
                },
            },
        };

        var json = JsonSerializer.Serialize(request, CamelCase);
        var restored = JsonSerializer.Deserialize<PrepareBuildRequest>(json, CamelCase)!;
        var geometryJson = JsonSerializer.Serialize(priorGeometryCut, CamelCase);
        var restoredGeometry = JsonSerializer.Deserialize<FactoryGeometryCutSnapshot>(geometryJson, CamelCase)!;
        var route = restored.BeltRoutePreview!;

        Assert.Equal(BeltPathModes.NativeRoutePreview, restored.BeltPathMode);
        Assert.Equal("player-state-hash", restored.ExpectedPlayerStateHash);
        Assert.Equal(GeometryHash, route.ExpectedFactoryGeometryHash);
        Assert.Equal(2011, route.SourceSorterItemId);
        Assert.Equal(2012, route.DestinationSorterItemId);
        Assert.Equal(2, route.Segments.Count);
        Assert.Equal(route.Segments[0].End.X, route.Segments[1].Start.X);
        Assert.Equal(Coordinates(restoredGeometry.Terrain[0].GroundGridSnapPosition!), Coordinates(route.Segments[0].Start));
        Assert.Equal(Coordinates(restoredGeometry.Terrain[1].GroundGridSnapPosition!), Coordinates(route.Segments[0].End));
        Assert.Equal(Coordinates(restoredGeometry.Terrain[1].GroundGridSnapPosition!), Coordinates(route.Segments[1].Start));
        Assert.Equal(Coordinates(restoredGeometry.Terrain[2].GroundGridSnapPosition!), Coordinates(route.Segments[1].End));
        Assert.Equal(41, route.Source.ExistingObjectId);
        Assert.Equal(43, route.Destination.ExistingObjectId);
    }

    [Fact]
    public void ObservedGridSnapReadbackRoundTripsGroundAndAllAltitudeCandidates()
    {
        var observed = TerrainSnap(0, Point(6.11f, 200, 3.08f), Point(6, 200, 3));
        var json = JsonSerializer.Serialize(observed, CamelCase);
        var restored = JsonSerializer.Deserialize<FactoryTerrainPointSnapshot>(json, CamelCase)!;

        Assert.True(restored.Observed);
        Assert.Equal((6f, 200f, 3f), Coordinates(restored.GroundGridSnapPosition!));
        Assert.Equal(4, restored.AltitudeGridSnapPositions.Count);
        Assert.Equal(new[] { 200f, 200.2f, 200.4f, 200.6f }, restored.AltitudeGridSnapPositions.Select(point => point.Y));
        Assert.All(restored.AltitudeGridSnapPositions, point =>
            Assert.All(new[] { point.X, point.Y, point.Z }, value => Assert.True(!float.IsNaN(value) && !float.IsInfinity(value))));
    }

    [Fact]
    public void FailedRouteEchoRoundTripsAsTokenlessNonExecutableNegativeWithCompletePath()
    {
        var result = new PreparedNormalAction
        {
            Prepared = false,
            ActionKind = NormalActionKinds.Build,
            PlanToken = string.Empty,
            ExpectedStateHash = "player-state-hash",
            BuildKind = "belt_route_preview",
            PlannedBeltPath = new BeltPathPlanSnapshot
            {
                NewObjectCount = 3,
                RoutingMode = BeltPathModes.NativeRoutePreview,
                NativeValidationMode = "full_path_stage1_and_joint_all_objects",
                SourceBindingMode = "none",
                DestinationBindingMode = "none",
            },
            BeltRoutePreview = new BeltRoutePreviewSnapshot
            {
                Phase = "read_only_bounded_belt_route_preview_v2",
                FactoryGeometryHash = GeometryHash,
                NativePathCheckPerformed = true,
                NativePathCheckPassed = false,
                JointNativeCheckPerformed = false,
                JointNativeCheckPassed = false,
                FailedBeltIndex = 1,
                FailureStage = "native_full_path",
                FailureCondition = "native_collision",
                Segments = new()
                {
                    new NativeBeltRouteSegment
                    {
                        Index = 0, RoutingMode = BeltPathModes.NativeGrid, FirstBeltIndex = 0,
                        LastBeltIndex = 2, NativePointCount = 3,
                    },
                },
                Points = new()
                {
                    RoutePoint(0, 2), RoutePoint(1, 5, "Collision"), RoutePoint(2, 8),
                },
                Endpoints = new BeltEndpointPreviewSnapshot
                {
                    Phase = "read_only_belt_endpoint_preview",
                    SessionId = "owned-session",
                    PlanetId = 104,
                    Revision = 4,
                    CapturedAtGameTick = 500,
                    NativeCheckPerformed = false,
                    NativeCheckPassed = false,
                    AssessmentHash = "assessment-hash",
                    Attachments = new(),
                    Blockers = new() { "native_route_collision" },
                },
            },
        };
        result.PlannedPath.AddRange(new[] { Point(2, 200, 0), Point(5, 200, 0), Point(8, 200, 0) });
        result.ItemBudget.Add(new ActionItemBudget
        {
            ItemId = 2001, Count = 3, Direction = "preview-construction-consumption",
        });

        var json = JsonSerializer.Serialize(result, CamelCase);
        var restored = JsonSerializer.Deserialize<PreparedNormalAction>(json, CamelCase)!;
        var route = restored.BeltRoutePreview!;

        Assert.False(restored.Prepared);
        Assert.False(restored.CommitAllowedNow);
        Assert.Empty(restored.PlanToken);
        Assert.False(route.Executable);
        Assert.False(route.NativePathCheckPassed);
        Assert.False(route.JointNativeCheckPassed);
        Assert.Equal(1, route.FailedBeltIndex);
        Assert.Equal("native_full_path", route.FailureStage);
        Assert.Equal("native_collision", route.FailureCondition);
        Assert.Equal(3, route.Points.Count);
        Assert.Single(route.Segments);
        Assert.Empty(route.Endpoints.Attachments);
        Assert.Equal(3, restored.PlannedPath.Count);
    }

    [Fact]
    public void RuntimeBeltColliderWorldBoundsSourceRoundTripsSeparatelyFromEntityPose()
    {
        var cut = new FactoryGeometryCutSnapshot
        {
            Phase = "read_only_factory_geometry_cut_v2",
            State = "observed",
            SessionId = "owned-session",
            PlanetId = 104,
            Revision = 23,
            CapturedAtGameTick = 7900,
            FactoryGeometryHash = GeometryHash,
            BuiltObjectCount = 6492,
            PrebuildObjectCount = 0,
            Objects = new()
            {
                new FactoryObjectGeometrySnapshot
                {
                    ObjectId = 41,
                    ItemId = 2001,
                    Name = "belt",
                    Observed = true,
                    Position = Point(10, 200, 0),
                    Position2 = Point(11, 200, 0),
                    Rotation = IdentityRotation(),
                    Rotation2 = IdentityRotation(),
                    GeometrySource = "native_runtime_belt_collider_world_bounds",
                    NativeHasBuildCollider = true,
                    Colliders = new()
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
                    },
                },
            },
        };

        var restored = JsonSerializer.Deserialize<FactoryGeometryCutSnapshot>(
            JsonSerializer.Serialize(cut, CamelCase), CamelCase)!;
        var belt = Assert.Single(restored.Objects);
        var collider = Assert.Single(belt.Colliders);

        Assert.Equal("native_runtime_belt_collider_world_bounds", belt.GeometrySource);
        Assert.NotEqual(belt.Position.X, collider.Centre.X);
        Assert.NotEqual(Math.Abs(belt.Position2.X - belt.Position.X) / 2, collider.HalfExtents.X);
        Assert.Equal((15f, 200f, 0f), Coordinates(collider.Centre));
        Assert.Equal((4f, .5f, .5f), Coordinates(collider.HalfExtents));
    }

    [Fact]
    public void FactoryGeometryCutRoundTripsSignedIdsBoundsUnknownsAndConservativeFlag()
    {
        var request = new InspectFactoryEntityRequest
        {
            PlanetId = 104,
            ObjectId = 41,
            Geometry = new FactoryGeometryReadRequest
            {
                ObjectIds = new() { -41, 42 },
                TerrainPoints = new() { Point(20, 200, 0) },
                ExpectedFactoryGeometryHash = GeometryHash,
            },
        };
        var entity = new FactoryEntitySnapshot
        {
            SessionId = "owned-session",
            PlanetId = 104,
            ObjectId = 41,
            ItemId = 2011,
            GeometryCut = new FactoryGeometryCutSnapshot
            {
                Phase = "read_only_factory_geometry_cut_v2",
                State = "unavailable",
                SessionId = "owned-session",
                PlanetId = 104,
                Revision = 5,
                CapturedAtGameTick = 600,
                FactoryGeometryHash = GeometryHash,
                BuiltObjectCount = 6492,
                PrebuildObjectCount = 0,
                Objects = new()
                {
                    new FactoryObjectGeometrySnapshot
                    {
                        ObjectId = -41, ItemId = 2011, Name = "belt", Observed = false,
                        ReasonCode = "native_geometry_unavailable",
                        Colliders = new()
                        {
                            new FactoryBuildColliderSnapshot
                            {
                                NativeShape = "Sphere", ConservativeEnclosure = true,
                                Centre = Point(20, 200, 0), AxisX = Point(1, 0, 0),
                                AxisY = Point(0, 1, 0), AxisZ = Point(0, 0, 1),
                                HalfExtents = Point(.5f, .5f, .5f), PaddingMetres = .01f,
                            },
                        },
                        Connections = new(),
                    },
                    new FactoryObjectGeometrySnapshot
                    {
                        ObjectId = 42, ItemId = 2001, Name = "belt", Observed = true,
                        Position = Point(22, 200, 0), Position2 = Point(23, 200, 0),
                    Rotation = IdentityRotation(), Rotation2 = IdentityRotation(), Tilt = 0,
                        Colliders = new(), Connections = new(),
                    },
                },
                Terrain = new()
                {
                    new FactoryTerrainPointSnapshot
                    {
                        Index = 0, RequestedPosition = Point(20, 200, 0), Observed = false,
                        ReasonCode = "terrain_unavailable",
                    },
                },
                Blockers = new() { "partial_native_geometry" },
            },
        };

        var requestJson = JsonSerializer.Serialize(request, CamelCase);
        var entityJson = JsonSerializer.Serialize(entity, CamelCase);
        var restoredRequest = JsonSerializer.Deserialize<InspectFactoryEntityRequest>(requestJson, CamelCase)!;
        var restoredEntity = JsonSerializer.Deserialize<FactoryEntitySnapshot>(entityJson, CamelCase)!;

        Assert.Equal(new[] { -41, 42 }, restoredRequest.Geometry!.ObjectIds);
        Assert.Equal(GeometryHash, restoredRequest.Geometry.ExpectedFactoryGeometryHash);
        Assert.False(restoredEntity.GeometryCut!.Executable);
        Assert.Equal("unavailable", restoredEntity.GeometryCut.State);
        Assert.False(restoredEntity.GeometryCut.Objects[0].Observed);
        Assert.True(restoredEntity.GeometryCut.Objects[0].Colliders[0].ConservativeEnclosure);
        Assert.Equal("native_geometry_unavailable", restoredEntity.GeometryCut.Objects[0].ReasonCode);
        Assert.Equal("partial_native_geometry", Assert.Single(restoredEntity.GeometryCut.Blockers));
    }

    [Fact]
    public void OldPayloadWithoutOptInGeometryRemainsUnknownAndNonExecutable()
    {
        var oldRequest = JsonSerializer.Deserialize<InspectFactoryEntityRequest>(
            "{\"planetId\":104,\"objectId\":41}", CamelCase)!;
        var oldEntity = JsonSerializer.Deserialize<FactoryEntitySnapshot>(
            "{\"sessionId\":\"owned-session\",\"planetId\":104,\"objectId\":41,\"itemId\":2011}", CamelCase)!;
        var partialRoute = JsonSerializer.Deserialize<PreparedNormalAction>(
            "{\"beltRoutePreview\":{\"phase\":\"read_only_bounded_belt_route_preview_v1\",\"jointNativeCheckPassed\":true}}",
            CamelCase)!;

        Assert.Null(oldRequest.Geometry);
        Assert.Null(oldEntity.GeometryCut);
        Assert.False(partialRoute.Prepared);
        Assert.False(partialRoute.CommitAllowedNow);
        Assert.Empty(partialRoute.PlanToken);
        Assert.False(partialRoute.BeltRoutePreview!.Executable);
        Assert.Empty(partialRoute.BeltRoutePreview.Points);
        Assert.Empty(partialRoute.BeltRoutePreview.Segments);
        Assert.False(partialRoute.BeltRoutePreview.NativePathCheckPerformed);
        Assert.False(partialRoute.BeltRoutePreview.NativePathCheckPassed);
        Assert.Empty(partialRoute.BeltRoutePreview.FactoryGeometryHash);
        Assert.Empty(partialRoute.BeltRoutePreview.Endpoints.SessionId);
    }

    private static PlannedBeltEndpointBinding Binding(int id, int slot) => new()
    {
        ExistingObjectId = id,
        ExpectedEndpointStateHash = $"endpoint-{id}",
        ExistingSlot = slot,
        ExistingBeltQuarterTurns = 0,
        PlannedBeltQuarterTurns = 1,
    };

    private static BeltRouteSegment Segment(Vector3Snapshot start, Vector3Snapshot end) => new()
    {
        RoutingMode = BeltPathModes.NativeGrid,
        Start = start,
        End = end,
    };

    private static NativeBeltRoutePoint RoutePoint(int index, float x, string condition = "Ok") => new()
    {
        Index = index,
        Position = Point(x, 200, 0),
        PlannedPosition = Point(x, 200, 0),
        Rotation = IdentityRotation(),
        Tilt = 0,
        NativeCondition = condition,
    };

    private static QuaternionSnapshot IdentityRotation() => new() { W = 1 };

    private static FactoryTerrainPointSnapshot TerrainSnap(int index, Vector3Snapshot requested, Vector3Snapshot ground) => new()
    {
        Index = index,
        RequestedPosition = requested,
        Observed = true,
        GroundRayHit = true,
        GroundRadius = 201,
        NativeModifiedSurfaceRadius = 201,
        MaximumNeighbourGradient = .1,
        GroundGridSnapPosition = ground,
        AltitudeGridSnapPositions = Enumerable.Range(0, 4)
            .Select(level => Point(ground.X, ground.Y + level * .2f, ground.Z)).ToList(),
    };

    private static (float X, float Y, float Z) Coordinates(Vector3Snapshot point) => (point.X, point.Y, point.Z);

    private static Vector3Snapshot Point(float x, float y, float z) => new() { X = x, Y = y, Z = z };
}
