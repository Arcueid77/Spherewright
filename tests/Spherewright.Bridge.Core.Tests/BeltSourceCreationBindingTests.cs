using Spherewright.Bridge.Core.Factory;
using Spherewright.Bridge.Core.Safety;
using Xunit;

namespace Spherewright.Bridge.Core.Tests;

public sealed class BeltSourceCreationBindingTests
{
    private static readonly string Source = CanonicalStateHash.Combine("synthetic-source-path-and-neighbourhood");
    private static readonly string EmptyPath = CanonicalStateHash.Combine("synthetic-complete-empty-unfed-path");

    [Fact]
    public void ElevatedPrepareAndCreateUseTheSameWrappedBindingAndPreserveTheRawPostCreateProof()
    {
        var prepared = BeltSourceReusePolicy.BindEmptyElevatedPathEvidence(Source, EmptyPath);
        Assert.NotEqual(Source, prepared);
        Assert.True(BeltSourceReusePolicy.MatchesPreparedCreationBinding(prepared, Source, EmptyPath, EmptyPath));
        Assert.False(BeltSourceReusePolicy.SameEvidence(prepared, Source));
        Assert.True(BeltSourceReusePolicy.SameEvidence(Source, Source));
        Assert.False(BeltSourceReusePolicy.MatchesPreparedCreationBinding(prepared, prepared, EmptyPath, EmptyPath));
    }

    [Theory]
    [InlineData("source")]
    [InlineData("empty_path")]
    [InlineData("missing_current_path")]
    [InlineData("missing_prepared_path")]
    [InlineData("missing_source")]
    [InlineData("missing_prepared_binding")]
    [InlineData("empty_current_path")]
    public void ChangedOrIncompleteElevatedEvidenceCannotReachNativeCreate(string fault)
    {
        string? source = Source, currentPath = EmptyPath, preparedPath = EmptyPath;
        string? prepared = BeltSourceReusePolicy.BindEmptyElevatedPathEvidence(Source, EmptyPath);
        switch (fault)
        {
            case "source": source = CanonicalStateHash.Combine("changed-native-geometry-or-neighbour"); break;
            case "empty_path": currentPath = CanonicalStateHash.Combine("changed-path-members-or-cargo"); break;
            case "missing_current_path": currentPath = null; break;
            case "missing_prepared_path": preparedPath = null; break;
            case "missing_source": source = null; break;
            case "missing_prepared_binding": prepared = null; break;
            case "empty_current_path": currentPath = string.Empty; break;
        }
        Assert.False(BeltSourceReusePolicy.MatchesPreparedCreationBinding(prepared, source, preparedPath, currentPath));
    }

    [Fact]
    public void ExistingFlatSourceDoesNotAcquireAnElevatedBindingOrLoseItsExactGuard()
    {
        Assert.True(BeltSourceReusePolicy.MatchesPreparedCreationBinding(Source, Source, null, null));
        Assert.False(BeltSourceReusePolicy.MatchesPreparedCreationBinding(Source, Source, null, EmptyPath));
        Assert.False(BeltSourceReusePolicy.MatchesPreparedCreationBinding(Source, Source, EmptyPath, EmptyPath));
        Assert.False(BeltSourceReusePolicy.MatchesPreparedCreationBinding(Source, "changed", null, null));
    }
}
