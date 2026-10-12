using System.Reflection;
using UnityEngine;

namespace Spherewright.Plugin.Game;

// Tool-owned data only. Do not open/register this as the player's active UI tool.
// The native UI initializer allocates a visible anchor/GPUI buffers and global event hooks;
// native placement maths and condition checks do not need those rendering resources.
internal sealed class SpherewrightBlueprintBuildTool : BuildTool_BlueprintPaste, IDisposable
{
    private static readonly FieldInfo ReformGridIds = typeof(BuildTool_BlueprintPaste)
        .GetField("reformGridIds", BindingFlags.Instance | BindingFlags.NonPublic)
        ?? throw new MissingFieldException("Native blueprint tool layout is unsupported.");

    protected override void _OnInit()
    {
        dotsSnapped = new Vector3[1];
        dotsCursor = 1;
        bpGratBoxArr = new BPGratBox[8];
        bpGratBoxConditionArr = new IntVector4[8];
        // This is the exact tool-local initialization made by native _OnOpen. It is NOT
        // a game/world field, and only accumulates rejected terrain cells during checking.
        ReformGridIds.SetValue(this, new HashSet<int>());
        useReforms = false;
        autoBuryBase = false;
    }

    protected override void _OnFree()
    {
        foreach (var preview in bpPool ?? Array.Empty<BuildPreview>()) preview?.Free();
        bpPool = null;
        bpCursor = 0;
        dotsSnapped = null;
        bpGratBoxArr = null;
        bpGratBoxConditionArr = null;
        blueprint = null;
    }

    internal void Translate(BlueprintData data, Vector3 requestedPosition, int quarterTurns)
    {
        blueprint = data;
        yaw = quarterTurns * 90f;
        castGroundPosSnapped = planet.aux.Snap(requestedPosition, true);
        dotsSnapped[0] = BlueprintUtils.RecalculateCursorPos(castGroundPosSnapped, yaw, data, 0, segment);
        // Match the normal paste UI's cursor sequence. This only adjusts our one-dot
        // preview array; all returned poses still undergo the usual range/collision/
        // technology/material checks and become part of the fresh site approval hash.
        BlueprintUtils.SnapTropic(actionBuild, data, dotsSnapped, 1, yaw, segment);
        gratBoxCursor = BlueprintUtils.GenerateAreaGratBoxByBPData(data, bpGratBoxArr,
            bpGratBoxConditionArr, dotsSnapped, 1, yaw, segment);
        bpCursor = BlueprintUtils.InitBuildPreviewByBPData(data, ref bpPool, 1);
        // false never enters native platform/reform preparation. Native translation sets
        // every pose and its tropic condition, including sorter slot endpoint adjustment.
        BlueprintUtils.RefreshBuildPreview(planet, data, bpPool, ref previewReform, false,
            dotsSnapped, 1, bpGratBoxConditionArr, yaw, false, segment);
    }

    internal bool CheckNewObjects()
    {
        using (var ui = new NativeBuildPreviewUiScope())
            return CheckBuildConditionsPrestage() && CheckBuildConditions();
    }

    internal void SetReadOnlyLinkedSorters(IReadOnlyList<BuildPreview> sorters, int totalNewObjects)
    {
        // Only sorters enter the checked pool. Explicit existing endpoint references
        // and the separately native-validated NEW belt poses are not covered objects.
        // Metadata counts the ENTIRE NEW budget for the native technology gate.
        blueprint = new BlueprintData
        {
            primaryAreaIdx = 0,
            buildings = Enumerable.Range(0, totalNewObjects).Select(_ => new BlueprintBuilding()).ToArray(),
            areas = new[] { new BlueprintArea() },
            reformData = new BPReformData(),
        };
        bpPool = sorters.ToArray();
        bpCursor = bpPool.Length;
        gratBoxCursor = 0; // No translated blueprint area; one already validated normal belt span.
    }

    internal void SetReadOnlyLinkedObjects(IReadOnlyList<BuildPreview> objects)
    {
        SetReadOnlyLinkedSorters(objects,objects.Count);
        // The complete route, including ALL intermediate belts and both exact
        // sorters, enters ONE native condition pool. Never invoke CreatePrebuilds.
    }

    public void Dispose() => _Free();
}
