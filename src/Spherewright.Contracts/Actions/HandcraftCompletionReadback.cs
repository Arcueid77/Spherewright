namespace Spherewright.Contracts.Actions;

public sealed class HandcraftCompletionReadback
{
    public int Version { get; set; } = 1;
    public long CapturedAtGameTick { get; set; }
    public int RecipeId { get; set; }
    public int RequestedExecutions { get; set; }
    public bool OriginalTaskBound { get; set; }
    public int NativeTaskRecipeId { get; set; }
    public int InitialTaskExecutions { get; set; }
    public int RemainingExecutions { get; set; }
    public bool NativeTaskInQueue { get; set; }
    public long UndeliveredProductCount { get; set; }
    public bool InventoryProductsObserved { get; set; }

    public HandcraftCompletionReadback Copy() => (HandcraftCompletionReadback)MemberwiseClone();
}
