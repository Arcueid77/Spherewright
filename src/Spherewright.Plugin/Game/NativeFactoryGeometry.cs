using System.IO;
using System.Security.Cryptography;
using Spherewright.Bridge.Core.Factory;
using Spherewright.Contracts.Factory;
using UnityEngine;

namespace Spherewright.Plugin.Game;

// Unity-thread only. Existing native pose readers and tool-owned allocation;
// no active UI tool, command, world field or construction entry is changed.
internal static class NativeFactoryGeometry
{
    internal static SpherewrightBlueprintBuildTool Open(PlanetFactory factory)
    {
        var tool = new SpherewrightBlueprintBuildTool();
        tool._Init(GameMain.data!); tool.SetFactoryReferences();
        if (!ReferenceEquals(tool.factory,factory)) { tool.Dispose(); throw new InvalidOperationException("factory_geometry_local_binding_unavailable"); }
        return tool;
    }

    internal static string Fingerprint(PlanetFactory factory,string sessionId,
        SpherewrightBlueprintBuildTool tool,out int built,out int prebuild)
    {
        built=0; prebuild=0;
        if(factory.entityPool is null || factory.prebuildPool is null || factory.entityConnPool is null || factory.prebuildConnPool is null
            || factory.entityCursor<1 || factory.prebuildCursor<1
            || factory.entityCursor>FactoryGeometryReadPolicy.MaximumFactorySlots || factory.prebuildCursor>FactoryGeometryReadPolicy.MaximumFactorySlots
            || factory.entityCursor>factory.entityPool.Length || factory.prebuildCursor>factory.prebuildPool.Length)
            throw new InvalidOperationException("factory_geometry_pool_bounds_unavailable");
        var planet=factory.planet; var raw=planet.data;
        if(raw?.heightData is null || raw.modData is null || raw.heightData.Length>4194304 || raw.modData.Length>4194304)
            throw new InvalidOperationException("factory_geometry_terrain_bounds_unavailable");
        using var stream=new MemoryStream(); using var writer=new BinaryWriter(stream);
        writer.Write("owned-factory-layout-runtime-belt-colliders-and-terrain-v2"); writer.Write(sessionId); writer.Write(factory.planetId);
        writer.Write(planet.radius); writer.Write(planet.scale); writer.Write(planet.waterHeight); writer.Write(planet.waterItemId);
        writer.Write(factory.entityCursor); writer.Write(factory.prebuildCursor);
        var models=new HashSet<int>();
        for(var id=1;id<factory.entityCursor;id++)
        {
            var entity=factory.entityPool[id]; if(entity.id!=id) continue; built++;
            WriteObject(id,entity.protoId,entity.modelIndex,entity.tilt,false);
            // Configuration is static evidence; production progress and cargo are not.
            if (factory.factorySystem is null
                || entity.assemblerId < 0 || entity.labId < 0 || entity.inserterId < 0
                || entity.assemblerId > 0 && (factory.factorySystem.assemblerPool is null
                    || entity.assemblerId >= factory.factorySystem.assemblerPool.Length
                    || factory.factorySystem.assemblerPool[entity.assemblerId].id != entity.assemblerId)
                || entity.labId > 0 && (factory.factorySystem.labPool is null
                    || entity.labId >= factory.factorySystem.labPool.Length
                    || factory.factorySystem.labPool[entity.labId].id != entity.labId)
                || entity.inserterId > 0 && (factory.factorySystem.inserterPool is null
                    || entity.inserterId >= factory.factorySystem.inserterPool.Length
                    || factory.factorySystem.inserterPool[entity.inserterId].id != entity.inserterId))
                throw new InvalidOperationException("factory_geometry_static_configuration_unavailable");
            writer.Write(entity.assemblerId>0?factory.factorySystem.assemblerPool[entity.assemblerId].recipeId:0);
            writer.Write(entity.labId>0?factory.factorySystem.labPool[entity.labId].recipeId:0);
            writer.Write(entity.inserterId>0?factory.factorySystem.inserterPool[entity.inserterId].filter:0);
            if (entity.beltId > 0)
            {
                writer.Write(entity.colliderId);
                var actual = ReadRuntimeBeltCollider(factory, id);
                writer.Write(actual.HasValue);
                if (actual is { } collider)
                {
                    writer.Write(collider.idType); writer.Write(collider.link);
                    writer.Write((int)collider.shape); WriteVector(writer, collider.pos);
                    WriteQuaternion(writer, collider.q); WriteVector(writer, collider.ext); writer.Write(collider.radius);
                }
            }
        }
        for(var id=1;id<factory.prebuildCursor;id++)
        {
            var p=factory.prebuildPool[id]; if(p.id!=id) continue; prebuild++;
            WriteObject(-id,p.protoId,p.modelIndex,p.tilt,p.isDestroyed);
        }
        writer.Write(raw.heightData.Length); foreach(var value in raw.heightData) writer.Write(value);
        writer.Write(raw.modData.Length); writer.Write(raw.modData); writer.Flush();
        using var hash=SHA256.Create();
        return BitConverter.ToString(hash.ComputeHash(stream.GetBuffer(),0,checked((int)stream.Length))).Replace("-",string.Empty);

        void WriteObject(int id,int proto,int model,float tilt,bool destroyed)
        {
            var index=Math.Abs(id); var connections=id>0?factory.entityConnPool:factory.prebuildConnPool;
            if((long)index*16+16>connections.Length) throw new InvalidOperationException("factory_geometry_connection_bounds_unavailable");
            writer.Write(id); writer.Write(proto); writer.Write(model); writer.Write(tilt); writer.Write(destroyed);
            WritePose(writer,tool.GetObjectPose(id)); WritePose(writer,tool.GetObjectPose2(id));
            for(var slot=0;slot<16;slot++) writer.Write(connections[index*16+slot]);
            if(!models.Add(model)) return;
            var desc=tool.GetPrefabDesc(id);
            writer.Write(desc is not null);
            if(desc is null) return;
            writer.Write(desc.hasBuildCollider);
            var colliders=desc.buildColliders;
            if(colliders is {Length:>64}) throw new InvalidOperationException("factory_geometry_prototype_collider_limit");
            var source=colliders is {Length:>0}?colliders:desc.hasBuildCollider?new[]{desc.buildCollider}:Array.Empty<ColliderData>();
            writer.Write(source.Length);
            foreach(var col in source)
            {
                writer.Write((int)col.shape); WriteVector(writer,col.pos); WriteVector(writer,col.ext);
                WriteQuaternion(writer,col.q); writer.Write(col.radius);
            }
        }
    }

