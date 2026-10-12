using System;
using FlaxEngine;
using NUnit.Framework;
using static FlaxAIM.Tests.TestInput;

namespace FlaxAIM.Tests;

/// <summary>
/// Player overrides: rebound controls and action scales.
/// </summary>
[TestFixture]
public class OverrideTests : InputTestBase
{
    private InputAction _jump;
    private InputAction _look;
    private InputMappingContext _context;
    private InputOverrides _overrides;

    [SetUp]
    public void SetUpActions()
    {
        _jump = Action("Jump");
        _look = Action("Look", InputActionType.Axis2D);
        _context = Context("Gameplay",
            Map(_jump, Key(KeyboardKeys.Spacebar), Button(GamepadButton.A)),
            Map(_look, Axis(InputControl.RightStick)));
        Input.AddInputContext(_context);
        _overrides = new InputOverrides();
        Input.Overrides = _overrides;
    }

    private InputBindingKey JumpRow(int row) => new("Gameplay", _jump.ID, row);

    [Test]
    public void ReboundKey_DrivesTheAction_AndTheOldKeyDoesNot()
    {
        _overrides.SetBinding(JumpRow(0), Key(KeyboardKeys.J));

        Devices.Press(KeyboardKeys.Spacebar);
        Tick();
        Assert.That(Input.GetActionValue(_jump).Digital, Is.False);

        Devices.Release(KeyboardKeys.Spacebar);
        Devices.Press(KeyboardKeys.J);
        Tick();
        Assert.That(Input.GetActionValue(_jump).Digital, Is.True);
    }

    [Test]
    public void ReboundKey_LeavesTheOtherRowsAlone()
    {
        _overrides.SetBinding(JumpRow(0), Key(KeyboardKeys.J));

        Devices.Press(GamepadButton.A);
        Tick();

        Assert.That(Input.GetActionValue(_jump).Digital, Is.True);
    }

    [Test]
    public void ReboundKey_KeepsTheAssetsTriggers()
    {
        var hold = new Triggers.TriggerHold { HoldTimeThreshold = 1f };
        var mapping = _context.Mappings[0];
        mapping.Inputs[0] = Key(KeyboardKeys.Spacebar, hold);
        _context.Mappings[0] = mapping;
        Input.RebuildMappings();
        _overrides.SetBinding(JumpRow(0), Key(KeyboardKeys.J));

        Devices.Press(KeyboardKeys.J);
        Tick();

        Assert.That(Input.GetActionState(_jump), Is.Not.EqualTo(State.EnhancedInputActionState.Triggered));
    }

    [Test]
    public void KeyCanBecomeAMouseButton()
    {
        _overrides.SetBinding(JumpRow(0), Mouse(MouseButton.Right));

        Devices.Press(MouseButton.Right);
        Tick();

        Assert.That(Input.GetActionValue(_jump).Digital, Is.True);
    }

    [Test]
    public void RemovingTheOverride_RestoresTheAssetsKey()
    {
        _overrides.SetBinding(JumpRow(0), Key(KeyboardKeys.J));
        Assert.That(_overrides.RemoveBinding(JumpRow(0)), Is.True);

        Devices.Press(KeyboardKeys.Spacebar);
        Tick();

        Assert.That(Input.GetActionValue(_jump).Digital, Is.True);
    }

    [Test]
    public void OverridesForAnotherContext_AreIgnored()
    {
        _overrides.SetBinding(new InputBindingKey("Elsewhere", _jump.ID, 0), Key(KeyboardKeys.J));

        Devices.Press(KeyboardKeys.Spacebar);
        Tick();

        Assert.That(Input.GetActionValue(_jump).Digital, Is.True);
    }

    [Test]
    public void Scale_MultipliesTheFinalValue_PerComponent()
    {
        _overrides.SetScale(_look.ID, new Float3(2f, -1f, 1f));

        Devices.SetAxis(InputAxisType.GamepadRightStickX, 0.5f);
        Devices.SetAxis(InputAxisType.GamepadRightStickY, 0.5f);
        Tick();

        Assert.That(Input.GetActionValue(_look).Axis2D, Is.EqualTo(new Float2(1f, -0.5f)));
    }

    [Test]
    public void ScaleOfOne_IsRemoved()
    {
        _overrides.SetScale(_look.ID, new Float3(2f));
        _overrides.SetScale(_look.ID, Float3.One);

        Assert.That(_overrides.Scales, Is.Empty);
        Assert.That(_overrides.ScaleOf(_look.ID), Is.EqualTo(Float3.One));
    }

    [Test]
    public void Changed_IsRaisedOnlyForRealChanges()
    {
        var changes = 0;
        _overrides.Changed += () => changes++;

        _overrides.SetBinding(JumpRow(0), Key(KeyboardKeys.J));
        _overrides.SetBinding(JumpRow(0), Key(KeyboardKeys.J));
        _overrides.SetScale(_look.ID, Float3.One);
        _overrides.RemoveBinding(JumpRow(1));

        Assert.That(changes, Is.EqualTo(1));
    }

    [Test]
    public void Load_ReplacesEverything()
    {
        _overrides.SetBinding(JumpRow(1), Button(GamepadButton.B));
        _overrides.Load(
            [new(JumpRow(0), Key(KeyboardKeys.J))],
            [new(_look.ID, new Float3(3f))]);

        Assert.That(_overrides.Bindings.Keys, Is.EquivalentTo(new[] { JumpRow(0) }));
        Assert.That(_overrides.ScaleOf(_look.ID), Is.EqualTo(new Float3(3f)));
    }

    [Test]
    public void SharedOverrides_RecompileEveryProcessorUsingThem()
    {
        var second = new InputProcessor();
        second.Enable();
        second.AddInputContext(_context);
        second.Overrides = _overrides;
        try
        {
            _overrides.SetBinding(JumpRow(0), Key(KeyboardKeys.J));
            Devices.Press(KeyboardKeys.J);
            second.Update(Frame);

            Assert.That(second.GetActionValue(_jump).Digital, Is.True);
        }
        finally
        {
            second.Overrides = null;
            second.Disable();
        }
    }
}
