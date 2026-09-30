using System;
using System.Buffers;
using System.Collections.Generic;
using FlaxAIM.Modifiers;
using FlaxAIM.State;
using FlaxAIM.Triggers;
using FlaxEngine;

namespace FlaxAIM;

public partial class InputProcessor
{
    private readonly Dictionary<Guid, ActionStateTracker> _trackers = [];
    private readonly List<Guid> _staleTrackers = [];

    // Callback dispatch order within a single frame.
    private static readonly (TriggerEvent Event, EnhancedInputActionState State)[] DispatchOrder =
    [
        (TriggerEvent.Started,   EnhancedInputActionState.Started),
        (TriggerEvent.Ongoing,   EnhancedInputActionState.Ongoing),
        (TriggerEvent.Triggered, EnhancedInputActionState.Triggered),
        (TriggerEvent.Completed, EnhancedInputActionState.Completed),
        (TriggerEvent.Canceled,  EnhancedInputActionState.Canceled),
    ];

    /// <summary>
    /// Evaluates every action for this frame and dispatches callbacks. Does nothing while disabled.
    /// </summary>
    public void Update(float deltaTime)
    {
        if (!IsEnabled) return;

        _isUpdating = true;
        try
        {
            foreach (var tracker in _trackers.Values) tracker.MoveToNextFrame();

            foreach (var compiledAction in _compiled.Actions)
            {
                ProcessAction(compiledAction, deltaTime);
            }

            FlushStaleTrackers();
        }
        finally
        {
            _isUpdating = false;
        }

        if (_rebuildPending)
        {
            _rebuildPending = false;
            RebuildMappings();
        }
    }

    public float GetPreviousFrameMagnitude(InputAction action)
    {
        return TryGetTracker(action, out var tracker) ? tracker.PreviousMagnitude : 0f;
    }

    /// <summary>
    /// Returns the action's current trigger state: <see cref="EnhancedInputActionState.None"/>,
    /// <see cref="EnhancedInputActionState.Ongoing"/> or <see cref="EnhancedInputActionState.Triggered"/>.
    /// Use <see cref="GetActionEvents"/> for the transitions (Started, Completed, Canceled) raised this frame.
    /// </summary>
    public EnhancedInputActionState GetActionState(InputAction action)
    {
        return TryGetTracker(action, out var tracker) ? tracker.TriggerState : EnhancedInputActionState.None;
    }

    /// <summary>
    /// Returns the events the action raised this frame.
    /// </summary>
    public TriggerEvent GetActionEvents(InputAction action)
    {
        return TryGetTracker(action, out var tracker) ? tracker.Events : TriggerEvent.None;
    }

    /// <summary>
    /// Queries the live, fully modified multi-dimensional input data snapshot matching a registered BindAction Tag.
    /// </summary>
    /// <param name="bindingTag">The unique identifying Tag provided during the initial BindAction registration.</param>
    /// <returns>A structured container carrying 1D, 2D, 3D, and digital data values.</returns>
    public ProcessedInputActionValue GetActionValue(Tag bindingTag)
    {
        if (bindingTag == Tag.Default) return default;
        return _tagActions.TryGetValue(bindingTag, out var actionId) && _trackers.TryGetValue(actionId, out var tracker)
            ? tracker.CurrentValue
            : default;
    }

    /// <summary>
    /// Queries the live, fully modified multi-dimensional input data snapshot for a specific action asset.
    /// </summary>
    /// <param name="inputAction">The input action for this value.</param>
    /// <returns>A structured container carrying 1D, 2D, 3D, and digital data values.</returns>
    public ProcessedInputActionValue GetActionValue(InputAction inputAction)
    {
        return TryGetTracker(inputAction, out var tracker) ? tracker.CurrentValue : default;
    }

    private bool TryGetTracker(InputAction action, out ActionStateTracker tracker)
    {
        if (action == null)
        {
            tracker = null;
            return false;
        }
        return _trackers.TryGetValue(ActionIdentity.Of(action), out tracker);
    }

    private void ProcessAction(CompiledAction compiledAction, float deltaTime)
    {
        var action = compiledAction.Action;

        if (!_trackers.TryGetValue(compiledAction.Id, out var tracker))
        {
            tracker = new ActionStateTracker();
            _trackers[compiledAction.Id] = tracker;
        }
        tracker.Visited = true;

        var combined = Float3.Zero;
        var state = EnhancedInputActionState.None;

        foreach (var binding in compiledAction.Bindings)
        {
            var value = ReadBinding(binding, action.ActionType);
            combined = new Float3(HighestAbs(combined.X, value.X), HighestAbs(combined.Y, value.Y), HighestAbs(combined.Z, value.Z));

            var bindingState = EvaluateTriggers(binding.Triggers, action, deltaTime, value.Length);
            if (bindingState > state) state = bindingState;
        }

        combined = ApplyModifiers(compiledAction.Modifiers, combined, action.ActionType);

        var magnitude = action.ActionType switch
        {
            InputActionType.Axis2D => new Float2(combined.X, combined.Y).Length,
            InputActionType.Axis3D => combined.Length,
            _                      => Math.Abs(combined.X),
        };

        // Action-level triggers gate the bindings' result: both have to pass.
        if (compiledAction.Triggers.Length > 0)
        {
            var actionState = EvaluateTriggers(compiledAction.Triggers, action, deltaTime, magnitude);
            if (actionState < state) state = actionState;
        }

        tracker.CurrentMagnitude = magnitude;
        tracker.CurrentValue = new ProcessedInputActionValue
        {
            Digital = magnitude > 0f,
            Axis1D = combined.X,
            Axis2D = new Float2(combined.X, combined.Y),
            Axis3D = combined,
        };

        tracker.AdvanceStateMachine(state);
        DispatchCallbacks(compiledAction.Id, tracker);
    }

