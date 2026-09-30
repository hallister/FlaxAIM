using System;
using System.Collections.Generic;
using FlaxAIM.Modifiers;
using FlaxAIM.Triggers;
using FlaxEngine;

namespace FlaxAIM;

/// <summary>
/// One Flax virtual input a binding reads, and the component of the action value it drives.
/// </summary>
internal struct CompiledInput
{
    /// <summary> Name of the Flax virtual action/axis. Unique per input. </summary>
    public string VirtualName;

    /// <summary> True if this reads a virtual axis, false if it reads a virtual (button) action. </summary>
    public bool IsAxis;

    /// <summary> Component of the action value this input drives (0 = X, 1 = Y, 2 = Z). </summary>
    public int Component;
}

/// <summary>
/// One binding (an <see cref="InputMappingEntry"/> row) ready for evaluation.
/// </summary>
internal sealed class CompiledBinding
{
    /// <summary> The virtual inputs this binding reads: one, or two for a control read on both axes. </summary>
    public CompiledInput[] Inputs;

    public InputModifier[] Modifiers;

    /// <summary> This manager's own trigger instances, cloned from the asset's templates. </summary>
    public InputTrigger[] Triggers;
}

/// <summary>
/// Every binding of one action, gathered from all active contexts.
/// </summary>
internal sealed class CompiledAction
{
    public InputAction Action;

    /// <summary> Stable identity, see <see cref="ActionIdentity"/>. </summary>
    public Guid Id;

    public readonly List<CompiledBinding> Bindings = [];

    /// <summary> Action-level modifiers, applied to the combined value. </summary>
    public InputModifier[] Modifiers = [];

    /// <summary> Action-level triggers (cloned), evaluated against the combined value. </summary>
    public InputTrigger[] Triggers = [];

    /// <summary> Actions that must be evaluated before this one (chords). </summary>
    public readonly List<Guid> Dependencies = [];
}

internal sealed class CompiledInputMap
{
    public static readonly CompiledInputMap Empty = new();

    /// <summary> Actions in evaluation order: highest-priority context first, chord actions before their dependents. </summary>
    public readonly List<CompiledAction> Actions = [];

    public ActionConfig[] ActionConfigs = [];
    public AxisConfig[] AxisConfigs = [];
}

/// <summary>
/// Per-manager settings that affect compilation.
/// </summary>
internal struct CompileOptions
{
    /// <summary> Unique per manager, so managers' virtual inputs never collide. </summary>
    public int ManagerId;

    public InputGamepadIndex Gamepad;
    public bool UseKeyboardAndMouse;
}

public static class InputMappingCompiler
{
    /// <summary>
    /// One axis of a binding: the Flax axis it reads (<see cref="InputAxisType.KeyboardOnly"/> for keys and
    /// buttons), which of the control's axes it is (0 = X, 1 = Y) and the action component it drives.
    /// </summary>
    private readonly record struct Placement(InputAxisType Axis, int ControlAxis, int Component);

