using System;
using System.Collections.Generic;
using System.Linq;
using FlaxAIM.Modifiers;
using FlaxEngine;
using NUnit.Framework;
using static FlaxAIM.Tests.TestInput;
using S = FlaxAIM.State.EnhancedInputActionState;

namespace FlaxAIM.Tests;

/// <summary>
/// Callback binding: overloads, handles, unbinding, and tag lookups.
/// </summary>
[TestFixture]
public class BindingTests : InputTestBase
{
    private static readonly Tag JumpTag = new(1);
    private static readonly Tag OtherTag = new(2);

    private InputAction _jump;
    private InputAction _move;

    [SetUp]
    public void SetUpActions()
    {
        _jump = Action("Jump");
        _move = Action("Move", InputActionType.Axis2D);
        Input.AddInputContext(Context("Gameplay",
            Map(_jump, Key(KeyboardKeys.Spacebar)),
            Map(_move, Keys(KeyboardKeys.D, KeyboardKeys.A, InputAxisTarget.X), Keys(KeyboardKeys.W, KeyboardKeys.S, InputAxisTarget.Y))));
    }

    [Test]
    public void TypedCallbacks_ReceiveTheValue()
    {
        Float2 moved = default;
        ProcessedInputActionValue full = default;
        bool? jumped = null;
        var calls = 0;
        Input.BindAction<Float2>(_move, S.Triggered, v => moved = v);
        Input.BindAction(_move, S.Triggered, v => full = v);
        Input.BindAction<bool>(_jump, S.Triggered, v => jumped = v);
        Input.BindAction(_jump, S.Triggered, () => calls++);

        Devices.Press(KeyboardKeys.D, KeyboardKeys.Spacebar);
        Tick();

        Assert.That(moved, Is.EqualTo(new Float2(1f, 0f)));
        Assert.That(full.Axis2D, Is.EqualTo(new Float2(1f, 0f)));
        Assert.That(jumped, Is.True);
        Assert.That(calls, Is.EqualTo(1));
    }

    [Test]
    public void UnsupportedValueTypes_AreRejectedWhenBinding()
    {
        Assert.Throws<ArgumentException>(() => Input.BindAction<int>(_jump, S.Triggered, _ => { }));
    }

    [Test]
    public void DisposingTheHandle_Unbinds()
    {
        var calls = 0;
        var handle = Input.BindAction(_jump, S.Triggered, () => calls++);
        Assert.That(handle.IsBound, Is.True);

        handle.Dispose();
        Devices.Press(KeyboardKeys.Spacebar);
        Tick();

        Assert.That(calls, Is.Zero);
        Assert.That(handle.IsBound, Is.False);
        Assert.DoesNotThrow(handle.Dispose, "Disposing twice");
    }

    [Test]
    public void BindingTheSameCallbackTwice_CallsItOnce()
    {
        var tags = new List<Tag>();
        Action<Tag> callback = tags.Add;
        Input.BindAction(_jump, S.Triggered, callback, JumpTag);
        Input.BindAction(_jump, S.Triggered, callback, JumpTag);

        Devices.Press(KeyboardKeys.Spacebar);
        Tick();

        Assert.That(tags, Has.Count.EqualTo(1));
    }

    [Test]
    public void UnbindAll_RemovesEveryCallbackOfTheOwner()
    {
        var owner = new Listener();
        var other = 0;
        Input.BindAction(_jump, S.Triggered, owner.OnJump);
        Input.BindAction<Float2>(_move, S.Triggered, owner.OnMove);
        Input.BindAction(_jump, S.Triggered, () => other++);

        Input.UnbindAll(owner);
        Devices.Press(KeyboardKeys.Spacebar, KeyboardKeys.W);
        Tick();

        Assert.That(owner.Calls, Is.Zero);
        Assert.That(other, Is.EqualTo(1));
    }

    [Test]
    public void UnbindActionWithATag_KeepsTheCallbacksOtherTags()
    {
        var tags = new List<Tag>();
        Action<Tag> callback = tags.Add;
        Input.BindAction(_jump, S.Triggered, callback, JumpTag);
        Input.BindAction(_jump, S.Triggered, callback, OtherTag);

        Input.UnbindAction(_jump, S.Triggered, callback, JumpTag);
        Devices.Press(KeyboardKeys.Spacebar);
        Tick();

        Assert.That(tags.Select(t => t.Index), Is.EqualTo(new[] { OtherTag.Index }));
    }

    [Test]
    public void UnbindActionWithoutATag_RemovesEveryTag()
    {
        var tags = new List<Tag>();
        Action<Tag> callback = tags.Add;
        Input.BindAction(_jump, S.Triggered, callback, JumpTag);
        Input.BindAction(_jump, S.Triggered, callback, OtherTag);

        Input.UnbindAction(_jump, S.Triggered, callback);
        Devices.Press(KeyboardKeys.Spacebar);
        Tick();

        Assert.That(tags, Is.Empty);
    }

    [Test]
    public void BindingAndUnbindingInsideACallback_IsSafe()
    {
        var late = 0;
        InputBindingHandle handle = null;
        handle = Input.BindAction(_jump, S.Triggered, () =>
        {
            handle.Dispose();
            Input.BindAction(_jump, S.Triggered, () => late++);
        });

        Devices.Press(KeyboardKeys.Spacebar);
        Assert.DoesNotThrow(() => Tick());
        Assert.That(late, Is.Zero, "Bound during dispatch: first called next frame");

        Tick();
        Assert.That(late, Is.EqualTo(1));
    }

    [Test]
    public void ValueByTag_IsAvailableWithoutACallbackFiring()
    {
        // Only bound to Completed, so nothing is dispatched while the key is held
        Input.BindAction(_jump, S.Completed, _ => { }, JumpTag);

        Devices.Press(KeyboardKeys.Spacebar);
        Tick();

        Assert.That(Input.GetActionValue(JumpTag).Digital, Is.True);
        Assert.That(Input.GetActionValue(Tag.Default).Digital, Is.False);
    }

    [Test]
    public void BindingATagToAnotherAction_WarnsAndFollowsTheNewAction()
    {
        Input.BindAction(_jump, S.Triggered, _ => { }, JumpTag);
        Input.BindAction(_move, S.Triggered, _ => { }, JumpTag);

        Devices.Press(KeyboardKeys.W);
        Tick();

        Assert.That(TestEnvironment.Warnings.Any(w => w.Contains("Tag1")), Is.True);
        Assert.That(Input.GetActionValue(JumpTag).Axis2D, Is.EqualTo(new Float2(0f, 1f)));
    }

    [Test]
    public void UnbindingTheLastCallbackForATag_ForgetsTheTag()
    {
        Action<Tag> callback = _ => { };
        Input.BindAction(_jump, S.Triggered, callback, JumpTag);
        Input.UnbindAction(_jump, S.Triggered, callback);

        Devices.Press(KeyboardKeys.Spacebar);
        Tick();

        Assert.That(Input.GetActionValue(JumpTag).Digital, Is.False);
    }

    [Test]
    public void BindingBeforeTheContextIsAdded_Works()
    {
        var sprint = Action("Sprint");
        var calls = 0;
        Input.BindAction(sprint, S.Triggered, () => calls++);

        Input.AddInputContext(Context("Late", Map(sprint, Key(KeyboardKeys.Shift))));
        Devices.Press(KeyboardKeys.Shift);
        Tick();

        Assert.That(calls, Is.EqualTo(1));
    }

    private sealed class Listener
    {
        public int Calls;
        public void OnJump() => Calls++;
        public void OnMove(Float2 value) => Calls++;
    }
}
