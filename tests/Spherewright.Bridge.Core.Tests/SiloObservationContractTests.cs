using Spherewright.Bridge.Core.Safety;
using Spherewright.Contracts.Factory;
using Xunit;

namespace Spherewright.Bridge.Core.Tests;

public sealed class SiloObservationContractTests
{
    [Fact]
    public void MissingSphereAndCoverageNeverBecomeVerifiedZeroOrLaunchReceipt()
    {
        var value = new SiloStateSnapshot { FiredAnimationActive = true };
        Assert.Null(value.InFlightRocketCount);
        Assert.Null(value.StarSiloCount);
        Assert.False(value.RocketCoverageComplete);
        Assert.False(value.NodeCoverageComplete);
        Assert.False(value.StarSiloCoverageComplete);
        Assert.False(value.FiredAnimationIsLaunchReceipt);
    }

    [Theory]
    [InlineData("factory")]
    [InlineData("endpoint")]
    [InlineData("configuration")]
    public void SameTickDetailCannotChangePreexistingActionHashDomains(string domain)
    {
        var entity = new FactoryEntitySnapshot { SessionId = "synthetic-session", PlanetId = 402,
            ObjectId = 1, ItemId = 2312, ComponentKind = "silo" };
        string Hash() => domain == "factory" ? CanonicalStateHash.Factory(entity)
            : domain == "endpoint" ? CanonicalStateHash.FactoryEndpoint(entity) : CanonicalStateHash.FactoryConfiguration(entity);
        var before = Hash();
        entity.Silo = new SiloStateSnapshot { NativeBindingVerified = true, RocketItemId = 1503,
            RocketCount = 1, FiredAnimationActive = true, InFlightRocketCount = 0 };
        Assert.Equal(before, Hash());
        entity.Silo.RocketCount = 0;
        entity.Silo.Rockets.Add(new DysonRocketSnapshot { RocketId = 2, SourcePlanetId = 402,
            TargetLayerId = 1, TargetNodeId = 3, NativeFlightProgress = -.1f });
        entity.Silo.Nodes.Add(new DysonReceivingNodeSnapshot { LayerId = 1, NodeId = 3, StructurePoints = 1 });
        Assert.Equal(before, Hash());
        Assert.False(entity.Silo.FiredAnimationIsLaunchReceipt);
    }
}
