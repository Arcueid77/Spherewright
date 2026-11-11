using Spherewright.Contracts.Actions;

namespace Spherewright.Bridge.Core.Safety;

public enum HandcraftCompletionState { Unproven, Pending, Completed, Incomplete }

public static class HandcraftCompletionPolicy
{
    public static HandcraftCompletionState Evaluate(HandcraftCompletionReadback? value)
    {
        if (value is null || value.Version != 1 || !value.OriginalTaskBound
            || value.RecipeId <= 0 || value.NativeTaskRecipeId != value.RecipeId
            || value.RequestedExecutions <= 0 || value.InitialTaskExecutions < value.RequestedExecutions
            || value.RemainingExecutions < 0 || value.RemainingExecutions > value.InitialTaskExecutions
            || value.UndeliveredProductCount < 0 || value.CapturedAtGameTick < 0)
            return HandcraftCompletionState.Unproven;
        if (value.NativeTaskInQueue) return HandcraftCompletionState.Pending;
        return value.RemainingExecutions == 0 && value.UndeliveredProductCount == 0
            ? HandcraftCompletionState.Completed : HandcraftCompletionState.Incomplete;
    }
}
