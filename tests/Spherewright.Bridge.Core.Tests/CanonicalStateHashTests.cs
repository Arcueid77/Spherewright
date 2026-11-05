using Spherewright.Bridge.Core.Safety;
using Spherewright.Bridge.Core.Logistics;
using Spherewright.Bridge.Core.Factory;
using Spherewright.Contracts.Actions;
using Spherewright.Contracts.Factory;
using Spherewright.Contracts.Logistics;
using Spherewright.Contracts.Players;
using Spherewright.Contracts.Progression;
using Xunit;

namespace Spherewright.Bridge.Core.Tests;

public sealed class CanonicalStateHashTests
{
    [Fact]
    public void SpaceObservationBindsUniverseMotionAndPauseWithoutChangingAbsentSpaceGroundDomain()
    {
        var p=new Spherewright.Contracts.Players.PlayerStateSnapshot {SessionId="synthetic",PlanetId=0};
        var legacy=CanonicalStateHash.PlayerAction(p);
        p.SpaceTransit=new Spherewright.Contracts.Players.NativeSpaceTransitSnapshot
            {NativePaused=true,UniverseX=100000,VelocityX=.4,Speed=.4};
        var space=CanonicalStateHash.PlayerAction(p);Assert.NotEqual(legacy,space);
        p.SpaceTransit.VelocityX=.3;Assert.NotEqual(space,CanonicalStateHash.PlayerAction(p));
        p.SpaceTransit=null;Assert.Equal(legacy,CanonicalStateHash.PlayerAction(p));
    }
    [Fact]
    public void LogisticsStationChargePolicy_MirrorsBoundedThreeMegawattUiSteps()
    {
        Assert.True(LogisticsStationChargePolicy.TryNormalizeUiPower(
            200_000,
            12_000_000,
            out var requested,
            out var minimum,
            out var maximum));
        Assert.Equal(200_000, requested);
        Assert.Equal(100_000, minimum);
        Assert.Equal(1_000_000, maximum);

        Assert.True(LogisticsStationChargePolicy.TryNormalizeUiPower(
            200_000,
            6_000_000,
            out _,
            out _,
            out _));
        Assert.True(LogisticsStationChargePolicy.TryNormalizeUiPower(
            200_000,
            60_000_000,
            out _,
            out _,
            out _));
        Assert.False(LogisticsStationChargePolicy.TryNormalizeUiPower(
            200_000,
            7_000_000,
            out _,
            out _,
            out _));
        Assert.False(LogisticsStationChargePolicy.TryNormalizeUiPower(
            200_000,
            63_000_000,
            out _,
            out _,
            out _));
    }

    [Fact]
    public void ProgressionSelection_IgnoresNaturalUpload_ButBindsQueueAndEligibility()
    {
        var snapshot = new ProgressionStateSnapshot
        {
            SessionId = "session",
            PlanetId = 104,
            CurrentTechId = 1703,
            TechQueue = new List<int> { 1703 },
            Technologies = new List<TechStateSnapshot>
            {
                new TechStateSnapshot
                {
                    TechId = 1604,
                    CurrentLevel = 0,
                    MaximumLevel = 1,
                    HashRequired = 144_000,
                    PrerequisiteTechIds = new List<int> { 1113, 1603, 3701 },
                },
                new TechStateSnapshot
                {
                    TechId = 1703,
                    CurrentLevel = 0,
                    MaximumLevel = 1,
                    HashUploaded = 100,
                    HashRequired = 288_000,
                    IsLabTech = true,
                    IsQueued = true,
                },
            },
        };

        var fullBeforeUpload = CanonicalStateHash.Progression(snapshot);
        var selectionBeforeUpload = CanonicalStateHash.ProgressionSelection(snapshot);
        snapshot.Technologies[1].HashUploaded = 200;
        snapshot.Technologies[1].UnlockTick = 8_400_000;
        Assert.NotEqual(fullBeforeUpload, CanonicalStateHash.Progression(snapshot));
        Assert.Equal(selectionBeforeUpload, CanonicalStateHash.ProgressionSelection(snapshot));

        snapshot.TechQueue.Add(1604);
        snapshot.Technologies[0].IsQueued = true;
        Assert.NotEqual(selectionBeforeUpload, CanonicalStateHash.ProgressionSelection(snapshot));
        snapshot.TechQueue.Remove(1604);
        snapshot.Technologies[0].IsQueued = false;

        snapshot.Technologies[0].Unlocked = true;
        Assert.NotEqual(selectionBeforeUpload, CanonicalStateHash.ProgressionSelection(snapshot));
    }

