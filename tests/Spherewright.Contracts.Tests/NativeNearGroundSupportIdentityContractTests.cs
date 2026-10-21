using System.Text.Json;
using Spherewright.Contracts.Players;
using Xunit;

namespace Spherewright.Contracts.Tests;

public sealed class NativeNearGroundSupportIdentityContractTests
{
    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web);

    [Fact]
    public void MissingIdentityAndUnobservedIdentityStayUnknown()
    {
        var old = JsonSerializer.Deserialize<NativeNearGroundSupportSnapshot>(
            "{\"state\":\"observed\",\"castHit\":false}", Options)!;
        Assert.Null(old.Identity);
        Assert.Null(old.Copy().Identity);
        var empty = new NativeNearGroundSupportIdentitySnapshot().Copy();
        Assert.Null(empty.QueriedHit);
        Assert.Null(empty.ObservationReasonCode);
        Assert.All(new bool?[] { empty.SameManagedReference, empty.SameUnityObject, empty.SameNativeInstance,
            empty.RetainedRecordMatches, empty.RetainedColliderMatches, empty.RetainedPoolCellMatches,
            empty.RetainedObjectPoolMatches, empty.RetainedChainMatches, empty.PlanetColliderDirty },
            value => Assert.Null(value));
        Assert.False(old.SafetyCertified);
    }

    [Fact]
    public void CopyAndJsonDetachQueriedHitFromTheOriginalAndRetainedMetadata()
    {
        var source = new NativeNearGroundSupportSnapshot
        {
            State = "unavailable", Shape = "retained-shape", Layer = 7,
            Identity = new()
            {
                QueriedHit = new() { Type = "capsule", Layer = 10, IsTrigger = false,
                    Enabled = true, ActiveInHierarchy = true, SameDefaultPhysicsScene = true },
                SameManagedReference = false, SameUnityObject = false, SameNativeInstance = false,
                RetainedRecordMatches = true, RetainedColliderMatches = false, RetainedPoolCellMatches = true,
                RetainedObjectPoolMatches = true, RetainedChainMatches = false, PlanetColliderDirty = true,
                ObservationReasonCode = "synthetic-identity-detail",
            },
        };
        var copy = source.Copy();
        var json = JsonSerializer.Serialize(source, Options);
        var roundTrip = JsonSerializer.Deserialize<NativeNearGroundSupportSnapshot>(json, Options)!;
        Assert.Equal(json, JsonSerializer.Serialize(copy, Options));
        Assert.Equal(json, JsonSerializer.Serialize(roundTrip, Options));
        var originalIdentity = source.Identity!;
        var copiedIdentity = copy.Identity!;
        var originalHit = originalIdentity.QueriedHit!;
        var copiedHit = copiedIdentity.QueriedHit!;
        Assert.NotSame(originalIdentity, copiedIdentity);
        Assert.NotSame(originalHit, copiedHit);
        Assert.NotSame(originalHit, roundTrip.Identity!.QueriedHit);

        originalHit.Type = "changed";
        originalIdentity.RetainedPoolCellMatches = null;
        copiedHit.Layer = 0;
        Assert.Equal("capsule", copiedHit.Type);
        Assert.True(copiedIdentity.RetainedPoolCellMatches);
        Assert.Equal(10, originalHit.Layer);
        Assert.Equal(10, roundTrip.Identity!.QueriedHit!.Layer);
        Assert.Equal("retained-shape", copy.Shape);
        Assert.Equal(7, copy.Layer);
        Assert.Equal("unavailable", copy.State);
    }

    [Fact]
    public void PartialIdentitySurvivesUnavailableOuterStateWithoutConvertingFalseToUnknown()
    {
        var value = JsonSerializer.Deserialize<NativeNearGroundSupportSnapshot>(
            "{\"state\":\"unavailable\",\"identity\":{\"queriedHit\":{\"type\":\"sphere\",\"isTrigger\":false},"
            + "\"sameManagedReference\":false,\"sameUnityObject\":false,\"retainedColliderMatches\":false,"
            + "\"observationReasonCode\":\"synthetic-partial-observation\"}}", Options)!.Copy();
        Assert.Equal("unavailable", value.State);
        var identity = value.Identity!;
        var queriedHit = identity.QueriedHit!;
        Assert.Equal("sphere", queriedHit.Type);
        Assert.False(queriedHit.IsTrigger);
        Assert.Null(queriedHit.Layer);
        Assert.Null(queriedHit.Enabled);
        Assert.False(identity.SameManagedReference);
        Assert.False(identity.SameUnityObject);
        Assert.False(identity.RetainedColliderMatches);
        Assert.Null(identity.SameNativeInstance);
        Assert.Null(identity.RetainedRecordMatches);
        Assert.Null(identity.RetainedPoolCellMatches);
        Assert.Null(identity.RetainedObjectPoolMatches);
        Assert.Null(identity.RetainedChainMatches);
        Assert.Null(identity.PlanetColliderDirty);
        Assert.Equal("synthetic-partial-observation", identity.ObservationReasonCode);
        Assert.Null(value.ActualGroundSphereHit);
        Assert.Null(value.CurrentPoolReference);
        Assert.False(value.SafetyCertified);
    }

    [Fact]
    public void AllTrueIdentityDiagnosticsCannotInventGroundOwnershipOrSafety()
    {
        var source = new NativeNearGroundSupportSnapshot
        {
            State = "observed", ActualGroundSphereHit = false,
            Identity = new()
            {
                QueriedHit = new() { Type = "capsule", Layer = 10, IsTrigger = false,
                    Enabled = true, ActiveInHierarchy = true, SameDefaultPhysicsScene = true },
                SameManagedReference = true, SameUnityObject = true, SameNativeInstance = true,
                RetainedRecordMatches = true, RetainedColliderMatches = true, RetainedPoolCellMatches = true,
                RetainedObjectPoolMatches = true, RetainedChainMatches = true, PlanetColliderDirty = true,
            },
        };
        var json = JsonSerializer.Serialize(source.Copy(), Options);
        var value = JsonSerializer.Deserialize<NativeNearGroundSupportSnapshot>(json, Options)!;
        var identity = value.Identity!;
        Assert.True(identity.SameNativeInstance);
        Assert.True(identity.RetainedChainMatches);
        Assert.False(value.ActualGroundSphereHit);
        Assert.Null(value.CurrentPoolReference);
        Assert.Null(value.OwnerType);
        Assert.Null(value.ObjectId);
        Assert.Null(value.LiveObject);
        Assert.Null(value.ColliderChain);
        Assert.False(value.SafetyCertified);
    }
}
