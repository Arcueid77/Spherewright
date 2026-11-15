using System.Text;
using Spherewright.Bridge.Core.Safety;
using Xunit;

namespace Spherewright.Bridge.Core.Tests;

public sealed class NativeSavedWorldIdentityTests
{
    [Fact]
    public void SameCreationAndGalaxyFieldsMatchAcrossDifferentNativeSaveNames()
    {
        using var files = new TestFiles();
        files.Write("first", "synthetic-original-owned-name", 12345, 73);
        files.Write("second", "synthetic-short-name", 12399, 73);
        using var first = OwnedSaveRecoveryLease.Open(files.PathFor("first"), "synthetic-original-owned-name");
        Assert.Throws<InvalidDataException>(() => OwnedSaveRecoveryLease.Open(files.PathFor("second"), "synthetic-original-owned-name"));
        using var second = OwnedSaveRecoveryLease.OpenForRenamedLastExit(files.PathFor("second"), "synthetic-original-owned-name");
        Assert.True(first.IsBoundToIdentity("synthetic-original-owned-name"));
        Assert.False(first.IsBoundToIdentity("synthetic-short-name"));
        Assert.False(second.Prefix.MatchesExpectedIdentity);
        Assert.True(second.IsBoundToIdentity("synthetic-original-owned-name"));
        Assert.True(second.MatchesEmbeddedIdentity("synthetic-short-name"));
        Assert.False(second.MatchesEmbeddedIdentity("synthetic-original-owned-name"));
        Assert.True(first.HasSameNativeWorldIdentity(second));
        Assert.NotEqual(first.Prefix.GameTick, second.Prefix.GameTick);
        Assert.NotEqual(first.Fingerprint, second.Fingerprint);
    }

    [Theory]
    [InlineData("seed")] [InlineData("creation")] [InlineData("truncated")]
    [InlineData("invalid_creation")] [InlineData("empty_galaxy")]
    public void AChangedOrIncompleteWorldDescriptorCannotMatch(string fault)
    {
        using var files = new TestFiles();
        files.Write("first", "synthetic-original-owned-name", 12345, 73);
        files.Write("second", "synthetic-short-name", 12399, fault == "seed" ? 74 : 73,
            creationTicks: fault == "creation" ? 638000000000000001L : fault == "invalid_creation" ? 0 : 638000000000000000L,
            starCount: fault == "empty_galaxy" ? 0 : 64, truncated: fault == "truncated");
        using var first = OwnedSaveRecoveryLease.Open(files.PathFor("first"), "synthetic-original-owned-name");
        using var second = OwnedSaveRecoveryLease.OpenForRenamedLastExit(files.PathFor("second"), "synthetic-original-owned-name");
        Assert.False(first.HasSameNativeWorldIdentity(second));
    }

    [Theory]
    [InlineData("valid")] [InlineData("wrong_tick")] [InlineData("wrong_loaded_name")]
    [InlineData("wrong_source_fingerprint")] [InlineData("different_world")] [InlineData("primary_not_older")]
    public void OnlyTheLeasedSameWorldCanRestoreTheManagedNameWithoutEditingSaves(string fault)
    {
        using var files = new TestFiles();
        files.Write("primary", "synthetic-original-owned-name", fault == "primary_not_older" ? 12399 : 12345, 73);
        files.Write("last-exit", "synthetic-user-save-name", 12399, fault == "different_world" ? 74 : 73);
        var originalPrimary = File.ReadAllBytes(files.PathFor("primary"));
        var originalExit = File.ReadAllBytes(files.PathFor("last-exit"));
        using var primary = OwnedSaveRecoveryLease.Open(files.PathFor("primary"), "synthetic-original-owned-name");
        using var source = OwnedSaveRecoveryLease.OpenForRenamedLastExit(files.PathFor("last-exit"), "synthetic-original-owned-name");
        var loadedName = fault == "wrong_loaded_name" ? "different-synthetic-world" : "synthetic-user-save-name";
        var sets = 0;
        var result = RenamedSavedIdentityRestoration.Restore(source, primary, "synthetic-original-owned-name",
            fault == "wrong_source_fingerprint" ? "different-fingerprint" : source.Fingerprint, primary.Fingerprint,
            fault == "wrong_tick" ? 12398 : 12399, 12399, () => loadedName, name => { sets++; loadedName = name; });
        Assert.Equal(fault == "valid", result is null);
        Assert.Equal(fault == "valid" ? 1 : 0, sets);
        Assert.Equal(fault == "valid" ? "synthetic-original-owned-name"
            : fault == "wrong_loaded_name" ? "different-synthetic-world" : "synthetic-user-save-name", loadedName);
        Assert.Equal(originalPrimary, File.ReadAllBytes(files.PathFor("primary")));
        Assert.Equal(originalExit, File.ReadAllBytes(files.PathFor("last-exit")));
    }

    private sealed class TestFiles : IDisposable
    {
        private readonly string _root = Path.Combine(Path.GetTempPath(), "spherewright-world-identity-" + Guid.NewGuid().ToString("N"));
        public TestFiles() => Directory.CreateDirectory(_root);
        public string PathFor(string name) => Path.Combine(_root, name + ".dsv");
        public void Write(string name, string identity, long tick, int seed,
            long creationTicks = 638000000000000000L, int starCount = 64, bool truncated = false)
        {
            using var stream = OwnedSavePrefixReaderTests.Fixture(identity: identity, versionPatch: 35,
                versionBuild: 29151, patch: 23, gameDescVersion: 10);
            stream.Position = 0;
            var prefix = OwnedSavePrefixReader.Read(stream, identity);
            stream.SetLength(prefix.PrefixEnd);
            stream.Position = prefix.PrefixEnd;
            using (var writer = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: true))
            {
                writer.Write(creationTicks);
                if (!truncated)
                {
                    writer.Write(0); writer.Write(10); writer.Write(35); writer.Write(29151);
                    writer.Write(20200403); writer.Write(seed); writer.Write(starCount); writer.Write(0); writer.Write(1);
                }
                stream.Position = 36; writer.Write(tick);
                stream.Position = 6; writer.Write(stream.Length);
            }
            File.WriteAllBytes(PathFor(name), stream.ToArray());
        }
        public void Dispose() => Directory.Delete(_root, recursive: true);
    }
}