    internal static FactoryObjectGeometrySnapshot CaptureObject(PlanetFactory factory,
        SpherewrightBlueprintBuildTool tool,int objectId)
    {
        var result=new FactoryObjectGeometrySnapshot{ObjectId=objectId,ReasonCode="factory_geometry_object_unavailable"};
        var index=Math.Abs(objectId);
        if(objectId>0 ? index>=factory.entityCursor || factory.entityPool[index].id!=index
            : index>=factory.prebuildCursor || factory.prebuildPool[index].id!=index) return result;
        var proto=objectId>0?factory.entityPool[index].protoId:factory.prebuildPool[index].protoId;
        var desc=tool.GetPrefabDesc(objectId);
        result.ItemId=proto; result.Name=LDB.items.Select(proto)?.name??string.Empty;
        if(desc is null) {result.ReasonCode="factory_geometry_prototype_unavailable";return result;}
        result.NativeHasBuildCollider=desc.hasBuildCollider;
        result.Tilt=objectId>0?factory.entityPool[index].tilt:factory.prebuildPool[index].tilt;
        var a=tool.GetObjectPose(objectId); var b=tool.GetObjectPose2(objectId);
        result.Position=Vector(a.position); result.Position2=Vector(b.position);
        result.Rotation=Rotation(a.rotation); result.Rotation2=Rotation(b.rotation);
        if(!FactoryGeometryReadPolicy.ValidPoint(result.Position) || !FactoryGeometryReadPolicy.ValidPoint(result.Position2)
            || !FactoryGeometryReadPolicy.ValidRotation(result.Rotation) || !FactoryGeometryReadPolicy.ValidRotation(result.Rotation2)
            || !FactoryGeometryReadPolicy.Finite(result.Tilt))
        {result.ReasonCode="factory_geometry_native_pose_unavailable";return result;}
        for(var slot=0;slot<16;slot++)
        {
            factory.ReadObjectConn(objectId,slot,out var output,out var other,out var otherSlot);
            if(other!=0) result.Connections.Add(new FactoryConnectionSnapshot{Slot=slot,IsOutput=output,OtherObjectId=other,OtherSlot=otherSlot});
        }
        var linkedInput=result.Connections.Where(c=>!c.IsOutput && c.Slot==1).Select(c=>c.OtherObjectId).FirstOrDefault();
        var linkedOutput=result.Connections.Where(c=>c.IsOutput && c.Slot==0).Select(c=>c.OtherObjectId).FirstOrDefault();
        var preview=new BuildPreview{desc=desc,item=LDB.items.Select(proto),lpos=a.position,lrot=a.rotation,
            lpos2=b.position,lrot2=b.rotation,tilt=result.Tilt,inputObjId=linkedInput,outputObjId=linkedOutput};
        try
        {
            if (objectId > 0 && desc.isBelt)
            {
                result.GeometrySource = "native_runtime_belt_collider_world_bounds";
                var actual = ReadRuntimeBeltCollider(factory, objectId);
                if (actual is not { } collider || collider.shape != EColliderShape.Box
                    || !FactoryGeometryReadPolicy.ValidPoint(Vector(collider.pos))
                    || !FactoryGeometryReadPolicy.ValidRotation(Rotation(collider.q))
                    || !FactoryGeometryReadPolicy.Finite(collider.ext.x) || !FactoryGeometryReadPolicy.Finite(collider.ext.y)
                    || !FactoryGeometryReadPolicy.Finite(collider.ext.z) || collider.ext.x <= 0 || collider.ext.y <= 0 || collider.ext.z <= 0)
                    throw new InvalidOperationException("factory_geometry_runtime_belt_collider_unavailable");
                result.Colliders = new List<FactoryBuildColliderSnapshot> { new()
                {
                    NativeShape = collider.shape.ToString(), ConservativeEnclosure = false,
                    Centre = Vector(collider.pos), AxisX = Vector((collider.q * Vector3.right).normalized),
                    AxisY = Vector((collider.q * Vector3.up).normalized), AxisZ = Vector((collider.q * Vector3.forward).normalized),
                    HalfExtents = Vector(new Vector3(Math.Abs(collider.ext.x), Math.Abs(collider.ext.y), Math.Abs(collider.ext.z))),
                    PaddingMetres = 0,
                } };
            }
            else result.Colliders=NormalGameActionCoordinator.ReadOnlyWorldBuildColliders(preview);
            result.InserterSpanExpanded=desc.isInserter;
            result.Observed=!desc.hasBuildCollider || result.Colliders.Count>0;
            result.ReasonCode=result.Observed?null:"factory_geometry_build_colliders_unavailable";
        }
        catch(InvalidOperationException) {result.ReasonCode="factory_geometry_build_colliders_unavailable";}
        finally {preview.Free();}
        return result;
    }