    /// <summary>
    /// Reads one binding's raw inputs, places each in its target component and applies the binding's modifiers.
    /// </summary>
    private static Float3 ReadBinding(CompiledBinding binding, InputActionType actionType)
    {
        var backend = VirtualInputRegistry.Backend;
        var value = Float3.Zero;

        foreach (var input in binding.Inputs)
        {
            var raw = input.IsAxis
                ? backend.GetAxis(input.VirtualName)
                : backend.GetAction(input.VirtualName) ? 1f : 0f;
            value[input.Component] = raw;
        }

        return ApplyModifiers(binding.Modifiers, value, actionType);
    }

    private static Float3 ApplyModifiers(InputModifier[] modifiers, Float3 value, InputActionType actionType)
    {
        foreach (var modifier in modifiers)
        {
            switch (actionType)
            {
                case InputActionType.Axis2D:
                    var modified = modifier.Modify(new Float2(value.X, value.Y));
                    value = new Float3(modified.X, modified.Y, 0f);
                    break;
                case InputActionType.Axis3D:
                    value = modifier.Modify(value);
                    break;
                default:
                    value = new Float3(modifier.Modify(value.X), 0f, 0f);
                    break;
            }
        }

        return value;
    }

    /// <summary>
    /// Combines a set of triggers. Any explicit trigger triggering (or, with no explicit triggers, the input being
    /// actuated) triggers the set, provided every implicit trigger is also triggered.
    /// </summary>
    private EnhancedInputActionState EvaluateTriggers(InputTrigger[] triggers, InputAction action, float deltaTime, float magnitude)
    {
        var hasExplicit = false;
        var explicitTriggered = false;
        var implicitsTriggered = true;
        var anyOngoing = false;

        foreach (var trigger in triggers)
        {
            var result = trigger.Evaluate(this, action, deltaTime, magnitude);
            if (result == EnhancedInputActionState.Ongoing) anyOngoing = true;

            if (trigger.TriggerType == TriggerType.Implicit)
            {
                implicitsTriggered &= result == EnhancedInputActionState.Triggered;
            }
            else
            {
                hasExplicit = true;
                explicitTriggered |= result == EnhancedInputActionState.Triggered;
            }
        }

        var explicitPassed = hasExplicit ? explicitTriggered : magnitude >= DefaultActuationThreshold;

        if (explicitPassed && implicitsTriggered) return EnhancedInputActionState.Triggered;
        return anyOngoing ? EnhancedInputActionState.Ongoing : EnhancedInputActionState.None;
    }

    private static float HighestAbs(float a, float b) => Math.Abs(b) > Math.Abs(a) ? b : a;

    /// <summary>
    /// Ends actions whose context was removed: they get one final None evaluation (raising Completed or Canceled)
    /// and are dropped once idle.
    /// </summary>
    private void FlushStaleTrackers()
    {
        _staleTrackers.Clear();
        foreach (var (actionId, tracker) in _trackers)
        {
            if (!tracker.Visited) _staleTrackers.Add(actionId);
        }

        foreach (var actionId in _staleTrackers)
        {
            var tracker = _trackers[actionId];
            if (tracker.TriggerState == EnhancedInputActionState.None)
            {
                _trackers.Remove(actionId);
                continue;
            }

            tracker.CurrentValue = default;
            tracker.AdvanceStateMachine(EnhancedInputActionState.None);
            DispatchCallbacks(actionId, tracker);
        }
    }

    /// <summary>
    /// Dispatches this frame's events to the callbacks bound to them. Handlers are snapshotted first, so
    /// callbacks may bind, unbind or change contexts safely. Handlers owned by destroyed objects are removed.
    /// </summary>
    private void DispatchCallbacks(Guid actionId, ActionStateTracker tracker)
    {
        var events = tracker.Events;
        if (events == TriggerEvent.None) return;

        foreach (var (triggerEvent, state) in DispatchOrder)
        {
            if ((events & triggerEvent) == 0) continue;

            var key = new ActionBindingKey(actionId, state);
            if (!_boundActions.TryGetValue(key, out var handlerList) || handlerList.Count == 0) continue;

            var count = handlerList.Count;
            var snapshot = ArrayPool<RegisteredCallbackHandler>.Shared.Rent(count);
            var hasDeadOwners = false;
            try
            {
                handlerList.CopyTo(snapshot);
                for (var i = 0; i < count; i++)
                {
                    if (!snapshot[i].IsOwnerAlive)
                    {
                        hasDeadOwners = true;
                        continue;
                    }
                    snapshot[i].Invoke(tracker.CurrentValue);
                }
            }
            finally
            {
                ArrayPool<RegisteredCallbackHandler>.Shared.Return(snapshot, clearArray: true);
            }

            if (hasDeadOwners) RemoveHandlers(key, h => !h.IsOwnerAlive);
        }
    }
}
