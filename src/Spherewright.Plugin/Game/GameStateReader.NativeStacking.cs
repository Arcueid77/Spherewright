using Spherewright.Bridge.Core.Factory;
using Spherewright.Bridge.Core.Safety;
using Spherewright.Contracts.Factory;

namespace Spherewright.Plugin.Game;

internal sealed partial class GameStateReader
{
    internal static string NativeStackFamily(PrefabDesc desc)
    {
        if (desc.isBattleBase) return "battle-base";
        if (desc.isTank) return "tank";
        if (desc.isStorage) return "storage";
        if (desc.isLab) return "lab";
        if (desc.isSplitter) return "splitter";
        if (desc.isPiler) return "piler";
        if (desc.isSpraycoster) return "spraycoater";
        return string.Empty;
    }

    private static List<NativeStackingCatalogEntry> CaptureNativeStackingCatalog(out List<string> blockers)
    {
        blockers = new List<string>();
        var result = new List<NativeStackingCatalogEntry>();
        foreach (var item in LDB.items.dataArray)
        {
            if (item is null || !item.CanBuild || !item.IsEntity) continue;
            var modelCount = Math.Max(1, item.ModelCount);
            if (modelCount > 32)
            {
                blockers.Add("native_model_limit:" + item.ID);
                continue;
            }
            for (var offset = 0; offset < modelCount; offset++)
            {
                var model = LDB.models.Select(item.ModelIndex + offset);
                var desc = model?.prefabDesc;
                if (desc is null)
                {
                    blockers.Add("native_model_unavailable:" + item.ID + ":" + offset);
                    continue;
                }
                if (!desc.multiLevel && desc.addonType != EAddonType.Storage && !desc.isBattleBase)
                    continue;
                if (result.Count >= 256)
                {
                    blockers.Add("native_stacking_catalog_limit");
                    return result;
                }
                var alternatives = desc.multiLevelAlternativeIds ?? Array.Empty<int>();
                var transposes = desc.multiLevelAlternativeYawTransposes ?? Array.Empty<bool>();
                result.Add(new NativeStackingCatalogEntry
                {
                    ItemId = item.ID,
                    Name = item.name ?? string.Empty,
                    ModelIndex = model!.ID,
                    BuildMode = item.BuildMode,
                    Family = NativeStackFamily(desc),
                    CanStack = desc.multiLevel,
                    StorageAttachment = desc.addonType == EAddonType.Storage,
                    Unlocked = GameMain.history.ItemUnlocked(item.ID),
                    InventoryCount = GameMain.mainPlayer.package.GetItemCount(item.ID),
                    MaximumLayers = desc.isLab ? GameMain.history.labLevel
                        : desc.isStorage || desc.isTank || desc.isSplitter ? GameMain.history.storageLevel : null,
                    HeightLimitKind = desc.isLab || desc.isStorage || desc.isTank || desc.isSplitter ? "technology" : "native_reach",
                    MaximumConstructionHeightMetres = GameMain.history.buildMaxHeight,
                    AllowsRotation = desc.multiLevelAllowRotate,
                    AllowsPorts = desc.multiLevelAllowPortsOrSlots,
                    JointOffset = CaptureVector(desc.lapJoint),
                    CompatibleBaseItemIds = new[] { item.ID }.Concat(alternatives).Distinct().ToList(),
                    QuarterTurnBaseItemIds = alternatives.Where((_, index) => index < transposes.Length && transposes[index]).ToList(),
                });
            }
        }
        return result;
    }