    private static ColliderData? ReadRuntimeBeltCollider(PlanetFactory factory, int entityId)
    {
        // Same bounded identity/chunk checks already used by the installed
        // native belt rotation/extent preservation proof. Read data only.
        var entity = factory.entityPool[entityId];
        var chunks = factory.planet.physics?.colChunks;
        var chunk = entity.colliderId >> 20;
        var index = entity.colliderId & 0xFFFFF;
        if (!factory.planet.factoryLoaded || entity.beltId <= 0 || entity.colliderId <= 0 || chunks is null
            || chunk < 0 || chunk >= chunks.Length || chunks[chunk]?.colliderPool is null
            || index <= 0 || index >= chunks[chunk].cursor || index >= chunks[chunk].colliderPool.Length) return null;
        var collider = chunks[chunk].colliderPool[index];
        return collider.objId == entityId && collider.objType == EObjectType.Entity && collider.link == 0 ? collider : null;
    }

    internal static FactoryTerrainPointSnapshot CaptureTerrain(PlanetData planet,Vector3Snapshot requested,int index)
    {
        var result=new FactoryTerrainPointSnapshot{Index=index,RequestedPosition=new Vector3Snapshot{X=requested.X,Y=requested.Y,Z=requested.Z},WaterItemId=planet.waterItemId};
        if(planet.data?.heightData is null || planet.data.modData is null || planet.realRadius<=1 || planet.waterItemId<0
            || planet.aux?.customGrids is null || planet.aux.activeGrid is null)
        {result.ReasonCode="factory_geometry_terrain_unavailable";return result;}
        var direction=new Vector3(requested.X,requested.Y,requested.Z).normalized;
        var radius=planet.data.QueryModifiedHeight(direction)*planet.scale;
        if(float.IsNaN(radius) || float.IsInfinity(radius) || radius<=1) {result.ReasonCode="factory_geometry_terrain_unavailable";return result;}
        result.NativeModifiedSurfaceRadius=radius;
        result.GroundGridSnapPosition=Vector(planet.aux.Snap(new Vector3(requested.X,requested.Y,requested.Z),onTerrain:true));
        for(var level=0;level<=3;level++)
        {
            var snapped=planet.aux.Snap(direction*(planet.radius+.2f+level*BeltElevationPolicy.NativeLayerHeight),onTerrain:false);
            if(!BeltElevationPolicy.TryGetLevel(Vector(snapped),planet.radius+.2f,out var actual) || actual!=level)
            {result.ReasonCode="factory_geometry_native_grid_level_unavailable";return result;}
            result.AltitudeGridSnapPositions.Add(Vector(snapped));
        }
        var ray=new Ray(direction*(planet.realRadius+50),-direction);
        result.GroundRayHit=Physics.Raycast(ray,out var ground,100,8704,QueryTriggerInteraction.Collide);
        result.WaterRayHit=Physics.Raycast(ray,out var water,100,16,QueryTriggerInteraction.Collide);
        if(result.GroundRayHit) result.GroundRadius=ground.point.magnitude;
        if(result.WaterRayHit) result.WaterRadius=water.point.magnitude;
        var tangent=Vector3.Cross(direction,Math.Abs(direction.y)<.9f?Vector3.up:Vector3.right).normalized;
        var other=Vector3.Cross(direction,tangent).normalized;
        var gradient=0d;
        foreach(var axis in new[]{tangent,-tangent,other,-other})
        {
            var neighbour=planet.data.QueryModifiedHeight((direction+axis/planet.realRadius).normalized)*planet.scale;
            if(float.IsNaN(neighbour) || float.IsInfinity(neighbour)) {result.ReasonCode="factory_geometry_terrain_gradient_unavailable";return result;}
            gradient=Math.Max(gradient,Math.Abs(neighbour-radius)); // one metre tangent offset; observation, not a native placement verdict.
        }
        result.MaximumNeighbourGradient=gradient;
        result.Observed=result.GroundRayHit;
        result.ReasonCode=result.Observed?null:"factory_geometry_ground_ray_unavailable";
        return result;
    }

    private static void WritePose(BinaryWriter writer,Pose pose) {WriteVector(writer,pose.position);WriteQuaternion(writer,pose.rotation);}
    private static void WriteVector(BinaryWriter writer,Vector3 v) {writer.Write(v.x);writer.Write(v.y);writer.Write(v.z);}
    private static void WriteQuaternion(BinaryWriter writer,Quaternion q) {writer.Write(q.x);writer.Write(q.y);writer.Write(q.z);writer.Write(q.w);}
    internal static Vector3Snapshot Vector(Vector3 v)=>new(){X=v.x,Y=v.y,Z=v.z};
    internal static QuaternionSnapshot Rotation(Quaternion q)=>new(){X=q.x,Y=q.y,Z=q.z,W=q.w};
}
