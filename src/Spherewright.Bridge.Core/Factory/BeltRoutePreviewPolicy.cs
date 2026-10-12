using Spherewright.Contracts.Actions;
using Spherewright.Contracts.Factory;
using Spherewright.Bridge.Core.Safety;

namespace Spherewright.Bridge.Core.Factory;

public static class BeltRoutePreviewPolicy
{
    public const string Phase = "read_only_bounded_belt_route_preview_v2";
    public const int MaximumSegments = 12;
    public const int MaximumPoints = 192;
    public const double MaximumPolylineMetres = 240;

    // An assessment hash cannot enter the executable belt-routing domain.
    public static string BindReadOnlyGeometry(string mode, string geometryHash)
    {
        if (mode != BeltPathModes.NativeRoutePreview)
            throw new ArgumentException("A read-only route fingerprint requires the explicit preview mode.", nameof(mode));
        if (geometryHash is null || !geometryHash.StartsWith("sha256:", StringComparison.Ordinal)
            || !FactoryGeometryReadPolicy.ValidHash(geometryHash.Substring(7)))
            throw new ArgumentException("A complete build-step geometry hash is required.", nameof(geometryHash));
        return CanonicalStateHash.Combine(Phase, mode, geometryHash);
    }

    public static string? ValidateRequest(PrepareBuildRequest request)
    {
        var p = request.BeltRoutePreview;
        if (p is null) return request.BeltPathMode == BeltPathModes.NativeRoutePreview ? "belt_route_preview_request_required" : null;
        if (request.BuildingItemId != 2001 || request.BeltPathMode != BeltPathModes.NativeRoutePreview
            || request.PreferredYaw.HasValue || request.PreferredDistance != 12f || request.PathLength != 6f
            || request.BeltEndpointPreview is not null || request.PreferredPosition is not null || request.PathEnd is not null
            || request.SourceObjectId.HasValue || request.DestinationObjectId.HasValue || request.ResourceNodeId.HasValue
            || request.ExpectedSourceStateHash is not null || request.ExpectedDestinationStateHash is not null
            || request.ExpectedResourceStateHash is not null || request.BeltStartAltitudeLevel.HasValue
            || request.BeltEndAltitudeLevel.HasValue || request.InitialSorterFilterItemId != 0
            || request.StateHashVersion != 1 || string.IsNullOrWhiteSpace(request.ExpectedPlayerStateHash)
            || request.ExpectedPlayerStateHash.Length > 256) return "belt_route_preview_requires_explicit_read_only_request";
        if (!FactoryGeometryReadPolicy.ValidHash(p.ExpectedFactoryGeometryHash) || p.FilterItemId <= 0 || p.FilterItemId > short.MaxValue
            || p.SourceSorterItemId is not (2011 or 2012) || p.DestinationSorterItemId is not (2011 or 2012)
            || !ValidBinding(p.Source) || !ValidBinding(p.Destination)
            || p.Source.ExistingObjectId == p.Destination.ExistingObjectId) return "belt_route_preview_binding_invalid";
        if (p.Segments is null || p.Segments.Count < 1 || p.Segments.Count > MaximumSegments) return "belt_route_preview_segment_limit";
        var length = 0d;
        for (var i=0; i<p.Segments.Count; i++)
        {
            var s=p.Segments[i];
            if (s is null || !FactoryGeometryReadPolicy.ValidPoint(s.Start) || !FactoryGeometryReadPolicy.ValidPoint(s.End)
                || (s.RoutingMode != BeltPathModes.NativeGrid && s.RoutingMode != BeltPathModes.NativeElevatedGrid)
                || (s.RoutingMode == BeltPathModes.NativeGrid && (s.StartAltitudeLevel != 0 || s.EndAltitudeLevel != 0))
                || (s.RoutingMode == BeltPathModes.NativeElevatedGrid && !BeltElevationPolicy.ValidLevels(s.StartAltitudeLevel,s.EndAltitudeLevel)))
                return "belt_route_preview_segment_invalid";
            var distance = Distance(s.Start,s.End); length += distance;
            if (distance < 1.5 || distance > 30) return "belt_route_preview_native_span_limit";
            if (i>0 && (!FactoryGeometryReadPolicy.SamePoint(p.Segments[i-1].End,s.Start)
                || p.Segments[i-1].EndAltitudeLevel != s.StartAltitudeLevel)) return "belt_route_preview_segment_join_invalid";
        }
        return length > MaximumPolylineMetres ? "belt_route_preview_polyline_limit" : null;
    }

