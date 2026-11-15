using System.Text.Json;
using Spherewright.Contracts.Actions;
using Xunit;

namespace Spherewright.Contracts.Tests;

public class NormalActionFailureContractTests
{
    [Fact]
    public void PowerStarvedHarvestRetainsFailedOutcomeAndStableReasonAcrossSerialization()
    {
        var result = new ActionResultSnapshot
        {
            ActionKind = NormalActionKinds.Harvest,
            State = NormalActionStates.ActionFailed,
            Terminal = true,
            Succeeded = false,
            RecoveryRequired = false,
            FailureKind = NormalActionFailureKinds.MechaPowerStarved,
        };
        var copy = JsonSerializer.Deserialize<ActionResultSnapshot>(JsonSerializer.Serialize(result))!;
        Assert.Equal("mecha_power_starved", copy.FailureKind);
        Assert.Equal(NormalActionStates.ActionFailed, copy.State);
        Assert.True(copy.Terminal);
        Assert.False(copy.Succeeded);
        Assert.False(copy.RecoveryRequired);
    }

    [Fact]
    public void OldActionResultsWithoutAReasonRemainReadable()
    {
        var old = JsonSerializer.Deserialize<ActionResultSnapshot>("{\"ActionKind\":\"harvest\",\"Terminal\":true,\"Succeeded\":false}")!;
        Assert.Null(old.FailureKind);
    }
}
