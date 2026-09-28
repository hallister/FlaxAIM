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
    public readonly List<CompiledBinding> Bindings = [];

    /// <summary> Actions that must be evaluated before this one (chords). </summary>
    public readonly List<Guid> Dependencies = [];
}

internal sealed class CompiledInputMap
{
    public static readonly CompiledInputMap Empty = new();

    /// <summary> Actions in evaluation order: highest-priority context first, chord actions before their dependents. </summary>
    public readonly List<CompiledAction> Actions = [];
}

public static class InputMappingCompiler
{
    private const string VirtualNamePrefix = "AIM";

    /// <summary>
    /// Builds the runtime plan for a context stack and commits its virtual inputs to Flax's input tables.
    /// </summary>
    internal static CompiledInputMap Compile(List<InputMappingContext> contextStack)
    {
        var map = new CompiledInputMap();
        var actionsById = new Dictionary<Guid, CompiledAction>();
        var assetIdsByActionId = new Dictionary<Guid, Guid>();
        var actionConfigs = new List<ActionConfig>();
        var axisConfigs = new List<AxisConfig>();

        // Highest priority (top of the stack) first.
        for (var c = contextStack.Count - 1; c >= 0; c--)
        {
            var context = contextStack[c];
            if (context?.Mappings == null) continue;

            foreach (var actionEntry in context.Mappings)
            {
                var action = actionEntry.InputAction.Instance;
                if (action == null) continue;

                CheckForDuplicateId(actionEntry.InputAction, action, assetIdsByActionId);

                if (!actionsById.TryGetValue(action.ID, out var compiledAction))
                {
                    compiledAction = new CompiledAction { Action = action };
                    actionsById[action.ID] = compiledAction;
                    map.Actions.Add(compiledAction);
                }

                if (actionEntry.InputMapping == null) continue;

                for (var row = 0; row < actionEntry.InputMapping.Count; row++)
                {
                    var entry = actionEntry.InputMapping[row];
                    var binding = CompileBinding(context, action, entry, row, actionConfigs.Count + axisConfigs.Count);

                    if (binding.IsAxis)
                        axisConfigs.Add(CreateNativeAxisConfig(binding.VirtualName, entry));
                    else
                        actionConfigs.Add(CreateNativeActionConfig(binding.VirtualName, entry));

                    foreach (var trigger in binding.Triggers)
                    {
                        if (trigger is TriggerChord chord && chord.ChordAction.Instance is { } chordAction && chordAction.ID != action.ID)
                            compiledAction.Dependencies.Add(chordAction.ID);
                    }

                    compiledAction.Bindings.Add(binding);
                }
            }
        }

        SortByDependencies(map.Actions, actionsById);

        Input.ActionMappings = actionConfigs.ToArray();
        Input.AxisMappings = axisConfigs.ToArray();

        Debug.Log($"[InputManager] Compiled {contextStack.Count} context(s): {map.Actions.Count} action(s), {actionConfigs.Count + axisConfigs.Count} binding(s).");
        return map;
    }

    private static CompiledBinding CompileBinding(InputMappingContext context, InputAction action, InputMappingEntry entry, int row, int ordinal)
    {
        var binding = new CompiledBinding
        {
            VirtualName = $"{VirtualNamePrefix}{ordinal}.{action.Name}",
            IsAxis = entry.UseAxis,
            Component = ResolveComponent(context, action, entry.Target, row),
        };

        var modifiers = new List<InputModifier>();
        if (entry.Modifiers != null)
        {
            foreach (var modifier in entry.Modifiers)
            {
                if (modifier != null) modifiers.Add(modifier);
            }
        }
        binding.Modifiers = modifiers.ToArray();

        var triggers = new List<InputTrigger>();
        if (entry.Triggers != null)
        {
            foreach (var template in entry.Triggers)
            {
                switch (template)
                {
                    case null:
                        break;
                    case InputTrigger trigger:
                        triggers.Add(trigger.CreateInstance());
                        break;
                    default:
                        Debug.LogWarning($"[InputManager] {context.ContextName}/{action.Name} row {row}: trigger {template} is not an InputTrigger and was ignored.");
                        break;
                }
            }
        }
        binding.Triggers = triggers.ToArray();

        return binding;
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

        Debug.LogWarning(target == InputAxisTarget.Auto
            ? $"[InputManager] {context.ContextName}/{action.Name} row {row}: {action.ActionType} actions only have {channelCount} components, so row-based targeting can't place this row. Set its Target explicitly. Using X."
            : $"[InputManager] {context.ContextName}/{action.Name} row {row}: target {target} doesn't exist on a {action.ActionType} action. Using X.");
        return 0;
    }

    private static void CheckForDuplicateId(JsonAssetReference<InputAction> reference, InputAction action, Dictionary<Guid, Guid> assetIdsByActionId)
    {
        var assetId = reference.Asset?.ID ?? Guid.Empty;
        if (assetIdsByActionId.TryGetValue(action.ID, out var existingAssetId))
        {
            if (existingAssetId != assetId)
                Debug.LogError($"[InputManager] Input action '{action.Name}' ({reference.Asset?.Path}) has the same ID as another input action asset, probably because it was duplicated. They will be treated as one action. Give one of them a new ID.");
            return;
        }
        assetIdsByActionId[action.ID] = assetId;
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
            var id = compiledAction.Action.ID;
            if (visited.Contains(id)) return;
            if (!visiting.Add(id))
            {
                Debug.LogWarning($"[InputManager] Chord cycle detected involving '{compiledAction.Action.Name}'. One of the chords will read the previous frame's state.");
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

    private static ActionConfig CreateNativeActionConfig(string name, InputMappingEntry binding)
    {
        return new ActionConfig
        {
            Name = name,
            Mode = InputActionMode.Pressing,
            Key = binding.Key,
            GamepadButton = binding.GamepadButton,
            Gamepad = InputGamepadIndex.All
        };
    }

    /// <summary>
    /// Clean factory utility method to allocate native Flax configurations uniformly.
    /// </summary>
    private static AxisConfig CreateNativeAxisConfig(string name, InputMappingEntry binding)
    {
        return new AxisConfig
        {
            Name = name,
            Gamepad = InputGamepadIndex.All,
            Scale = 1.0f,
            DeadZone = 0.1f,
            Axis = binding.AxisType,
            PositiveButton = binding.KeyPositive,
            NegativeButton = binding.KeyNegative,
            Sensitivity = 1.0f,
            GamepadPositiveButton = binding.GamepadPositiveButton,
            GamepadNegativeButton = binding.GamepadNegativeButton
        };
    }
}
