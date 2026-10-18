using System.Text.Json;
using Spherewright.Bridge.Core.Safety;
using Spherewright.Contracts.Actions;
using Xunit;

namespace Spherewright.Bridge.Core.Tests;

public sealed class MechaWarperSupplyPolicyTests
{
    [Theory]
    [InlineData(1, 1)]
    [InlineData(19, 19)]
    [InlineData(20, 20)]
    [InlineData(21, 20)]
    [InlineData(900, 20)]
    public void NativeOneStackCountIsExactForRealPackageStock(int packageCount, int exact)
    {
        var facts = Before();
        facts.PackageCount = packageCount;
        facts.PackageInc = 0;
        var result = MechaWarperSupplyPolicy.Evaluate(facts, exact);
        Assert.True(result.Allowed);
        Assert.Equal(exact, result.ExactNativeCount);
        Assert.Equal(packageCount, facts.PackageCount);
        Assert.Equal(0, facts.ChamberCount);
        Assert.False(facts.AutoReplenishWarper);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(19)]
    [InlineData(21)]
    public void RequestedPartialOrOversizedTransferCannotBecomeNativeOneStack(int count)
    {
        var result = MechaWarperSupplyPolicy.Evaluate(Before(), count);
        Assert.False(result.Allowed);
        Assert.Equal(20, result.ExactNativeCount);
        Assert.Equal("warper_supply_exact_count_required", result.BlockerCode);
    }

