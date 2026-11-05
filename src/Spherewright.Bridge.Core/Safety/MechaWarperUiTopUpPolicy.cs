namespace Spherewright.Bridge.Core.Safety;

/// <summary>Native UI TakeItem/AddItemStacked transfer, never blind partial AutoReplenishWarper.</summary>
public static class MechaWarperUiTopUpPolicy
{
    public static MechaWarperSupplyDecision Evaluate(MechaWarperSupplyFacts f,int count)
    {
        var location=f.CurrentPlanetVerified && f.IsGrounded || f.PausedSpaceSourceAuthorized
            && f.SpaceStopSpeed is >=0 and <=.5d;
        if(!f.OwnedBindingVerified || !location || !f.PlayerStateHashFresh
            || !f.IsAlive || !f.ControllerEnabled || f.NativeGuideRunning || !f.IsIdle
            || !f.ChamberIsFiltered || f.ChamberSlotCount!=1 || f.ChamberGridCount!=1
            || f.ChamberBannedGridCount!=0 || f.ChamberItemId!=1210 || f.ChamberFilterItemId!=1210
            || f.ChamberStackSize!=20 || f.ChamberCount is <1 or >=20 || f.PackageCount<=0
            || f.PackageInc!=0 || f.ChamberInc!=0)
            return new() {BlockerCode="warper_ui_topup_exact_native_partial_chamber_required",
                Message="UI top-up requires the exact owned grounded idle binding, a partial native 1210/20 chamber and zero-inc stock."};
        var exact=Math.Min(20-f.ChamberCount,f.PackageCount);
        return count==exact ? new() {Allowed=true,ExactNativeCount=exact}
            : new() {ExactNativeCount=exact,BlockerCode="warper_ui_topup_exact_count_required",
                Message="Use exactly the native chamber's remaining capacity or the smaller real package balance."};
    }

    public static MechaWarperSupplyVerification Verify(MechaWarperSupplyFacts before,MechaWarperSupplyFacts after,
        int count,bool? nativeSuccess)
    {
        var valid=Evaluate(before,count).Allowed && nativeSuccess==true
            && after.OwnedBindingVerified && after.IsAlive
            && (after.CurrentPlanetVerified && after.IsGrounded || after.PausedSpaceSourceAuthorized
                && before.PausedSpaceSourceAuthorized && after.SpaceStopSpeed==before.SpaceStopSpeed)
            && after.ControllerEnabled && !after.NativeGuideRunning && after.IsIdle
            && after.ChamberIsFiltered && after.ChamberSlotCount==1 && after.ChamberGridCount==1
            && after.ChamberBannedGridCount==0 && after.ChamberItemId==1210 && after.ChamberFilterItemId==1210
            && after.ChamberStackSize==20 && after.ChamberCount<=20
            && after.PackageCount==before.PackageCount-count && after.ChamberCount==before.ChamberCount+count
            && after.PackageInc==0 && after.ChamberInc==0 && after.AutoReplenishWarper==before.AutoReplenishWarper;
        return new() {Succeeded=valid,ActualStored=after.ChamberCount-before.ChamberCount,
            BlockerCode=valid?null:"warper_ui_topup_conservation_unproved"};
    }
}
