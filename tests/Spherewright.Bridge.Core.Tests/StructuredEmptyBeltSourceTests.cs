using Spherewright.Bridge.Core.Factory;
using Spherewright.Bridge.Core.Safety;
using Spherewright.Contracts.Factory;
using Spherewright.Plugin.Transport;
using Xunit;

namespace Spherewright.Bridge.Core.Tests;

public sealed class StructuredEmptyBeltSourceTests
{
    private static string Fingerprint(string role) => CanonicalStateHash.Combine("synthetic-empty-source", role);
    internal static (StructuredConstructionBinding Binding, BlueprintSiteSnapshot Site) Scenario(bool extension = true)
    {
        var binding = new StructuredConstructionBinding
        {
            Kind = "belt", JourneyId = Guid.NewGuid().ToString(), NativePlanHash = Fingerprint("native-plan"), SourceObjectId = 9,
            Endpoints = new() { new() { ObjectId = 9, ItemId = 2001, Position = new() { Y = 200 }, Rotation = new() { W = 1 },
                ExistingConnections = new() { new() { Slot = 1, IsOutput = false, OtherObjectId = 8, OtherSlot = 0 } } } },
            Steps = new()
            {
                new() { InputObjectId = 9, InputStepIndex = -1, InputFromSlot = 0, InputToSlot = 1,
                    OutputStepIndex = 1, OutputFromSlot = 0, OutputToSlot = 1, IsConnectionNode = true },
                new() { InputStepIndex = 0, InputFromSlot = 0, InputToSlot = 1,
                    OutputStepIndex = -1, OutputFromSlot = 0, OutputToSlot = -1, IsConnectionNode = true },
            },
        };
        if (extension) binding.EmptyBeltSource = new()
        {
            ObjectId = 9, BindingHash = Fingerprint("whole-path-and-neighbours"), CargoHash = Fingerprint("complete-empty-cargo"),
            CompletionTopologyHash = Fingerprint("native-rotation-and-old-ports"), CargoStackCount = 0,
        };
        var site = new BlueprintSiteSnapshot
        {
            SessionId = "synthetic-owned-session", PlanetId = 201, NativeCheckPerformed = true, NativeCheckPassed = true,
            Objects = new() { new() { Index = 0, ItemId = 2001 }, new() { Index = 1, ItemId = 2001 } },
        };
        return (binding, site);
    }

    [Fact]
    public void FreshNativeCopiedEvidenceMustStayEmptyAndPreservePathCargoAndNeighbourBindings()
    {
        var (b, _) = Scenario(); var proof = b.EmptyBeltSource!;
        Assert.True(proof.MatchesCurrent(9, proof.BindingHash, proof.CargoHash, proof.CompletionTopologyHash, 0));
        Assert.False(proof.MatchesCurrent(9, proof.BindingHash, proof.CargoHash, proof.CompletionTopologyHash, 1));
        Assert.False(proof.MatchesCurrent(10, proof.BindingHash, proof.CargoHash, proof.CompletionTopologyHash, 0));
        Assert.False(proof.MatchesCurrent(9, Fingerprint("changed-path"), proof.CargoHash, proof.CompletionTopologyHash, 0));
        Assert.False(proof.MatchesCurrent(9, proof.BindingHash, Fingerprint("changed-cargo"), proof.CompletionTopologyHash, 0));
        Assert.False(proof.MatchesCurrent(9, proof.BindingHash, proof.CargoHash, Fingerprint("changed-neighbour"), 0));
    }

    [Fact]
    public void AnEmptyPaidTailUsesSeparateImmutableEvidenceAndSurvivesProductionJsonRoundTrip()
    {
        var (binding, site) = Scenario(); binding.Validate(site);
        var original = binding.ImmutableHash(); var json = PluginJson.Serialize(binding);
        var restored = PluginJson.Deserialize<StructuredConstructionBinding>(json)!;
        restored.Validate(site); Assert.Equal(original, restored.ImmutableHash());
        Assert.Equal(0, restored.EmptyBeltSource!.CargoStackCount);
        restored.EmptyBeltSource.CompletionTopologyHash = Fingerprint("changed-neighbours");
        Assert.NotEqual(original, restored.ImmutableHash());
    }

    [Fact]
    public void UnchangedLegacyBindingsKeepTheirExactOldJsonShapeAndHashDomain()
    {
        var (binding, site) = Scenario(extension: false); binding.Validate(site);
        Assert.DoesNotContain("emptyBeltSource", PluginJson.Serialize(binding));
        var expected = CanonicalStateHash.Combine("structured-native-construction-v1", binding.Kind, binding.JourneyId,
            binding.NativePlanHash, binding.ResourceNodeId, binding.ResourceStateHash, binding.SourceObjectId, binding.DestinationObjectId,
            CanonicalStateHash.Combine("connections", binding.Steps.Select(s => (object)s.Hash()).ToArray()),
            CanonicalStateHash.Combine("endpoints", binding.Endpoints.Select(e => (object)e.Hash()).ToArray()));
        Assert.Equal(expected, binding.ImmutableHash());
    }

    [Theory]
    [InlineData("cargo")][InlineData("source")][InlineData("binding")][InlineData("cargo-hash")][InlineData("topology")]
    [InlineData("destination")][InlineData("occupied-tail")][InlineData("slot")][InlineData("kind")][InlineData("wrong-item")]
    [InlineData("native-unproved")][InlineData("cycle")][InlineData("external-middle")]
    public void AnExtensionCannotBorrowNonemptyForeignOrUnboundEndpointEvidence(string fault)
    {
        var (b, site) = Scenario();
        switch (fault)
        {
            case "cargo": b.EmptyBeltSource!.CargoStackCount = 1; break;
            case "source": b.EmptyBeltSource!.ObjectId = 10; break;
            case "binding": b.EmptyBeltSource!.BindingHash = "unproved"; break;
            case "cargo-hash": b.EmptyBeltSource!.CargoHash = ""; break;
            case "topology": b.EmptyBeltSource!.CompletionTopologyHash = ""; break;
            case "destination": b.DestinationObjectId = 10; break;
            case "occupied-tail": b.Endpoints[0].ExistingConnections.Add(new() { Slot = 0, IsOutput = true, OtherObjectId = 11, OtherSlot = 1 }); break;
            case "slot": b.Steps[0].InputFromSlot = 2; break;
            case "kind": b.Kind = "core"; break;
            case "wrong-item": b.Endpoints[0].ItemId = 2002; break;
            case "native-unproved": site.NativeCheckPassed = false; break;
            case "cycle": b.Steps[1].OutputStepIndex = 0; break;
            case "external-middle": b.Steps[1].InputObjectId = 10; break;
        }
        Assert.Throws<InvalidDataException>(() => b.Validate(site));
    }
}
