using Spherewright.Bridge.Core.Factory;
using Spherewright.Contracts.Factory;
using Xunit;

namespace Spherewright.Bridge.Core.Tests;

public sealed class StorageConfigurationSelectionPolicyTests
{
    [Theory]
    [InlineData("valid")]
    [InlineData("missing")]
    [InlineData("selector")]
    [InlineData("target")]
    [InlineData("root")]
    [InlineData("duplicate")]
    [InlineData("parent")]
    [InlineData("peer")]
    public void LayerConfigurationRequiresTheFullMatchingEcho(string fault)
    {
        var request = new Spherewright.Contracts.Actions.PrepareConfigureBuildingRequest
            { EntityId = 9, StorageOperation = "clear-filters" };
        var response = new Spherewright.Contracts.Actions.PreparedNormalAction
        {
            Prepared = true, ActionKind = "configure-building", PlanToken = "synthetic-token", TargetObjectId = 8,
            PlannedStorageOperation = "clear-filters", PlannedStorageConfiguration = new(),
            NativeStorageConfiguration = new() { SelectedObjectId = 9, TargetObjectId = 8, RootObjectId = 7,
                StorageItemId = 2102, LayerObjectIds = [7, 8], AttachmentObjectIds = [9], PreservesOtherLayers = true },
        };
        switch (fault)
        {
            case "missing": response.NativeStorageConfiguration = null; break;
            case "selector": response.NativeStorageConfiguration!.SelectedObjectId++; break;
            case "target": response.NativeStorageConfiguration!.TargetObjectId++; break;
            case "root": response.NativeStorageConfiguration!.RootObjectId++; break;
            case "duplicate": response.NativeStorageConfiguration!.LayerObjectIds.Add(8); break;
            case "parent": response.NativeStorageConfiguration!.AttachmentObjectIds.Clear(); break;
            case "peer": response.NativeStorageConfiguration!.PreservesOtherLayers = false; break;
        }
        Assert.Equal(fault == "valid", StorageConfigurationSelectionPolicy.ConfirmsEcho(request, response));
    }

    [Theory]
    [InlineData(2101)]
    [InlineData(2102)]
    public void ResolvesTheExactSelectedOrdinaryLayer(int item)
    {
        var selected = Snapshot(item);
        Assert.Equal(8, StorageConfigurationSelectionPolicy.ResolveTarget(selected));
        selected.Stack!.Layers[1].ItemId = 3009;
        Assert.Equal(0, StorageConfigurationSelectionPolicy.ResolveTarget(selected));
    }

    [Theory]
    [InlineData("valid", 8)]
    [InlineData("missing", 0)]
    [InlineData("partial", 0)]
    [InlineData("duplicate", 0)]
    [InlineData("parent", 0)]
    [InlineData("selection", 0)]
    [InlineData("root", 0)]
    public void DistributorSelectsOnlyItsVerifiedParent(string fault, int expected)
    {
        var selected = Snapshot(2102); selected.ObjectId = 9; selected.ItemId = 2107;
        selected.Stack!.SelectedObjectId = 9;
        selected.Stack.Attachments.Add(new() { ParentObjectId = 8, Entity = new() { ObjectId = 9, ItemId = 2107 } });
        switch (fault)
        {
            case "missing": selected.Stack = null; break;
            case "partial": selected.Stack.Complete = false; break;
            case "duplicate": selected.Stack.Attachments.Add(selected.Stack.Attachments[0]); break;
            case "parent": selected.Stack.Attachments[0].ParentObjectId = 6; break;
            case "selection": selected.Stack.SelectedObjectId = 8; break;
            case "root": selected.Stack.RootObjectId = 8; break;
        }
        Assert.Equal(expected, StorageConfigurationSelectionPolicy.ResolveTarget(selected));
    }

    private static FactoryEntitySnapshot Snapshot(int item) => new()
    {
        ObjectId = 8, ItemId = item, Stack = new() { Complete = true, State = "observed",
            SelectedObjectId = 8, RootObjectId = 7, TopObjectId = 8,
            Layers = [new() { ObjectId = 7, ItemId = 3009 }, new() { ObjectId = 8, ItemId = item }] },
    };
}
