using System.Text.Json;
using Spherewright.Contracts.Actions;
using Spherewright.Contracts.Diagnostics;
using Spherewright.Contracts.Errors;
using Spherewright.Contracts.Factory;
using Spherewright.Contracts.Journals;
using Spherewright.Contracts.Logistics;
using Spherewright.Contracts.Players;
using Spherewright.Contracts.Protocol;
using Spherewright.Contracts.Progression;
using Spherewright.Contracts.Sessions;
using Spherewright.Contracts.Versioning;
using Xunit;

namespace Spherewright.Contracts.Tests;

public sealed class ProtocolContractTests
{
    [Fact]
    public void FuelPowerCatalogProfileIsOptionalAndPreservesLongNativeBaseRatings()
    {
        var legacy = JsonSerializer.Deserialize<BuildCatalogItem>("{}", JsonOptions)!;
        Assert.Null(legacy.FuelPowerProfile);
        Assert.Null(JsonSerializer.Deserialize<BuildCatalogItem>("{\"fuelPowerProfile\":null}", JsonOptions)!.FuelPowerProfile);
        legacy.ItemId = 2211;
        legacy.FuelPowerProfile = new FuelPowerCatalogProfile
        {
            GenerationEnergyPerTick = 3000000000L,
            FuelEnergyUsePerTick = 4000000000L,
            FuelTypeMask = 2,
        };
        var json = JsonSerializer.Serialize(legacy, JsonOptions);
        var copy = JsonSerializer.Deserialize<BuildCatalogItem>(json, JsonOptions)!.FuelPowerProfile!;
        Assert.Equal(3000000000L, copy.GenerationEnergyPerTick);
        Assert.Equal(4000000000L, copy.FuelEnergyUsePerTick);
        Assert.Equal(2, copy.FuelTypeMask);
        Assert.Null(legacy.WindGenerationAtCurrentPlanetPerTick);
    }

