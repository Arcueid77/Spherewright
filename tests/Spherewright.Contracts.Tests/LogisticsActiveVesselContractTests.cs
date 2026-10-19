using System.Text.Json;
using Spherewright.Contracts.Factory;
using Spherewright.Contracts.Logistics;
using Xunit;

namespace Spherewright.Contracts.Tests;

public sealed class LogisticsActiveVesselContractTests
{
    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web);

    [Fact]
    public void LegacyOrListStationLeavesActiveShipsUnobserved()
    {
        var station = new LogisticsStationSnapshot();
        Assert.Null(station.ActiveVessels);
        using var json = JsonDocument.Parse(JsonSerializer.Serialize(station, Options));
        Assert.Equal(JsonValueKind.Null, json.RootElement.GetProperty("activeVessels").ValueKind);
    }

    [Fact]
    public void UnknownPoolIsDifferentFromVerifiedZeroActiveShips()
    {
        var unknown = new LogisticsActiveVesselObservation();
        Assert.Equal("unavailable", unknown.State);
        Assert.Null(unknown.NativeWorkingVesselCount);
        Assert.False(unknown.Complete);
        var empty = new LogisticsActiveVesselObservation { State = "observed", NativeWorkingVesselCount = 0, Complete = true };
        using var json = JsonDocument.Parse(JsonSerializer.Serialize(empty, Options));
        Assert.Equal(0, json.RootElement.GetProperty("nativeWorkingVesselCount").GetInt32());
        Assert.True(json.RootElement.GetProperty("complete").GetBoolean());
        Assert.Equal(0, json.RootElement.GetProperty("vessels").GetArrayLength());
    }

    [Fact]
    public void PublicShipIsOnlyTheNativeScalarAllowlist()
    {
        var ship = new LogisticsActiveVesselSnapshot { OwnerGId = 7, ShipIndex = 9, Gene = -1, OtherGId = 8, T = 3.5f };
        using var json = JsonDocument.Parse(JsonSerializer.Serialize(ship, Options));
        Assert.Equal(new[] { "direction", "gene", "inc", "itemCount", "itemId", "otherGId", "ownerGId", "shipIndex", "stage", "t", "uSpeed", "warpState", "warperCount" },
            json.RootElement.EnumerateObject().Select(p => p.Name).OrderBy(p => p, StringComparer.Ordinal).ToArray());
        Assert.Equal(9, json.RootElement.GetProperty("shipIndex").GetInt32());
        Assert.Equal(-1, json.RootElement.GetProperty("gene").GetInt32());
        Assert.Equal(3.5f, json.RootElement.GetProperty("t").GetSingle());
    }

    [Fact]
    public void RemoteStationReadIsExplicitlyOptInAndDefaultsToExistingScope()
    {
        Assert.False(new InspectFactoryEntityRequest().OwnedRemoteLogisticsStation);
        var request = new InspectFactoryEntityRequest { PlanetId = 104, ObjectId = 8, OwnedRemoteLogisticsStation = true };
        var json = JsonSerializer.Serialize(request, Options);
        Assert.Contains("\"ownedRemoteLogisticsStation\":true", json, StringComparison.Ordinal);
        Assert.True(JsonSerializer.Deserialize<InspectFactoryEntityRequest>(json, Options)!.OwnedRemoteLogisticsStation);
    }
}
