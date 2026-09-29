using System;
using System.Collections.Generic;
using FlaxAIM.Modifiers;
using FlaxAIM.Triggers;
using FlaxEngine;

namespace FlaxAIM;

/// <summary>
/// One binding (an <see cref="InputMappingEntry"/> row) ready for evaluation.
/// </summary>
internal sealed class CompiledBinding
{
    /// <summary> Name of the Flax virtual action/axis this binding reads. Unique per binding. </summary>
    public string VirtualName;

    /// <summary> True if the binding reads a virtual axis, false if it reads a virtual (button) action. </summary>
    public bool IsAxis;

    /// <summary> Component of the action value this binding drives (0 = X, 1 = Y, 2 = Z). </summary>
    public int Component;

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

        foreach (var context in contextsByPriority)
        {
            if (context?.Mappings == null) continue;
            consumedByContext.Clear();

            foreach (var actionEntry in context.Mappings)
            {
                var action = actionEntry.RuntimeAction ?? actionEntry.InputAction.Instance;
                if (action == null) continue;

                var actionId = actionEntry.RuntimeAction != null
                    ? ActionIdentity.Of(action)
                    : ActionIdentity.Register(actionEntry.InputAction, action);
                if (!actionsById.TryGetValue(actionId, out var compiledAction))
                {
                    compiledAction = CompileAction(action, actionId);
                    actionsById[actionId] = compiledAction;
                    map.Actions.Add(compiledAction);
                }

                if (actionEntry.InputMapping == null) continue;

                for (var row = 0; row < actionEntry.InputMapping.Count; row++)
                {
                    var entry = AdaptToDevices(actionEntry.InputMapping[row], options);

                    bindingInputs.Clear();
                    CollectInputs(entry, bindingInputs);
                    if (bindingInputs.Count == 0) continue;
                    if (bindingInputs.Exists(consumed.Contains)) continue;

                    if (action.ConsumeInput) consumedByContext.UnionWith(bindingInputs);

                    var ordinal = actionConfigs.Count + axisConfigs.Count;
                    var binding = CompileBinding(context, action, entry, row, $"{VirtualInputRegistry.NamePrefix}{options.ManagerId}:{ordinal}:{action.Name}");

                    if (binding.IsAxis)
                        axisConfigs.Add(CreateNativeAxisConfig(binding.VirtualName, entry, options.Gamepad));
                    else
                        actionConfigs.Add(CreateNativeActionConfig(binding.VirtualName, entry, options.Gamepad));

                    AddChordDependencies(compiledAction, binding.Triggers);
                    compiledAction.Bindings.Add(binding);
                }
            }

            consumed.UnionWith(consumedByContext);
        }

        SortByDependencies(map.Actions, actionsById);

        map.ActionConfigs = actionConfigs.ToArray();
        map.AxisConfigs = axisConfigs.ToArray();

        InputLog.Info($"Compiled {contextsByPriority.Count} context(s): {map.Actions.Count} action(s), {actionConfigs.Count + axisConfigs.Count} binding(s).");
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

    private static CompiledBinding CompileBinding(InputMappingContext context, InputAction action, InputMappingEntry entry, int row, string virtualName)
    {
        return new CompiledBinding
        {
            VirtualName = virtualName,
            IsAxis = entry.UseAxis,
            Component = ResolveComponent(context, action, entry.Target, row),
            Modifiers = CollectModifiers(entry.Modifiers),
            Triggers = CloneTriggers(entry.Triggers, $"{context.ContextName}/{action.Name} row {row}"),
        };
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
    /// Strips the parts of a binding this manager doesn't read (keyboard and mouse when disabled).
    /// </summary>
    private static InputMappingEntry AdaptToDevices(InputMappingEntry entry, CompileOptions options)
    {
        if (options.UseKeyboardAndMouse) return entry;

        entry.Key = KeyboardKeys.None;
        entry.MouseButton = MouseButton.None;
        entry.KeyPositive = KeyboardKeys.None;
        entry.KeyNegative = KeyboardKeys.None;
        if (entry.AxisType is InputAxisType.MouseX or InputAxisType.MouseY or InputAxisType.MouseWheel)
            entry.AxisType = InputAxisType.KeyboardOnly;

        return entry;
    }

    /// <summary>
    /// Collects an identifier for every physical input a binding reads. Used for input consumption, and to skip
    /// bindings that read nothing.
    /// </summary>
    private static void CollectInputs(InputMappingEntry entry, List<int> inputs)
    {
        const int keyboard = 1 << 16, gamepadButton = 2 << 16, axis = 3 << 16, mouseButton = 4 << 16;

        if (!entry.UseAxis)
        {
            if (entry.Key != KeyboardKeys.None) inputs.Add(keyboard | (int)entry.Key);
            if (entry.MouseButton != MouseButton.None) inputs.Add(mouseButton | (int)entry.MouseButton);
            if (entry.GamepadButton != GamepadButton.None) inputs.Add(gamepadButton | (int)entry.GamepadButton);
            return;
        }

        if (entry.AxisType != InputAxisType.KeyboardOnly) inputs.Add(axis | (int)entry.AxisType);
        if (entry.KeyPositive != KeyboardKeys.None) inputs.Add(keyboard | (int)entry.KeyPositive);
        if (entry.KeyNegative != KeyboardKeys.None) inputs.Add(keyboard | (int)entry.KeyNegative);
        if (entry.GamepadPositiveButton != GamepadButton.None) inputs.Add(gamepadButton | (int)entry.GamepadPositiveButton);
        if (entry.GamepadNegativeButton != GamepadButton.None) inputs.Add(gamepadButton | (int)entry.GamepadNegativeButton);
    }

    private static int ResolveComponent(InputMappingContext context, InputAction action, InputAxisTarget target, int row)
    {
        var channelCount = action.ActionType switch
        {
            InputActionType.Axis2D => 2,
            InputActionType.Axis3D => 3,
            _                      => 1,
        };

        if (channelCount == 1)
            return 0;

        var component = target == InputAxisTarget.Auto ? row : (int)target - 1;
        if (component < channelCount)
            return component;

        InputLog.Warning(target == InputAxisTarget.Auto
            ? $"{context.ContextName}/{action.Name} row {row}: {action.ActionType} actions only have {channelCount} components, so row-based targeting can't place this row. Set its Target explicitly. Using X."
            : $"{context.ContextName}/{action.Name} row {row}: target {target} doesn't exist on a {action.ActionType} action. Using X.");
        return 0;
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
            Key = binding.Key,
            MouseButton = binding.MouseButton,
            GamepadButton = binding.GamepadButton,
            Gamepad = gamepad
        };
    }

    private static AxisConfig CreateNativeAxisConfig(string name, InputMappingEntry binding, InputGamepadIndex gamepad)
    {
        return new AxisConfig
        {
            Name = name,
            Gamepad = gamepad,
            Axis = binding.AxisType,
            PositiveButton = binding.KeyPositive,
            NegativeButton = binding.KeyNegative,
            GamepadPositiveButton = binding.GamepadPositiveButton,
            GamepadNegativeButton = binding.GamepadNegativeButton,
            DeadZone = binding.AxisDeadZone,
            Sensitivity = binding.AxisSensitivity,
            Gravity = binding.AxisGravity,
            Scale = binding.AxisScale,
            Snap = binding.AxisSnap,
        };
    }
}