    /// <summary>
    /// Builds the runtime plan for a set of contexts, ordered from highest to lowest priority.
    /// </summary>
    internal static CompiledInputMap Compile(IReadOnlyList<InputMappingContext> contextsByPriority, CompileOptions options)
    {
        var map = new CompiledInputMap();
        var actionsById = new Dictionary<Guid, CompiledAction>();
        var actionConfigs = new List<ActionConfig>();
        var axisConfigs = new List<AxisConfig>();

        // Inputs consumed by higher-priority contexts, and the ones this context will consume once it's done.
        var consumed = new HashSet<int>();
        var consumedByContext = new HashSet<int>();
        var bindingInputs = new List<int>();
        var placements = new List<Placement>(2);

        foreach (var context in contextsByPriority)
        {
            if (context?.Mappings == null) continue;
            consumedByContext.Clear();

            foreach (var mapping in context.Mappings)
            {
                var action = mapping.RuntimeAction ?? mapping.InputAction.Instance;
                if (action == null) continue;

                var actionId = mapping.RuntimeAction != null
                    ? ActionIdentity.Of(action)
                    : ActionIdentity.Register(mapping.InputAction, action);
                if (!actionsById.TryGetValue(actionId, out var compiledAction))
                {
                    compiledAction = CompileAction(action, actionId);
                    actionsById[actionId] = compiledAction;
                    map.Actions.Add(compiledAction);
                }

                if (mapping.Inputs == null) continue;

                for (var row = 0; row < mapping.Inputs.Count; row++)
                {
                    var entry = mapping.Inputs[row];

                    // Players that only use a gamepad skip keyboard and mouse bindings entirely
                    if (!options.UseKeyboardAndMouse && entry.Control.Device() != InputDevice.Gamepad) continue;

                    bindingInputs.Clear();
                    CollectInputs(entry, bindingInputs);
                    if (bindingInputs.Count == 0) continue;
                    if (bindingInputs.Exists(consumed.Contains)) continue;

                    if (action.ConsumeInput) consumedByContext.UnionWith(bindingInputs);

                    var owner = $"{context.ContextName}/{action.Name} row {row}";
                    placements.Clear();
                    Place(entry, action, owner, placements);

                    var inputs = new CompiledInput[placements.Count];
                    for (var i = 0; i < placements.Count; i++)
                    {
                        var placement = placements[i];
                        var virtualName = $"{VirtualInputRegistry.NamePrefix}{options.ManagerId}:{actionConfigs.Count + axisConfigs.Count}:{action.Name}";
                        var isAxis = !entry.Control.IsButton();

                        if (isAxis)
                            axisConfigs.Add(CreateNativeAxisConfig(virtualName, entry, placement, options.Gamepad));
                        else
                            actionConfigs.Add(CreateNativeActionConfig(virtualName, entry, options.Gamepad));

                        inputs[i] = new CompiledInput { VirtualName = virtualName, IsAxis = isAxis, Component = placement.Component };
                    }

                    var binding = new CompiledBinding
                    {
                        Inputs = inputs,
                        Modifiers = CollectModifiers(entry.Modifiers),
                        Triggers = CloneTriggers(entry.Triggers, owner),
                    };
                    AddChordDependencies(compiledAction, binding.Triggers);
                    compiledAction.Bindings.Add(binding);
                }
            }

            consumed.UnionWith(consumedByContext);
        }

        SortByDependencies(map.Actions, actionsById);

        map.ActionConfigs = actionConfigs.ToArray();
        map.AxisConfigs = axisConfigs.ToArray();

        InputLog.Info($"Compiled {contextsByPriority.Count} context(s): {map.Actions.Count} action(s), {actionConfigs.Count + axisConfigs.Count} virtual input(s).");
        return map;
    }

    private static CompiledAction CompileAction(InputAction action, Guid actionId)
    {
        var compiledAction = new CompiledAction
        {
            Action = action,
            Id = actionId,
            Modifiers = CollectModifiers(action.Modifiers),
            Triggers = CloneTriggers(action.Triggers, action.Name),
        };
        AddChordDependencies(compiledAction, compiledAction.Triggers);
        return compiledAction;
    }

    private static InputModifier[] CollectModifiers(List<InputModifier> modifiers)
    {
        return modifiers == null ? [] : modifiers.FindAll(m => m != null).ToArray();
    }

    private static InputTrigger[] CloneTriggers(List<IInputTrigger> templates, string owner)
    {
        var triggers = new List<InputTrigger>();
        if (templates == null) return [];

        foreach (var template in templates)
        {
            switch (template)
            {
                case null:
                    break;
                case InputTrigger trigger:
                    triggers.Add(trigger.CreateInstance());
                    break;
                default:
                    InputLog.Warning($"{owner}: trigger {template} is not an InputTrigger and was ignored.");
                    break;
            }
        }
        return triggers.ToArray();
    }

    private static void AddChordDependencies(CompiledAction compiledAction, InputTrigger[] triggers)
    {
        foreach (var trigger in triggers)
        {
            if (trigger is not TriggerChord chord || chord.ResolvedChordAction is not { } chordAction) continue;

            var chordId = chord.RuntimeChordAction != null
                ? ActionIdentity.Of(chordAction)
                : ActionIdentity.Register(chord.ChordAction, chordAction);
            if (chordId != compiledAction.Id) compiledAction.Dependencies.Add(chordId);
        }
    }