    public static bool ConfirmsReadOnlyEcho(PrepareBuildRequest request, PreparedNormalAction? result,string sessionId)
    {
        if (ValidateRequest(request) is not null || request.BeltRoutePreview is not { } wanted || result is null
            || result.Prepared || result.CommitAllowedNow || !string.IsNullOrEmpty(result.PlanToken)
            || result.BuildKind != "belt_route_preview" || result.ActionKind != "build"
            || result.ExpectedStateHash != request.ExpectedPlayerStateHash || result.BeltEndpointPreview is not null
            || result.SourceObjectId.HasValue || result.DestinationObjectId.HasValue
            || result.BeltRoutePreview is not { } actual || actual.Executable || actual.Phase != Phase
            || actual.FactoryGeometryHash != wanted.ExpectedFactoryGeometryHash
            || actual.Endpoints is not { } e || e.SessionId != sessionId || e.PlanetId != request.PlanetId
            || e.Revision < 0 || e.CapturedAtGameTick < 0 || e.Executable || string.IsNullOrWhiteSpace(e.AssessmentHash)
            || e.Phase != BeltEndpointPreviewPolicy.Phase || e.Attachments is null
            || (e.Attachments.Count != 2 && (e.Attachments.Count != 0
                || actual.NativePathCheckPassed && actual.FailureStage!="pre_native_endpoint_guard"))
            || e.Blockers is null || e.Blockers.Count > 32
            || actual.Segments is null || actual.Segments.Count != wanted.Segments.Count
            || actual.Points is null || actual.Points.Count < 2 || actual.Points.Count > MaximumPoints
            || result.PlannedPath is null || result.PlannedPath.Count != actual.Points.Count
            || result.PlannedBeltPath is not { } path || path.NewObjectCount != actual.Points.Count
            || path.RoutingMode != BeltPathModes.NativeRoutePreview || path.NativeValidationMode != "full_path_stage1_and_joint_all_objects"
            || path.SourceBindingMode != "none" || path.DestinationBindingMode != "none"
            || path.ReusedSourceObjectId.HasValue || path.ReusedDestinationObjectId.HasValue
            || path.SourcePreservationMode is not null || path.DestinationPreservationMode is not null) return false;
        for (var i=0; i<actual.Points.Count; i++)
            if (actual.Points[i] is not { } point || point.Index != i
                || !FactoryGeometryReadPolicy.ValidPoint(point.Position)
                || !FactoryGeometryReadPolicy.ValidPoint(point.PlannedPosition)
                || !FactoryGeometryReadPolicy.ValidRotation(point.Rotation) || !FactoryGeometryReadPolicy.Finite(point.Tilt)
                || string.IsNullOrWhiteSpace(point.NativeCondition)
                || !FactoryGeometryReadPolicy.SamePoint(point.PlannedPosition,result.PlannedPath[i])) return false;
        for (var i=0; i<actual.Segments.Count; i++)
        {
            var s=actual.Segments[i]; var w=wanted.Segments[i];
            if (s is null || s.Index != i || s.RoutingMode != w.RoutingMode
                || s.StartAltitudeLevel != w.StartAltitudeLevel || s.EndAltitudeLevel != w.EndAltitudeLevel
                || s.NativePointCount < 2 || s.NativePointCount > 64 || s.FirstBeltIndex < 0
                || s.LastBeltIndex >= actual.Points.Count || s.LastBeltIndex-s.FirstBeltIndex+1 != s.NativePointCount
                || s.FirstBeltIndex != (i==0 ? 0 : actual.Segments[i-1].LastBeltIndex)
                || !FactoryGeometryReadPolicy.SamePoint(w.Start,actual.Points[s.FirstBeltIndex].PlannedPosition)
                || !FactoryGeometryReadPolicy.SamePoint(w.End,actual.Points[s.LastBeltIndex].PlannedPosition)) return false;
        }
        if (actual.Segments[actual.Segments.Count-1].LastBeltIndex != actual.Points.Count-1) return false;
        foreach (var role in e.Attachments.Count == 2 ? new[] {"source","destination"} : Array.Empty<string>())
        {
            var binding=role=="source" ? wanted.Source : wanted.Destination;
            var sorter=role=="source" ? wanted.SourceSorterItemId : wanted.DestinationSorterItemId;
            var rows=e.Attachments.Where(a=>a is not null && a.Role==role).ToArray();
            if (rows.Length!=1) return false; var a=rows[0]; var source=role=="source";
            if (a.ExistingObjectId!=binding.ExistingObjectId || a.EndpointStateHash!=binding.ExpectedEndpointStateHash
                || a.SorterItemId!=sorter || a.FilterItemId!=wanted.FilterItemId || a.ExistingItemId<=0
                || ((binding.ExistingSlot==-1)!=(a.ExistingItemId>=2001 && a.ExistingItemId<=2003))
                || a.ExistingBeltQuarterTurns!=binding.ExistingBeltQuarterTurns || a.PlannedBeltQuarterTurns!=binding.PlannedBeltQuarterTurns
                || a.PlannedBeltIndex!=(source?0:actual.Points.Count-1) || a.Attachment is null
                || a.Attachment.Mode!="read_only_planned_belt_exact_slots"
                || a.Attachment.SourceSlot!=(source?binding.ExistingSlot:-1) || a.Attachment.DestinationSlot!=(source?-1:binding.ExistingSlot)
                || a.Attachment.InputOffset!=0 || a.Attachment.OutputOffset!=0
                || !FactoryGeometryReadPolicy.ValidPoint(a.Attachment.SourcePosition) || !FactoryGeometryReadPolicy.ValidPoint(a.Attachment.DestinationPosition)
                || !FactoryGeometryReadPolicy.SamePoint(source?a.Attachment.DestinationPosition:a.Attachment.SourcePosition,actual.Points[a.PlannedBeltIndex].PlannedPosition)) return false;
        }
        var budget=new Dictionary<int,int>{{2001,actual.Points.Count}};
        foreach(var id in new[]{wanted.SourceSorterItemId,wanted.DestinationSorterItemId}) {budget.TryGetValue(id,out var n);budget[id]=n+1;}
        if(result.ItemBudget is null || result.ItemBudget.Count!=budget.Count || result.ItemBudget.Any(b=>b is null || b.Direction!="preview-construction-consumption")
            || budget.Any(b=>result.ItemBudget.Count(i=>i.ItemId==b.Key && i.Count==b.Value)!=1)) return false;
        if (e.NativeCheckPerformed != actual.JointNativeCheckPerformed) return false;
        if (actual.JointNativeCheckPassed)
            return actual.NativePathCheckPerformed && actual.NativePathCheckPassed && actual.JointNativeCheckPerformed
                && e.NativeCheckPerformed && e.NativeCheckPassed && e.Blockers.Count==0
                && actual.Points.All(p=>p.NativeCondition=="Ok" && FactoryGeometryReadPolicy.SamePoint(p.Position,p.PlannedPosition))
                && e.Attachments.Count==2 && e.Attachments.All(a=>a.NativeCondition=="Ok" && a.NativeSpan>=1 && a.NativeSpan<=3)
                && actual.FailedBeltIndex is null && actual.FailureCondition is null && actual.FailureStage is null;
        return !e.NativeCheckPassed && e.Blockers.Count>0
            && !string.IsNullOrWhiteSpace(actual.FailureCondition)
            && (actual.FailedBeltIndex is null || actual.FailedBeltIndex>=0 && actual.FailedBeltIndex<actual.Points.Count)
            && (actual.FailedBeltIndex is null || actual.NativePathCheckPerformed)
            && (actual.FailedBeltIndex is null || actual.Points[actual.FailedBeltIndex.Value].NativeCondition!="Ok"
                && actual.Points[actual.FailedBeltIndex.Value].NativeCondition==actual.FailureCondition)
            && (!actual.JointNativeCheckPerformed || actual.NativePathCheckPerformed && actual.NativePathCheckPassed)
            && (!actual.NativePathCheckPassed || actual.NativePathCheckPerformed)
            && (actual.FailureStage switch
            {
                "pre_native_path_guard" => !actual.NativePathCheckPerformed && !actual.JointNativeCheckPerformed && actual.FailedBeltIndex is null,
                "native_full_path" => actual.NativePathCheckPerformed && !actual.NativePathCheckPassed && !actual.JointNativeCheckPerformed,
                "pre_native_endpoint_guard" => actual.NativePathCheckPassed && !actual.JointNativeCheckPerformed && actual.FailedBeltIndex is null,
                "joint_native" => actual.NativePathCheckPassed && actual.JointNativeCheckPerformed,
                _ => false,
            });
    }

    private static bool ValidBinding(PlannedBeltEndpointBinding? b) => b is not null && b.ExistingObjectId>0 && b.ExistingObjectId<65536
        && !string.IsNullOrWhiteSpace(b.ExpectedEndpointStateHash) && b.ExpectedEndpointStateHash.Length<=256
        && b.ExistingSlot>=-1 && b.ExistingSlot<=15 && b.ExistingBeltQuarterTurns>=0 && b.ExistingBeltQuarterTurns<=3
        && b.PlannedBeltQuarterTurns>=0 && b.PlannedBeltQuarterTurns<=3 && (b.ExistingSlot==-1 || b.ExistingBeltQuarterTurns==0);
    private static double Distance(Vector3Snapshot a,Vector3Snapshot b) => Math.Sqrt(Math.Pow((double)a.X-b.X,2)+Math.Pow((double)a.Y-b.Y,2)+Math.Pow((double)a.Z-b.Z,2));
}
