namespace Spherewright.Plugin.Game;

internal sealed class SpherewrightAddonBuildTool : BuildTool_Addon
{
    public void BindPreparedPose(BuildPreview preview)
    {
        cursorValid = true;
        cursorTarget = preview.lpos;
        castGroundPos = preview.lpos;
        castGroundPosSnapped = preview.lpos;
        multiLevelCovering = preview.inputObjId > 0 && preview.inputToSlot == 14;
        castObjectId = multiLevelCovering ? preview.inputObjId : 0;
    }

    public bool CheckPreparedBuildConditions()
    {
        for (var index = 0; index < buildPreviews.Count; index++) FindPotentialBeltStrict(index);
        return CheckBuildConditions();
    }

    public void SnapshotPlayerInventory()
    {
        tmpPackage = new StorageComponent(player.package.size);
        Array.Copy(player.package.grids, tmpPackage.grids, tmpPackage.size);
        tmpInhandId = player.inhandItemId;
        tmpInhandCount = player.inhandItemCount;
    }

    public void ReleaseSnapshot()
    {
        tmpPackage?.Free();
        tmpPackage = null;
    }
}
