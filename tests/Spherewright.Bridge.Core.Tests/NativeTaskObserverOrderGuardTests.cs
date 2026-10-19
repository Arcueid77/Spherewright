using Spherewright.Plugin.Hosting;
using Xunit;

namespace Spherewright.Bridge.Core.Tests;

// Linked production metadata guard on real managed multicast delegates.
// All handler bodies throw: no test invokes Native or even a fake task.
public sealed class NativeTaskObserverOrderGuardTests
{
    [Fact]
    public void ExactNativeMayBeAppendedWithOneObserverWithoutInvokingEitherHandler()
    {
        var source = new Logic(); var observer = new Observer(); var unrelated = new Observer();
        Action<int, int, int> native = source.Handler;
        Action<int, int, int> own = observer.Task;
        Assert.True(NativeTaskObserverOrderGuard.HasUniqueNative(native, source, typeof(Logic)));
        var callbacks = unrelated.Handler + native + own + unrelated.Handler;
        Assert.True(NativeTaskObserverOrderGuard.HasNativeBeforeObserver(callbacks, source, typeof(Logic), own));
    }

    [Theory]
    [InlineData("remove_native")] [InlineData("native_duplicate")] [InlineData("observer_duplicate")]
    [InlineData("observer_first")] [InlineData("replace_target")] [InlineData("replace_method")]
    [InlineData("wrong_declaring_type")] [InlineData("wrong_signature")] [InlineData("remove_observer")]
    public void AStableWorldAndStillPresentObserverDoNotHideAMutatedNativeInvocationList(string mutation)
    {
        var source = new Logic(); var observer = new Observer();
        Action<int, int, int> native = source.Handler;
        Action<int, int, int> own = observer.Task;
        Delegate callbacks = native + own;
        switch (mutation)
        {
            case "remove_native": callbacks = own; break;
            case "native_duplicate": callbacks = native + native + own; break;
            case "observer_duplicate": callbacks = native + own + own; break;
            case "observer_first": callbacks = own + native; break;
            case "replace_target": callbacks = new Logic().Handler + own; break;
            case "replace_method": callbacks = source.Other + own; break;
            case "wrong_declaring_type": callbacks = new ForeignLogic().Handler + own; break;
            case "wrong_signature": callbacks = source.WrongSignature; break;
            case "remove_observer": callbacks = native; break;
        }
        Assert.False(NativeTaskObserverOrderGuard.HasNativeBeforeObserver(callbacks, source, typeof(Logic), own));
    }

    [Fact]
    public void FreshPostFactoryReadDoesNotReuseASuccessfulPrePlayerOrderCheck()
    {
        var source = new Logic(); var observer = new Observer();
        Action<int, int, int> native = source.Handler;
        Action<int, int, int> own = observer.Task;
        Action<int, int, int> callbacks = native + own;
        Assert.True(NativeTaskObserverOrderGuard.HasNativeBeforeObserver(callbacks, source, typeof(Logic), own));
        callbacks = own + native;
        Assert.False(NativeTaskObserverOrderGuard.HasNativeBeforeObserver(callbacks, source, typeof(Logic), own));
        callbacks = native + own;
        Assert.True(NativeTaskObserverOrderGuard.HasNativeBeforeObserver(callbacks, source, typeof(Logic), own));
    }

    [Fact]
    public void RegistrationNeedsOneExactNativeMethodRatherThanAnyDelegateOnTheSameTarget()
    {
        var source = new Logic();
        Assert.False(NativeTaskObserverOrderGuard.HasUniqueNative(null, source, typeof(Logic)));
        Assert.False(NativeTaskObserverOrderGuard.HasUniqueNative(source.Other, source, typeof(Logic)));
        Assert.False(NativeTaskObserverOrderGuard.HasUniqueNative(source.WrongSignature, source, typeof(Logic)));
        Assert.False(NativeTaskObserverOrderGuard.HasUniqueNative(source.Handler + source.Handler, source, typeof(Logic)));
    }

    private sealed class Logic
    {
        internal Action<int, int, int> Handler => OnGameLogicFrame;
        internal Action<int, int> WrongSignature => OnGameLogicFrame;
        internal void Other(int task, int ordinal, int count) => throw new InvalidOperationException("Metadata only");
        private void OnGameLogicFrame(int task, int ordinal, int count) => throw new InvalidOperationException("Metadata only");
        private void OnGameLogicFrame(int task, int ordinal) => throw new InvalidOperationException("Metadata only");
    }
    private sealed class ForeignLogic
    {
        internal Action<int, int, int> Handler => OnGameLogicFrame;
        private void OnGameLogicFrame(int task, int ordinal, int count) => throw new InvalidOperationException("Metadata only");
    }
    private sealed class Observer
    {
        internal Action<int, int, int> Handler => Task;
        internal void Task(int task, int ordinal, int count) => throw new InvalidOperationException("Metadata only");
    }
}
