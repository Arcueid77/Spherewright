namespace Spherewright.Bridge.Core.Safety;

// The caller supplies the exact native loader/world boundary. This helper
// checks the still-leased files and loaded name before using the same ordinary
// game-name setter as DSP's Save As; it does not write or rename either file.
public static class RenamedSavedIdentityRestoration
{
    public static string? Restore(OwnedSaveRecoveryLease source, OwnedSaveRecoveryLease primary,
        string managedIdentity, string expectedSourceFingerprint, string expectedPrimaryFingerprint,
        long loadedGameTick, long expectedGameTick, Func<string?> readLoadedName, Action<string> setGameName)
    {
        if (source is null || primary is null || string.IsNullOrWhiteSpace(managedIdentity)
            || readLoadedName is null || setGameName is null || loadedGameTick != expectedGameTick
            || source.Prefix.GameTick != expectedGameTick || source.Prefix.MatchesExpectedIdentity
            || primary.Prefix.GameTick >= expectedGameTick || !source.Prefix.Peaceful || !primary.Prefix.Peaceful
            || !primary.Prefix.MatchesExpectedIdentity || source.Fingerprint != expectedSourceFingerprint
            || primary.Fingerprint != expectedPrimaryFingerprint || !source.IsBoundToIdentity(managedIdentity)
            || !primary.IsBoundToIdentity(managedIdentity) || !source.HasSameNativeWorldIdentity(primary)
            || !source.MatchesEmbeddedIdentity(readLoadedName()))
            return "remote_resume_renamed_loaded_source_unproved";
        setGameName(managedIdentity);
        return string.Equals(readLoadedName(), managedIdentity, StringComparison.Ordinal)
            ? null : "remote_resume_managed_name_restore_unproved";
    }
}