    [Theory]
    [InlineData("not_owned", "warper_supply_binding_not_fresh")]
    [InlineData("wrong_planet", "warper_supply_binding_not_fresh")]
    [InlineData("stale_player", "warper_supply_binding_not_fresh")]
    [InlineData("dead", "warper_supply_grounded_idle_required")]
    [InlineData("airborne", "warper_supply_grounded_idle_required")]
    [InlineData("disabled", "warper_supply_grounded_idle_required")]
    [InlineData("guide", "warper_supply_grounded_idle_required")]
    [InlineData("busy", "warper_supply_grounded_idle_required")]
    [InlineData("not_filtered", "warper_supply_native_chamber_required")]
    [InlineData("size", "warper_supply_native_chamber_required")]
    [InlineData("grid_count", "warper_supply_native_chamber_required")]
    [InlineData("bans", "warper_supply_native_chamber_required")]
    [InlineData("unbound_item", "warper_supply_native_chamber_required")]
    [InlineData("wrong_filter", "warper_supply_native_chamber_required")]
    [InlineData("capacity", "warper_supply_native_chamber_required")]
    [InlineData("top_up", "warper_supply_empty_chamber_required")]
    [InlineData("empty_with_inc", "warper_supply_empty_chamber_required")]
    [InlineData("negative_count", "warper_supply_empty_chamber_required")]
    [InlineData("no_package", "warper_supply_package_empty_or_invalid")]
    [InlineData("negative_inc", "warper_supply_package_empty_or_invalid")]
    public void OnlyFreshNativeEmptyChamberIsSupported(string fault, string expectedBlocker)
    {
        var facts = Before();
        switch (fault)
        {
            case "not_owned": facts.OwnedBindingVerified = false; break;
            case "wrong_planet": facts.CurrentPlanetVerified = false; break;
            case "stale_player": facts.PlayerStateHashFresh = false; break;
            case "dead": facts.IsAlive = false; break;
            case "airborne": facts.IsGrounded = false; break;
            case "disabled": facts.ControllerEnabled = false; break;
            case "guide": facts.NativeGuideRunning = true; break;
            case "busy": facts.IsIdle = false; break;
            case "not_filtered": facts.ChamberIsFiltered = false; break;
            case "size": facts.ChamberSlotCount = 2; break;
            case "grid_count": facts.ChamberGridCount = 2; break;
            case "bans": facts.ChamberBannedGridCount = 1; break;
            case "unbound_item": facts.ChamberItemId = 0; break;
            case "wrong_filter": facts.ChamberFilterItemId = 1209; break;
            case "capacity": facts.ChamberStackSize = 100; break;
            case "top_up": facts.ChamberCount = 1; break;
            case "empty_with_inc": facts.ChamberInc = 1; break;
            case "negative_count": facts.ChamberCount = -1; break;
            case "no_package": facts.PackageCount = 0; break;
            case "negative_inc": facts.PackageInc = -1; break;
            default: throw new ArgumentException(nameof(fault));
        }
        var result = MechaWarperSupplyPolicy.Evaluate(facts, 20);
        Assert.False(result.Allowed);
        Assert.Equal(expectedBlocker, result.BlockerCode);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void RealNativeSupplyPreservesAutomaticSettingAndBilateralCountInc(bool autoReplenish)
    {
        var before = Before();
        before.AutoReplenishWarper = autoReplenish;
        var after = After();
        after.AutoReplenishWarper = autoReplenish;
        Assert.True(MechaWarperSupplyPolicy.Evaluate(before, 20).Allowed);
        var verified = MechaWarperSupplyPolicy.VerifyReadback(before, after, 20, true);
        Assert.True(verified.Succeeded);
        Assert.Equal(20, verified.ActualStored);
        Assert.Null(verified.BlockerCode);
        Assert.Equal(900, before.PackageCount);
        Assert.Equal(0, before.ChamberCount);
        Assert.Equal(autoReplenish, before.AutoReplenishWarper);
        Assert.Equal(autoReplenish, after.AutoReplenishWarper);
    }

    [Theory]
    [InlineData("partial")]
    [InlineData("package_loss")]
    [InlineData("chamber_gain")]
    [InlineData("inc_loss")]
    [InlineData("inc_creation")]
    [InlineData("setting")]
    [InlineData("shape")]
    [InlineData("binding")]
    [InlineData("negative_after_inc")]
    public void NativeTrueAloneCannotProveSupply(string fault)
    {
        var before = Before();
        var after = After();
        switch (fault)
        {
            case "partial": after.PackageCount = 899; after.ChamberCount = 1; break;
            case "package_loss": after.PackageCount = 879; break;
            case "chamber_gain": after.ChamberCount = 21; break;
            case "inc_loss": after.ChamberInc = 39; break;
            case "inc_creation": after.PackageInc = 1761; break;
            case "setting": after.AutoReplenishWarper = true; break;
            case "shape": after.ChamberStackSize = 100; break;
            case "binding": after.OwnedBindingVerified = false; break;
            case "negative_after_inc": after.PackageInc = -1; break;
            default: throw new ArgumentException(nameof(fault));
        }
        var result = MechaWarperSupplyPolicy.VerifyReadback(before, after, 20, true);
        Assert.False(result.Succeeded);
        Assert.NotNull(result.BlockerCode);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(null)]
    public void RejectedOrThrowingNativeEntryRemainsUnknownEvenWithPlausibleLaterStock(bool? nativeReturned)
    {
        var result = MechaWarperSupplyPolicy.VerifyReadback(Before(), After(), 20, nativeReturned);
        Assert.False(result.Succeeded);
        Assert.Equal(20, result.ActualStored);
        Assert.Equal("warper_supply_native_result_unknown", result.BlockerCode);
    }

    [Fact]
    public void FullNativeRejectionAndReturnToPackageIsNotSuccessfulSupply()
    {
        var result = MechaWarperSupplyPolicy.VerifyReadback(Before(), Before(), 20, true);
        Assert.False(result.Succeeded);
        Assert.Equal(0, result.ActualStored);
        Assert.Equal("warper_supply_conservation_unproved", result.BlockerCode);
    }

    [Fact]
    public void PartialPackageStackHasExactConservationWithoutRequiringTwentyItems()
    {
        var before = Before();
        before.PackageCount = 5;
        before.PackageInc = 10;
        var after = After();
        after.PackageCount = after.PackageInc = 0;
        after.ChamberCount = 5;
        after.ChamberInc = 10;
        var result = MechaWarperSupplyPolicy.VerifyReadback(before, after, 5, true);
        Assert.True(result.Succeeded);
        Assert.Equal(5, result.ActualStored);
    }

    [Fact]
    public void LargeCopiedIncCannotWrapTheConservationComparison()
    {
        var before = Before();
        before.PackageInc = int.MaxValue;
        var after = After();
        after.PackageInc = int.MaxValue - 40;
        Assert.True(MechaWarperSupplyPolicy.VerifyReadback(before, after, 20, true).Succeeded);
        after.ChamberInc = int.MaxValue;
        Assert.False(MechaWarperSupplyPolicy.VerifyReadback(before, after, 20, true).Succeeded);
    }

    [Theory]
    [InlineData("package_count")]
    [InlineData("package_inc")]
    [InlineData("chamber_count")]
    [InlineData("chamber_inc")]
    [InlineData("auto_flag")]
    [InlineData("filter")]
    [InlineData("capacity")]
    public void PreparedNativeFingerprintBindsEveryTransferBalanceAndSetting(string changed)
    {
        var before = Before();
        var original = MechaWarperSupplyPolicy.Fingerprint(before);
        Assert.Equal(original, MechaWarperSupplyPolicy.Fingerprint(Before()));
        switch (changed)
        {
            case "package_count": before.PackageCount--; break;
            case "package_inc": before.PackageInc--; break;
            case "chamber_count": before.ChamberCount++; break;
            case "chamber_inc": before.ChamberInc++; break;
            case "auto_flag": before.AutoReplenishWarper = true; break;
            case "filter": before.ChamberFilterItemId++; break;
            case "capacity": before.ChamberStackSize++; break;
            default: throw new ArgumentException(nameof(changed));
        }
        Assert.NotEqual(original, MechaWarperSupplyPolicy.Fingerprint(before));
    }

    [Fact]
    public void UnknownNativeReadbackNeverClaimsZeroAfterInventoryOrSuccess()
    {
        var request = new PrepareSupplyMechaWarpersRequest();
        Assert.Equal(20, request.Count);
        Assert.Equal(1, request.StateHashVersion);
        var unknown = new MechaWarperSupplyReadback();
        Assert.Null(unknown.NativeReturnedSuccess);
        Assert.Null(unknown.ActualStored);
        Assert.Null(unknown.PackageCountAfter);
        Assert.Null(unknown.ChamberCountAfter);
        Assert.False(unknown.ReadbackComplete);
        Assert.False(unknown.ConservationVerified);
        Assert.DoesNotContain("NaN", JsonSerializer.Serialize(unknown));
    }

    private static MechaWarperSupplyFacts Before() => new MechaWarperSupplyFacts
    {
        OwnedBindingVerified = true, CurrentPlanetVerified = true, PlayerStateHashFresh = true,
        IsAlive = true, IsGrounded = true, ControllerEnabled = true, IsIdle = true,
        ChamberIsFiltered = true, ChamberSlotCount = 1, ChamberGridCount = 1,
        ChamberItemId = 1210, ChamberFilterItemId = 1210, ChamberStackSize = 20,
        PackageCount = 900, PackageInc = 1800,
    };

    private static MechaWarperSupplyFacts After()
    {
        var after = Before();
        after.PackageCount = 880;
        after.PackageInc = 1760;
        after.ChamberCount = 20;
        after.ChamberInc = 40;
        return after;
    }
}
