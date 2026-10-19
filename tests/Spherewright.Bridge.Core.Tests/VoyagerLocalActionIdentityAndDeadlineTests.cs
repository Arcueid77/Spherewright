using Spherewright.Bridge.Core.Safety;
using Xunit;

namespace Spherewright.Bridge.Core.Tests;

public sealed class VoyagerLocalActionIdentityAndDeadlineTests
{
    [Theory]
    [InlineData("other-world", "native", true, "current-world", "current-world", "native")]
    [InlineData("world", "old-native", true, "world", "world", "native")]
    [InlineData("world", "native", false, "world", "world", "native")]
    [InlineData("world", "native", true, "world", "other-world", "native")]
    [InlineData("world", "native", true, "World", "world", "native")]
    [InlineData("", "native", true, "", "", "native")]
    [InlineData("world", null, true, "world", "world", null)]
    public void SamePlanetAndPhaseCannotBorrowAnotherOwnedWorldOrVersionBudget(string? stored, string? version,
        bool owned, string? actual, string? session, string? currentVersion) =>
        Assert.False(VoyagerLocalActionBudgetPolicy.MatchesOwnedJourneyIdentity(stored, version, owned, actual, session, currentVersion));

    [Fact]
    public void ExactOwnedWorldAndVersionRetainTheExistingJourneyIdentity() =>
        Assert.True(VoyagerLocalActionBudgetPolicy.MatchesOwnedJourneyIdentity("world", "native", true, "world", "world", "native"));

    [Fact]
    public void LocalDeadlineKeepsOriginalReturnWallTimeEvenWhenGameTicksPause()
    {
        var start = new DateTimeOffset(2025, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var deadline = VoyagerLocalActionBudgetPolicy.WorkDeadlineUtc(start, start.AddSeconds(950), 7200, 54000);
        Assert.Equal(start.AddSeconds(50), deadline);
        Assert.True(VoyagerLocalActionBudgetPolicy.LocalWindowStillOpen(100, 101, 7300, start, start.AddSeconds(49), deadline));
        Assert.False(VoyagerLocalActionBudgetPolicy.LocalWindowStillOpen(100, 101, 7300, start, start.AddSeconds(50), deadline));
        Assert.Equal(start.AddSeconds(120), VoyagerLocalActionBudgetPolicy.WorkDeadlineUtc(start, start.AddHours(1), 7200, 54000));
    }

    [Fact]
    public void ExpiredOrMissingClocksNeverGrantAnotherWorkWindow()
    {
        var start = new DateTimeOffset(2025, 1, 1, 0, 0, 0, TimeSpan.Zero);
        Assert.Null(VoyagerLocalActionBudgetPolicy.WorkDeadlineUtc(start, start.AddSeconds(900), 7200, 54000));
        Assert.Null(VoyagerLocalActionBudgetPolicy.WorkDeadlineUtc(default, start.AddHours(1), 7200, 54000));
        Assert.False(VoyagerLocalActionBudgetPolicy.LocalWindowStillOpen(100, 99, 7300, start, start, start.AddMinutes(1)));
        Assert.False(VoyagerLocalActionBudgetPolicy.LocalWindowStillOpen(100, 100, 7300, start, start, null));
        Assert.False(VoyagerLocalActionBudgetPolicy.LocalWindowStillOpen(100, 100, 7300, start, start.AddTicks(-1), start.AddMinutes(1)));
        Assert.False(VoyagerLocalActionBudgetPolicy.LocalWindowStillOpen(100, 7300, 7300, start, start, start.AddMinutes(1)));
    }
}
