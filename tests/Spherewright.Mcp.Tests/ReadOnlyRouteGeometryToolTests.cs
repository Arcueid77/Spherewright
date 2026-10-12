using System.Reflection;
using System.Text.Json;
using Spherewright.Bridge.Core.Factory;
using Spherewright.Contracts.Actions;
using Spherewright.Contracts.Errors;
using Spherewright.Contracts.Factory;
using Spherewright.Mcp.BridgeClient;
using Spherewright.Mcp.Tools;
using Xunit;

namespace Spherewright.Mcp.Tests;

public sealed class ReadOnlyRouteGeometryToolTests
{
    private const string GeometryHash = "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef";

    [Fact]
    public async Task InvalidRouteRequestIsRejectedBeforeAnyBridgeCall()
    {
        var bridge = NewBridge(SuccessfulResult);
        var request = Request();
        request.Segments.Clear();

        var result = await SpherewrightTools.PrepareBuildAsync(
            bridge, "owned-session", 104, 2001, "player-state", beltRoutePreview: request);

        Assert.True(result.IsError);
        Assert.Empty(Proxy(bridge).Calls);
        Assert.Contains("belt_route_preview_segment_limit", result.StructuredContent!.Value.GetRawText());
    }

    [Theory]
    [InlineData("source")]
    [InlineData("destination")]
    [InlineData("resource")]
    public async Task NegativeLegacyBindingsRemainPresentAndAreRejectedBeforeAnyBridgeCall(string bindingKind)
    {
        var bridge = NewBridge(SuccessfulResult);
        var request = Request();
        var result = bindingKind switch
        {
            "source" => await SpherewrightTools.PrepareBuildAsync(
                bridge, "owned-session", 104, 2001, "player-state", sourceObjectId: -41, beltRoutePreview: request),
            "destination" => await SpherewrightTools.PrepareBuildAsync(
                bridge, "owned-session", 104, 2001, "player-state", destinationObjectId: -43, beltRoutePreview: request),
            "resource" => await SpherewrightTools.PrepareBuildAsync(
                bridge, "owned-session", 104, 2001, "player-state", resourceNodeId: -77, beltRoutePreview: request),
            _ => throw new InvalidOperationException("Unexpected binding kind."),
        };

        Assert.True(result.IsError);
        Assert.Equal(BridgeErrorCodes.InvalidRequest,
            result.StructuredContent!.Value.GetProperty("error").GetProperty("code").GetString());
        Assert.Empty(Proxy(bridge).Calls);
    }

    [Theory]
    [InlineData("legacy_token")]
    [InlineData("missing_route_echo")]
    public async Task LegacyOrIncompletePluginResponseFailsClosed(string responseKind)
    {
        var bridge = NewBridge(request => responseKind switch
        {
            "legacy_token" => LegacyPreparedToken(),
            "missing_route_echo" => MissingRouteEcho(request),
            _ => throw new InvalidOperationException("Unexpected fixture."),
        });

        var result = await SpherewrightTools.PrepareBuildAsync(
            bridge, "owned-session", 104, 2001, "player-state", beltRoutePreview: Request());

        Assert.True(result.IsError);
        var content = result.StructuredContent!.Value;
        Assert.Equal(BridgeErrorCodes.BridgeNotReady, content.GetProperty("error").GetProperty("code").GetString());
        Assert.Equal(JsonValueKind.Null, content.GetProperty("result").ValueKind);
        Assert.DoesNotContain("legacy-token", content.GetRawText());
        Assert.DoesNotContain("planToken", content.GetRawText());
        Assert.Single(Proxy(bridge).Calls);
        Assert.Equal("PrepareBuildAsync", Proxy(bridge).Calls[0]);
    }

