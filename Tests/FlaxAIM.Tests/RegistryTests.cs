using System.Linq;
using FlaxEngine;
using NUnit.Framework;
using static FlaxAIM.Tests.TestInput;
using S = FlaxAIM.State.EnhancedInputActionState;

namespace FlaxAIM.Tests;

/// <summary>
/// Sharing Flax's global mapping tables with the project's own mappings and with other processors.
/// </summary>
[TestFixture]
public class RegistryTests : InputTestBase
{
    private static readonly ActionConfig ProjectFire = new() { Name = "Fire", Key = KeyboardKeys.F };
    private static readonly AxisConfig ProjectLook = new() { Name = "LookX", Axis = InputAxisType.MouseX };

    [Test]
    public void ProjectMappings_AreKeptWhileEnabledAndRestoredAfter()
    {
        Devices.ActionMappings = [ProjectFire];
        Devices.AxisMappings = [ProjectLook];

        Input.AddInputContext(Context("Gameplay", Map(Action("Jump"), Key(KeyboardKeys.Spacebar))));
        Assert.That(Devices.ActionMappings.Select(c => c.Name), Has.Member("Fire"));
        Assert.That(Devices.ActionMappings, Has.Length.EqualTo(2));

        Devices.Press(KeyboardKeys.F);
        Assert.That(Devices.GetAction("Fire"), Is.True, "The project's own action still works");

        Input.Disable();
        Assert.That(Devices.ActionMappings.Select(c => c.Name), Is.EqualTo(new[] { "Fire" }));
        Assert.That(Devices.AxisMappings.Select(c => c.Name), Is.EqualTo(new[] { "LookX" }));
    }

    [Test]
    public void TwoProcessors_KeepBothSetsOfBindings()
    {
        var jump = Action("Jump");
        var other = new InputProcessor();
        other.Enable();
        try
        {
            Input.AddInputContext(Context("Player1", Map(jump, Key(KeyboardKeys.Spacebar))));
            other.AddInputContext(Context("Player2", Map(jump, Key(KeyboardKeys.Return))));

            Devices.Press(KeyboardKeys.Spacebar, KeyboardKeys.Return);
            Tick();
            other.Update(Frame);

            Assert.That(Input.GetActionState(jump), Is.EqualTo(S.Triggered));
            Assert.That(other.GetActionState(jump), Is.EqualTo(S.Triggered));
            Assert.That(Devices.ActionMappings, Has.Length.EqualTo(2));
        }
        finally
        {
            other.Disable();
        }

        Assert.That(Devices.ActionMappings, Has.Length.EqualTo(1), "The other processor's inputs are withdrawn");
    }

    [Test]
    public void LeftoverPluginMappings_AreNotTreatedAsTheProjects()
    {
        Devices.ActionMappings = [ProjectFire, new ActionConfig { Name = "AIM:999:0:Stale" }];

        Input.AddInputContext(Context("Gameplay", Map(Action("Jump"), Key(KeyboardKeys.Spacebar))));
        Input.Disable();

        Assert.That(Devices.ActionMappings.Select(c => c.Name), Is.EqualTo(new[] { "Fire" }));
    }
}