    internal NativeStackSnapshot CaptureNativeStack(PlanetFactory factory, int selectedObjectId)
    {
        var result = new NativeStackSnapshot
        {
            SelectedObjectId = selectedObjectId,
            CapturedAtGameTick = GameMain.gameTick,
        };
        try
        {
            var selected = StackEntity(factory, selectedObjectId);
            var selectedDesc = LDB.models.Select(selected.modelIndex)?.prefabDesc;
            if (selectedDesc is null) throw new InvalidOperationException("stack_model_unavailable");
            var current = selectedObjectId;
            if (selectedDesc.addonType == EAddonType.Storage)
            {
                factory.ReadObjectConn(current, 0, out _, out current, out _);
                StackEntity(factory, current);
            }
            var seen = new HashSet<int>();
            while (true)
            {
                if (!seen.Add(current) || seen.Count > NativeStackingPolicy.MaximumObservedLayers)
                    throw new InvalidOperationException("stack_chain_cycle_or_limit");
                factory.ReadObjectConn(current, 14, out var output, out var lower, out var lowerSlot);
                if (lower == 0) break;
                if (lower < 0 || output || lowerSlot != 15)
                    throw new InvalidOperationException("stack_lower_link_unavailable");
                StackEntity(factory, lower);
                factory.ReadObjectConn(lower, 15, out var reverseOutput, out var reverse, out var reverseSlot);
                if (!reverseOutput || reverse != current || reverseSlot != 14)
                    throw new InvalidOperationException("stack_lower_link_not_reciprocal");
                current = lower;
            }
            result.RootObjectId = current;
            seen.Clear();
            var fields = new List<object?> { _sessions.SessionId, factory.planetId, selectedObjectId,
                selected.modelIndex, GameMain.history.storageLevel, GameMain.history.labLevel };
            while (true)
            {
                if (!seen.Add(current) || seen.Count > NativeStackingPolicy.MaximumObservedLayers)
                    throw new InvalidOperationException("stack_chain_cycle_or_limit");
                var entity = StackEntity(factory, current);
                var desc = LDB.models.Select(entity.modelIndex)?.prefabDesc;
                if (desc is null || !desc.multiLevel && !desc.isBattleBase)
                    throw new InvalidOperationException("stack_member_not_stackable");
                var snapshot = TryCaptureFactoryEntity(factory, current)
                    ?? throw new InvalidOperationException("stack_member_unavailable");
                var device = CaptureNativeStackDevice(factory, entity);
                result.Layers.Add(new NativeStackLayerSnapshot
                {
                    ObjectId = current,
                    ItemId = entity.protoId,
                    ModelIndex = entity.modelIndex,
                    Family = NativeStackFamily(desc),
                    IsStorageHostBase = desc.isBattleBase,
                    Position = snapshot.Position,
                    Rotation = snapshot.Rotation,
                    EndpointStateHash = snapshot.EndpointStateHash,
                    Connections = snapshot.Connections,
                    StorageConfiguration = snapshot.StorageConfiguration,
                    DeviceState = device,
                    Buffers = snapshot.Buffers,
                    TankFluidCount = snapshot.TankFluidCount,
                    RecipeId = snapshot.RecipeId,
                    Progress = snapshot.Progress,
                    ProgressRequired = snapshot.ProgressRequired,
                });
                fields.Add(current);
                fields.Add(entity.modelIndex);
                fields.Add(snapshot.EndpointStateHash);
                fields.Add(snapshot.RecipeId);
                fields.Add(device?.ResearchMode);
                fields.Add(device?.TechId);
                fields.Add(device?.InputEnabled);
                fields.Add(device?.OutputEnabled);
                fields.Add(device?.InputPriority);
                fields.Add(device?.OutputPriority);
                fields.Add(device?.OutputFilterItemId);
                fields.Add(device?.OutputFilterPreset);
                fields.Add(device?.PrioritySlotPresets);
                fields.Add(device?.PilerMode);
                fields.Add(device?.ProliferatorItemId);
                fields.Add(desc.lapJoint.x);
                fields.Add(desc.lapJoint.y);
                fields.Add(desc.lapJoint.z);
                factory.ReadObjectConn(current, 13, out var attachmentOutput, out var attachmentId, out var attachmentSlot);
                if (attachmentId != 0)
                {
                    if (!attachmentOutput || attachmentSlot != 0 || attachmentId <= 0)
                        throw new InvalidOperationException("stack_storage_attachment_pending_or_unavailable");
                    var attachment = StackEntity(factory, attachmentId);
                    var attachmentDesc = LDB.models.Select(attachment.modelIndex)?.prefabDesc;
                    factory.ReadObjectConn(attachmentId, 0, out var attachmentReverseOutput, out var parentId, out var parentSlot);
                    if (!desc.isStorage || attachmentDesc?.addonType != EAddonType.Storage
                        || attachmentReverseOutput || parentId != current || parentSlot != 13
                        || result.Attachments.Any(value => value.Entity.ObjectId == attachmentId))
                        throw new InvalidOperationException("stack_storage_attachment_not_reciprocal");
                    var attachmentSnapshot = TryCaptureFactoryEntity(factory, attachmentId)
                        ?? throw new InvalidOperationException("stack_storage_attachment_unavailable");
                    attachmentSnapshot.ModelIndex = attachment.modelIndex;
                    var dispenser = CaptureNativeDispenser(factory, attachment);
                    result.Attachments.Add(new() { ParentObjectId = current, Entity = attachmentSnapshot, Dispenser = dispenser });
                    fields.Add(attachmentId); fields.Add(attachment.modelIndex); fields.Add(attachmentSnapshot.EndpointStateHash);
                    fields.Add(dispenser.ComponentId); fields.Add(dispenser.StorageId); fields.Add(dispenser.PlayerMode);
                    fields.Add(dispenser.StorageMode); fields.Add(dispenser.FilterItemId); fields.Add(dispenser.AutoReplenishCouriers);
                }
                factory.ReadObjectConn(current, 15, out var output, out var upper, out var upperSlot);
                if (upper == 0) break;
                if (upper < 0 || !output || upperSlot != 14)
                    throw new InvalidOperationException("stack_upper_link_pending_or_unavailable");
                StackEntity(factory, upper);
                factory.ReadObjectConn(upper, 14, out var reverseOutput, out var reverse, out var reverseSlot);
                if (reverseOutput || reverse != current || reverseSlot != 15)
                    throw new InvalidOperationException("stack_upper_link_not_reciprocal");
                current = upper;
            }
            result.TopObjectId = current;
            result.StateHash = CanonicalStateHash.Combine("native-stack-observation-v1", fields.ToArray());
            result.State = "observed";
            result.Complete = true;
        }
        catch (InvalidOperationException error)
        {
            result.ReasonCode = error.Message;
        }
        return result;
    }