    [Fact]
    public async Task CompleteReadOnlyRouteEchoIsAcceptedWithoutConstructionCapability()
    {
        var bridge = NewBridge(SuccessfulResult);
        var request = Request();

        var result = await SpherewrightTools.PrepareBuildAsync(
            bridge, "owned-session", 104, 2001, "player-state", beltRoutePreview: request);

        Assert.False(result.IsError);
        var content = result.StructuredContent!.Value;
        var prepared = content.GetProperty("result");
        Assert.False(prepared.GetProperty("prepared").GetBoolean());
        Assert.False(prepared.GetProperty("commitAllowedNow").GetBoolean());
        Assert.Equal(string.Empty, prepared.GetProperty("planToken").GetString());
        var preview = prepared.GetProperty("beltRoutePreview");
        Assert.False(preview.GetProperty("executable").GetBoolean());
        Assert.True(preview.GetProperty("nativePathCheckPassed").GetBoolean());
        Assert.True(preview.GetProperty("jointNativeCheckPassed").GetBoolean());
        Assert.Equal(GeometryHash, preview.GetProperty("factoryGeometryHash").GetString());
        Assert.Equal(5, preview.GetProperty("points").GetArrayLength());

        var proxy = Proxy(bridge);
        Assert.Single(proxy.Calls);
        Assert.Equal("PrepareBuildAsync", proxy.Calls[0]);
        Assert.NotNull(proxy.LastRequest);
        Assert.Equal(BeltPathModes.NativeRoutePreview, proxy.LastRequest!.BeltPathMode);
        Assert.Same(request, proxy.LastRequest.BeltRoutePreview);
    }

    [Fact]
    public async Task InvalidGeometryRequestIsRejectedBeforeAnyBridgeCall()
    {
        var bridge = NewGeometryBridge(request => GeometryEntity(request, "observed"));
        var request = GeometryRequest();
        request.ObjectIds.Add(request.ObjectIds[0]);

        var result = await SpherewrightTools.InspectFactoryEntityAsync(
            bridge, "owned-session", 104, 41, geometry: request);

        Assert.True(result.IsError);
        Assert.Equal(BridgeErrorCodes.InvalidRequest,
            result.StructuredContent!.Value.GetProperty("error").GetProperty("code").GetString());
        Assert.Contains("factory_geometry_request_bounds_invalid", result.StructuredContent.Value.GetRawText());
        Assert.Empty(Proxy(bridge).Calls);
        Assert.Null(Proxy(bridge).LastInspectRequest);
    }

    [Theory]
    [InlineData("missing_cut")]
    [InlineData("incomplete_cut")]
    public async Task MissingOrIncompleteGeometryEchoFailsClosed(string responseKind)
    {
        var bridge = NewGeometryBridge(request =>
        {
            var entity = GeometryEntity(request, "observed");
            if (responseKind == "missing_cut")
            {
                entity.GeometryCut = null;
            }
            else
            {
                entity.GeometryCut!.Objects.RemoveAt(entity.GeometryCut.Objects.Count - 1);
            }

            return entity;
        });

        var result = await SpherewrightTools.InspectFactoryEntityAsync(
            bridge, "owned-session", 104, 41, geometry: GeometryRequest());

        Assert.True(result.IsError);
        Assert.Equal(BridgeErrorCodes.BridgeNotReady,
            result.StructuredContent!.Value.GetProperty("error").GetProperty("code").GetString());
        Assert.Equal(JsonValueKind.Null, result.StructuredContent.Value.GetProperty("result").ValueKind);
        Assert.Single(Proxy(bridge).Calls);
        Assert.Equal(nameof(IBridgeClient.InspectFactoryEntityAsync), Proxy(bridge).Calls[0]);
    }

