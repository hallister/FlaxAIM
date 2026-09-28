using System.Linq;
using FlaxAIM.Triggers;
using FlaxEngine;
using NUnit.Framework;
using static FlaxAIM.Tests.TestInput;

namespace FlaxAIM.Tests;

/// <summary>
/// The compiled plan and the Flax virtual input configs it produces.
/// </summary>
[TestFixture]
public class CompilerTests
{
    private static readonly CompileOptions Options = new() { ManagerId = 7, Gamepad = InputGamepadIndex.Gamepad2, UseKeyboardAndMouse = true };

    [SetUp]
    public void SetUp() => TestEnvironment.Logs.Clear();

    [Test]
    public void EveryBindingGetsItsOwnVirtualInput()
    {
        var jump = Action("Jump");
        var move = Action("Move", InputActionType.Axis2D);
        var context = Context("Gameplay",
            Map(jump, Key(KeyboardKeys.Spacebar), Button(GamepadButton.A)),
            Map(move, Keys(KeyboardKeys.D, KeyboardKeys.A), Keys(KeyboardKeys.W, KeyboardKeys.S)));

        var map = InputMappingCompiler.Compile([context], Options);

        var names = map.ActionConfigs.Select(c => c.Name).Concat(map.AxisConfigs.Select(c => c.Name)).ToArray();
        Assert.That(names, Has.Length.EqualTo(4));
        Assert.That(names, Is.Unique);
        Assert.That(names, Has.All.StartsWith("AIM:7:"));
        Assert.That(map.ActionConfigs.Select(c => c.Gamepad), Has.All.EqualTo(InputGamepadIndex.Gamepad2));
    }

    [Test]
    public void AxisSettings_AreCopiedToTheConfig()
    {
        var throttle = Action("Throttle", InputActionType.Axis1D);
        var binding = Axis(InputAxisType.GamepadRightTrigger);
        binding.AxisDeadZone = 0.25f;
        binding.AxisSensitivity = 3f;
        binding.AxisGravity = 2f;
        binding.AxisScale = -1f;
        binding.AxisSnap = true;

        var map = InputMappingCompiler.Compile([Context("Gameplay", Map(throttle, binding))], Options);

        var config = map.AxisConfigs.Single();
        Assert.That(config.Axis, Is.EqualTo(InputAxisType.GamepadRightTrigger));
        Assert.That(config.DeadZone, Is.EqualTo(0.25f));
        Assert.That(config.Sensitivity, Is.EqualTo(3f));
        Assert.That(config.Gravity, Is.EqualTo(2f));
        Assert.That(config.Scale, Is.EqualTo(-1f));
        Assert.That(config.Snap, Is.True);
    }

    [Test]
    public void BindingsThatReadNothing_AreSkipped()
    {
        var jump = Action("Jump");

        var map = InputMappingCompiler.Compile([Context("Gameplay", Map(jump, new InputMappingEntry(), Key(KeyboardKeys.Spacebar)))], Options);

        Assert.That(map.Actions.Single().Bindings, Has.Count.EqualTo(1));
    }

    [Test]
    public void TriggerTemplates_AreClonedForEveryCompile()
    {
        var template = new TriggerHold();
        var context = Context("Gameplay", Map(Action("Interact"), Key(KeyboardKeys.E, template)));

        var first = InputMappingCompiler.Compile([context], Options).Actions[0].Bindings[0].Triggers[0];
        var second = InputMappingCompiler.Compile([context], Options).Actions[0].Bindings[0].Triggers[0];

        Assert.That(first, Is.InstanceOf<TriggerHold>());
        Assert.That(first, Is.Not.SameAs(template));
        Assert.That(second, Is.Not.SameAs(first));
    }

    [Test]
    public void NullAndUnknownTriggers_AreIgnored()
    {
        var entry = Key(KeyboardKeys.E);
        entry.Triggers = [null, new NotATrigger(), new TriggerPressed()];

        var map = InputMappingCompiler.Compile([Context("Gameplay", Map(Action("Interact"), entry))], Options);

        Assert.That(map.Actions[0].Bindings[0].Triggers, Has.Length.EqualTo(1));
        Assert.That(TestEnvironment.Warnings.Count(), Is.EqualTo(1));
    }

    [Test]
    public void ChordActions_AreOrderedBeforeTheirDependents()
    {
        var modifier = Action("Modifier");
        var dash = Action("Dash");
        var superDash = Action("SuperDash");
        var context = Context("Gameplay",
            Map(superDash, Key(KeyboardKeys.Q, Chord(dash))),
            Map(dash, Key(KeyboardKeys.Spacebar, Chord(modifier))),
            Map(modifier, Key(KeyboardKeys.Shift)));

        var map = InputMappingCompiler.Compile([context], Options);

        Assert.That(map.Actions.Select(a => a.Action.Name), Is.EqualTo(new[] { "Modifier", "Dash", "SuperDash" }));
    }

    [Test]
    public void ChordCycles_Warn()
    {
        var a = Action("A");
        var b = Action("B");
        var context = Context("Gameplay", Map(a, Key(KeyboardKeys.Q, Chord(b))), Map(b, Key(KeyboardKeys.E, Chord(a))));

        InputMappingCompiler.Compile([context], Options);

        Assert.That(TestEnvironment.Warnings.Any(w => w.Contains("cycle")), Is.True);
    }

    [Test]
    public void ActionLevelTriggersAndModifiers_AreCompiledOnce()
    {
        var charge = Action("Charge");
        charge.Triggers = [new TriggerHold()];
        charge.Modifiers = [new Modifiers.ModifierScale()];
        var context = Context("Gameplay", Map(charge, Key(KeyboardKeys.E)), Map(charge, Key(KeyboardKeys.F)));

        var compiled = InputMappingCompiler.Compile([context], Options).Actions.Single();

        Assert.That(compiled.Bindings, Has.Count.EqualTo(2));
        Assert.That(compiled.Triggers, Has.Length.EqualTo(1));
        Assert.That(compiled.Modifiers, Has.Length.EqualTo(1));
    }

    private sealed class NotATrigger : IInputTrigger;
}