    /// <summary>
    /// Collects an identifier for every physical input a binding reads. Used for input consumption, and to skip
    /// bindings that read nothing.
    /// </summary>
    private static void CollectInputs(InputMappingEntry entry, List<int> inputs)
    {
        const int keyboard = 1 << 16, mouseButton = 2 << 16, gamepadButton = 3 << 16, axis = 4 << 16;

        void Key(KeyboardKeys key)
        {
            if (key != KeyboardKeys.None) inputs.Add(keyboard | (int)key);
        }

        void Button(GamepadButton button)
        {
            if (button != GamepadButton.None) inputs.Add(gamepadButton | (int)button);
        }

        switch (entry.Control)
        {
            case InputControl.Key:
                Key(entry.Key);
                break;
            case InputControl.KeyAxis:
                Key(entry.KeyPositive);
                Key(entry.KeyNegative);
                break;
            case InputControl.DirectionalKeys:
                Key(entry.KeyUp);
                Key(entry.KeyDown);
                Key(entry.KeyLeft);
                Key(entry.KeyRight);
                break;
            case InputControl.MouseButton:
                if (entry.MouseButton != MouseButton.None) inputs.Add(mouseButton | (int)entry.MouseButton);
                break;
            case InputControl.GamepadButton:
                Button(entry.GamepadButton);
                break;
            case InputControl.GamepadButtonAxis:
                Button(entry.GamepadPositiveButton);
                Button(entry.GamepadNegativeButton);
                break;
            case InputControl.DPad:
                // Flax reads the D-pad axes from the D-pad buttons, so they conflict with bindings to those buttons
                if (entry.Axes != InputControlAxes.Y)
                {
                    Button(GamepadButton.DPadRight);
                    Button(GamepadButton.DPadLeft);
                }
                if (entry.Axes != InputControlAxes.X)
                {
                    Button(GamepadButton.DPadUp);
                    Button(GamepadButton.DPadDown);
                }
                break;
            default:
                var (x, y) = FlaxAxes(entry.Control);
                if (!entry.Control.Is2D() || entry.Axes != InputControlAxes.Y) inputs.Add(axis | (int)x);
                if (entry.Control.Is2D() && entry.Axes != InputControlAxes.X) inputs.Add(axis | (int)y);
                break;
        }
    }

    /// <summary>
    /// The Flax axes an analog control reads: X, and Y for controls with two axes.
    /// Keys and buttons read <see cref="InputAxisType.KeyboardOnly"/>.
    /// </summary>
    private static (InputAxisType X, InputAxisType Y) FlaxAxes(InputControl control) => control switch
    {
        InputControl.MouseDelta   => (InputAxisType.MouseX, InputAxisType.MouseY),
        InputControl.MouseWheel   => (InputAxisType.MouseWheel, InputAxisType.KeyboardOnly),
        InputControl.LeftStick    => (InputAxisType.GamepadLeftStickX, InputAxisType.GamepadLeftStickY),
        InputControl.RightStick   => (InputAxisType.GamepadRightStickX, InputAxisType.GamepadRightStickY),
        InputControl.DPad         => (InputAxisType.GamepadDPadX, InputAxisType.GamepadDPadY),
        InputControl.LeftTrigger  => (InputAxisType.GamepadLeftTrigger, InputAxisType.KeyboardOnly),
        InputControl.RightTrigger => (InputAxisType.GamepadRightTrigger, InputAxisType.KeyboardOnly),
        _                         => (InputAxisType.KeyboardOnly, InputAxisType.KeyboardOnly),
    };