    [Theory]
    [InlineData("observed")]
    [InlineData("unavailable")]
    public async Task ExactObservedOrUnavailableGeometryEchoIsAcceptedAsNonExecutable(string state)
    {
        var request = GeometryRequest();
        var bridge = NewGeometryBridge(actual => GeometryEntity(actual, state));

        var result = await SpherewrightTools.InspectFactoryEntityAsync(
            bridge, "owned-session", 104, 41, geometry: request);

        Assert.False(result.IsError);
        var payload = result.StructuredContent!.Value;
        var entity = payload.GetProperty("result");
        var cut = entity.GetProperty("geometryCut");
        Assert.False(cut.GetProperty("executable").GetBoolean());
        Assert.Equal(state, cut.GetProperty("state").GetString());
        Assert.Equal(2, cut.GetProperty("objects").GetArrayLength());
        Assert.Equal(2, cut.GetProperty("terrain").GetArrayLength());
        if (state == "observed")
        {
            Assert.All(cut.GetProperty("objects").EnumerateArray(), item => Assert.True(item.GetProperty("observed").GetBoolean()));
            var runtimeBelt = cut.GetProperty("objects")[0];
            Assert.Equal("native_runtime_belt_collider_world_bounds",
                runtimeBelt.GetProperty("geometrySource").GetString());
            var runtimeCollider = runtimeBelt.GetProperty("colliders")[0];
            Assert.Equal(51, runtimeCollider.GetProperty("centre").GetProperty("x").GetSingle());
            Assert.Equal(3, runtimeCollider.GetProperty("halfExtents").GetProperty("x").GetSingle());
            Assert.All(cut.GetProperty("terrain").EnumerateArray(), item => Assert.True(item.GetProperty("observed").GetBoolean()));
            Assert.Empty(cut.GetProperty("blockers").EnumerateArray());
        }
        else
        {
            Assert.All(cut.GetProperty("objects").EnumerateArray(), item =>
            {
                Assert.False(item.GetProperty("observed").GetBoolean());
                Assert.False(string.IsNullOrWhiteSpace(item.GetProperty("reasonCode").GetString()));
            });
            Assert.All(cut.GetProperty("terrain").EnumerateArray(), item =>
            {
                Assert.False(item.GetProperty("observed").GetBoolean());
                Assert.False(string.IsNullOrWhiteSpace(item.GetProperty("reasonCode").GetString()));
            });
            Assert.NotEmpty(cut.GetProperty("blockers").EnumerateArray());
        }
        Assert.DoesNotContain("planToken", payload.GetRawText());
        Assert.DoesNotContain("commitAllowed", payload.GetRawText());

        var proxy = Proxy(bridge);
        Assert.Single(proxy.Calls);
        Assert.Equal(nameof(IBridgeClient.InspectFactoryEntityAsync), proxy.Calls[0]);
        Assert.NotNull(proxy.LastInspectRequest);
        Assert.Equal(request.ObjectIds, proxy.LastInspectRequest!.Geometry!.ObjectIds);
        Assert.Equal(request.ExpectedFactoryGeometryHash,
            proxy.LastInspectRequest.Geometry.ExpectedFactoryGeometryHash);
    }

    private static BeltRoutePreviewRequest Request()
    {
        var points = new[] { Point(2), Point(5), Point(8), Point(11), Point(14) };
        return new BeltRoutePreviewRequest
        {
            ExpectedFactoryGeometryHash = GeometryHash,
            FilterItemId = 1001,
            SourceSorterItemId = 2011,
            DestinationSorterItemId = 2012,
            Source = Binding(41, 3, 0, 2),
            Destination = Binding(43, 4, 0, 1),
            Segments = new()
            {
                new BeltRouteSegment { RoutingMode = BeltPathModes.NativeGrid, Start = points[0], End = points[2] },
                new BeltRouteSegment { RoutingMode = BeltPathModes.NativeGrid, Start = points[2], End = points[4] },
            },
        };
    }

