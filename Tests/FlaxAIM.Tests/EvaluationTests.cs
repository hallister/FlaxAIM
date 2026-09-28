using System.Collections.Generic;
using System.Linq;
using FlaxAIM.Modifiers;
using FlaxAIM.Triggers;
using FlaxEngine;
using NUnit.Framework;
using static FlaxAIM.Tests.TestInput;
using S = FlaxAIM.State.EnhancedInputActionState;

namespace FlaxAIM.Tests;

/// <summary>
/// Reading bindings, combining them into action values, and triggers and modifiers inside a processor.
/// </summary>
[TestFixture]
public class EvaluationTests : InputTestBase
{
    private const float Tolerance = 0.0001f;

    [Test]
    public void DigitalAction_TriggersFromEveryBoundKey()
    {
        var jump = Action("Jump");
        Input.AddInputContext(Context("Gameplay", Map(jump, Key(KeyboardKeys.Spacebar), Key(KeyboardKeys.Return))));

        Devices.Press(KeyboardKeys.Return);
        Tick();
        Assert.That(Input.GetActionState(jump), Is.EqualTo(S.Triggered), "Second row");

        Devices.ReleaseAll();
        Tick();
        Devices.Press(KeyboardKeys.Spacebar);
        Tick();
        Assert.That(Input.GetActionState(jump), Is.EqualTo(S.Triggered), "First row");
        Assert.That(Input.GetActionValue(jump).Digital, Is.True);
    }

    [Test]
    public void Press_RaisesStartedAndTriggeredTogether_ThenCompletedOnRelease()
    {
        var jump = Action("Jump");
        Input.AddInputContext(Context("Gameplay", Map(jump, Key(KeyboardKeys.Spacebar))));
        var events = RecordEvents(jump);

        Devices.Press(KeyboardKeys.Spacebar);
        Tick();
        Assert.That(events, Is.EqualTo(new[] { S.Started, S.Triggered }));

        Tick();
        Devices.ReleaseAll();
        Tick();
        Tick();
        Assert.That(events, Is.EqualTo(new[] { S.Started, S.Triggered, S.Triggered, S.Completed }));
    }

    [Test]
    public void Axis1D_TakesTheStrongestBindingInsteadOfSumming()
    {
        var throttle = Action("Throttle", InputActionType.Axis1D);
        Input.AddInputContext(Context("Gameplay", Map(throttle, Keys(KeyboardKeys.W, KeyboardKeys.S), Axis(InputAxisType.GamepadRightTrigger))));

        Devices.Press(KeyboardKeys.W);
        Devices.SetAxis(InputAxisType.GamepadRightTrigger, 0.5f);
        Tick();
        Assert.That(Input.GetActionValue(throttle).Axis1D, Is.EqualTo(1f));

        Devices.Release(KeyboardKeys.W);
        Tick();
        Assert.That(Input.GetActionValue(throttle).Axis1D, Is.EqualTo(0.5f));
    }

    [Test]
    public void Axis2D_KeyboardAndStickCanBothDriveEachAxis()
    {
        var move = Action("Move", InputActionType.Axis2D);
        Input.AddInputContext(Context("Gameplay", Map(move,
            Keys(KeyboardKeys.D, KeyboardKeys.A, InputAxisTarget.X),
            Keys(KeyboardKeys.W, KeyboardKeys.S, InputAxisTarget.Y),
            Axis(InputAxisType.GamepadLeftStickX, InputAxisTarget.X),
            Axis(InputAxisType.GamepadLeftStickY, InputAxisTarget.Y))));

        Devices.Press(KeyboardKeys.W);
        Tick();
        Assert.That(Input.GetActionValue(move).Axis2D, Is.EqualTo(new Float2(0f, 1f)));

        Devices.ReleaseAll();
        Devices.SetAxis(InputAxisType.GamepadLeftStickX, 0.3f);
        Devices.SetAxis(InputAxisType.GamepadLeftStickY, -0.4f);
        Tick();
        Assert.That(Input.GetActionValue(move).Axis2D, Is.EqualTo(new Float2(0.3f, -0.4f)));
    }

