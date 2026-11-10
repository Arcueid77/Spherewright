using Spherewright.Bridge.Core.Factory;
using Spherewright.Contracts.Factory;
using Xunit;

namespace Spherewright.Bridge.Core.Tests;

public sealed class NativeBeltInsertionPolicyTests
{
    [Theory]
    [InlineData("valid", true)]
    [InlineData("missing", false)]
    [InlineData("closed", false)]
    [InlineData("input", false)]
    [InlineData("output", false)]
    [InlineData("count", false)]
    [InlineData("stacks", false)]
    [InlineData("items", false)]
    [InlineData("path", false)]
    [InlineData("length", false)]
    [InlineData("members", false)]
    [InlineData("duplicate", false)]
    [InlineData("selection", false)]
    public void InsertionRequiresACompleteEmptyIndependentPath(string fault, bool expected)
    {
        var path = new MaterialCargoPathSnapshot { PathId = 2, PathLengthCells = 64, BeltObjectIds = [7, 8] };
        switch (fault)
        {
            case "closed": path.PathClosed = true; break;
            case "input": path.InputPathIds = [3]; break;
            case "output": path.OutputPathId = 3; break;
            case "count": path.ItemCount = 1; break;
            case "stacks": path.CargoStackCount = 1; break;
            case "items": path.Items.Add(new() { ItemId = 1101, Count = 1 }); break;
            case "path": path.PathId = 0; break;
            case "length": path.PathLengthCells = 4097; break;
            case "members": path.BeltObjectIds = Enumerable.Range(1, 65).ToList(); break;
            case "duplicate": path.BeltObjectIds.Add(7); break;
            case "selection": path.BeltObjectIds = [8]; break;
        }
        Assert.Equal(expected, NativeBeltInsertionPolicy.IsEmptyIndependentPath(7, fault == "missing" ? null : path));
    }
}