    [Fact]
    public async Task NativeSegmentBatchUsesOnlyTheReadEndpointAndPreservesGeneratedOnlyEcho()
    {
        var request = GeometryRequest();
        request.NativeBeltSegments = new() { new BeltRouteSegment { Start=Point(0),End=Point(6) } };
        var bridge = NewGeometryBridge(r => GeometryEntity(r, "observed"));
        var result = await SpherewrightTools.InspectFactoryEntityAsync(bridge, "owned-session", 104, 41, geometry:request);
        Assert.False(result.IsError);
        Assert.Single(Proxy(bridge).Calls);
        Assert.Single(Proxy(bridge).LastInspectRequest!.Geometry!.NativeBeltSegments!);
        var row=result.StructuredContent!.Value.GetProperty("result").GetProperty("geometryCut").GetProperty("nativeBeltSegments")[0];
        Assert.Equal("read_only_native_belt_segment_points_v1",row.GetProperty("phase").GetString());
        Assert.False(row.GetProperty("executable").GetBoolean());
        Assert.False(row.GetProperty("nativeConditionsChecked").GetBoolean());
        Assert.Equal(4,row.GetProperty("points").GetArrayLength());
    }

    [Theory]
    [InlineData("legacy_missing_segment_echo")]
    [InlineData("claims_native_check")]
    [InlineData("drops_last_point")]
    public async Task NativeSegmentEchoCannotDowngradeIntoLegacyGeometryOrPlacementEvidence(string fault)
    {
        var request=GeometryRequest();request.NativeBeltSegments=new(){new BeltRouteSegment{Start=Point(0),End=Point(6)}};
        var bridge=NewGeometryBridge(r =>
        {
            var entity=GeometryEntity(r,"observed");var rows=entity.GeometryCut!.NativeBeltSegments;
            if(fault=="legacy_missing_segment_echo")rows.Clear();
            else if(fault=="claims_native_check")rows[0].NativeConditionsChecked=true;
            else rows[0].Points.RemoveAt(rows[0].Points.Count-1);
            return entity;
        });
        var result=await SpherewrightTools.InspectFactoryEntityAsync(bridge,"owned-session",104,41,geometry:request);
        Assert.True(result.IsError);
        Assert.Equal(BridgeErrorCodes.BridgeNotReady,result.StructuredContent!.Value.GetProperty("error").GetProperty("code").GetString());
        Assert.Single(Proxy(bridge).Calls);
    }

    [Fact]
    public async Task NativeSegmentWithoutLayoutHashDoesNotCallTheBridge()
    {
        var request=GeometryRequest();request.ExpectedFactoryGeometryHash=null;
        request.NativeBeltSegments=new(){new BeltRouteSegment{Start=Point(0),End=Point(6)}};
        var bridge=NewGeometryBridge(r=>GeometryEntity(r,"observed"));
        var result=await SpherewrightTools.InspectFactoryEntityAsync(bridge,"owned-session",104,41,geometry:request);
        Assert.True(result.IsError);
        Assert.Equal(BridgeErrorCodes.InvalidRequest,result.StructuredContent!.Value.GetProperty("error").GetProperty("code").GetString());
        Assert.Empty(Proxy(bridge).Calls);
    }

    private static FactoryGeometryReadRequest GeometryRequest() => new()
    {
        ObjectIds = new() { 41, 43 },
        TerrainPoints = new() { Point(20), Point(22) },
        ExpectedFactoryGeometryHash = GeometryHash,
    };