    private static EntityData StackEntity(PlanetFactory factory, int id)
    {
        if (id <= 0 || id >= factory.entityCursor || id >= factory.entityPool.Length
            || factory.entityPool[id].id != id || factory.entityPool[id].protoId <= 0)
            throw new InvalidOperationException("stack_built_entity_unavailable");
        return factory.entityPool[id];
    }

    private static NativeStackDeviceState? CaptureNativeStackDevice(PlanetFactory factory, EntityData entity)
    {
        if (entity.labId > 0)
        {
            var pool = factory.factorySystem.labPool;
            if (entity.labId >= factory.factorySystem.labCursor || entity.labId >= pool.Length
                || pool[entity.labId].id != entity.labId || pool[entity.labId].entityId != entity.id)
                throw new InvalidOperationException("stack_lab_component_unavailable");
            var lab = pool[entity.labId];
            return new NativeStackDeviceState
            {
                NextComponentId = lab.nextLabId, ResearchMode = lab.researchMode, TechId = lab.techId,
                BonusProgress = lab.extraTime, ResearchHashBytes = lab.hashBytes, BonusResearchHashBytes = lab.extraHashBytes,
            };
        }
        if (entity.tankId > 0)
        {
            var pool = factory.factoryStorage.tankPool;
            if (entity.tankId >= factory.factoryStorage.tankCursor || entity.tankId >= pool.Length
                || pool[entity.tankId].id != entity.tankId || pool[entity.tankId].entityId != entity.id)
                throw new InvalidOperationException("stack_tank_component_unavailable");
            var tank = pool[entity.tankId];
            return new NativeStackDeviceState
            {
                NextComponentId = tank.nextTankId, PreviousComponentId = tank.lastTankId,
                FluidId = tank.fluidId, FluidInc = tank.fluidInc, FluidCapacity = tank.fluidCapacity,
                InputEnabled = tank.inputSwitch, OutputEnabled = tank.outputSwitch,
            };
        }
        if (entity.splitterId > 0)
        {
            var pool = factory.cargoTraffic.splitterPool;
            if (entity.splitterId >= factory.cargoTraffic.splitterCursor || entity.splitterId >= pool.Length
                || pool[entity.splitterId].id != entity.splitterId || pool[entity.splitterId].entityId != entity.id)
                throw new InvalidOperationException("stack_splitter_component_unavailable");
            var splitter = pool[entity.splitterId];
            return new NativeStackDeviceState
            {
                NextComponentId = splitter.topId, PreviousComponentId = splitter.bottomId,
                InputPriority = splitter.inPriority, OutputPriority = splitter.outPriority,
                OutputFilterItemId = splitter.outFilter, OutputFilterPreset = splitter.outFilterPreset,
                PrioritySlotPresets = splitter.prioritySlotPresets,
                NativeBeltIds = new() { splitter.beltA, splitter.beltB, splitter.beltC, splitter.beltD },
                NativeInputBeltIds = new() { splitter.input0, splitter.input1, splitter.input2, splitter.input3 },
                NativeOutputBeltIds = new() { splitter.output0, splitter.output1, splitter.output2, splitter.output3 },
            };
        }
        if (entity.pilerId > 0)
        {
            var traffic = factory.cargoTraffic;
            var pool = traffic.pilerPool;
            if (entity.pilerId >= traffic.pilerCursor || entity.pilerId >= pool.Length
                || pool[entity.pilerId].id != entity.pilerId || pool[entity.pilerId].entityId != entity.id)
                throw new InvalidOperationException("stack_piler_component_unavailable");
            var piler = pool[entity.pilerId];
            return new NativeStackDeviceState
            {
                PilerMode = (int)piler.pilerState, CooldownTicks = piler.cacheCdTick, WorkProgress = piler.timeSpend,
                NativeInputBeltIds = new() { piler.inputBeltId }, NativeOutputBeltIds = new() { piler.outputBeltId },
                CachedCargo = new()
                {
                    new() { Role = "piler-cache-1", ItemId = piler.cacheItemId1, Count = piler.cacheCargoStack1, Inc = piler.cacheCargoInc1 },
                    new() { Role = "piler-cache-2", ItemId = piler.cacheItemId2, Count = piler.cacheCargoStack2, Inc = piler.cacheCargoInc2 },
                },
            };
        }
        if (entity.spraycoaterId > 0)
        {
            var traffic = factory.cargoTraffic;
            var pool = traffic.spraycoaterPool;
            if (entity.spraycoaterId >= traffic.spraycoaterCursor || entity.spraycoaterId >= pool.Length
                || pool[entity.spraycoaterId].id != entity.spraycoaterId || pool[entity.spraycoaterId].entityId != entity.id)
                throw new InvalidOperationException("stack_spraycoater_component_unavailable");
            var coater = pool[entity.spraycoaterId];
            return new NativeStackDeviceState
            {
                NativeBeltIds = new() { coater.cargoBeltId, coater.incBeltId },
                NativeBeltSlots = CaptureNativeAddonBeltSlots(entity),
                ProliferatorItemId = coater.incItemId, ProliferatorAbility = coater.incAbility,
                SprayTimesPerItem = coater.incSprayTimes, RemainingSprays = coater.incCount,
                ExtraSprays = coater.extraIncCount, SprayCapacity = coater.incCapacity,
                ProliferatorUsed = coater.incUsed, WorkProgress = coater.sprayTime, CargoBeltItemId = coater.cargoBeltItemId,
            };
        }
        return null;
    }

