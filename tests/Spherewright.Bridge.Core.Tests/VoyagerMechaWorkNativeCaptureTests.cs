using Spherewright.Plugin.Game;
using Xunit;

namespace Spherewright.Bridge.Core.Tests;

[Collection("Owned resume native substitutes")]
public sealed class VoyagerMechaWorkNativeCaptureTests : IDisposable
{
    private readonly TestHistory? _previous = GameMain.history;
    private readonly Player _player = new();
    public VoyagerMechaWorkNativeCaptureTests()
    {
        GameMain.history = new() { currentTech = 42 };
        GameMain.history.techQueue[0] = 42;
        GameMain.history.techStates[42] = 1;
        LDB.items.rows.Clear(); LDB.recipes.rows.Clear(); LDB.techs.rows.Clear();
        LDB.items.rows[6001] = new() { ID = 6001 };
        LDB.recipes.rows[9] = new() { ID = 9 };
        LDB.techs.rows[42] = new() { ID = 42, Items = [6001], ItemPoints = [12], UnlockRecipes = [9] };
        _player.mecha.lab = new() { player = _player, mecha = _player.mecha, gameHistory = GameMain.history };
    }
    public void Dispose()
    {
        GameMain.history = _previous;
        LDB.items.rows.Clear(); LDB.recipes.rows.Clear(); LDB.techs.rows.Clear();
    }

    [Fact]
    public void ProductionCaptureProvesFactoryOnlyResearchAndRechecksNewPackageInputs()
    {
        Assert.True(NormalGameActionCoordinator.CaptureMechaWorkForTest(_player).FactoryOnlyResearch);
        _player.package.grids[0] = new() { itemId = 6001, count = 1 };
        Assert.False(NormalGameActionCoordinator.CaptureMechaWorkForTest(_player).Proved);
    }

    [Fact]
    public void BindingLossAndFractionalNativePointsBlock()
    {
        _player.mecha.lab!.player = new();
        Assert.False(NormalGameActionCoordinator.CaptureMechaWorkForTest(_player).Proved);
        _player.mecha.lab.player = _player;
        _player.mecha.lab.itemPoints.items[6001] = 1;
        Assert.False(NormalGameActionCoordinator.CaptureMechaWorkForTest(_player).Proved);
    }

    [Fact]
    public void NativeUnlockFunctionAndAwardMetadataAreNotDiscarded()
    {
        LDB.techs.rows[42].UnlockFunctions = [6]; LDB.techs.rows[42].UnlockValues = [1d];
        Assert.False(NormalGameActionCoordinator.CaptureMechaWorkForTest(_player).Proved);
        LDB.techs.rows[42].UnlockFunctions = []; LDB.techs.rows[42].UnlockValues = [];
        LDB.techs.rows[42].AddItems = [6001]; LDB.techs.rows[42].AddItemCounts = [1];
        Assert.False(NormalGameActionCoordinator.CaptureMechaWorkForTest(_player).Proved);
    }

    [Fact]
    public void NativeMalformedQueueAndMissingCatalogStayUnknown()
    {
        GameMain.history!.techQueue[0] = 0; GameMain.history.techQueue[1] = 42;
        Assert.False(NormalGameActionCoordinator.CaptureMechaWorkForTest(_player).Proved);
        GameMain.history.techQueue[0] = 42; GameMain.history.techQueue[1] = 0;
        LDB.recipes.rows.Clear();
        Assert.False(NormalGameActionCoordinator.CaptureMechaWorkForTest(_player).Proved);
    }
}
