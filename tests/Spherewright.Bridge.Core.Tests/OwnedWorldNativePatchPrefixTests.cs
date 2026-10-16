using Spherewright.Bridge.Core.Safety;
using Xunit;

namespace Spherewright.Bridge.Core.Tests;

public sealed class OwnedWorldNativePatchPrefixTests
{
    [Theory]
    [InlineData(29151, 13, 23, 10, true)]
    [InlineData(29151, 13, 22, 10, false)]
    [InlineData(29151, 13, 23, 9, false)]
    [InlineData(29151, 14, 23, 10, false)]
    [InlineData(29152, 13, 23, 10, false)]
    public void CurrentPatchPrefixStillRequiresTheExactResearchedFormatTuple(
        int build, int dataVersion, int patch, int descriptor, bool allowed)
    {
        const string identity = "synthetic-owned-native-patch";
        using var stream = OwnedSavePrefixReaderTests.Fixture(
            identity: identity, versionPatch: 35, versionBuild: build,
            dataVersion: dataVersion, patch: patch, gameDescVersion: descriptor);
        if (allowed)
        {
            var evidence = OwnedSavePrefixReader.Read(stream, identity);
            Assert.Equal("0.10.35.29151", evidence.GameVersion);
            Assert.True(evidence.MatchesExpectedIdentity);
        }
        else Assert.Throws<InvalidDataException>(() => OwnedSavePrefixReader.Read(stream, identity));
    }
}