    private static List<NativeAddonBeltSlotSnapshot> CaptureNativeAddonBeltSlots(EntityData entity)
    {
        var desc = LDB.models.Select(entity.modelIndex)?.prefabDesc;
        var poses = desc?.addonAreaPoses;
        var boxes = desc?.addonAreaColPoses;
        var sizes = desc?.addonAreaSize;
        if (poses is null || boxes is null || sizes is null || poses.Length < 1 || poses.Length > 16
            || boxes.Length != poses.Length || sizes.Length != poses.Length)
            throw new InvalidOperationException("stack_addon_belt_geometry_unavailable");
        var slots = new List<NativeAddonBeltSlotSnapshot>();
        for (var i = 0; i < poses.Length; i++)
        {
            var rotation = entity.rot * boxes[i].rotation;
            slots.Add(new()
            {
                Index = i, Position = CaptureVector(entity.pos + entity.rot * poses[i].position),
                Forward = CaptureVector(rotation * UnityEngine.Vector3.forward),
                Up = CaptureVector(rotation * UnityEngine.Vector3.up), HalfExtents = CaptureVector(sizes[i]),
            });
        }
        return slots;
    }

    internal string CaptureNativeStackContents(PlanetFactory factory, IReadOnlyList<int> objectIds)
    {
        if (objectIds.Count < 1 || objectIds.Count > 2 * NativeStackingPolicy.MaximumObservedLayers
            || objectIds.Distinct().Count() != objectIds.Count)
            throw new InvalidOperationException("stack_inventory_selection_invalid");
        var fields = new List<object?>();
        foreach (var id in objectIds)
        {
            var snapshot = TryCaptureFactoryEntity(factory, id)
                ?? throw new InvalidOperationException("stack_inventory_member_unavailable");
            fields.Add(id);
            fields.Add(snapshot.ItemId);
            fields.Add(snapshot.RecipeId);
            fields.Add(snapshot.Progress);
            fields.Add(snapshot.ProgressRequired);
            fields.Add(snapshot.TankFluidCount);
            var device = CaptureNativeStackDevice(factory, StackEntity(factory, id));
            fields.Add(device?.BonusProgress);
            fields.Add(device?.ResearchHashBytes);
            fields.Add(device?.BonusResearchHashBytes);
            fields.Add(device?.FluidInc);
            fields.Add(device?.CooldownTicks);
            fields.Add(device?.WorkProgress);
            fields.Add(device?.ProliferatorAbility);
            fields.Add(device?.SprayTimesPerItem);
            fields.Add(device?.RemainingSprays);
            fields.Add(device?.ExtraSprays);
            fields.Add(device?.ProliferatorUsed);
            var entity = StackEntity(factory, id);
            if (entity.dispenserId > 0)
            {
                var dispenser = CaptureNativeDispenser(factory, entity);
                fields.Add(dispenser.ComponentId); fields.Add(dispenser.IdleCouriers); fields.Add(dispenser.WorkingCouriers);
                fields.Add(dispenser.Energy); fields.Add(dispenser.HeldItemCount);
                fields.Add(dispenser.PlayerMode); fields.Add(dispenser.StorageMode); fields.Add(dispenser.FilterItemId);
                fields.Add(dispenser.AutoReplenishCouriers); fields.Add(dispenser.StorageId);
                foreach (var cargo in dispenser.HeldCargo.Concat(dispenser.CourierCargo))
                {
                    fields.Add(cargo.Role); fields.Add(cargo.ItemId); fields.Add(cargo.Count); fields.Add(cargo.Inc);
                }
            }
            foreach (var buffer in device?.CachedCargo ?? Enumerable.Empty<FactoryBufferSnapshot>())
            {
                fields.Add(buffer.Role);
                fields.Add(buffer.ItemId);
                fields.Add(buffer.Count);
                fields.Add(buffer.Inc);
            }
            foreach (var buffer in snapshot.Buffers)
            {
                fields.Add(buffer.Role);
                fields.Add(buffer.ItemId);
                fields.Add(buffer.Count);
                fields.Add(buffer.Inc);
            }
        }
        return CanonicalStateHash.Combine("native-stack-original-contents-v1", fields.ToArray());
    }

