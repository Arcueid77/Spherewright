using Spherewright.Plugin.Game;
using UnityEngine;
using Xunit;

namespace Spherewright.Bridge.Core.Tests;

public sealed class NativeContactTransformBindingTests
{
    [Fact]
    public void NativeRotationAndDerivedLossyRoundingDoNotResizeTheBoundActor()
    {
        var parent = new Transform(); var actor = new Transform { parent = parent };
        var formerFrozenLossyScale = actor.lossyScale;
        var binding = Assert.IsType<NativeContactTransformBinding>(NativeContactTransformBinding.Capture(actor));
        actor.localRotation = new(0f, .6f, 0f, .8f);
        actor.lossyScale = new(1.0000002f, 1.0000001f, 1.0000001f);
        // The former exact-derived-scale predicate fails on the identical
        // Native-contract input. This reproduces a source defect, not proof of
        // which member failed in the historical conjunction-only receipt.
        Assert.False(actor.lossyScale.Equals(formerFrozenLossyScale));
        Assert.True(binding.Matches(actor));
        actor.localScale = new(1.0000001f, 1f, 1f);
        Assert.False(binding.Matches(actor));
    }

    [Theory]
    [InlineData(0)] [InlineData(1)] [InlineData(2)]
    public void AnyActualScaleInputChangeFailsEvenWhenDerivedLossyScaleIsUnchanged(int level)
    {
        var root = new Transform(); var parent = new Transform { parent = root }; var actor = new Transform { parent = parent };
        var binding = Assert.IsType<NativeContactTransformBinding>(NativeContactTransformBinding.Capture(actor));
        new[] { actor, parent, root }[level].localScale = new(1f, 1f, 1.0000001f);
        Assert.False(binding.Matches(actor));
    }

    [Theory]
    [InlineData(0)] [InlineData(1)] [InlineData(2)]
    public void ReparentAtAnyDepthCannotBorrowTheOriginalBinding(int level)
    {
        var root = new Transform(); var parent = new Transform { parent = root }; var actor = new Transform { parent = parent };
        var binding = Assert.IsType<NativeContactTransformBinding>(NativeContactTransformBinding.Capture(actor));
        new[] { actor, parent, root }[level].parent = new Transform();
        Assert.False(binding.Matches(actor));
        Assert.False(binding.Matches(new Transform()));
    }

    [Theory]
    [InlineData(0f)] [InlineData(-1f)] [InlineData(float.NaN)] [InlineData(float.PositiveInfinity)]
    public void InvalidScaleAtEitherLeafOrAncestorCannotArm(float value)
    {
        Assert.Null(NativeContactTransformBinding.Capture(new Transform { localScale = new(value, 1, 1) }));
        Assert.Null(NativeContactTransformBinding.Capture(new Transform { parent = new Transform { localScale = new(value, value, value) } }));
    }

    [Fact]
    public void NonuniformAncestorCannotAuthorizeRotationInducedShear()
    {
        Assert.Null(NativeContactTransformBinding.Capture(new Transform { parent = new Transform { localScale = new(1, 2, 1) } }));
        var actor = new Transform { localScale = new(1, 2, 3), parent = new Transform { localScale = new(.5f, .5f, .5f) } };
        Assert.True(NativeContactTransformBinding.Capture(actor)!.Matches(actor));
    }

    [Fact]
    public void CycleAndUnboundedHierarchyCannotArm()
    {
        var cycle = new Transform(); cycle.parent = cycle;
        Assert.Null(NativeContactTransformBinding.Capture(cycle));
        var actor = new Transform(); var node = actor;
        for (var i = 1; i < 32; i++) { node.parent = new Transform(); node = node.parent; }
        Assert.NotNull(NativeContactTransformBinding.Capture(actor));
        node.parent = new Transform();
        Assert.Null(NativeContactTransformBinding.Capture(actor));
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void NonfinitePoseOrInvalidRotationNeverArmsOrKeepsABinding(bool ancestor)
    {
        var root = new Transform(); var actor = new Transform { parent = root };
        var changed = ancestor ? root : actor;
        var binding = Assert.IsType<NativeContactTransformBinding>(NativeContactTransformBinding.Capture(actor));
        changed.localPosition = new(float.NaN, 0, 0);
        Assert.False(binding.Matches(actor)); Assert.Null(NativeContactTransformBinding.Capture(actor));
        changed.localPosition = default; changed.localRotation = new(0, 0, 0, 0);
        Assert.False(binding.Matches(actor)); Assert.Null(NativeContactTransformBinding.Capture(actor));
    }
}
