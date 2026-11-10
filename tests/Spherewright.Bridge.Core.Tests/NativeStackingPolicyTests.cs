using Spherewright.Bridge.Core.Factory;
using Spherewright.Bridge.Core.Safety;
using Spherewright.Contracts.Actions;
using Spherewright.Contracts.Factory;
using Xunit;

namespace Spherewright.Bridge.Core.Tests;

public sealed class NativeStackingPolicyTests
{
    [Theory]
    [InlineData("none", true)]
    [InlineData("layer_slots", false)]
    [InlineData("wrong_item", false)]
    [InlineData("click_tool", false)]
    [InlineData("technology_cap", false)]
    [InlineData("existing_attachment", false)]
    public void DistributorEchoBindsTheNativeAttachmentJoint(string fault, bool expected)
    {
        var plan = AttachmentPlan();
        switch (fault)
        {
            case "layer_slots": plan.InputFromSlot = 15; plan.InputToSlot = 14; break;
            case "wrong_item": plan.BuildingItemId = 2101; break;
            case "click_tool": plan.BuildToolKind = "click"; break;
            case "technology_cap": plan.MaximumLayers = 3; plan.HeightLimitKind = "technology"; break;
            case "existing_attachment": plan.ExistingAttachmentObjectIds.Add(7); break;
        }
        var request = Request(); request.BuildingItemId = 2107; request.PreferredModelIndex = 371;
        Assert.Equal(expected, NativeStackingPolicy.ConfirmsPlan(request,
            new PreparedNormalAction { PlannedStackConstruction = plan }));
    }

    [Theory]
    [InlineData("none", true)]
    [InlineData("missing", false)]
    [InlineData("extra_layer", false)]
    [InlineData("parent", false)]
    [InlineData("model", false)]
    [InlineData("member_identity", false)]
    public void NewAttachmentPreservesStorageLayersAndRequiresItsExactReadback(string fault, bool expected)
    {
        var plan = AttachmentPlan();
        var snapshot = new NativeStackSnapshot
        {
            Complete = true, RootObjectId = 1, TopObjectId = 1,
            Layers = new() { new() { ObjectId = 1 } },
            Attachments = new() { new() { ParentObjectId = 1,
                Entity = new() { ObjectId = 2, ItemId = 2107, ModelIndex = 371 } } },
        };
        switch (fault)
        {
            case "missing": snapshot.Attachments.Clear(); break;
            case "extra_layer": snapshot.Layers.Add(new() { ObjectId = 3 }); snapshot.TopObjectId = 3; break;
            case "parent": snapshot.Attachments[0].ParentObjectId = 3; break;
            case "model": snapshot.Attachments[0].Entity.ModelIndex++; break;
            case "member_identity": snapshot.Layers[0].ObjectId = 3; break;
        }
        Assert.Equal(expected, NativeStackingPolicy.ConfirmsCompletedMembers(plan, snapshot, out var created));
        if (expected) Assert.Equal(2, created);
    }

    [Fact]
    public void OrdinaryLayerCompletionPreservesAnExistingAttachmentIdentity()
    {
        var plan = Plan(); plan.ExistingAttachmentObjectIds.Add(7);
        var snapshot = new NativeStackSnapshot { Complete = true, RootObjectId = 1, TopObjectId = 2,
            Layers = new() { new() { ObjectId = 1 }, new() { ObjectId = 2 } },
            Attachments = new() { new() { ParentObjectId = 2, Entity = new() { ObjectId = 7 } } } };
        Assert.True(NativeStackingPolicy.ConfirmsCompletedMembers(plan, snapshot, out var created));
        Assert.Equal(2, created);
        snapshot.Attachments[0].Entity.ObjectId = 8;
        Assert.False(NativeStackingPolicy.ConfirmsCompletedMembers(plan, snapshot, out _));
    }

    private static NativeStackConstructionPlan AttachmentPlan()
    {
        var plan = Plan(); plan.BuildingItemId = 2107; plan.ModelIndex = 371;
        plan.MaximumLayers = null; plan.HeightLimitKind = "native_reach"; plan.BuildToolKind = "addon";
        plan.InputFromSlot = 13; plan.InputToSlot = 0;
        return plan;
    }

