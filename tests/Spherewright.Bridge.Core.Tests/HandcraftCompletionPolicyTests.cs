using Spherewright.Bridge.Core.Safety;
using Spherewright.Contracts.Actions;
using Xunit;

namespace Spherewright.Bridge.Core.Tests;

public sealed class HandcraftCompletionPolicyTests
{
    private static HandcraftCompletionReadback Finished(bool inventoryProductsObserved = false) => new()
    {
        CapturedAtGameTick = 100,
        RecipeId = 6,
        RequestedExecutions = 5,
        OriginalTaskBound = true,
        NativeTaskRecipeId = 6,
        InitialTaskExecutions = 5,
        RemainingExecutions = 0,
        NativeTaskInQueue = false,
        UndeliveredProductCount = 0,
        InventoryProductsObserved = inventoryProductsObserved,
    };

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void OriginalCompletedTaskQualifiesWithInventoryOrAutomaticConsumption(bool inventoryObserved)
        => Assert.Equal(HandcraftCompletionState.Completed, HandcraftCompletionPolicy.Evaluate(Finished(inventoryObserved)));

    [Fact]
    public void ATaskStillInTheNativeQueueRemainsPending()
    {
        var value = Finished(); value.NativeTaskInQueue = true;
        Assert.Equal(HandcraftCompletionState.Pending, HandcraftCompletionPolicy.Evaluate(value));
    }

    [Theory]
    [InlineData(5)]
    [InlineData(2)]
    public void CancelledOrPartiallyCompletedTasksKeepTheirRemainingExecutions(int remaining)
    {
        var value = Finished(inventoryProductsObserved: true); value.RemainingExecutions = remaining;
        Assert.Equal(HandcraftCompletionState.Incomplete, HandcraftCompletionPolicy.Evaluate(value));
    }

    [Fact]
    public void RetainedUndeliveredProductsDoNotQualifyAsCompletedDelivery()
    {
        var value = Finished(); value.UndeliveredProductCount = 2;
        Assert.Equal(HandcraftCompletionState.Incomplete, HandcraftCompletionPolicy.Evaluate(value));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(4)]
    public void AnInitialTaskSmallerThanTheRequestIsUnproven(int initial)
    {
        var value = Finished(); value.InitialTaskExecutions = initial;
        Assert.Equal(HandcraftCompletionState.Unproven, HandcraftCompletionPolicy.Evaluate(value));
    }

    [Theory]
    [InlineData(6)]
    [InlineData(25)]
    [InlineData(1005)]
    public void NativeMergedRootsCompleteAfterAllQueuedExecutions(int initial)
    {
        var value = Finished(); value.InitialTaskExecutions = initial;
        Assert.Equal(HandcraftCompletionState.Completed, HandcraftCompletionPolicy.Evaluate(value));
        value.NativeTaskInQueue = true; value.RemainingExecutions = initial;
        Assert.Equal(HandcraftCompletionState.Pending, HandcraftCompletionPolicy.Evaluate(value));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(6)]
    public void ACancelledMergedRootRetainsItsIncompleteOutcome(int remaining)
    {
        var value = Finished(); value.InitialTaskExecutions = 10; value.RemainingExecutions = remaining;
        Assert.Equal(HandcraftCompletionState.Incomplete, HandcraftCompletionPolicy.Evaluate(value));
    }

    [Fact]
    public void WrongRecipeMissingTaskAndInvalidCountersStayUnproven()
    {
        Assert.Equal(HandcraftCompletionState.Unproven, HandcraftCompletionPolicy.Evaluate(null));
        var value = Finished(); value.NativeTaskRecipeId++;
        Assert.Equal(HandcraftCompletionState.Unproven, HandcraftCompletionPolicy.Evaluate(value));
        value = Finished(); value.OriginalTaskBound = false;
        Assert.Equal(HandcraftCompletionState.Unproven, HandcraftCompletionPolicy.Evaluate(value));
        value = Finished(); value.RemainingExecutions = -1;
        Assert.Equal(HandcraftCompletionState.Unproven, HandcraftCompletionPolicy.Evaluate(value));
        value = Finished(); value.UndeliveredProductCount = -1;
        Assert.Equal(HandcraftCompletionState.Unproven, HandcraftCompletionPolicy.Evaluate(value));
    }

    [Fact]
    public void ReturnedProofIsAnIndependentDtoCopy()
    {
        var original = Finished();
        var copy = original.Copy();
        copy.RemainingExecutions = 4;
        Assert.Equal(0, original.RemainingExecutions);
        Assert.Equal(HandcraftCompletionState.Completed, HandcraftCompletionPolicy.Evaluate(original));
    }
}