    [Theory]
    [InlineData("native_single_belt_segment", 3, 0)]
    [InlineData("native_two_belt_segments", -1, 5)]
    public void InserterAttachmentPlanIsOptionalAndRetainsSignedOffsetsWithoutPrivateGeometry(string mode, int destinationSlot, int outputOffset)
    {
        var legacy = JsonSerializer.Deserialize<PreparedNormalAction>("{}", JsonOptions)!;
        Assert.Null(legacy.PlannedInserterAttachment);
        var prepared = new PreparedNormalAction
        {
            PlannedInserterAttachment = new InserterAttachmentPlanSnapshot
            {
                Mode = mode, SourceSlot = -1, DestinationSlot = destinationSlot,
                InputOffset = -4, OutputOffset = outputOffset,
                SourcePosition = new Vector3Snapshot { X = 1, Y = 200, Z = 4 },
                DestinationPosition = new Vector3Snapshot { X = 1, Y = 200, Z = 0 },
            },
        };
        var json = JsonSerializer.Serialize(prepared, JsonOptions);
        var copy = JsonSerializer.Deserialize<PreparedNormalAction>(json, JsonOptions)!.PlannedInserterAttachment!;
        Assert.Equal(mode, copy.Mode);
        Assert.Equal(-1, copy.SourceSlot); Assert.Equal(destinationSlot, copy.DestinationSlot);
        Assert.Equal(-4, copy.InputOffset); Assert.Equal(outputOffset, copy.OutputOffset);
        Assert.Equal(4, copy.SourcePosition.Z);
        Assert.DoesNotContain("geometryHash", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("pathId", json, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void StorageReadbackIsOptionalForOldActionsAndRoundTripsItsInstantBoundary()
    {
        var action = JsonSerializer.Deserialize<ActionResultSnapshot>("{}", JsonOptions)!;
        Assert.Null(action.StorageConfigurationReadback);
        action.StorageConfigurationReadback = new StorageConfigurationReadback
        {
            CapturedAtGameTick = 100, EntityId = 761, Operation = StorageConfigurationOperations.SetBans,
            ConfigurationBefore = new StorageConfigurationSnapshot { GridCount = 30, BannedGridCount = 30 },
            ConfigurationAfter = new StorageConfigurationSnapshot { GridCount = 30, BannedGridCount = 0 },
            BuffersBefore = new List<FactoryBufferSnapshot> { new() { ItemId = 1120, Count = 1, Inc = 2 } },
            BuffersAfter = new List<FactoryBufferSnapshot> { new() { ItemId = 1120, Count = 1, Inc = 2 } },
            VerifiedConnectionCount = 5,
        };
        var copy = JsonSerializer.Deserialize<ActionResultSnapshot>(JsonSerializer.Serialize(action, JsonOptions), JsonOptions)!;
        var proof = copy.StorageConfigurationReadback!;
        Assert.Equal(100, proof.CapturedAtGameTick); Assert.Equal(761, proof.EntityId);
        Assert.Equal("synchronous_native_configuration_boundary", proof.EvidenceScope);
        Assert.Equal(30, proof.ConfigurationBefore.BannedGridCount); Assert.Equal(0, proof.ConfigurationAfter.BannedGridCount);
        Assert.Equal(1, Assert.Single(proof.BuffersAfter).Count); Assert.Equal(2, proof.BuffersAfter[0].Inc);
    }

    [Fact]
    public void StorageCapacityFieldsAreOptionalAndRoundTripExplicitUiIntent()
    {
        var old = JsonSerializer.Deserialize<PrepareConfigureBuildingRequest>("{}", JsonOptions)!;
        Assert.Equal("", old.StorageOperation); Assert.Equal(-1, old.StorageBannedGridCount);
        Assert.Equal(BuildingConfigurationModes.Production, old.Mode);
        old.Mode = BuildingConfigurationModes.StorageCapacity;
        old.StorageOperation = StorageConfigurationOperations.SetBans; old.StorageBannedGridCount = 30;
        var copy = JsonSerializer.Deserialize<PrepareConfigureBuildingRequest>(JsonSerializer.Serialize(old, JsonOptions), JsonOptions)!;
        Assert.Equal(StorageConfigurationOperations.SetBans, copy.StorageOperation); Assert.Equal(30, copy.StorageBannedGridCount);
    }

    [Fact]
    public void StorageConfigurationEchoIsOptionalForOldPlansAndCarriesNoInventory()
    {
        var old = JsonSerializer.Deserialize<PreparedNormalAction>("{}", JsonOptions)!;
        Assert.Null(old.PlannedStorageConfiguration); Assert.Null(old.PlannedStorageOperation);
        old.PlannedStorageOperation = StorageConfigurationOperations.FilterEmptyOrMatching;
        old.PlannedStorageConfiguration = new StorageConfigurationSnapshot { GridCount = 2, Mode = "filtered", GridFilterItemIds = new List<int> { 1114, 1000 } };
        var copy = JsonSerializer.Deserialize<PreparedNormalAction>(JsonSerializer.Serialize(old, JsonOptions), JsonOptions)!;
        Assert.Equal(new[] { 1114, 1000 }, copy.PlannedStorageConfiguration!.GridFilterItemIds);
    }

    [Fact]
    public void OptionalSorterGeometryDistinguishesOldDtoAndUnknownVirtualOccupancy()
    {
        var old = JsonSerializer.Deserialize<FactoryEntitySnapshot>("{}", JsonOptions)!;
        Assert.Null(old.SorterEndpoints);
        old.SorterEndpoints = new SorterEndpointObservation
        {
            State = "observed", Kind = "belt_virtual", CapturedAtGameTick = 123,
            Endpoints = new List<SorterEndpointSnapshot> { new SorterEndpointSnapshot { Slot = -1, Outward = new Vector3Snapshot { Z = 1 } } },
        };
        var copy = JsonSerializer.Deserialize<FactoryEntitySnapshot>(JsonSerializer.Serialize(old, JsonOptions), JsonOptions)!;
        Assert.Equal(123, copy.SorterEndpoints!.CapturedAtGameTick);
        var point = Assert.Single(copy.SorterEndpoints.Endpoints);
        Assert.Equal(-1, point.Slot); Assert.Null(point.Occupied); Assert.Equal(1, point.Outward.Z);
    }

    private static readonly JsonSerializerOptions JsonOptions = new JsonSerializerOptions
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    [Fact]
    public void OldBuildRequestStillMeansUnfilteredAndNewFilterRoundTrips()
    {
        var old = JsonSerializer.Deserialize<PrepareBuildRequest>("{\"buildingItemId\":2011}", JsonOptions)!;
        Assert.Equal(0, old.InitialSorterFilterItemId);
        old.InitialSorterFilterItemId = 1000;
        var copy = JsonSerializer.Deserialize<PrepareBuildRequest>(JsonSerializer.Serialize(old, JsonOptions), JsonOptions)!;
        Assert.Equal(1000, copy.InitialSorterFilterItemId);
    }

    [Theory]
    [InlineData(null)]
    [InlineData(0)]
    [InlineData(1000)]
    public void PreparedBuildDisclosesFilterWithoutConfusingAbsentAndUnfiltered(int? filter)
    {
        var prepared = new PreparedNormalAction { PlannedSorterFilterItemId = filter };
        var copy = JsonSerializer.Deserialize<PreparedNormalAction>(JsonSerializer.Serialize(prepared, JsonOptions), JsonOptions)!;
        Assert.Equal(filter, copy.PlannedSorterFilterItemId);
    }

    [Fact]
    public void BeltCargoUnavailableDoesNotSerializeAsObservedZero()
    {
        var snapshot = new FactoryEntitySnapshot { BeltCargo = new BeltCargoSnapshot { ReasonCode = "cargo_path_unavailable" } };
        using var json = JsonDocument.Parse(JsonSerializer.Serialize(snapshot, JsonOptions));
        var cargo = json.RootElement.GetProperty("beltCargo");
        Assert.Equal("unavailable", cargo.GetProperty("state").GetString());
        Assert.Equal(JsonValueKind.Null, cargo.GetProperty("itemCount").ValueKind);
        Assert.Equal(JsonValueKind.Null, cargo.GetProperty("cargoStackCount").ValueKind);
        Assert.Equal("cargo_path_unavailable", cargo.GetProperty("reasonCode").GetString());
        Assert.Equal(0, cargo.GetProperty("items").GetArrayLength());
        Assert.Null(JsonSerializer.Deserialize<FactoryEntitySnapshot>("{\"objectId\":1}", JsonOptions)!.BeltCargo);
    }

    [Fact]
    public void BeltCargoObservationRoundTripsExplicitLocalCoverageAndNativeStackUnits()
    {
        var snapshot = new FactoryEntitySnapshot
        {
            BeltCargo = new BeltCargoSnapshot
            {
                State = "observed", CapturedAtGameTick = 1234, PathId = 9, PathLengthCells = 1000,
                SegmentStartCell = 100, SegmentLengthCells = 10, PathClosed = false,
                CargoStackCount = 1, ItemCount = 4,
                Items = new List<BeltCargoItemSnapshot> { new() { ItemId = 1112, Count = 4, Inc = 12, CargoStackCount = 1 } },
            },
        };
        var copy = JsonSerializer.Deserialize<FactoryEntitySnapshot>(JsonSerializer.Serialize(snapshot, JsonOptions), JsonOptions)!;
        Assert.Empty(copy.Buffers);
        Assert.Equal("unique_stacks_touching_selected_belt_segment", copy.BeltCargo!.Coverage);
        Assert.Equal(1234, copy.BeltCargo.CapturedAtGameTick);
        Assert.Equal(4, copy.BeltCargo.ItemCount);
        Assert.Equal(12, Assert.Single(copy.BeltCargo.Items).Inc);
    }

    [Fact]
    public void UpgradeResultCarriesImmediateCargoAndTimingNotLaterPollState()
    {
        Assert.Null(new ActionResultSnapshot().UpgradeReadback);
        var snapshot = new ActionResultSnapshot
        {
            Terminal = true, Succeeded = true, ActionKind = NormalActionKinds.Upgrade,
            UpgradeReadback = new UpgradeReadback
            {
                CapturedAtGameTick = 500, SourceObjectId = 7, ResultObjectId = 17, SourceItemId = 2011,
                TargetItemId = 2012, FilterItemId = 1101, VerifiedConnectionCount = 2,
                NativeTimingPolicy = "basic_sorter_cycle_fraction_retained",
                ProgressBefore = 123456, ProgressAfter = 61728,
                BuffersBefore = new List<FactoryBufferSnapshot> { new FactoryBufferSnapshot { ItemId = 1101, Count = 1, Inc = 2 } },
                BuffersAfter = new List<FactoryBufferSnapshot> { new FactoryBufferSnapshot { ItemId = 1101, Count = 1, Inc = 2 } },
            },
        };
        using var json = JsonDocument.Parse(JsonSerializer.Serialize(snapshot, JsonOptions));
        var proof = json.RootElement.GetProperty("upgradeReadback");
        Assert.Equal(500, proof.GetProperty("capturedAtGameTick").GetInt64());
        Assert.Equal(17, proof.GetProperty("resultObjectId").GetInt32());
        Assert.Equal(1101, proof.GetProperty("filterItemId").GetInt32());
        Assert.Equal(1, proof.GetProperty("buffersBefore")[0].GetProperty("count").GetInt32());
        Assert.Equal(2, proof.GetProperty("buffersAfter")[0].GetProperty("inc").GetInt32());
    }

    [Fact]
    public void ResearchMatrixBufferDeclaresNativePointScaleWithoutChangingRawCount()
    {
        Assert.Equal(1, new FactoryBufferSnapshot().UnitsPerItem);
        Assert.Equal("items", new FactoryBufferSnapshot().CountUnit);
        var research = new FactoryBufferSnapshot
        { Role = "research-matrix", Count = 36000, Inc = 72000, UnitsPerItem = 3600, CountUnit = "research_matrix_points" };
        using var json = JsonDocument.Parse(JsonSerializer.Serialize(research, JsonOptions));
        Assert.Equal(36000, json.RootElement.GetProperty("count").GetInt32());
        Assert.Equal(3600, json.RootElement.GetProperty("unitsPerItem").GetInt32());
        Assert.Equal("research_matrix_points", json.RootElement.GetProperty("countUnit").GetString());
        Assert.Equal(10, research.Count / research.UnitsPerItem);
    }

    [Fact]
    public void ProgressionSelection_UsesDedicatedStableHashContract()
    {
        var snapshot = new ProgressionStateSnapshot
        {
            StateHash = "sha256:full",
            SelectionStateHash = "sha256:selection",
            SelectionStateHashVersion = 1,
        };
        var request = new PrepareSelectResearchRequest
        {
            PlanetId = 104,
            TechId = 1604,
            ExpectedSelectionStateHash = "sha256:selection",
        };

        using var snapshotJson = JsonDocument.Parse(JsonSerializer.Serialize(snapshot, JsonOptions));
        using var requestJson = JsonDocument.Parse(JsonSerializer.Serialize(request, JsonOptions));

        Assert.Equal("sha256:selection", snapshotJson.RootElement.GetProperty("selectionStateHash").GetString());
        Assert.Equal(1, snapshotJson.RootElement.GetProperty("selectionStateHashVersion").GetInt32());
        Assert.Equal("sha256:selection", requestJson.RootElement.GetProperty("expectedSelectionStateHash").GetString());
    }

    [Fact]
    public void FactoryEntity_ExposesDedicatedConfigurationHashContract()
    {
        var snapshot = new FactoryEntitySnapshot
        {
            StateHash = "sha256:full",
            ConfigurationStateHash = "sha256:configuration",
            ConfigurationStateHashVersion = 1,
            EndpointStateHash = "sha256:endpoint",
        };

        using var json = JsonDocument.Parse(JsonSerializer.Serialize(snapshot, JsonOptions));

        Assert.Equal("sha256:configuration", json.RootElement.GetProperty("configurationStateHash").GetString());
        Assert.Equal(1, json.RootElement.GetProperty("configurationStateHashVersion").GetInt32());
    }

    [Fact]
    public void PlayerState_ExposesMechaResearchReservationWithoutSaveIdentity()
    {
        var snapshot = new PlayerStateSnapshot
        {
            AutoManageResearchItems = true,
            MechaResearchPower = 0d,
            MechaResearchItemBuffer = new List<MechaResearchItemSnapshot>
            {
                new MechaResearchItemSnapshot
                {
                    ItemId = 6001,
                    Name = "Electromagnetic Matrix",
                    PointCount = 903_600,
                    WholeItemCount = 251,
                    RemainderPoints = 0,
                },
            },
        };

        var text = JsonSerializer.Serialize(snapshot, JsonOptions);
        using var json = JsonDocument.Parse(text);
        var reserved = json.RootElement.GetProperty("mechaResearchItemBuffer")[0];

        Assert.True(json.RootElement.GetProperty("autoManageResearchItems").GetBoolean());
        Assert.Equal(0d, json.RootElement.GetProperty("mechaResearchPower").GetDouble());
        Assert.Equal(903_600, reserved.GetProperty("pointCount").GetInt32());
        Assert.Equal(251, reserved.GetProperty("wholeItemCount").GetInt32());
        Assert.Equal(0, reserved.GetProperty("remainderPoints").GetInt32());
        Assert.DoesNotContain("saveName", text, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void BridgeStatus_UsesStableCamelCaseContract()
    {
        var status = new BridgeStatus
        {
            BridgeConnected = true,
            BridgeInstanceId = "instance",
            PluginVersion = SpherewrightProduct.CurrentVersion,
            ProtocolVersion = ProtocolConstants.CurrentVersion,
            GameVersion = "0.10.34.28529",
            GameLoaded = false,
            WritesConfigured = false,
            WriteHealth = WriteHealthStates.Healthy,
        };

        var json = JsonSerializer.Serialize(status, JsonOptions);

        Assert.Contains("\"bridgeConnected\":true", json, StringComparison.Ordinal);
        Assert.Contains("\"protocolVersion\":1", json, StringComparison.Ordinal);
        Assert.DoesNotContain("authToken", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("pipeName", json, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void BridgeStatus_PerformanceIsOptionalAndVersioned()
    {
        var legacy = JsonSerializer.Deserialize<BridgeStatus>("{\"bridgeConnected\":true}", JsonOptions);
        Assert.Null(legacy!.RuntimePerformance);
        var status = new BridgeStatus { RuntimePerformance = new RuntimePerformanceSnapshot
        {
            SampleCount = 0, SampleCapacity = 600, PendingMainThreadRequests = 2,
            MainThreadQueueCapacity = 128, MaxRequestsPerFrame = 8, MainThreadBudgetMs = 2,
        } };
        using var json = JsonDocument.Parse(JsonSerializer.Serialize(status, JsonOptions));
        var metrics = json.RootElement.GetProperty("runtimePerformance");
        Assert.Equal(1, metrics.GetProperty("version").GetInt32());
        Assert.Equal("unity_update_stopwatch_v1", metrics.GetProperty("source").GetString());
        Assert.Equal("plugin-process", metrics.GetProperty("scope").GetString());
        Assert.Equal(JsonValueKind.Null, metrics.GetProperty("updateInterval").ValueKind);
        Assert.Equal(JsonValueKind.Null, metrics.GetProperty("sampleAgeMs").ValueKind);
        Assert.Equal(2, metrics.GetProperty("pendingMainThreadRequests").GetInt32());
    }

    [Fact]
    public void ErrorCodes_HaveStableWireValues()
    {
        Assert.Equal("BRIDGE_NOT_READY", BridgeErrorCodes.BridgeNotReady);
        Assert.Equal("AUTH_FAILED", BridgeErrorCodes.AuthFailed);
        Assert.Equal("STALE_REVISION", BridgeErrorCodes.StaleRevision);
        Assert.Equal("ACTION_OUTCOME_UNKNOWN", BridgeErrorCodes.ActionOutcomeUnknown);
        Assert.Equal("SANDBOX_MODE_ACTIVE", BridgeErrorCodes.SandboxModeActive);
        Assert.Equal("USER_CONFIRMATION_REQUIRED", BridgeErrorCodes.UserConfirmationRequired);
        Assert.Equal("confirmed_disabled", SandboxModeStates.ConfirmedDisabled);
    }

    [Fact]
    public void UserSaveImportContracts_ExposeConversationGateWithoutSaveIdentityOrCode()
    {
        var plan = new PreparedUserSaveImportPlan
        {
            Prepared = true,
            PlanToken = "opaque-plan",
            ExpectedRevision = 7,
            OriginalSavePreserved = true,
            HistoricalCoverageComplete = false,
            UserConfirmationRequired = true,
            ConfirmationPrompt = "Confirm this exact import in the conversation.",
            CommitAllowedNow = false,
        };
        var commit = new CommitUserSaveImportRequest
        {
            PlanToken = "opaque-plan",
            IdempotencyKey = "f1078b10-c48b-430f-b0e0-4de18438762c",
            UserConfirmedInConversation = true,
            AcknowledgeOriginalSaveRemainsUnchanged = true,
            AcknowledgeJournalStartsAtImport = true,
        };
        var result = new UserSaveImportResult
        {
            ActionId = "action",
            Accepted = true,
            State = NormalActionStates.Completed,
            OriginalSavePreserved = true,
            HistoricalCoverageComplete = false,
        };

        using var planJson = JsonDocument.Parse(JsonSerializer.Serialize(plan, JsonOptions));
        using var commitJson = JsonDocument.Parse(JsonSerializer.Serialize(commit, JsonOptions));
        using var resultJson = JsonDocument.Parse(JsonSerializer.Serialize(result, JsonOptions));

        Assert.True(planJson.RootElement.GetProperty("userConfirmationRequired").GetBoolean());
        Assert.False(planJson.RootElement.GetProperty("commitAllowedNow").GetBoolean());
        Assert.True(commitJson.RootElement.GetProperty("userConfirmedInConversation").GetBoolean());
        Assert.False(commitJson.RootElement.TryGetProperty("authorizationCode", out _));
        Assert.False(planJson.RootElement.TryGetProperty("saveName", out _));
        Assert.False(planJson.RootElement.TryGetProperty("savePath", out _));
        Assert.False(planJson.RootElement.TryGetProperty("expectedPlanetId", out _));
        Assert.False(planJson.RootElement.TryGetProperty("expectedGameTick", out _));
        Assert.False(resultJson.RootElement.TryGetProperty("saveName", out _));
        Assert.False(resultJson.RootElement.TryGetProperty("savePath", out _));
        Assert.Equal(
            GameplayJournalTrackingModes.AttachedExistingSave,
            planJson.RootElement.GetProperty("journalTrackingMode").GetString());
        Assert.False(planJson.RootElement.GetProperty("historicalCoverageComplete").GetBoolean());
    }

    [Fact]
    public void RequestEnvelope_DefaultsToCurrentProtocol()
    {
        var request = new BridgeRequestEnvelope<EmptyPayload>();

        Assert.Equal(ProtocolConstants.CurrentVersion, request.ProtocolVersion);
        Assert.Equal(BridgeMessageTypes.Request, request.MessageType);
    }

    [Fact]
    public void GameplayJournal_ExposesTimesWithoutRawSaveIdentity()
    {
        var journal = new GameplayJournalSnapshot
        {
            SessionId = "session",
            JournalId = "opaque-hash",
            CreatedAtActualTime = "2026-09-01T00:00:00+08:00",
            TrackingStartedAtGameTick = 120,
            TrackingStartedAtGameTime = "000d 00:00:02",
            DurableThroughSequence = 0,
            PersistencePending = true,
            PersistenceError = "IOException",
            Entries = new List<GameplayJournalEntry>
            {
                new GameplayJournalEntry
                {
                    Sequence = 1,
                    Kind = GameplayJournalEventKinds.ManualItemFirst,
                    ItemId = 1101,
                    ActualTime = "2026-09-01T00:00:01+08:00",
                    GameTick = 180,
                    GameTime = "000d 00:00:03",
                },
            },
        };

        var json = JsonSerializer.Serialize(journal, JsonOptions);
        using var parsed = JsonDocument.Parse(json);
        var entry = parsed.RootElement.GetProperty("entries")[0];

        Assert.Equal("2026-09-01T00:00:01+08:00", entry.GetProperty("actualTime").GetString());
        Assert.Equal(180, entry.GetProperty("gameTick").GetInt64());
        Assert.Equal(0, parsed.RootElement.GetProperty("durableThroughSequence").GetInt64());
        Assert.True(parsed.RootElement.GetProperty("persistencePending").GetBoolean());
        Assert.Equal("IOException", parsed.RootElement.GetProperty("persistenceError").GetString());
        Assert.DoesNotContain("saveName", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("filePath", json, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void OverseerWindowAndFinding_DoNotExposeProtectedSaveIdentity()
    {
        var window = new OverseerWindowSnapshot
        {
            State = OverseerWindowStates.Ready,
            StartGameTick = 100,
            EndGameTick = 700,
            ElapsedGameTicks = 600,
            ElapsedGameSeconds = 10,
            WallClockElapsedSeconds = 3_610,
            ExcludedNonGameSeconds = 3_600,
            CrossedSessionBoundary = true,
        };
        var finding = new OverseerFindingSnapshot
        {
            Kind = OverseerFindingKinds.MaterialShortage,
            Confidence = OverseerFindingConfidences.Confirmed,
            Severity = OverseerFindingSeverities.Stopped,
            PlanetId = 104,
            ObjectId = 774,
            ItemId = 1112,
            Summary = "Missing diamond",
        };

        var json = JsonSerializer.Serialize(new { window, finding }, JsonOptions);

        Assert.Contains("\"excludedNonGameSeconds\":3600", json, StringComparison.Ordinal);
        Assert.DoesNotContain("saveName", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("saveIdentity", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("protectedSaveKey", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("authToken", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("planToken", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("filePath", json, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void OverseerProduction_DeclaresNativeWindowAndOmitsPrivateIdentity()
    {
        var snapshot = new OverseerProductionSnapshot
        {
            SessionId = "session",
            CapturedAtGameTick = 12_000,
            SnapshotId = "opaque-snapshot",
            TotalFactoryCount = 2,
            ReturnedFactoryCount = 1,
            RequestedItemIds = new List<int> { 6001, 6003 },
            RateSource = OverseerRateSources.NativeFactoryStatisticsLevel0,
            Window = new OverseerWindowSnapshot
            {
                State = OverseerWindowStates.Ready,
                StartGameTick = 11_401,
                EndGameTick = 12_000,
                ElapsedGameTicks = 600,
                ElapsedGameSeconds = 10,
            },
            Planets = new List<OverseerPlanetProductionSnapshot>
            {
                new OverseerPlanetProductionSnapshot
                {
                    FactoryIndex = 0,
                    PlanetId = 104,
                    PlanetName = "Owned planet",
                    Production = new List<ProductionRateSnapshot>
                    {
                        new ProductionRateSnapshot
                        {
                            PlanetId = 104,
                            ItemId = 6003,
                            ProducedCount = 1,
                            ActualProductionPerMinute = 6,
                            TheoreticalProductionPerMinute = 20,
                            Utilization = 0.3,
                            TheoreticalRateSource = OverseerTheoreticalRateSources.CurrentRuntimeComponentFormulaV1,
                            RateSource = OverseerRateSources.NativeFactoryStatisticsLevel0,
                            TheoreticalCoverage = OverseerTheoreticalCoverageStates.Complete,
                            DirectDiagnosticCoverage = OverseerDirectDiagnosticCoverageStates.Complete,
                            DirectProducerCount = 1,
                            DirectDiagnosedProducerCount = 1,
                            FindingCount = 1,
                            Findings = new List<OverseerFindingSnapshot>
                            {
                                new OverseerFindingSnapshot
                                {
                                    Kind = OverseerFindingKinds.MaterialShortage,
                                    Confidence = OverseerFindingConfidences.Confirmed,
                                    Severity = OverseerFindingSeverities.Stopped,
                                    PlanetId = 104,
                                    ObjectId = 774,
                                    ItemId = 6003,
                                    Summary = "Missing diamond",
                                },
                            },
                        },
                    },
                },
            },
        };

        var json = JsonSerializer.Serialize(snapshot, JsonOptions);
        using var parsed = JsonDocument.Parse(json);

        Assert.Equal(
            OverseerRateSources.NativeFactoryStatisticsLevel0,
            parsed.RootElement.GetProperty("rateSource").GetString());
        Assert.Equal(600, parsed.RootElement.GetProperty("window").GetProperty("elapsedGameTicks").GetInt64());
        Assert.Equal(
            OverseerTheoreticalCoverageStates.Complete,
            parsed.RootElement.GetProperty("planets")[0]
                .GetProperty("production")[0]
                .GetProperty("theoreticalCoverage")
                .GetString());
        Assert.Equal(
            OverseerTheoreticalRateSources.CurrentRuntimeComponentFormulaV1,
            parsed.RootElement.GetProperty("planets")[0]
                .GetProperty("production")[0]
                .GetProperty("theoreticalRateSource")
                .GetString());
        Assert.Equal(
            0.3,
            parsed.RootElement.GetProperty("planets")[0]
                .GetProperty("production")[0]
                .GetProperty("utilization")
                .GetDouble());
        Assert.Equal(
            OverseerFindingKinds.MaterialShortage,
            parsed.RootElement.GetProperty("planets")[0]
                .GetProperty("production")[0]
                .GetProperty("findings")[0]
                .GetProperty("kind")
                .GetString());
        Assert.Equal(
            OverseerDirectDiagnosticCoverageStates.Complete,
            parsed.RootElement.GetProperty("planets")[0]
                .GetProperty("production")[0]
                .GetProperty("directDiagnosticCoverage")
                .GetString());
        Assert.DoesNotContain("saveName", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("protectedSaveKey", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("authToken", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("planToken", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("filePath", json, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void OverseerSummary_SeparatesPlanetDomainsFromGlobalResearchWithoutPrivateIdentity()
    {
        var snapshot = new OverseerSummarySnapshot
        {
            SessionId = "session",
            CapturedAtGameTick = 42,
            SnapshotId = "opaque-snapshot",
            TotalFactoryCount = 3,
            ReturnedFactoryCount = 1,
            Research = new OverseerResearchSummarySnapshot
            {
                CurrentTechId = 1704,
                CurrentHashUploaded = 100,
                CurrentHashRequired = 1_000,
                CurrentHashRemaining = 900,
                QueuedTechCount = 1,
                TechQueue = new List<int> { 1704 },
            },
            Planets = new List<OverseerPlanetSummarySnapshot>
            {
                new OverseerPlanetSummarySnapshot
                {
                    FactoryIndex = 0,
                    PlanetId = 104,
                    Power = new OverseerPowerSummarySnapshot
                    {
                        ActiveNetworkCount = 2,
                        TotalEnergyGenerated = 750,
                        TotalEnergyExported = 25,
                        MinimumConsumerRatio = 0.75,
                    },
                    Logistics = new OverseerLogisticsSummarySnapshot
                    {
                        StationCount = 1,
                        InterstellarStationCount = 1,
                        OutstandingRemoteOrderMagnitude = 100,
                    },
                },
            },
        };

        var json = JsonSerializer.Serialize(snapshot, JsonOptions);
        using var parsed = JsonDocument.Parse(json);

        Assert.Equal(1704, parsed.RootElement.GetProperty("research").GetProperty("currentTechId").GetInt32());
        Assert.Equal(
            0.75,
            parsed.RootElement.GetProperty("planets")[0]
                .GetProperty("power")
                .GetProperty("minimumConsumerRatio")
                .GetDouble());
        Assert.Equal(
            25,
            parsed.RootElement.GetProperty("planets")[0]
                .GetProperty("power")
                .GetProperty("totalEnergyExported")
                .GetInt64());
        Assert.Equal(
            100,
            parsed.RootElement.GetProperty("planets")[0]
                .GetProperty("logistics")
                .GetProperty("outstandingRemoteOrderMagnitude")
                .GetInt64());
        Assert.DoesNotContain("saveName", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("protectedSaveKey", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("authToken", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("planToken", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("filePath", json, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void OverseerDiagnosticBundle_JoinsOnlyAllowlistedSameTickDomains()
    {
        var snapshot = new OverseerDiagnosticBundleSnapshot
        {
            SessionId = "session",
            CapturedAtGameTick = 42,
            SnapshotId = "opaque-snapshot",
            TotalFactoryCount = 1,
            ReturnedFactoryCount = 1,
            RequestedItemIds = new List<int> { 6003 },
            Window = new OverseerWindowSnapshot
            {
                State = OverseerWindowStates.Ready,
                EndGameTick = 42,
            },
            Research = new OverseerResearchSummarySnapshot
            {
                CurrentTechId = 1704,
            },
            Planets = new List<OverseerDiagnosticBundlePlanetSnapshot>
            {
                new OverseerDiagnosticBundlePlanetSnapshot
                {
                    FactoryIndex = 0,
                    PlanetId = 104,
                    CapturedAtGameTick = 42,
                    Power = new OverseerPowerSummarySnapshot
                    {
                        MinimumConsumerRatio = 0.75,
                    },
                    Logistics = new OverseerLogisticsSummarySnapshot
                    {
                        InterstellarStationCount = 1,
                    },
                    Production = new List<ProductionRateSnapshot>
                    {
                        new ProductionRateSnapshot
                        {
                            PlanetId = 104,
                            ItemId = 6003,
                            FindingCount = 1,
                            Findings = new List<OverseerFindingSnapshot>
                            {
                                new OverseerFindingSnapshot
                                {
                                    Kind = OverseerFindingKinds.MaterialShortage,
                                    PlanetId = 104,
                                    ObjectId = 774,
                                    ItemId = 6003,
                                },
                            },
                        },
                    },
                },
            },
        };

        var json = JsonSerializer.Serialize(snapshot, JsonOptions);
        using var parsed = JsonDocument.Parse(json);

        Assert.Equal(
            OverseerDiagnosticBundleProfiles.CurrentSchemaVersion,
            parsed.RootElement.GetProperty("schemaVersion").GetInt32());
        Assert.Equal(
            OverseerDiagnosticBundleProfiles.PublicAllowlistV1,
            parsed.RootElement.GetProperty("privacyProfile").GetString());
        Assert.Equal(
            parsed.RootElement.GetProperty("capturedAtGameTick").GetInt64(),
            parsed.RootElement.GetProperty("planets")[0].GetProperty("capturedAtGameTick").GetInt64());
        Assert.Equal(
            OverseerFindingKinds.MaterialShortage,
            parsed.RootElement.GetProperty("planets")[0]
                .GetProperty("production")[0]
                .GetProperty("findings")[0]
                .GetProperty("kind")
                .GetString());
        Assert.DoesNotContain("saveName", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("saveIdentity", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("protectedSaveKey", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("authToken", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("planToken", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("filePath", json, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void LogisticsStation_UsesExplicitRawSettingsAndStableHashes()
    {
        var station = new LogisticsStationSnapshot
        {
            SessionId = "session",
            PlanetId = 104,
            EntityId = 920,
            StationId = 2,
            GalacticStationId = 7,
            BuildingItemId = 2104,
            RequestedChargeEnergyPerTick = 50_000,
            RequestedChargePowerWatts = 3_000_000,
            MaximumChargeEnergyPerTick = 100_000,
            MaximumChargePowerWatts = 6_000_000,
            DroneCapacity = 50,
            VesselCapacity = 10,
            DroneTripRangeRaw = 180d,
            VesselTripRangeRaw = 12d,
            WarpEnableDistanceRaw = 0.5d,
            StateHash = "sha256:live",
            ConfigurationStateHash = "sha256:config",
            FleetStateHash = "sha256:fleet",
        };

        var json = JsonSerializer.Serialize(station, JsonOptions);
        using var parsed = JsonDocument.Parse(json);

        Assert.Equal(180d, parsed.RootElement.GetProperty("droneTripRangeRaw").GetDouble());
        Assert.Equal(50_000, parsed.RootElement.GetProperty("requestedChargeEnergyPerTick").GetInt64());
        Assert.Equal(6_000_000, parsed.RootElement.GetProperty("maximumChargePowerWatts").GetInt64());
        Assert.Equal("sha256:config", parsed.RootElement.GetProperty("configurationStateHash").GetString());
        Assert.Equal(50, parsed.RootElement.GetProperty("droneCapacity").GetInt32());
        Assert.Equal(10, parsed.RootElement.GetProperty("vesselCapacity").GetInt32());
        Assert.Equal("sha256:fleet", parsed.RootElement.GetProperty("fleetStateHash").GetString());
        Assert.DoesNotContain("saveName", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("filePath", json, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void LogisticsStationFleetTransfer_BindsDedicatedFleetHash()
    {
        var request = new PrepareLogisticsStationFleetTransferRequest
        {
            PlanetId = 104,
            StationEntityId = 920,
            Direction = LogisticsStationFleetTransferDirections.PlayerToStation,
            ItemId = LogisticsFleetItemIds.Drone,
            Count = 10,
            ExpectedPlayerStateHash = "sha256:player",
            ExpectedStationFleetStateHash = "sha256:fleet",
        };

        using var parsed = JsonDocument.Parse(JsonSerializer.Serialize(request, JsonOptions));
        Assert.Equal("player-to-station", parsed.RootElement.GetProperty("direction").GetString());
        Assert.Equal(5001, parsed.RootElement.GetProperty("itemId").GetInt32());
        Assert.Equal("sha256:fleet", parsed.RootElement.GetProperty("expectedStationFleetStateHash").GetString());
    }

    [Fact]
    public void LogisticsStationConfiguration_BindsSeparateConfigurationHashAndSlotIntent()
    {
        var request = new PrepareConfigureBuildingRequest
        {
            PlanetId = 104,
            EntityId = 920,
            Mode = BuildingConfigurationModes.LogisticsStationStorage,
            StationStorageIndex = 1,
            StationItemId = 1106,
            StationMaximumCount = 5_000,
            StationLocalLogic = LogisticsStorageLogics.Demand,
            StationRemoteLogic = LogisticsStorageLogics.Supply,
            ExpectedFactoryStateHash = "sha256:factory",
            ExpectedStationConfigurationStateHash = "sha256:station-config",
        };

        var json = JsonSerializer.Serialize(request, JsonOptions);
        using var parsed = JsonDocument.Parse(json);

        Assert.Equal("logistics-station-storage", parsed.RootElement.GetProperty("mode").GetString());
        Assert.Equal(1, parsed.RootElement.GetProperty("stationStorageIndex").GetInt32());
        Assert.Equal("demand", parsed.RootElement.GetProperty("stationLocalLogic").GetString());
        Assert.Equal("sha256:station-config", parsed.RootElement.GetProperty("expectedStationConfigurationStateHash").GetString());
    }

    [Fact]
    public void LogisticsStationChargeConfiguration_UsesExplicitPowerAndConfigurationHash()
    {
        var request = new PrepareConfigureBuildingRequest
        {
            PlanetId = 104,
            EntityId = 920,
            Mode = BuildingConfigurationModes.LogisticsStationCharge,
            StationMaximumChargePowerWatts = 12_000_000,
            ExpectedFactoryStateHash = "sha256:factory",
            ExpectedStationConfigurationStateHash = "sha256:station-config",
        };

        var json = JsonSerializer.Serialize(request, JsonOptions);
        using var parsed = JsonDocument.Parse(json);

        Assert.Equal("logistics-station-charge", parsed.RootElement.GetProperty("mode").GetString());
        Assert.Equal(12_000_000, parsed.RootElement.GetProperty("stationMaximumChargePowerWatts").GetInt64());
        Assert.Equal("sha256:station-config", parsed.RootElement.GetProperty("expectedStationConfigurationStateHash").GetString());
    }

    [Fact]
    public void LogisticsStationBeltConfiguration_BindsOutputPortAndPublicStorageIndex()
    {
        var request = new PrepareConfigureBuildingRequest
        {
            PlanetId = 104,
            EntityId = 920,
            Mode = BuildingConfigurationModes.LogisticsStationBelt,
            StationBeltSlotIndex = 3,
            StationBeltStorageIndex = 0,
            ExpectedFactoryStateHash = "sha256:factory",
            ExpectedStationConfigurationStateHash = "sha256:station-config",
        };

        var json = JsonSerializer.Serialize(request, JsonOptions);
        using var parsed = JsonDocument.Parse(json);

        Assert.Equal("logistics-station-belt", parsed.RootElement.GetProperty("mode").GetString());
        Assert.Equal(3, parsed.RootElement.GetProperty("stationBeltSlotIndex").GetInt32());
        Assert.Equal(0, parsed.RootElement.GetProperty("stationBeltStorageIndex").GetInt32());
        Assert.Equal("sha256:station-config", parsed.RootElement.GetProperty("expectedStationConfigurationStateHash").GetString());
    }

    [Fact]
    public void Dismantle_BindsStableEndpointAndPlayerHashes()
    {
        var request = new PrepareDismantleRequest
        {
            PlanetId = 102,
            ObjectId = 17,
            ExpectedEndpointStateHash = "sha256:endpoint",
            ExpectedPlayerStateHash = "sha256:player",
        };
        var prepared = new PreparedNormalAction
        {
            ActionKind = NormalActionKinds.Dismantle,
            TargetObjectId = 17,
            PlannedResourceNodeIds = new List<int> { 245, 249, 252, 255, 256 },
        };

        using var requestJson = JsonDocument.Parse(JsonSerializer.Serialize(request, JsonOptions));
        using var preparedJson = JsonDocument.Parse(JsonSerializer.Serialize(prepared, JsonOptions));

        Assert.Equal("sha256:endpoint", requestJson.RootElement.GetProperty("expectedEndpointStateHash").GetString());
        Assert.Equal("sha256:player", requestJson.RootElement.GetProperty("expectedPlayerStateHash").GetString());
        Assert.Equal(17, preparedJson.RootElement.GetProperty("targetObjectId").GetInt32());
        Assert.Equal(5, preparedJson.RootElement.GetProperty("plannedResourceNodeIds").GetArrayLength());
    }
}