    private static FactoryEntitySnapshot GeometryEntity(InspectFactoryEntityRequest request, string state)
    {
        var observed = state == "observed";
        var geometry = request.Geometry!;
        var cut = new FactoryGeometryCutSnapshot
        {
            Phase = "read_only_factory_geometry_cut_v2",
            State = state,
            SessionId = "owned-session",
            PlanetId = request.PlanetId,
            Revision = 7,
            CapturedAtGameTick = 120,
            FactoryGeometryHash = GeometryHash,
            BuiltObjectCount = 6492,
            PrebuildObjectCount = 0,
            Objects = geometry.ObjectIds.Select(id => new FactoryObjectGeometrySnapshot
            {
                ObjectId = id,
                ItemId = observed ? 2001 : 0,
                Name = observed ? "belt" : string.Empty,
                Observed = observed,
                ReasonCode = observed ? null : "native_geometry_unavailable",
                Position = Point(id),
                Position2 = Point(id + 1),
                Rotation = IdentityRotation(),
                Rotation2 = IdentityRotation(),
                Tilt = 0,
                GeometrySource = observed && id > 0
                    ? "native_runtime_belt_collider_world_bounds"
                    : "native_build_collider_world_bounds",
                NativeHasBuildCollider = observed && id > 0,
                Colliders = observed && id > 0
                    ? new()
                    {
                        new FactoryBuildColliderSnapshot
                        {
                            NativeShape = "Box",
                            Centre = Point(id + 10),
                            AxisX = new Vector3Snapshot { X = 1 },
                            AxisY = new Vector3Snapshot { Y = 1 },
                            AxisZ = new Vector3Snapshot { Z = 1 },
                            HalfExtents = new Vector3Snapshot { X = 3, Y = .5f, Z = .5f },
                        },
                    }
                    : new(),
                Connections = new(),
            }).ToList(),
            Terrain = geometry.TerrainPoints.Select((point, index) => new FactoryTerrainPointSnapshot
            {
                Index = index,
                RequestedPosition = ClonePoint(point),
                Observed = observed,
                ReasonCode = observed ? null : "native_geometry_unavailable",
                NativeModifiedSurfaceRadius = observed ? 200 : null,
                GroundRayHit = observed,
                GroundRadius = observed ? 199 : null,
                WaterRayHit = false,
                WaterRadius = null,
                WaterItemId = 0,
                MaximumNeighbourGradient = observed ? 0.1 : null,
                GroundGridSnapPosition = observed ? Point(point.X) : null,
                AltitudeGridSnapPositions = observed
                    ? new() { Point(point.X), Point(point.X + 1), Point(point.X + 2), Point(point.X + 3) }
                    : new(),
            }).ToList(),
            NativeBeltSegments = (geometry.NativeBeltSegments ?? new()).Select((s,index) => new FactoryNativeBeltSegmentSnapshot
            {
                Index=index,Observed=observed,ReasonCode=observed?null:"native_segment_unavailable",
                Segment=new BeltRouteSegment{RoutingMode=s.RoutingMode,Start=ClonePoint(s.Start),End=ClonePoint(s.End),
                    StartAltitudeLevel=s.StartAltitudeLevel,EndAltitudeLevel=s.EndAltitudeLevel},
                NativeGroundRadius=200.2f,NativeMaximumSlope=0,
                Points=observed?new(){ClonePoint(s.Start),Point(s.Start.X+(s.End.X-s.Start.X)/3),Point(s.Start.X+(s.End.X-s.Start.X)*2/3),ClonePoint(s.End)}:new(),
            }).ToList(),
            Blockers = observed ? new() : new() { "native_geometry_unavailable" },
        };

        return new FactoryEntitySnapshot
        {
            SessionId = "owned-session",
            PlanetId = request.PlanetId,
            ObjectId = request.ObjectId,
            ObjectKind = FactoryObjectKinds.Entity,
            ItemId = 2001,
            Name = "belt",
            ComponentKind = "belt",
            Position = Point(request.ObjectId),
            Rotation = IdentityRotation(),
            CapturedAtGameTick = 120,
            GeometryCut = cut,
        };
    }