    [Theory]
    [InlineData(5, "spraycoater", "addon")]
    [InlineData(9, "spraycoater", null)]
    [InlineData(1, "spraycoater", null)]
    [InlineData(1, "piler", "click")]
    [InlineData(5, "piler", null)]
    [InlineData(1, "storage", "click")]
    [InlineData(0, "lab", null)]
    public void PlacementUsesTheNativeItemBuildMode(int mode, string family, string? expected)
    {
        Assert.Equal(expected, NativeStackingPolicy.PlacementToolKind(mode, family));
    }

    [Fact]
    public void OrdinaryRequestsKeepTheirLegacyShape()
    {
        Assert.Null(NativeStackingPolicy.ValidateRequest(new PrepareBuildRequest { BuildingItemId = 2101 }));
        Assert.True(NativeStackingPolicy.ConfirmsPlan(new PrepareBuildRequest(), null));
        Assert.Null(NativeStackingPolicy.PlanHash(null));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void RejectsUnbuiltOrInvalidAnchors(int id)
    {
        var request = Request();
        request.StackOnObjectId = id;
        Assert.NotNull(NativeStackingPolicy.ValidateRequest(request));
    }

    [Fact]
    public void RejectsAmbiguousGroundResourceAndConnectionPlacement()
    {
        var mutations = new Action<PrepareBuildRequest>[]
        {
            request => request.PreferredPosition = new(),
            request => request.ResourceNodeId = 1,
            request => request.SourceObjectId = 1,
            request => request.DestinationObjectId = 1,
            request => request.PathEnd = new(),
            request => request.InitialSorterFilterItemId = 1101,
            request => request.BeltStartAltitudeLevel = 1,
            request => request.BeltEndAltitudeLevel = 1,
            request => request.BeltEndpointPreview = new(),
            request => request.BeltRoutePreview = new(),
        };
        foreach (var mutate in mutations)
        {
            var request = Request();
            mutate(request);
            Assert.NotNull(NativeStackingPolicy.ValidateRequest(request));
        }
    }

    [Fact]
    public void MissingOrDifferentCohortEchoCannotExposeAStackPlan()
    {
        var request = Request();
        Assert.False(NativeStackingPolicy.ConfirmsPlan(request, new PreparedNormalAction()));
        var response = new PreparedNormalAction { PlannedStackConstruction = Plan() };
        Assert.True(NativeStackingPolicy.ConfirmsPlan(request, response));
        response.PlannedStackConstruction.ModelIndex++;
        Assert.False(NativeStackingPolicy.ConfirmsPlan(request, response));
        response.PlannedStackConstruction = Plan();
        response.PlannedStackConstruction.BeforeStackStateHash = "sha256:changed";
        Assert.False(NativeStackingPolicy.ConfirmsPlan(request, response));
    }

    [Fact]
    public void PlanIdentityBindsModelAnchorWholeChainAndOriginalTechnology()
    {
        var mutations = new Action<NativeStackConstructionPlan>[]
        {
            plan => plan.ModelIndex++,
            plan => plan.SelectedObjectId++,
            plan => plan.BeforeStackStateHash += "changed",
            plan => plan.MaximumLayers++,
            plan => { plan.ExistingObjectIds[0]++; plan.RootObjectId++; plan.AnchorObjectId++; },
        };
        var original = NativeStackingPolicy.PlanHash(Plan());
        foreach (var mutate in mutations)
        {
            var plan = Plan();
            mutate(plan);
            Assert.NotEqual(original, NativeStackingPolicy.PlanHash(plan));
        }
    }

    [Fact]
    public void CorruptOrIncompleteStoredIdentityFailsWithABoundedDataError()
    {
        var mutations = new Action<NativeStackConstructionPlan>[]
        {
            plan => plan.ExistingObjectIds = null!,
            plan => plan.ExistingObjectIds.Clear(),
            plan => plan.ExistingObjectIds[0] = 0,
            plan => plan.AnchorObjectId++,
            plan => plan.InputFromSlot = 0,
            plan => plan.ModelIndex = 0,
            plan => plan.ExistingAttachmentObjectIds = null!,
            plan => plan.ExistingAttachmentObjectIds = new() { 1 },
            plan => plan.ExistingAttachmentObjectIds = new() { 2, 2 },
        };
        foreach (var mutate in mutations)
        {
            var plan = Plan();
            mutate(plan);
            Assert.Throws<InvalidDataException>(() => NativeStackingPolicy.PlanHash(plan));
            Assert.False(NativeStackingPolicy.ConfirmsPlan(Request(), new PreparedNormalAction { PlannedStackConstruction = plan }));
        }
    }

    [Theory]
    [InlineData(2101)]
    [InlineData(2102)]
    [InlineData(2106)]
    [InlineData(2020)]
    [InlineData(2040)]
    [InlineData(2313)]
    [InlineData(2901)]
    [InlineData(2902)]
    [InlineData(3009)]
    public void KnownNativeStackFamiliesHaveRemoteComponentProofAdapters(int item)
    {
        Assert.True(StructuredConstructionBinding.IsFactoryCoreItem(item));
    }

    [Theory]
    [InlineData("click")]
    [InlineData("addon")]
    public void NativeHeightStacksHaveNoInventedStorageLayerCapAndBindTheirTool(string tool)
    {
        var plan = Plan();
        plan.MaximumLayers = null;
        plan.HeightLimitKind = "native_reach";
        plan.BuildToolKind = tool;
        NativeStackingPolicy.ValidatePlan(plan);
        Assert.True(NativeStackingPolicy.ConfirmsPlan(Request(), new PreparedNormalAction { PlannedStackConstruction = plan }));
        var hash = NativeStackingPolicy.PlanHash(plan);
        plan.BuildToolKind = tool == "click" ? "addon" : "click";
        Assert.NotEqual(hash, NativeStackingPolicy.PlanHash(plan));
        plan.MaximumLayers = 3;
        Assert.Throws<InvalidDataException>(() => NativeStackingPolicy.ValidatePlan(plan));
        plan.MaximumLayers = null;
        plan.HeightLimitKind = "technology";
        Assert.Throws<InvalidDataException>(() => NativeStackingPolicy.ValidatePlan(plan));
    }

    [Fact]
    public void ExistingTechnologyPlanRetainsItsOriginalHash()
    {
        var plan = Plan();
        var original = CanonicalStateHash.Combine("native-stack-construction-v1", plan.EvidenceVersion,
            plan.SelectedObjectId, plan.RootObjectId, plan.AnchorObjectId, plan.BeforeStackStateHash,
            plan.BuildingItemId, plan.ModelIndex, plan.ExistingLayerCount, plan.MaximumLayers,
            plan.InputFromSlot, plan.InputToSlot,
            CanonicalStateHash.Combine("existing-stack-members", plan.ExistingObjectIds.Cast<object>().ToArray()));
        Assert.Equal(original, NativeStackingPolicy.PlanHash(plan));
    }

    [Fact]
    public void NativeStorageAttachmentIdentityIsBoundSeparatelyFromOriginalStackMembers()
    {
        var plan = Plan();
        var original = NativeStackingPolicy.PlanHash(plan);
        plan.ExistingAttachmentObjectIds.Add(2);
        var attached = NativeStackingPolicy.PlanHash(plan);
        Assert.NotEqual(original, attached);
        plan.ExistingAttachmentObjectIds[0] = 3;
        Assert.NotEqual(attached, NativeStackingPolicy.PlanHash(plan));
        plan.ExistingAttachmentObjectIds.Clear();
        Assert.Equal(original, NativeStackingPolicy.PlanHash(plan));
    }

    [Fact]
    public void NativeModelChangesApprovedSiteIdentityAndLegacyNullStillMatches()
    {
        var site = new BlueprintSiteSnapshot { Objects = new() { new() { ItemId = 2020 } } };
        var original = BlueprintSitePolicy.AssessmentHash(site, "player");
        site.Objects[0].NativeModelIndex = 38;
        var first = BlueprintSitePolicy.AssessmentHash(site, "player");
        Assert.NotEqual(original, first);
        site.Objects[0].NativeModelIndex = 39;
        Assert.NotEqual(first, BlueprintSitePolicy.AssessmentHash(site, "player"));
        site.Objects[0].NativeModelIndex = null;
        Assert.Equal(original, BlueprintSitePolicy.AssessmentHash(site, "player"));
    }

    private static PrepareBuildRequest Request() => new()
    {
        BuildingItemId = 2101,
        StackOnObjectId = 1,
        ExpectedStackStateHash = "sha256:stack",
        PreferredModelIndex = 51,
    };

    private static NativeStackConstructionPlan Plan() => new()
    {
        SelectedObjectId = 1,
        RootObjectId = 1,
        AnchorObjectId = 1,
        BeforeStackStateHash = "sha256:stack",
        BuildingItemId = 2101,
        ModelIndex = 51,
        ExistingLayerCount = 1,
        ExistingObjectIds = new() { 1 },
        MaximumLayers = 4,
    };
}