    [Test]
    public void AutoTarget_UsesTheRowOrderForExistingAssets()
    {
        var move = Action("Move", InputActionType.Axis2D);
        Input.AddInputContext(Context("Gameplay", Map(move, Keys(KeyboardKeys.D, KeyboardKeys.A), Keys(KeyboardKeys.W, KeyboardKeys.S))));

        Devices.Press(KeyboardKeys.D, KeyboardKeys.S);
        Tick();

        Assert.That(Input.GetActionValue(move).Axis2D, Is.EqualTo(new Float2(1f, -1f)));
    }

    [Test]
    public void AutoTarget_PastTheLastComponent_WarnsAndUsesX()
    {
        var move = Action("Move", InputActionType.Axis2D);
        Input.AddInputContext(Context("Gameplay", Map(move,
            Keys(KeyboardKeys.D, KeyboardKeys.A),
            Keys(KeyboardKeys.W, KeyboardKeys.S),
            Keys(KeyboardKeys.E, KeyboardKeys.Q))));

        Devices.Press(KeyboardKeys.E);
        Tick();

        Assert.That(Input.GetActionValue(move).Axis2D, Is.EqualTo(new Float2(1f, 0f)));
        Assert.That(TestEnvironment.Warnings.Any(w => w.Contains("row 2")), Is.True);
    }

    [Test]
    public void Axis3D_BindingsCanTargetZ()
    {
        var fly = Action("Fly", InputActionType.Axis3D);
        Input.AddInputContext(Context("Gameplay", Map(fly, Keys(KeyboardKeys.E, KeyboardKeys.Q, InputAxisTarget.Z))));

        Devices.Press(KeyboardKeys.Q);
        Tick();

        Assert.That(Input.GetActionValue(fly).Axis3D, Is.EqualTo(new Float3(0f, 0f, -1f)));
        Assert.That(Input.GetActionState(fly), Is.EqualTo(S.Triggered));
    }

    [Test]
    public void TriggersOnOneBinding_DontAffectTheOthers()
    {
        var interact = Action("Interact");
        Input.AddInputContext(Context("Gameplay", Map(interact,
            Key(KeyboardKeys.E, new TriggerHold { HoldTimeThreshold = 0.5f }),
            Key(KeyboardKeys.F))));

        Devices.Press(KeyboardKeys.F);
        Tick();
        Assert.That(Input.GetActionState(interact), Is.EqualTo(S.Triggered), "F has no hold");

        Devices.ReleaseAll();
        Tick();
        Devices.Press(KeyboardKeys.E);
        Advance(0.25f);
        Assert.That(Input.GetActionState(interact), Is.EqualTo(S.Ongoing), "E is still being held");

        Advance(0.3f);
        Assert.That(Input.GetActionState(interact), Is.EqualTo(S.Triggered));
    }

    [Test]
    public void Hold_ReleasedEarly_IsCanceled()
    {
        var interact = Action("Interact");
        Input.AddInputContext(Context("Gameplay", Map(interact, Key(KeyboardKeys.E, new TriggerHold { HoldTimeThreshold = 0.5f }))));
        var events = RecordEvents(interact);

        Devices.Press(KeyboardKeys.E);
        Tick();
        Devices.ReleaseAll();
        Tick();

        Assert.That(events, Is.EqualTo(new[] { S.Started, S.Ongoing, S.Canceled }));
    }

    [Test]
    public void Pressed_TriggersOnceWhileHeld()
    {
        var fire = Action("Fire");
        Input.AddInputContext(Context("Gameplay", Map(fire, Key(KeyboardKeys.Spacebar, new TriggerPressed()))));
        var events = RecordEvents(fire);

        Devices.Press(KeyboardKeys.Spacebar);
        Tick();
        Tick();
        Tick();

        Assert.That(events, Is.EqualTo(new[] { S.Started, S.Triggered, S.Completed }));
    }

    [Test]
    public void BindingModifiers_OnlyApplyToTheirOwnBinding()
    {
        var throttle = Action("Throttle", InputActionType.Axis1D);
        Input.AddInputContext(Context("Gameplay", Map(throttle,
            Keys(KeyboardKeys.W, KeyboardKeys.S).With(new ModifierScale { ScaleX = 0.5f }),
            Axis(InputAxisType.GamepadRightTrigger))));

        Devices.Press(KeyboardKeys.W);
        Tick();
        Assert.That(Input.GetActionValue(throttle).Axis1D, Is.EqualTo(0.5f));

        Devices.ReleaseAll();
        Devices.SetAxis(InputAxisType.GamepadRightTrigger, 0.8f);
        Tick();
        Assert.That(Input.GetActionValue(throttle).Axis1D, Is.EqualTo(0.8f));
    }

