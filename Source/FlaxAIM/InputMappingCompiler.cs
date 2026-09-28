using System.Collections.Generic;
using FlaxEngine;

namespace FlaxAIM;

public static class InputMappingCompiler
{
    public static void Compile(List<InputMappingContext> contextStack)
    {
        var targetActions = new List<ActionConfig>();
        var targetAxes = new List<AxisConfig>();
        
        var processedActions = new Dictionary<string, ActionConfig>();
        var processedAxes = new Dictionary<string, List<AxisConfig>>();

        foreach (var context in contextStack)
        {
            if (context == null) continue;

            foreach (var actionEntry in context.Mappings)
            {
                var action = actionEntry.InputAction.Instance;
 
                if (action == null || string.IsNullOrEmpty(action.Name)) continue;

                if (action.ActionType == InputActionType.Digital)
                {
                    Debug.Log($"[EnhancedInputService] Binding action {action.Name} as {action.ActionType}");
                    ProcessDigitalMapping(actionEntry, action.Name, processedActions);
                }
                else
                {
                    Debug.Log($"[EnhancedInputService] Binding axis {action.Name} as {action.ActionType}");
                    ProcessAxisMapping(actionEntry, action.Name, processedAxes);
                }
            }
        }

        // Collect all processed configurations into structural arrays
        targetActions.AddRange(processedActions.Values);

        foreach (var axisGroup in processedAxes.Values)
        {
            targetAxes.AddRange(axisGroup);
        }

        // Commit structural configurations instantly into Flax's live system
        Input.ActionMappings = targetActions.ToArray();
        Input.AxisMappings = targetAxes.ToArray();
    }
    
    private static void ProcessDigitalMapping(InputActionEntry actionEntry, string actionName, Dictionary<string, ActionConfig> processedActions)
    {
        foreach (var mapping in actionEntry.InputMapping)
        {
            var binding = mapping;
            var config = new ActionConfig
            {
                Name = actionName,
                Key = binding.Key,
                GamepadButton = binding.GamepadButton,
                Gamepad = InputGamepadIndex.All
            };

            processedActions[actionName] = config;
        }
    }

    private static void ProcessAxisMapping(InputActionEntry actionEntry, string actionName, Dictionary<string, List<AxisConfig>> processedAxes)
    {
        var action = actionEntry.InputAction.Instance;

        if (action.ActionType == InputActionType.Axis1D)
        {
            if (!processedAxes.TryGetValue(actionName, out var axisConfigs))
            {
                axisConfigs = new List<AxisConfig>();
                processedAxes[actionName] = axisConfigs;
            }

            foreach (var mapping in actionEntry.InputMapping)
            {
                axisConfigs.Add(CreateNativeAxisConfig(actionName, mapping));
            }
            return;
        }

        // Axis2D compilation trace
        var suffixes = new[] { "_X", "_Y" };

        for (var i = 0; i < actionEntry.InputMapping.Count; i++)
        {
            if (i >= 2) break; 

            var mappedName = $"{actionName}{suffixes[i]}";
            var binding = actionEntry.InputMapping[i];

            if (!processedAxes.TryGetValue(mappedName, out var axisConfigs))
            {
                axisConfigs = new List<AxisConfig>();
                processedAxes[mappedName] = axisConfigs;
            }

            Debug.Log($"[EnhancedInput] COMPILING Row index {i} into Virtual Axis: '{mappedName}' of type '{binding.AxisType} | Pos: {binding.KeyPositive}, Neg: {binding.KeyNegative}");
            axisConfigs.Add(CreateNativeAxisConfig(mappedName, binding));
        }
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
