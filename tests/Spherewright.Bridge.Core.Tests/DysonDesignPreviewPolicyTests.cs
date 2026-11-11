using Spherewright.Bridge.Core.Celestial;
using Spherewright.Contracts.Celestial;
using Xunit;

namespace Spherewright.Bridge.Core.Tests;

public sealed class DysonDesignPreviewPolicyTests
{
    [Fact]
    public void PositiveReferencesCannotBorrowPredictedFutureAllocations()
    {
        var request = Request();
        Assert.True(DysonDesignPreviewPolicy.ExistingReferencesBound(request, Array.Empty<int>()));
        request.Steps[2].Structure.NodeBId = 1;
        Assert.False(DysonDesignPreviewPolicy.ExistingReferencesBound(request, Array.Empty<int>()));
        Assert.True(DysonDesignPreviewPolicy.ExistingReferencesBound(request, new[] { 1 }));
    }
    [Fact]
    public void PriorNegativeNodeAliasesAndPositiveExistingIdsAreSupported()
    {
        var request = Request();
        Assert.Null(DysonDesignPreviewPolicy.ValidateRequest(1, request));
        request.Steps[2].Structure.NodeBId = 7;
        Assert.Null(DysonDesignPreviewPolicy.ValidateRequest(1, request));
    }

    [Theory]
    [InlineData("all_layers")][InlineData("hash")][InlineData("empty")][InlineData("limit")]
    [InlineData("duplicate")][InlineData("positive_key")][InlineData("forward_reference")]
    [InlineData("frame_reference")][InlineData("foreign_layer")][InlineData("duplicate_nodes")]
    [InlineData("null_step")][InlineData("null_structure")]
    public void MalformedOrUnboundedDesignsFailBeforeNativeCalculation(string fault)
    {
        var request = Request();
        var layer = 1;
        switch (fault)
        {
            case "all_layers": layer = 0; break;
            case "hash": request.ExpectedDesignStateHash = string.Empty; break;
            case "empty": request.Steps.Clear(); break;
            case "limit": request.Steps = Enumerable.Repeat(request.Steps[0], 65).ToList(); break;
            case "duplicate": request.Steps[1].LocalId = -1; break;
            case "positive_key": request.Steps[0].LocalId = 1; break;
            case "forward_reference": request.Steps.Reverse(); break;
            case "frame_reference": request.Steps.Add(new() { LocalId = -4,
                Structure = new() { Kind = DysonStructureKinds.Frame, LayerId = 1, NodeAId = -3, NodeBId = -2 } }); break;
            case "foreign_layer": request.Steps[0].Structure.LayerId = 2; break;
            case "duplicate_nodes": request.Steps[2].Structure.NodeBId = -1; break;
            case "null_step": request.Steps[0] = null!; break;
            case "null_structure": request.Steps[0].Structure = null!; break;
        }
        Assert.NotNull(DysonDesignPreviewPolicy.ValidateRequest(layer, request));
    }

    [Fact]
    public void RequestCopiesPreserveNegativeAliasesAndDetachCoordinates()
    {
        var request = Request();
        var copy = request.Copy();
        request.Steps[0].Structure.LocalPosition!.X++;
        request.Steps[2].Structure.NodeAId = 7;
        Assert.Equal(11000, copy.Steps[0].Structure.LocalPosition!.X);
        Assert.Equal(-1, copy.Steps[2].Structure.NodeAId);
        Assert.True(new DysonDesignPreview().ReadOnly);
        Assert.False(new DysonDesignPreview().Executable);
        Assert.False(new DysonDesignPreview().CommitAllowedNow);
        Assert.Null(new DysonDesignPreview().RequiredCarrierRockets);
    }

    private static DysonDesignPreviewRequest Request() => new()
    {
        ExpectedDesignStateHash = "synthetic-design",
        Steps = new()
        {
            new() { LocalId = -1, Structure = new() { Kind = DysonStructureKinds.Node, LayerId = 1, LocalPosition = new() { X = 11000 } } },
            new() { LocalId = -2, Structure = new() { Kind = DysonStructureKinds.Node, LayerId = 1, LocalPosition = new() { X = 10900, Z = 1480 } } },
            new() { LocalId = -3, Structure = new() { Kind = DysonStructureKinds.Frame, LayerId = 1, NodeAId = -1, NodeBId = -2 } },
        },
    };
}
