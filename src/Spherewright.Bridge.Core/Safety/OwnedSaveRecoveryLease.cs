using System.Diagnostics;
using System.Security.Cryptography;

namespace Spherewright.Bridge.Core.Safety;

/// <summary>A caller-selected fixed-slot file is held read-only through native asynchronous loading.</summary>
public sealed class OwnedSaveRecoveryLease : IDisposable
{
    public const long MaximumFileBytes = 64L * 1024 * 1024;
    private readonly FileStream _stream;
    private readonly string _protectedIdentity;

    private OwnedSaveRecoveryLease(FileStream stream, OwnedSavePrefixEvidence prefix,
        DateTimeOffset writtenAtUtc, string contentHash, string protectedIdentity)
    {
        _stream = stream;
        _protectedIdentity = protectedIdentity;
        Prefix = prefix;
        WrittenAtUtc = writtenAtUtc;
        ContentFingerprint = "sha256:" + contentHash.ToLowerInvariant();
        Fingerprint = CanonicalStateHash.Combine("verified-owned-last-exit-v1", prefix.FileLength,
            prefix.GameTick, prefix.SavedAtUtc, prefix.GameVersion, prefix.Peaceful, prefix.Sandbox,
            prefix.MatchesExpectedIdentity, prefix.PrefixEnd, writtenAtUtc, contentHash);
    }

    public OwnedSavePrefixEvidence Prefix { get; }
    public DateTimeOffset WrittenAtUtc { get; }
    public string Fingerprint { get; }
    public string ContentFingerprint { get; }

    // Do not expose either name or the world's seed. These are the immutable
    // creation time/version and galaxy descriptor fields from researched
    // GameDesc v10, not a claim about loaded gameplay or the old action.
    public bool IsBoundToIdentity(string expectedIdentity) =>
        string.Equals(_protectedIdentity, expectedIdentity, StringComparison.Ordinal);

    // Compares a loaded native name to the leased file without disclosing it.
    public bool MatchesEmbeddedIdentity(string? actualIdentity) => !string.IsNullOrWhiteSpace(actualIdentity)
        && Prefix.EmbeddedIdentityFingerprint == CanonicalStateHash.Combine("native-save-embedded-identity-v1", actualIdentity);

    public bool HasSameNativeWorldIdentity(OwnedSaveRecoveryLease other)
    {
        if (other is null || Prefix.GameVersion != other.Prefix.GameVersion
            || !OwnedWorldVersionCompatibilityPolicy.IsResearchedTarget(Prefix.GameVersion)) return false;
        var left = ReadNativeWorldIdentity(); var right = other.ReadNativeWorldIdentity();
        return left is not null && right is not null && left.SequenceEqual(right);
    }

    private byte[]? ReadNativeWorldIdentity()
    {
        const int identityBytes = 44;
        var position = _stream.Position;
        try
        {
            if (Prefix.PrefixEnd < 0 || _stream.Length - Prefix.PrefixEnd < identityBytes) return null;
            _stream.Position = Prefix.PrefixEnd;
            using var reader = new BinaryReader(_stream, System.Text.Encoding.UTF8, leaveOpen: true);
            var value = reader.ReadBytes(identityBytes);
            if (value.Length != identityBytes) return null;
            var creationTicks = BitConverter.ToInt64(value, 0);
            var starCount = BitConverter.ToInt32(value, 32);
            return creationTicks > 0 && creationTicks <= DateTime.MaxValue.Ticks && starCount > 0 ? value : null;
        }
        finally { _stream.Position = position; }
    }

    public static OwnedSaveRecoveryLease Open(string fixedSlotPath, string protectedIdentity) =>
        OpenCore(fixedSlotPath, protectedIdentity, allowRenamedIdentity: false);

    // This is only a file reader, not a source selector or authorization.
    // The caller must independently bind a fixed LastExit, original world
    // descriptor, exact current tick and separately observed fingerprint.
    public static OwnedSaveRecoveryLease OpenForRenamedLastExit(string fixedSlotPath, string protectedIdentity)
    {
        if ((File.GetAttributes(fixedSlotPath) & FileAttributes.ReparsePoint) != 0)
            throw new InvalidDataException("The fixed LastExit must be a regular file.");
        return OpenCore(fixedSlotPath, protectedIdentity, allowRenamedIdentity: true);
    }

    private static OwnedSaveRecoveryLease OpenCore(string fixedSlotPath, string protectedIdentity, bool allowRenamedIdentity)
    {
        var stream = new FileStream(fixedSlotPath, FileMode.Open, FileAccess.Read, FileShare.Read);
        try
        {
            if (stream.Length > MaximumFileBytes)
                throw new InvalidDataException("The recovery candidate exceeds the 64MiB bounded verification limit.");
            var timer = Stopwatch.StartNew();
            var prefix = OwnedSavePrefixReader.Read(stream, protectedIdentity);
            if (!prefix.EmbeddedIdentityPresent || !prefix.MatchesExpectedIdentity && !allowRenamedIdentity)
                throw new InvalidDataException("The fixed slot does not match the exact protected identity.");
            var writtenAt = new DateTimeOffset(File.GetLastWriteTimeUtc(fixedSlotPath), TimeSpan.Zero);
            stream.Position = 0;
            var buffer = new byte[65536];
            using (var hash = SHA256.Create())
            {
                int count;
                long total = 0;
                while ((count = stream.Read(buffer, 0, buffer.Length)) != 0)
                {
                    total += count;
                    if (total > MaximumFileBytes || timer.Elapsed > TimeSpan.FromSeconds(2))
                        throw new InvalidDataException("Bounded recovery verification exceeded its byte or time budget.");
                    hash.TransformBlock(buffer, 0, count, buffer, 0);
                }
                hash.TransformFinalBlock(Array.Empty<byte>(), 0, 0);
                if (total != prefix.FileLength || stream.Length != prefix.FileLength
                    || File.GetLastWriteTimeUtc(fixedSlotPath) != writtenAt.UtcDateTime)
                    throw new InvalidDataException("The recovery file changed during verification.");
                return new OwnedSaveRecoveryLease(stream, prefix, writtenAt,
                    BitConverter.ToString(hash.Hash!).Replace("-", string.Empty), protectedIdentity);
            }
        }
        catch
        {
            stream.Dispose();
            throw;
        }
    }

    public void Dispose() => _stream.Dispose();
}
