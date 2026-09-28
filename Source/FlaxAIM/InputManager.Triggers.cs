using System;
using System.Collections.Generic;
using System.IO;
using FlaxAIM.Modifiers;
using FlaxAIM.State;
using FlaxAIM.Triggers;
using FlaxEngine;

namespace FlaxAIM;

public partial class InputManager
{
    // Cache map: Stores the live instantiated triggers for each InputActionEntry
    private readonly Dictionary<string, List<InputTrigger>> _liveTriggerInstances = [];
    private readonly Dictionary<InputAction, ActionStateTracker> _trackers = [];
    
    // Stores the current frame's fully modified multidimensional values indexed by their unique binding tag
    private readonly Dictionary<Tag, ProcessedInputActionValue> _tagValues = new();
    
    public override void OnUpdate()
    {
        // 1. Reset frame buffers safely through our custom trackers
        foreach (var tracker in _trackers.Values) tracker.MoveToNextFrame();
        
        _tagValues.Clear();

        var deltaTime = Time.DeltaTime;

        // 2. Iterate layers
        for (var i = _contextStack.Count - 1; i >= 0; i--)
        {
            var context = _contextStack[i];
            if (context == null) continue;

            foreach (var actionEntry in context.Mappings)
            {
                var action = actionEntry.InputAction.Instance;
                if (action == null || string.IsNullOrEmpty(action.Name)) continue;

                // Fetch or instantiate state trackers
                if (!_trackers.TryGetValue(action, out var tracker))
                {
                    tracker = new ActionStateTracker();
                    _trackers[action] = tracker;
                }

                // Skip if a higher context layer already captured updates this frame
                if (tracker.CurrentMagnitude > 0f) continue; 

                // 3. Process calculations and step through execution states
                tracker.CurrentMagnitude = GatherRawInputMagnitude(actionEntry, out var value);
                tracker.CurrentValue = value;

                var evaluation = EvaluateTriggersForAction(actionEntry, action, deltaTime, tracker.CurrentMagnitude);
                tracker.AdvanceStateMachine(evaluation);

                // 4. Fire callbacks
                if (tracker.CurrentState != EnhancedInputActionState.None)
                {
                    DispatchCallbacks(action, tracker.CurrentState);
                }
            }
        }
    }
    
    public float GetPreviousFrameMagnitude(InputAction action)
    {
        if (action == null) return 0f;
        // Returns the cached field inside the tracker or 0 if it doesn't exist yet
        return _trackers.TryGetValue(action, out var tracker) ? tracker.PreviousMagnitude : 0f;
    }
    
    public EnhancedInputActionState GetActionState(InputAction action)
    {
        if (action == null) return EnhancedInputActionState.None;
        // Safely look up the live state inside the tracker class wrapper
        return _trackers.TryGetValue(action, out var tracker) ? tracker.CurrentState : EnhancedInputActionState.None;
    }
    
    /// <summary>
    /// Queries the live, fully modified multi-dimensional input data snapshot matching a registered BindAction Tag.
    /// </summary>
    /// <param name="bindingTag">The unique identifying Tag provided during the initial BindAction registration.</param>
    /// <returns>A structured container carrying 1D, 2D, 3D, and digital data values.</returns>
    public ProcessedInputActionValue GetActionValue(Tag bindingTag)
    {
        if (bindingTag == new Tag()) return default;
        return _tagValues.GetValueOrDefault(bindingTag);
    }
    
    /// <summary>
    /// Queries the live, fully modified multi-dimensional input data snapshot for a specific action asset.
    /// </summary>
    /// <param name="inputAction">The input action for this value.</param>
    /// <returns>A structured container carrying 1D, 2D, 3D, and digital data values.</returns>
    public ProcessedInputActionValue GetActionValue(InputAction inputAction)
    {
        return _trackers.TryGetValue(inputAction, out var tracker) ? tracker.CurrentValue : default;
    }
    
    /// <summary>
    /// Spawns and caches live instances of our soft type references when contexts change.
    /// </summary>
    private List<InputTrigger> GetOrCreateLiveTriggers(InputActionEntry actionEntry, int mappingIndex, InputMappingEntry mapping)
    {
        // Using a composite hash or a unique identifier to map this entry instance
        // For simplicity, we can use the InputAction Asset GUID or generate an internal reference key
        var lookupKey = $"{actionEntry.InputAction.Instance.ID}_{mappingIndex}";

        if (_liveTriggerInstances.TryGetValue(lookupKey, out var liveList))
        {
            return liveList;
        }

        liveList = [];

        if (mapping.Triggers == null)
        {
            Debug.LogError($"[InputManager.Triggers] InputAsset {Path.GetFileName(mapping.ToString())} has invalid triggers. Does your InputMappingEntry contain a null value?");
            return [];
        }

        foreach (var iInputTrigger in mapping.Triggers) // Update loop mapping index bounds safely
        {
            if (iInputTrigger is InputTrigger inputTrigger)
            {
                liveList.Add(inputTrigger);
            }
            else
            {
                Debug.LogWarning($"Failed to add live trigger {iInputTrigger} as it's not an InputTrigger.");
            }
        }

        _liveTriggerInstances[lookupKey] = liveList;
        return liveList;
    }