    [Fact]
    public void PlayerAction_IgnoresContinuousEnergyRecharge_ButBindsInventory()
    {
        var snapshot = new PlayerStateSnapshot
        {
            SessionId = "session",
            PlanetId = 103,
            IsAlive = true,
            IsOnPlanet = true,
            MovementState = "Walk",
            CoreEnergy = 100d,
            CoreEnergyCapacity = 1_000d,
            ReactorEnergy = 120d,
            ReactorItemId = 1006,
            ReactorItemInc = 0,
            AutoReplenishFuel = true,
            FuelStorageSlotCount = 4,
            InventorySlotCount = 40,
            ConstructionDrones = new ConstructionDroneSnapshot
            {
                Enabled = true,
                ConstructionEnabled = true,
                Total = 3,
                Alive = 3,
                Idle = 3,
            },
        };

        var beforeRecharge = CanonicalStateHash.PlayerAction(snapshot);
        snapshot.CoreEnergy = 200d;
        Assert.Equal(beforeRecharge, CanonicalStateHash.PlayerAction(snapshot));
        snapshot.ReactorEnergy = 60d;
        Assert.Equal(beforeRecharge, CanonicalStateHash.PlayerAction(snapshot));

        snapshot.FuelStorage.Add(new PlayerInventoryItem
        {
            ItemId = 1006,
            Count = 3,
            SlotCount = 1,
        });
        snapshot.FuelStorageOccupiedSlotCount = 1;
        var withFuel = CanonicalStateHash.PlayerAction(snapshot);
        Assert.NotEqual(beforeRecharge, withFuel);

        snapshot.Inventory.Add(new PlayerInventoryItem
        {
            ItemId = 1001,
            Count = 1,
            SlotCount = 1,
        });
        snapshot.InventoryOccupiedSlotCount = 1;
        Assert.NotEqual(withFuel, CanonicalStateHash.PlayerAction(snapshot));
    }

    [Fact]
    public void Factory_BindsSorterTopologyFilterAndHeldCargo()
    {
        var snapshot = new FactoryEntitySnapshot
        {
            SessionId = "session",
            PlanetId = 103,
            ObjectId = 12,
            ObjectKind = FactoryObjectKinds.Entity,
            ItemId = 2011,
            ComponentKind = "inserter",
            PickTargetObjectId = 10,
            InsertTargetObjectId = 20,
            InserterStage = "Picking",
            InserterStackCount = 0,
        };

        var unfiltered = CanonicalStateHash.Factory(snapshot);
        snapshot.FilterItemId = 1120;
        var filtered = CanonicalStateHash.Factory(snapshot);
        Assert.NotEqual(unfiltered, filtered);

        snapshot.Buffers.Add(new FactoryBufferSnapshot
        {
            Role = "inserter-held",
            ItemId = 1120,
            Count = 1,
        });
        snapshot.InserterStage = "Sending";
        snapshot.InserterStackCount = 1;
        Assert.NotEqual(filtered, CanonicalStateHash.Factory(snapshot));
    }

    [Theory]
    [InlineData(2203, 0)]
    [InlineData(2204, 1120)]
    [InlineData(2211, 1802)]
    public void FactoryConfiguration_PowerGeneratorBindsInstantaneousGeneration(int buildingItemId, int fuelItemId)
    {
        var snapshot = new FactoryEntitySnapshot
        {
            SessionId = "session",
            PlanetId = 103,
            ObjectId = 12,
            ObjectKind = FactoryObjectKinds.Entity,
            ItemId = buildingItemId,
            ComponentKind = "power-generator",
            CapturedAtGameTick = 100,
        };
        snapshot.Buffers.Add(FactoryBufferSemantics.PowerGeneration(492, fuelItemId, string.Empty));

        var original = CanonicalStateHash.FactoryConfiguration(snapshot);
        snapshot.CapturedAtGameTick = 101;
        Assert.Equal(original, CanonicalStateHash.FactoryConfiguration(snapshot));

        // This domain includes buffers. A different hash is not, by itself,
        // evidence that a generator moved or its static configuration changed.
        snapshot.Buffers[0].Count = 488;
        Assert.NotEqual(original, CanonicalStateHash.FactoryConfiguration(snapshot));
        snapshot.Buffers[0].Count = 492;
        Assert.Equal(original, CanonicalStateHash.FactoryConfiguration(snapshot));
    }

