using System.Globalization;
using System.Text;

namespace Spherewright.Bridge.Core.Safety;

/// <summary>
/// Reads only the researched DSP v7/v13 identity prefix. Never imports a world,
/// reads account values or screenshots, or returns the embedded save name.
/// The caller supplies the protected identity, not a filename prefix.
/// </summary>
public static class OwnedSavePrefixReader
{
    public const int MaximumScreenshotBytes = 16 * 1024 * 1024;
    public const int MaximumAccountNameBytes = 4096;
    public const int MaximumIdentityBytes = 1024;

    public static OwnedSavePrefixEvidence Read(Stream stream, string expectedOwnedIdentity)
    {
        if (stream is null) throw new ArgumentNullException(nameof(stream));
        if (!stream.CanRead || !stream.CanSeek || stream.Position != 0)
            throw new ArgumentException("A readable, seekable save prefix at offset zero is required.", nameof(stream));
        if (string.IsNullOrWhiteSpace(expectedOwnedIdentity)
            || Encoding.UTF8.GetByteCount(expectedOwnedIdentity) > MaximumIdentityBytes)
            throw new ArgumentException("A bounded protected identity is required.", nameof(expectedOwnedIdentity));

        using (var reader = new BinaryReader(stream, new UTF8Encoding(false, true), leaveOpen: true))
        {
            if (!reader.ReadBytes(6).SequenceEqual(Encoding.ASCII.GetBytes("VFSAVE")))
                throw new InvalidDataException("The native save signature is invalid.");
            var fileLength = reader.ReadInt64();
            if (fileLength != stream.Length || fileLength < 52 || reader.ReadInt32() != 7)
                throw new InvalidDataException("The native save length or header version is unsupported.");
            var sandbox = ReadBoolean(reader);
            var peaceful = ReadBoolean(reader);
            var version = new int[4];
            for (var i = 0; i < version.Length; i++)
            {
                version[i] = reader.ReadInt32();
                if (version[i] < 0) throw new InvalidDataException("Negative game version component.");
            }
            var gameTick = reader.ReadInt64();
            var savedAtTicks = reader.ReadInt64();
            if (gameTick < 0 || savedAtTicks < DateTime.MinValue.Ticks || savedAtTicks > DateTime.MaxValue.Ticks)
                throw new InvalidDataException("Invalid save time evidence.");

            var screenshotBytes = reader.ReadInt32();
            if (screenshotBytes < 0 || screenshotBytes > MaximumScreenshotBytes)
                throw new InvalidDataException("The screenshot exceeds the bounded prefix layout.");
            Skip(reader, screenshotBytes);
            SkipAccount(reader);
            Skip(reader, sizeof(ulong)); // Cluster generation, not ownership evidence.
            var dataVersion = reader.ReadInt32();
            var patchVersion = reader.ReadInt32();
            if (dataVersion != 13)
                throw new InvalidDataException("The GameData prefix version is unsupported.");
            SkipAccount(reader);
            var identityBytes = ReadStringByteLength(reader, MaximumIdentityBytes);
            var bytes = reader.ReadBytes(identityBytes);
            if (bytes.Length != identityBytes) throw new EndOfStreamException();
            var identity = new UTF8Encoding(false, true).GetString(bytes);
            var descriptorVersion = reader.ReadInt32();
            var gameVersion = string.Join(".", version.Select(value => value.ToString(CultureInfo.InvariantCulture)));
            if (!(gameVersion == OwnedWorldVersionCompatibilityPolicy.SourceVersion && patchVersion == 22 && descriptorVersion == 9)
                && !(OwnedWorldVersionCompatibilityPolicy.IsResearchedTarget(gameVersion) && patchVersion == 23 && descriptorVersion == 10))
                throw new InvalidDataException("The native game/version/patch/descriptor tuple is unsupported.");
            return new OwnedSavePrefixEvidence(
                fileLength, gameTick, new DateTimeOffset(savedAtTicks, TimeSpan.Zero),
                gameVersion,
                peaceful, sandbox, string.Equals(identity, expectedOwnedIdentity, StringComparison.Ordinal),
                stream.Position, CanonicalStateHash.Combine("native-save-embedded-identity-v1", identity),
                !string.IsNullOrWhiteSpace(identity));
        }
    }

    private static bool ReadBoolean(BinaryReader reader)
    {
        var value = reader.ReadByte();
        if (value > 1) throw new InvalidDataException("Noncanonical native Boolean.");
        return value == 1;
    }

    private static void SkipAccount(BinaryReader reader)
    {
        if (reader.ReadInt32() != 0) throw new InvalidDataException("Unsupported account prefix layout.");
        Skip(reader, sizeof(int) + sizeof(ulong));
        Skip(reader, ReadStringByteLength(reader, MaximumAccountNameBytes));
    }

    private static int ReadStringByteLength(BinaryReader reader, int maximum)
    {
        uint length = 0;
        for (var index = 0; index < 5; index++)
        {
            var value = reader.ReadByte();
            if (index == 4 && (value & 0xf0) != 0)
                throw new InvalidDataException("Invalid native string length.");
            length |= (uint)(value & 0x7f) << (index * 7);
            if ((value & 0x80) != 0) continue;
            if (index > 0 && value == 0)
                throw new InvalidDataException("Noncanonical native string length.");
            if (length > maximum || length > reader.BaseStream.Length - reader.BaseStream.Position)
                throw new InvalidDataException("A native string exceeds the bounded prefix.");
            return (int)length;
        }
        throw new InvalidDataException("Invalid native string length.");
    }

    private static void Skip(BinaryReader reader, int count)
    {
        if (count < 0 || count > reader.BaseStream.Length - reader.BaseStream.Position)
            throw new EndOfStreamException();
        reader.BaseStream.Seek(count, SeekOrigin.Current);
    }
}

public sealed class OwnedSavePrefixEvidence
{
    internal OwnedSavePrefixEvidence(long fileLength, long gameTick, DateTimeOffset savedAtUtc,
        string gameVersion, bool peaceful, bool sandbox, bool matchesExpectedIdentity, long prefixEnd,
        string embeddedIdentityFingerprint, bool embeddedIdentityPresent)
    {
        FileLength = fileLength;
        GameTick = gameTick;
        SavedAtUtc = savedAtUtc;
        GameVersion = gameVersion;
        Peaceful = peaceful;
        Sandbox = sandbox;
        MatchesExpectedIdentity = matchesExpectedIdentity;
        PrefixEnd = prefixEnd;
        EmbeddedIdentityFingerprint = embeddedIdentityFingerprint;
        EmbeddedIdentityPresent = embeddedIdentityPresent;
    }

    public long FileLength { get; }
    public long GameTick { get; }
    public DateTimeOffset SavedAtUtc { get; }
    public string GameVersion { get; }
    public bool Peaceful { get; }
    public bool Sandbox { get; }
    public bool MatchesExpectedIdentity { get; }
    public long PrefixEnd { get; }
    internal string EmbeddedIdentityFingerprint { get; }
    internal bool EmbeddedIdentityPresent { get; }
}
