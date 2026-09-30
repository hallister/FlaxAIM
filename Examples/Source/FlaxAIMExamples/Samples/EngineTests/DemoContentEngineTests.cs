using System;
using System.Linq;
using FlaxAIM.State;
using FlaxAIM.Triggers;
using FlaxEngine;
using FlaxEngine.Json;

namespace FlaxAIM.Samples.EngineTests;

/// <summary>
/// The demo's input assets (Content/Input) load and behave as the demo describes.
/// </summary>
public class DemoContentEngineTests : EngineTestFixture
{
    private static readonly Guid Gameplay = JsonSerializer.ParseID("89449d99d6b64d3aa5b45c8a087b2399");
    private static readonly Guid Menu = JsonSerializer.ParseID("24fee651047049df85d3d5195d8c03a2");

    private static InputMappingContext LoadContext(Guid id)
    {
        var asset = Check.NotNull(Content.Load<JsonAsset>(id), $"Context asset {id}");
        return Check.NotNull(asset.Instance as InputMappingContext, $"Context instance {asset.Path}");
    }

    private static InputAction ActionNamed(InputMappingContext context, string name)
    {
        var entry = context.Mappings.FirstOrDefault(m => m.InputAction.Instance?.Name == name);
        return Check.NotNull(entry.InputAction.Instance, $"{name} in {context.ContextName}");
    }

    [EngineTest]
    public void GameplayContextLoadsWithItsTriggers()
    {
        var gameplay = LoadContext(Gameplay);

        Check.Equal(6, gameplay.Mappings.Count, "Gameplay actions");
        Check.Equal(12, gameplay.Mappings.Sum(m => m.Inputs.Count), "Gameplay bindings");

        var move = gameplay.Mappings.Single(m => m.InputAction.Instance?.Name == "Move");
        Check.That(move.Inputs.Select(i => i.Control).SequenceEqual([InputControl.DirectionalKeys, InputControl.LeftStick]), "Move is WASD plus the left stick");

        var slide = gameplay.Mappings.Single(m => m.InputAction.Instance?.Name == "Slide");
        var chord = slide.Inputs[0].Triggers.OfType<TriggerChord>().Single();
        Check.Equal("Sprint", chord.ChordAction.Instance?.Name, "Slide's chord action");

        var spin = gameplay.Mappings.Single(m => m.InputAction.Instance?.Name == "Spin");
        Check.Near(0.6f, spin.Inputs[0].Triggers.OfType<TriggerHold>().Single().HoldTimeThreshold, "Spin hold time");

        var moveAction = ActionNamed(gameplay, "Move");
        Check.That(moveAction.Modifiers.Count == 1 && moveAction.Modifiers[0] is Modifiers.ModifierDeadZone, "Move's radial dead zone");
    }

    [EngineTest]
    public void DemoControlsBehaveAsDescribed()
    {
        var devices = UseScriptedInput();
        var gameplay = LoadContext(Gameplay);
        var menu = LoadContext(Menu);
        var slide = ActionNamed(gameplay, "Slide");
        var jump = ActionNamed(gameplay, "Jump");
        var move = ActionNamed(gameplay, "Move");
        var confirm = ActionNamed(menu, "MenuConfirm");
        var manager = CreateManager();
        manager.AddInputContext(gameplay);

        devices.Press(KeyboardKeys.C);
        Tick(manager);
        Check.Equal(EnhancedInputActionState.None, manager.GetActionState(slide), "Slide without sprint");

        devices.Press(KeyboardKeys.Shift);
        Tick(manager);
        Check.Equal(EnhancedInputActionState.Triggered, manager.GetActionState(slide), "Slide while sprinting");

        // Paused: the menu takes over WASD and Space
        devices.ReleaseAll();
        manager.AddInputContext(menu, 10);
        devices.Press(KeyboardKeys.W, KeyboardKeys.Spacebar);
        Tick(manager);
        Check.Equal(EnhancedInputActionState.Triggered, manager.GetActionState(confirm), "Menu confirm");
        Check.Equal(EnhancedInputActionState.None, manager.GetActionState(jump), "Jump while paused");
        Check.Equal(EnhancedInputActionState.None, manager.GetActionState(move), "Move while paused");
    }

    [EngineTest]
    public void DirectionalKeysDriveBothAxes()
    {
        var devices = UseScriptedInput();
        var gameplay = LoadContext(Gameplay);
        var move = ActionNamed(gameplay, "Move");
        var manager = CreateManager();
        manager.AddInputContext(gameplay);

        devices.Press(KeyboardKeys.W, KeyboardKeys.D);
        Tick(manager);
        Tick(manager);

        var value = manager.GetActionValue(move).Axis2D;
        Check.That(value.X > 0f && value.Y > 0f, $"W+D moves up and right (got {value})");
    }
}