    [Fact]
    public void FactoryConfiguration_IgnoresCargoFreeReturnProgress_ButBindsFilterTopologyAndCargo()
    {
        var snapshot = new FactoryEntitySnapshot
        {
            SessionId = "session",
            PlanetId = 103,
            ObjectId = 12,
            ObjectKind = FactoryObjectKinds.Entity,
            ItemId = 2011,
            ComponentKind = "inserter",
            PickTargetObjectId = 10,
            InsertTargetObjectId = 20,
            InserterStage = "Returning",
            InserterStackCount = 0,
            IsWorking = true,
            Progress = 200_000,
            ProgressRequired = 600_000,
        };
        snapshot.Connections.Add(new FactoryConnectionSnapshot
        {
            Slot = 0,
            IsOutput = true,
            OtherObjectId = 20,
            OtherSlot = 1,
        });

        var returning = CanonicalStateHash.FactoryConfiguration(snapshot);
        snapshot.InserterStage = "Picking";
        snapshot.IsWorking = false;
        snapshot.Progress = 0;
        Assert.Equal(returning, CanonicalStateHash.FactoryConfiguration(snapshot));

        snapshot.FilterItemId = 6001;
        var filtered = CanonicalStateHash.FactoryConfiguration(snapshot);
        Assert.NotEqual(returning, filtered);

        snapshot.Buffers.Add(new FactoryBufferSnapshot
        {
            Role = "inserter-held",
            ItemId = 6001,
            Count = 1,
        });
        snapshot.InserterStackCount = 1;
        Assert.NotEqual(filtered, CanonicalStateHash.FactoryConfiguration(snapshot));

        snapshot.Buffers.Clear();
        snapshot.InserterStackCount = 0;
        snapshot.Connections[0].OtherObjectId = 21;
        Assert.NotEqual(filtered, CanonicalStateHash.FactoryConfiguration(snapshot));
    }

    [Fact]
    public void SorterFilterPolicy_RequiresConnectedCargoFreeSorter_ButNotIdleMotionStage()
    {
        Assert.True(SorterFilterPolicy.IsSafeAssignmentWindow(
            6001,
            10,
            20,
            0,
            0,
            0,
            0));
        Assert.False(SorterFilterPolicy.IsSafeAssignmentWindow(
            6001,
            0,
            20,
            0,
            0,
            0,
            0));
        Assert.False(SorterFilterPolicy.IsSafeAssignmentWindow(
            6001,
            10,
            20,
            1104,
            1,
            1,
            0));
        Assert.False(SorterFilterPolicy.IsSafeAssignmentWindow(
            -1,
            10,
            20,
            0,
            0,
            0,
            0));
    }

    [Fact]
    public void FactoryEndpoint_IgnoresProductionProgressAndBuffers_ButBindsConnections()
    {
        var snapshot = new FactoryEntitySnapshot
        {
            SessionId = "session",
            PlanetId = 103,
            ObjectId = 12,
            ObjectKind = FactoryObjectKinds.Entity,
            ItemId = 2301,
            ComponentKind = "miner",
            Position = new Vector3Snapshot { X = 1f, Y = 2f, Z = 3f },
            Rotation = new QuaternionSnapshot { W = 1f },
            Progress = 10,
        };
        snapshot.Buffers.Add(new FactoryBufferSnapshot
        {
            Role = "mined-output",
            ItemId = 1001,
            Count = 1,
        });

        var endpoint = CanonicalStateHash.FactoryEndpoint(snapshot);
        snapshot.Progress = 20;
        snapshot.Buffers[0].Count = 50;
        Assert.Equal(endpoint, CanonicalStateHash.FactoryEndpoint(snapshot));

        snapshot.Connections.Add(new FactoryConnectionSnapshot
        {
            Slot = 0,
            IsOutput = true,
            OtherObjectId = 13,
            OtherSlot = 1,
        });
        Assert.NotEqual(endpoint, CanonicalStateHash.FactoryEndpoint(snapshot));
    }