    private static PreparedNormalAction SuccessfulResult(PrepareBuildRequest request)
    {
        var wanted = request.BeltRoutePreview!;
        var points = new[] { Point(2), Point(5), Point(8), Point(11), Point(14) };
        var route = new BeltRoutePreviewSnapshot
        {
            Phase = BeltRoutePreviewPolicy.Phase,
            FactoryGeometryHash = wanted.ExpectedFactoryGeometryHash,
            NativePathCheckPerformed = true,
            NativePathCheckPassed = true,
            JointNativeCheckPerformed = true,
            JointNativeCheckPassed = true,
            Points = points.Select((point, index) => new NativeBeltRoutePoint
            {
                Index = index,
                Position = ClonePoint(point),
                PlannedPosition = ClonePoint(point),
                Rotation = IdentityRotation(),
                Tilt = 0,
                NativeCondition = "Ok",
            }).ToList(),
            Segments = new()
            {
                new NativeBeltRouteSegment
                {
                    Index = 0, RoutingMode = BeltPathModes.NativeGrid, FirstBeltIndex = 0,
                    LastBeltIndex = 2, NativePointCount = 3,
                },
                new NativeBeltRouteSegment
                {
                    Index = 1, RoutingMode = BeltPathModes.NativeGrid, FirstBeltIndex = 2,
                    LastBeltIndex = 4, NativePointCount = 3,
                },
            },
            Endpoints = new BeltEndpointPreviewSnapshot
            {
                Phase = BeltEndpointPreviewPolicy.Phase,
                SessionId = "owned-session",
                PlanetId = 104,
                Revision = 7,
                CapturedAtGameTick = 120,
                NativeCheckPerformed = true,
                NativeCheckPassed = true,
                AssessmentHash = "assessment-hash",
                Attachments = new()
                {
                    Attachment(wanted.Source, "source", wanted.SourceSorterItemId, points[0], 0),
                    Attachment(wanted.Destination, "destination", wanted.DestinationSorterItemId, points[^1], 4),
                },
            },
        };
        var budget = new Dictionary<int, int> { [2001] = points.Length };
        foreach (var sorterId in new[] { wanted.SourceSorterItemId, wanted.DestinationSorterItemId })
        {
            budget.TryGetValue(sorterId, out var count);
            budget[sorterId] = count + 1;
        }

        var result = new PreparedNormalAction
        {
            Prepared = false,
            ActionKind = NormalActionKinds.Build,
            PlanToken = string.Empty,
            ExpectedStateHash = request.ExpectedPlayerStateHash,
            BuildKind = "belt_route_preview",
            BeltRoutePreview = route,
            PlannedBeltPath = new BeltPathPlanSnapshot
            {
                NewObjectCount = points.Length,
                RoutingMode = BeltPathModes.NativeRoutePreview,
                NativeValidationMode = "full_path_stage1_and_joint_all_objects",
                SourceBindingMode = "none",
                DestinationBindingMode = "none",
            },
            ItemBudget = budget.Select(item => new ActionItemBudget
            {
                ItemId = item.Key,
                Count = item.Value,
                Direction = "preview-construction-consumption",
            }).ToList(),
        };
        result.PlannedPath.AddRange(points);
        return result;
    }

    private static PreparedNormalAction LegacyPreparedToken() => new()
    {
        Prepared = true,
        ActionKind = NormalActionKinds.Build,
        PlanToken = "legacy-token",
        ExpectedStateHash = "player-state",
        BuildKind = "belt",
    };

    private static PreparedNormalAction MissingRouteEcho(PrepareBuildRequest request) => new()
    {
        Prepared = false,
        ActionKind = NormalActionKinds.Build,
        PlanToken = string.Empty,
        ExpectedStateHash = request.ExpectedPlayerStateHash,
        BuildKind = "belt_route_preview",
    };