    private static float GatherRawInputMagnitude(InputActionEntry actionEntry, out ProcessedInputActionValue finalValue)
    {
        finalValue = default;
        var action = actionEntry.InputAction.Instance;
        
        var channelCount = action.ActionType switch
        {
            InputActionType.Digital  => 0,
            InputActionType.Axis1D   => 1,
            InputActionType.Axis2D   => 2,
            InputActionType.Axis3D   => 3,
            _                        => 0
        };

        var rawVector = Float3.Zero;
        var isAnyBindingActuated = false;
        var suffixes = new[] { "_X", "_Y", "_Z" };

        for (var i = 0; i < actionEntry.InputMapping.Count; i++)
        {
            var mapping = actionEntry.InputMapping[i];
            var binding = mapping;

            var mappingPressed = false;
            var currentBindingVector = Float3.Zero;

            if (channelCount == 0)
            {
                mappingPressed = Input.GetAction(action.Name);
                currentBindingVector.X = mappingPressed ? 1.0f : 0.0f;
            }
            else if (channelCount == 1)
            {
                currentBindingVector.X = Input.GetAxis(action.Name);
                mappingPressed = Math.Abs(currentBindingVector.X) > 0.001f;
            }
            else
            {
                if (i < channelCount)
                {
                    // Read raw axis data directly from Flax Engine's static registry
                    var targetVirtualAxisName = $"{action.Name}{suffixes[i]}";
                    var val = Input.GetAxis(targetVirtualAxisName);
                    
                    if (i == 0) currentBindingVector.X = val;
                    if (i == 1) currentBindingVector.Y = val;

                    mappingPressed = Math.Abs(val) > 0.001f;
                    
                    if (mappingPressed)
                    {
                        // Debug.Log($"   -> [Row {i}] Virtual Axis '{targetVirtualAxisName}' caught active value: {val} (Keys: +{binding.KeyPositive}/-{binding.KeyNegative})");
                    }
                }
            }

            if (mappingPressed)
            {
                isAnyBindingActuated = true;

                if (binding.Modifiers != null && binding.Modifiers.Count > 0)
                {
                    foreach (var modifier in binding.Modifiers)
                    {
                        if (modifier == null) continue;

                        if (action.ActionType == InputActionType.Axis1D)
                            currentBindingVector.X = modifier.Modify(currentBindingVector.X);
                        else if (action.ActionType == InputActionType.Axis2D)
                        {
                            var prevVal = new Float2(currentBindingVector.X, currentBindingVector.Y);
                            var mod2D = modifier.Modify(prevVal);
                            
                            // Debug.Log($"      -> [Row {i}] Modifier '{modifier.GetType().Name}' altered value from {prevVal} to {mod2D}");
                            currentBindingVector.X = mod2D.X;
                            currentBindingVector.Y = mod2D.Y;
                        }
                    }
                }

                rawVector.X += currentBindingVector.X;
                rawVector.Y += currentBindingVector.Y;
                rawVector.Z += currentBindingVector.Z;
            }
        }

        finalValue.Digital = isAnyBindingActuated;
        finalValue.Axis1D  = rawVector.X;
        finalValue.Axis2D  = new Float2(rawVector.X, rawVector.Y);
        finalValue.Axis3D  = rawVector;

        if (channelCount >= 2 && isAnyBindingActuated)
        {
            // Debug.Log($"   => [Final Output] Action: {action.Name} | Absolute Axis2D Result Vector: {finalValue.Axis2D}");
        }

        if (!isAnyBindingActuated) return 0f;

        return action.ActionType == InputActionType.Axis2D 
            ? new Float2(rawVector.X, rawVector.Y).Length 
            : Math.Abs(rawVector.X);
    }

    
    private EnhancedInputActionState EvaluateTriggersForAction(InputActionEntry actionEntry, InputAction action, float deltaTime, float rawMagnitude)
    {
        var highestState = EnhancedInputActionState.None;

        for (var i = 0; i < actionEntry.InputMapping.Count; i++)
        {
            var mapping = actionEntry.InputMapping[i];
            
            var liveTriggers = GetOrCreateLiveTriggers(actionEntry, i, mapping);

            if (liveTriggers.Count == 0)
            {
                // Simple baseline: If pressed, evaluate as Triggered.
                if (rawMagnitude > 0.1f && highestState < EnhancedInputActionState.Triggered)
                {
                    highestState = EnhancedInputActionState.Triggered;
                }
                continue;
            }

            foreach (var trigger in liveTriggers)
            {
                if (trigger == null) continue;

                var triggerState = trigger.UpdateState(this, action, deltaTime, rawMagnitude);
                if (triggerState > highestState)
                {
                    highestState = triggerState;
                }
            }
        }

        return highestState;
    }

    /// <summary>
    /// Dispatches the active evaluation frames out to listeners matching target state enums.
    /// </summary>
    private void DispatchCallbacks(InputAction action, EnhancedInputActionState currentFrameState)
    {
        var key = new ActionBindingKey(action, currentFrameState);

        if (_boundActions.TryGetValue(key, out var handlerList))
        {
            // Retrieve the final fully modified multi-dimensional value snapshot from this frame
            var liveValue = GetActionValue(action);

            foreach (var handler in handlerList)
            {
                // Cache the processed input value using the exact tag provided during the initial BindAction!
                if (handler.FilterTag != Tag.Default)
                {
                    _tagValues[handler.FilterTag] = liveValue;
                }

                // Fire the ability system delegate callback handler
                handler.ActionDelegate?.Invoke(handler.FilterTag);
            }
        }
    }
}
