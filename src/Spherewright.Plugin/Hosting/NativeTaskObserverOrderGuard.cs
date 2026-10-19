namespace Spherewright.Plugin.Hosting;

// Metadata only: never calls GameLogic or any subscriber. The captured Native
// handler must still occur exactly once before our exact observer, including
// on the unchanged-binding fast path and at every actual task callback.
internal static class NativeTaskObserverOrderGuard
{
    internal static bool HasUniqueNative(Delegate? callbacks, object capturedLogic, Type nativeDeclaringType)
    {
        try
        {
            return callbacks is not null && callbacks.GetInvocationList()
                .Count(d => IsExactNative(d, capturedLogic, nativeDeclaringType)) == 1;
        }
        catch (Exception) { return false; }
    }

    internal static bool HasNativeBeforeObserver(Delegate? callbacks, object capturedLogic,
        Type nativeDeclaringType, Delegate observer)
    {
        try
        {
            if (callbacks is null) return false;
            var handlers = callbacks.GetInvocationList();
            var nativeCount = 0; var observerCount = 0; var nativeIndex = -1; var observerIndex = -1;
            for (var i = 0; i < handlers.Length; i++)
            {
                if (IsExactNative(handlers[i], capturedLogic, nativeDeclaringType)) { nativeCount++; nativeIndex = i; }
                if (handlers[i].Equals(observer)) { observerCount++; observerIndex = i; }
            }
            return nativeCount == 1 && observerCount == 1 && nativeIndex < observerIndex;
        }
        catch (Exception) { return false; }
    }

    private static bool IsExactNative(Delegate candidate, object capturedLogic, Type nativeDeclaringType)
    {
        var method = candidate.Method;
        if (!ReferenceEquals(candidate.Target, capturedLogic) || method.DeclaringType != nativeDeclaringType
            || method.Name != "OnGameLogicFrame" || method.ReturnType != typeof(void)) return false;
        var arguments = method.GetParameters();
        return arguments.Length == 3 && arguments.All(p => p.ParameterType == typeof(int));
    }
}