    private static PlannedBeltEndpointAttachment Attachment(
        PlannedBeltEndpointBinding binding,
        string role,
        int sorterItemId,
        Vector3Snapshot pathPoint,
        int beltIndex)
    {
        var source = role == "source";
        return new PlannedBeltEndpointAttachment
        {
            Role = role,
            ExistingObjectId = binding.ExistingObjectId,
            EndpointStateHash = binding.ExpectedEndpointStateHash,
            ExistingItemId = 2101,
            PlannedBeltIndex = beltIndex,
            SorterItemId = sorterItemId,
            FilterItemId = 1001,
            ExistingBeltQuarterTurns = binding.ExistingBeltQuarterTurns,
            PlannedBeltQuarterTurns = binding.PlannedBeltQuarterTurns,
            NativeCondition = "Ok",
            NativeSpan = 2,
            Attachment = new InserterAttachmentPlanSnapshot
            {
                Mode = "read_only_planned_belt_exact_slots",
                SourceSlot = source ? binding.ExistingSlot : -1,
                DestinationSlot = source ? -1 : binding.ExistingSlot,
                SourcePosition = source ? Point(1) : pathPoint,
                DestinationPosition = source ? pathPoint : Point(20),
            },
        };
    }

    private static PlannedBeltEndpointBinding Binding(int id, int slot, int existingTurns, int plannedTurns) => new()
    {
        ExistingObjectId = id,
        ExpectedEndpointStateHash = $"endpoint-{id}",
        ExistingSlot = slot,
        ExistingBeltQuarterTurns = existingTurns,
        PlannedBeltQuarterTurns = plannedTurns,
    };

    private static Vector3Snapshot Point(float x) => new() { X = x, Y = 200, Z = 0 };

    private static Vector3Snapshot ClonePoint(Vector3Snapshot point) => new() { X = point.X, Y = point.Y, Z = point.Z };

    private static QuaternionSnapshot IdentityRotation() => new() { W = 1 };

    private static IBridgeClient NewBridge(Func<PrepareBuildRequest, PreparedNormalAction> response)
    {
        var bridge = DispatchProxy.Create<IBridgeClient, ReadOnlyRoutePreviewBridgeProxy>();
        Proxy(bridge).Response = response;
        return bridge;
    }

    private static IBridgeClient NewGeometryBridge(Func<InspectFactoryEntityRequest, FactoryEntitySnapshot> response)
    {
        var bridge = DispatchProxy.Create<IBridgeClient, ReadOnlyRoutePreviewBridgeProxy>();
        Proxy(bridge).GeometryResponse = response;
        return bridge;
    }

    private static ReadOnlyRoutePreviewBridgeProxy Proxy(IBridgeClient bridge) =>
        (ReadOnlyRoutePreviewBridgeProxy)(object)bridge;

    public class ReadOnlyRoutePreviewBridgeProxy : DispatchProxy
    {
        public Func<PrepareBuildRequest, PreparedNormalAction>? Response { get; set; }
        public Func<InspectFactoryEntityRequest, FactoryEntitySnapshot>? GeometryResponse { get; set; }
        public List<string> Calls { get; } = new();
        public PrepareBuildRequest? LastRequest { get; private set; }
        public InspectFactoryEntityRequest? LastInspectRequest { get; private set; }

        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
        {
            Assert.NotNull(targetMethod);
            Calls.Add(targetMethod!.Name);
            if (targetMethod.Name == nameof(IBridgeClient.PrepareBuildAsync))
            {
                var request = Assert.IsType<PrepareBuildRequest>(args![1]);
                LastRequest = request;
                var response = Response ?? throw new InvalidOperationException("Prepare response fixture is missing.");
                return Task.FromResult(BridgeCallResult<PreparedNormalAction>.Succeeded(response(request)));
            }

            if (targetMethod.Name == nameof(IBridgeClient.InspectFactoryEntityAsync))
            {
                var request = Assert.IsType<InspectFactoryEntityRequest>(args![1]);
                LastInspectRequest = request;
                var response = GeometryResponse ?? throw new InvalidOperationException("Geometry response fixture is missing.");
                return Task.FromResult(BridgeCallResult<FactoryEntitySnapshot>.Succeeded(response(request)));
            }

            throw new InvalidOperationException($"Unexpected bridge call: {targetMethod.Name}");
        }
    }
}
