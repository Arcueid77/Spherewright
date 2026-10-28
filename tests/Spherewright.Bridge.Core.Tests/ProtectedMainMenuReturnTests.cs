using Spherewright.Bridge.Core.Safety;
using Xunit;

namespace Spherewright.Bridge.Core.Tests;

public sealed class ProtectedMainMenuReturnTests
{
    private static readonly DateTimeOffset Now = DateTimeOffset.Parse("2026-01-01T00:00:00Z");

    [Fact]
    public void SaveAndCompleteProofPrecedeNativeEndAndActualMenuObservation()
    {
        var calls = new List<string>();
        var operation = new ProtectedMainMenuReturn();
        operation.Begin(() => { calls.Add("save"); return 42; }, tick =>
        { Assert.Equal(42, tick); calls.Add("complete"); return true; }, () => calls.Add("end"), Now);
        Assert.Equal(new[] { "save", "complete", "end" }, calls);
        Assert.False(operation.Terminal);
        Assert.True(operation.Readback.CompleteSaveVerified);
        operation.Observe(() => false, Now.AddSeconds(1));
        Assert.False(operation.Terminal);
        operation.Observe(() => true, Now.AddSeconds(2));
        Assert.True(operation.Succeeded);
        Assert.Equal(42, operation.Readback.SavedGameTick);
    }

    [Theory]
    [InlineData("save", "normal_owned_save")]
    [InlineData("verify_false", "complete_current_owned_save")]
    [InlineData("verify_throw", "complete_current_owned_save")]
    [InlineData("invalid_tick", "complete_current_owned_save")]
    public void FailedSaveOrProofNeverEndsWorld(string fault, string expected)
    {
        var ended = false;
        var operation = new ProtectedMainMenuReturn();
        operation.Begin(() => fault == "save" ? throw new IOException("fixture") : fault == "invalid_tick" ? -1 : 42,
            _ => fault == "verify_throw" ? throw new IOException("fixture") : fault != "verify_false",
            () => ended = true, Now);
        Assert.False(ended);
        Assert.True(operation.Terminal);
        Assert.False(operation.Succeeded);
        Assert.Equal(expected, operation.Readback.FailedPredicate);
        operation.Observe(() => true, Now.AddSeconds(1));
        Assert.False(operation.Succeeded);
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void PendingOrFailedNativeEndCannotReplaySaveOrEnd(bool throwAtEnd)
    {
        var saves = 0; var ends = 0;
        var operation = new ProtectedMainMenuReturn();
        void End() { ends++; if (throwAtEnd) throw new InvalidOperationException("fixture"); }
        operation.Begin(() => { saves++; return 42; }, _ => true, End, Now);
        operation.Begin(() => { saves++; return 42; }, _ => true, End, Now.AddSeconds(1));
        Assert.Equal(1, saves); Assert.Equal(1, ends);
        Assert.True(operation.Readback.NativeEndRequested);
        Assert.Equal(throwAtEnd, operation.Terminal);
        if (throwAtEnd) Assert.Equal("native_end_game", operation.Readback.FailedPredicate);
    }

    [Theory]
    [InlineData(false, "main_menu_readback_timeout")]
    [InlineData(true, "main_menu_readback")]
    public void UnprovedMenuHasFiniteOriginalFailure(bool throwOnRead, string expected)
    {
        var operation = new ProtectedMainMenuReturn();
        operation.Begin(() => 42, _ => true, () => { }, Now);
        operation.Observe(() => throwOnRead ? throw new InvalidOperationException("fixture") : false,
            Now.AddSeconds(throwOnRead ? 1 : 10));
        Assert.True(operation.Terminal); Assert.False(operation.Succeeded);
        Assert.Equal(expected, operation.Readback.FailedPredicate);
        operation.Observe(() => true, Now.AddSeconds(11));
        Assert.Equal(expected, operation.Readback.FailedPredicate);
        Assert.False(operation.Succeeded);
    }

    [Fact]
    public void LateMenuObservationCannotBecomeBoundedSuccess()
    {
        var operation = new ProtectedMainMenuReturn();
        operation.Begin(() => 42, _ => true, () => { }, Now);
        operation.Observe(() => true, Now.AddSeconds(11));
        Assert.True(operation.Terminal);
        Assert.False(operation.Succeeded);
        Assert.Equal("main_menu_readback_timeout", operation.Readback.FailedPredicate);
    }
}