    [Fact]
    public void LogisticsStation_SeparatesVolatileInventoryFromConfiguration()
    {
        var snapshot = new LogisticsStationSnapshot
        {
            SessionId = "session",
            PlanetId = 103,
            EntityId = 12,
            StationId = 4,
            GalacticStationId = 9,
            BuildingItemId = 2104,
            IsInterstellar = true,
            Energy = 100,
            EnergyCapacity = 10_000,
            RequestedChargeEnergyPerTick = 100,
            RequestedChargePowerWatts = 6_000,
            MaximumChargeEnergyPerTick = 200,
            MaximumChargePowerWatts = 12_000,
            IdleDroneCount = 10,
            DroneTripRangeRaw = 180d,
            VesselTripRangeRaw = 12d,
            RemoteRoutePriority = "Ignore",
        };
        snapshot.StorageSlots.Add(new LogisticsStationStorageSlotSnapshot
        {
            Index = 0,
            ItemId = 1004,
            Count = 20,
            MaximumCount = 1_000,
            LocalLogic = "Supply",
            RemoteLogic = "Demand",
        });
        snapshot.BeltSlots.Add(new LogisticsStationBeltSlotSnapshot
        {
            Index = 0,
            Direction = "Input",
            BeltComponentId = 8,
            BeltEntityId = 19,
            StorageIndex = 0,
            Counter = 3,
        });

        var live = CanonicalStateHash.LogisticsStation(snapshot);
        var configuration = CanonicalStateHash.LogisticsStationConfiguration(snapshot);

        snapshot.Energy = 500;
        snapshot.RequestedChargeEnergyPerTick = 150;
        snapshot.RequestedChargePowerWatts = 9_000;
        snapshot.StorageSlots[0].Count = 40;
        snapshot.BeltSlots[0].Counter = 4;
        Assert.NotEqual(live, CanonicalStateHash.LogisticsStation(snapshot));
        Assert.Equal(configuration, CanonicalStateHash.LogisticsStationConfiguration(snapshot));

        snapshot.MaximumChargeEnergyPerTick = 300;
        snapshot.MaximumChargePowerWatts = 18_000;
        Assert.NotEqual(configuration, CanonicalStateHash.LogisticsStationConfiguration(snapshot));
        configuration = CanonicalStateHash.LogisticsStationConfiguration(snapshot);

        snapshot.StorageSlots[0].MaximumCount = 2_000;
        Assert.NotEqual(configuration, CanonicalStateHash.LogisticsStationConfiguration(snapshot));
        configuration = CanonicalStateHash.LogisticsStationConfiguration(snapshot);

        snapshot.BeltSlots[0].StorageIndex = 1;
        Assert.NotEqual(configuration, CanonicalStateHash.LogisticsStationConfiguration(snapshot));
    }

    [Fact]
    public void LogisticsStationFleet_HashesOnlyFleetIdentityAndCounters()
    {
        var snapshot = new LogisticsStationSnapshot
        {
            SessionId = "session",
            PlanetId = 104,
            EntityId = 920,
            StationId = 2,
            GalacticStationId = 7,
            BuildingItemId = 2104,
            IsInterstellar = true,
            DroneCapacity = 50,
            VesselCapacity = 10,
            IdleDroneCount = 10,
            WorkingDroneCount = 2,
            IdleVesselCount = 3,
            WorkingVesselCount = 1,
        };

        var fleet = CanonicalStateHash.LogisticsStationFleet(snapshot);
        snapshot.Energy = 10_000;
        snapshot.StorageSlots.Add(new LogisticsStationStorageSlotSnapshot { Index = 0, Count = 100 });
        Assert.Equal(fleet, CanonicalStateHash.LogisticsStationFleet(snapshot));

        snapshot.IdleDroneCount++;
        Assert.NotEqual(fleet, CanonicalStateHash.LogisticsStationFleet(snapshot));
    }

    [Fact]
    public void LogisticsStationFleetTransferPolicy_EnforcesUiCapacityIdleAndProliferatorRules()
    {
        Assert.True(LogisticsStationFleetTransferPolicy.TryValidate(
            true, false, false,
            LogisticsStationFleetTransferDirections.PlayerToStation,
            LogisticsFleetItemIds.Drone,
            10, 10, 0,
            30, 10, 50,
            2, 1, 10,
            true,
            out _));

        Assert.False(LogisticsStationFleetTransferPolicy.TryValidate(
            true, false, false,
            LogisticsStationFleetTransferDirections.PlayerToStation,
            LogisticsFleetItemIds.Drone,
            10, 10, 1,
            30, 10, 50,
            2, 1, 10,
            true,
            out var proliferated));
        Assert.Contains("Proliferated", proliferated, StringComparison.Ordinal);

        Assert.False(LogisticsStationFleetTransferPolicy.TryValidate(
            false, false, false,
            LogisticsStationFleetTransferDirections.PlayerToStation,
            LogisticsFleetItemIds.Vessel,
            1, 1, 0,
            0, 0, 50,
            0, 0, 0,
            true,
            out _));

        Assert.False(LogisticsStationFleetTransferPolicy.TryValidate(
            true, false, false,
            LogisticsStationFleetTransferDirections.StationToPlayer,
            LogisticsFleetItemIds.Vessel,
            3, 0, 0,
            0, 0, 50,
            2, 1, 10,
            true,
            out _));

        Assert.True(LogisticsStationFleetTransferPolicy.TryValidate(
            true, false, false,
            LogisticsStationFleetTransferDirections.StationToPlayer,
            LogisticsFleetItemIds.Vessel,
            2, 0, 0,
            0, 0, 50,
            2, 1, 10,
            true,
            out _));
    }
}