    private static NativeDispenserSnapshot CaptureNativeDispenser(PlanetFactory factory, EntityData entity)
    {
        var transport = factory.transport;
        var id = entity.dispenserId;
        if (id <= 0 || id >= transport.dispenserCursor || id >= transport.dispenserPool.Length
            || transport.dispenserPool[id] is not { } dispenser || dispenser.id != id || dispenser.entityId != entity.id
            || dispenser.workCourierDatas is null || dispenser.workCourierDatas.Length > 64
            || dispenser.workCourierCount < 0 || dispenser.workCourierCount > dispenser.workCourierDatas.Length
            || dispenser.holdupPackage is null || dispenser.holdupPackage.Length > 128)
            throw new InvalidOperationException("stack_dispenser_component_unavailable");
        var result = new NativeDispenserSnapshot
        {
            ComponentId = id, StorageId = dispenser.storageId, PlayerMode = (int)dispenser.playerMode,
            StorageMode = (int)dispenser.storageMode, FilterItemId = dispenser.filter,
            IdleCouriers = dispenser.idleCourierCount, WorkingCouriers = dispenser.workCourierCount,
            AutoReplenishCouriers = dispenser.courierAutoReplenish, Energy = dispenser.energy, HeldItemCount = dispenser.holdupItemCount,
        };
        for (var index = 0; index < dispenser.holdupPackage.Length; index++)
        {
            var cargo = dispenser.holdupPackage[index];
            result.HeldCargo.Add(new() { Role = "held-" + index, ItemId = cargo.itemId, Count = cargo.count, Inc = cargo.inc });
        }
        for (var index = 0; index < dispenser.workCourierCount; index++)
        {
            var cargo = dispenser.workCourierDatas[index];
            result.CourierCargo.Add(new() { Role = "courier-" + index, ItemId = cargo.itemId, Count = cargo.itemCount, Inc = cargo.inc });
        }
        return result;
    }
}
