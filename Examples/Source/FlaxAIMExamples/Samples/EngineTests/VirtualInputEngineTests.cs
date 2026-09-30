using System;
using System.Linq;
using FlaxEngine;

namespace FlaxAIM.Samples.EngineTests;

/// <summary>
/// Managers publishing to Flax's real virtual input tables, alongside the project's own mappings and each other.
/// </summary>
public class VirtualInputEngineTests : EngineTestFixture
{
    private const string ProbeName = "FlaxAIMEngineTestProbe";

    private static bool IsPluginMapping(string name) => name?.StartsWith(VirtualInputRegistry.NamePrefix) == true;

    private static string[] PluginNames() =>
        Input.ActionMappings.Select(c => c.Name).Concat(Input.AxisMappings.Select(c => c.Name)).Where(IsPluginMapping).ToArray();

    /// <summary>
    /// The manager ID segment of a plugin mapping name (<c>AIM:&lt;manager&gt;:&lt;binding&gt;:&lt;action&gt;</c>).
    /// </summary>
    private static string OwnerOf(string name) => name.Split(':')[1];

    /// <summary>
    /// Adds a mapping standing in for the project's own input settings, and restores the tables afterwards.
    /// </summary>
    private void AddProjectMapping()
    {
        var original = Input.ActionMappings;
        Defer(() => Input.ActionMappings = original);
        Input.ActionMappings = [..original, new ActionConfig { Name = ProbeName, Key = KeyboardKeys.F12 }];
    }

    [EngineTest]
    public void ProjectMappingsAreKeptAndRestored()
    {
        Check.Equal(0, PluginNames().Length, "Plugin mappings before the test");
        AddProjectMapping();

        var manager = CreateManager();
        manager.AddInputContext(Context("Gameplay", Map(ActionAsset("Jump"), Key(KeyboardKeys.Spacebar), Button(GamepadButton.A))));

        Check.That(Input.ActionMappings.Any(c => c.Name == ProbeName), "Project mapping kept while the manager is active");
        Check.Equal(2, PluginNames().Length, "Published bindings");

        manager.Processor.Disable();

        Check.Equal(0, PluginNames().Length, "Plugin mappings after disabling");
        Check.That(Input.ActionMappings.Any(c => c.Name == ProbeName), "Project mapping restored");
    }

    [EngineTest]
    public void TwoManagersPublishSideBySide()
    {
        var jump = ActionAsset("Jump");
        var first = CreateManager();
        var second = CreateManager();
        first.AddInputContext(Context("Player1", Map(jump, Key(KeyboardKeys.Spacebar))));
        second.AddInputContext(Context("Player2", Map(jump, Key(KeyboardKeys.Return))));

        var owners = PluginNames().Select(OwnerOf).Distinct().ToArray();
        Check.Equal(2, owners.Length, "Managers with published bindings");

        second.Processor.Disable();

        Check.Equal(1, PluginNames().Length, "Bindings left after disabling one manager");
    }

    [EngineTest]
    public void PublishedConfigsUseTheManagersSettings()
    {
        var throttle = ActionAsset("Throttle", InputActionType.Axis1D);
        var manager = CreateManager(configure: m => m.Gamepad = InputGamepadIndex.Gamepad1);
        manager.AddInputContext(Context("Gameplay",
            Map(ActionAsset("Jump"), Button(GamepadButton.A)),
            Map(throttle, new InputMappingEntry { Control = InputControl.RightTrigger, AxisSettings = new InputAxisSettings { DeadZone = 0.3f, Scale = 2f } })));

        var action = Input.ActionMappings.Single(c => IsPluginMapping(c.Name));
        var axis = Input.AxisMappings.Single(c => IsPluginMapping(c.Name));
        Check.Equal(InputGamepadIndex.Gamepad1, action.Gamepad, "Button gamepad");
        Check.Equal(GamepadButton.A, Input.GetActionConfigByName(action.Name).GamepadButton, "Config Flax returns by name");
        Check.Near(0.3f, axis.DeadZone, "Axis dead zone");
        Check.Near(2f, axis.Scale, "Axis scale");

        // Settings changed on the script (e.g. in the editor during play) are picked up on its next update
        manager.Gamepad = InputGamepadIndex.Gamepad2;
        manager.OnUpdate();

        Check.Equal(InputGamepadIndex.Gamepad2, Input.ActionMappings.Single(c => IsPluginMapping(c.Name)).Gamepad, "Gamepad after the change");
    }

    [EngineTest]
    public void HigherPriorityContextsConsumeSharedKeys()
    {
        var manager = CreateManager();
        manager.AddInputContext(Context("Gameplay", Map(ActionAsset("Jump"), Key(KeyboardKeys.Spacebar), Button(GamepadButton.A))));
        Check.Equal(2, PluginNames().Length, "Gameplay bindings");

        manager.AddInputContext(Context("Menu", Map(ActionAsset("Confirm"), Key(KeyboardKeys.Spacebar))), 10);

        // Menu's Space plus gameplay's gamepad A; gameplay's Space is consumed
        Check.Equal(2, PluginNames().Length, "Bindings with the menu on top");
        Check.That(PluginNames().Any(n => n.EndsWith(":Confirm")), "Menu binding published");
    }
}
