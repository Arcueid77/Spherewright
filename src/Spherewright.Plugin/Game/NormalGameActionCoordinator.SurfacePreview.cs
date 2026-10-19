using Spherewright.Bridge.Core.Safety;
using Spherewright.Contracts.Actions;
using Spherewright.Contracts.Factory;
using UnityEngine;

namespace Spherewright.Plugin.Game;

internal sealed partial class NormalGameActionCoordinator
{
    private static MovementSurfacePreview CaptureMoveSurfacePreview(Vector3Snapshot start, Vector3 target)
    {
        var tick = GameMain.gameTick;
        var planet = GameMain.localPlanet;
        var unavailable = new MovementSurfacePreview { CapturedAtGameTick = tick, ReasonCode = "surface_scene_unavailable" };
        if (planet is null || planet.factory is null || planet.data is null || planet.waterItemId < 0) return unavailable;
        var plan = MovementSurfacePreviewPolicy.CreatePlan(start, Snapshot(target), planet.realRadius);
        if (plan is null) { unavailable.ReasonCode = "surface_span_outside_bounded_preview"; return unavailable; }
        var rays = new List<MovementSurfaceRayEvidence>();
        // Exact current-DLL DetermineDrift downward masks; no live player/controller fields are changed.
        // 65 points maximum, two queries per point (130 total). Geometry/summary is pure Core over copied values.
        foreach (var point in plan.Points)
        {
            var normal = ToVector(point).normalized;
            var ray = new Ray(normal * (planet.realRadius + MovementSurfacePreviewPolicy.RayOriginAltitude), -normal);
            var groundHit = Physics.Raycast(ray, out var ground, MovementSurfacePreviewPolicy.RayLength, 8704, QueryTriggerInteraction.Collide);
            var waterHit = Physics.Raycast(ray, out var water, MovementSurfacePreviewPolicy.RayLength, 16, QueryTriggerInteraction.Collide);
            rays.Add(new MovementSurfaceRayEvidence
            {
                GroundHit = groundHit,
                GroundDistance = ground.distance,
                GroundAltitude = ground.point.magnitude - planet.realRadius,
                WaterHit = waterHit,
                WaterDistance = water.distance
            });
        }
        return MovementSurfacePreviewPolicy.Summarize(plan, rays, planet.waterItemId, tick);
    }
}