    [Test]
    public void ActionModifiers_SeeTheCombinedVector()
    {
        var move = Action("Move", InputActionType.Axis2D);
        move.Modifiers = [new ModifierDeadZone { LowerThreshold = 0.2f, UpperThreshold = 1f }];
        Input.AddInputContext(Context("Gameplay", Map(move,
            Axis(InputAxisType.GamepadLeftStickX, InputAxisTarget.X),
            Axis(InputAxisType.GamepadLeftStickY, InputAxisTarget.Y))));

        // Each axis alone is inside the dead zone; the stick's length isn't
        Devices.SetAxis(InputAxisType.GamepadLeftStickX, 0.15f);
        Devices.SetAxis(InputAxisType.GamepadLeftStickY, 0.15f);
        Tick();

        var value = Input.GetActionValue(move).Axis2D;
        Assert.That(value.X, Is.GreaterThan(0f));
        Assert.That(value.X, Is.EqualTo(value.Y).Within(Tolerance));
    }

    [Test]
    public void ActionTriggers_GateTheBindings()
    {
        var charge = Action("Charge");
        charge.Triggers = [new TriggerHold { HoldTimeThreshold = 0.2f }];
        Input.AddInputContext(Context("Gameplay", Map(charge, Key(KeyboardKeys.Spacebar))));

        Devices.Press(KeyboardKeys.Spacebar);
        Tick();
        Assert.That(Input.GetActionState(charge), Is.EqualTo(S.Ongoing));

        Advance(0.25f);
        Assert.That(Input.GetActionState(charge), Is.EqualTo(S.Triggered));
    }

    [Test]
    public void Chord_NeedsTheChordAction_WhicheverOrderTheyAreListedIn()
    {
        var shift = Action("Shift");
        var dash = Action("Dash");
        // The dependent action is listed first; the chord must still be evaluated before it
        Input.AddInputContext(Context("Gameplay",
            Map(dash, Key(KeyboardKeys.Spacebar, Chord(shift))),
            Map(shift, Key(KeyboardKeys.Shift))));

        Devices.Press(KeyboardKeys.Spacebar);
        Tick();
        Assert.That(Input.GetActionState(dash), Is.EqualTo(S.None), "Without the chord");

        Devices.Press(KeyboardKeys.Shift);
        Tick();
        Assert.That(Input.GetActionState(dash), Is.EqualTo(S.Triggered), "Same frame as the chord");
    }

    [Test]
    public void DefaultActuationThreshold_AppliesToBindingsWithoutTriggers()
    {
        var accelerate = Action("Accelerate", InputActionType.Axis1D);
        Input.AddInputContext(Context("Gameplay", Map(accelerate, Axis(InputAxisType.GamepadRightTrigger))));

        Devices.SetAxis(InputAxisType.GamepadRightTrigger, 0.05f);
        Tick();
        Assert.That(Input.GetActionState(accelerate), Is.EqualTo(S.None));

        Devices.SetAxis(InputAxisType.GamepadRightTrigger, 0.2f);
        Tick();
        Assert.That(Input.GetActionState(accelerate), Is.EqualTo(S.Triggered));

        Input.DefaultActuationThreshold = 0.3f;
        Tick();
        Assert.That(Input.GetActionState(accelerate), Is.EqualTo(S.None));
    }

    [Test]
    public void PreviousFrameMagnitude_IsLastFramesValue()
    {
        var throttle = Action("Throttle", InputActionType.Axis1D);
        Input.AddInputContext(Context("Gameplay", Map(throttle, Axis(InputAxisType.GamepadRightTrigger))));

        Devices.SetAxis(InputAxisType.GamepadRightTrigger, 0.4f);
        Tick();
        Devices.SetAxis(InputAxisType.GamepadRightTrigger, 0.9f);
        Tick();

        Assert.That(Input.GetPreviousFrameMagnitude(throttle), Is.EqualTo(0.4f));
    }

    private List<S> RecordEvents(InputAction action)
    {
        var events = new List<S>();
        foreach (var state in new[] { S.Started, S.Ongoing, S.Triggered, S.Completed, S.Canceled })
        {
            Input.BindAction(action, state, () => events.Add(state));
        }
        return events;
    }
}