    /// <summary>
    /// Decides the native inputs a binding compiles to and the action component each one drives. A binding read on
    /// both axes fills X and Y; any other binding drives its <see cref="InputMappingEntry.Target"/>.
    /// </summary>
    private static void Place(InputMappingEntry entry, InputAction action, string owner, List<Placement> placements)
    {
        var channelCount = action.ActionType switch
        {
            InputActionType.Axis2D => 2,
            InputActionType.Axis3D => 3,
            _                      => 1,
        };
        var (x, y) = FlaxAxes(entry.Control);

        if (entry.ComponentCount == 2)
        {
            placements.Add(new Placement(x, 0, 0));
            if (channelCount > 1)
                placements.Add(new Placement(y, 1, 1));
            else
                InputLog.Warning($"{owner}: {entry.Control.DisplayName()} reads two axes, but {action.ActionType} actions have one. Only X is used; set Axes to read one axis.");
            return;
        }

        var component = channelCount == 1 ? 0 : (int)entry.Target;
        if (component >= channelCount)
        {
            InputLog.Warning($"{owner}: target {entry.Target} doesn't exist on a {action.ActionType} action. Using X.");
            component = 0;
        }

        var readsY = entry.Control.Is2D() && entry.Axes == InputControlAxes.Y;
        placements.Add(new Placement(readsY ? y : x, readsY ? 1 : 0, component));
    }

    /// <summary>
    /// Stable topological sort so chord actions are evaluated before the actions that depend on them.
    /// </summary>
    private static void SortByDependencies(List<CompiledAction> actions, Dictionary<Guid, CompiledAction> actionsById)
    {
        var sorted = new List<CompiledAction>(actions.Count);
        var visited = new HashSet<Guid>();
        var visiting = new HashSet<Guid>();

        void Visit(CompiledAction compiledAction)
        {
            var id = compiledAction.Id;
            if (visited.Contains(id)) return;
            if (!visiting.Add(id))
            {
                InputLog.Warning($"Chord cycle detected involving '{compiledAction.Action.Name}'. One of the chords will read the previous frame's state.");
                return;
            }

            foreach (var dependencyId in compiledAction.Dependencies)
            {
                if (actionsById.TryGetValue(dependencyId, out var dependency))
                    Visit(dependency);
            }

            visiting.Remove(id);
            visited.Add(id);
            sorted.Add(compiledAction);
        }

        foreach (var compiledAction in actions)
            Visit(compiledAction);

        actions.Clear();
        actions.AddRange(sorted);
    }

    private static ActionConfig CreateNativeActionConfig(string name, InputMappingEntry binding, InputGamepadIndex gamepad)
    {
        return new ActionConfig
        {
            Name = name,
            Mode = InputActionMode.Pressing,
            Key = binding.Control == InputControl.Key ? binding.Key : KeyboardKeys.None,
            MouseButton = binding.Control == InputControl.MouseButton ? binding.MouseButton : MouseButton.None,
            GamepadButton = binding.Control == InputControl.GamepadButton ? binding.GamepadButton : GamepadButton.None,
            Gamepad = gamepad
        };
    }

    private static AxisConfig CreateNativeAxisConfig(string name, InputMappingEntry binding, Placement placement, InputGamepadIndex gamepad)
    {
        var config = new AxisConfig
        {
            Name = name,
            Gamepad = gamepad,
            Axis = placement.Axis,
            PositiveButton = KeyboardKeys.None,
            NegativeButton = KeyboardKeys.None,
            GamepadPositiveButton = GamepadButton.None,
            GamepadNegativeButton = GamepadButton.None,
            DeadZone = binding.AxisSettings.DeadZone,
            Sensitivity = binding.AxisSettings.Sensitivity,
            Gravity = binding.AxisSettings.Gravity,
            Scale = binding.AxisSettings.Scale,
            Snap = binding.AxisSettings.Snap,
        };

        switch (binding.Control)
        {
            case InputControl.KeyAxis:
                config.PositiveButton = binding.KeyPositive;
                config.NegativeButton = binding.KeyNegative;
                break;
            case InputControl.DirectionalKeys:
                config.PositiveButton = placement.ControlAxis == 0 ? binding.KeyRight : binding.KeyUp;
                config.NegativeButton = placement.ControlAxis == 0 ? binding.KeyLeft : binding.KeyDown;
                break;
            case InputControl.GamepadButtonAxis:
                config.GamepadPositiveButton = binding.GamepadPositiveButton;
                config.GamepadNegativeButton = binding.GamepadNegativeButton;
                break;
        }
        return config;
    }
}
