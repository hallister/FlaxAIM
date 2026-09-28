using System;
using System.Collections.Generic;
using FlaxAIM.State;
using FlaxEngine;

namespace FlaxAIM;

public partial class InputManager
{
    private readonly Dictionary<ActionBindingKey, List<RegisteredCallbackHandler>> _boundActions = new();

    /// <summary>
    /// Registers an action callback routed strictly by its hardware execution state enum, 
    /// with an explicit context tag returned as a parameter when fired.
    /// </summary>
    public void BindAction(InputAction action, EnhancedInputActionState targetState, Action<Tag> callback, Tag identifyingTag)
    {
        if (action == null || callback == null) return;
        
        Debug.Log($"[InputManager] Binding {action.Name} in state {targetState} to tag {identifyingTag}");

        var key = new ActionBindingKey(action, targetState);

        if (!_boundActions.TryGetValue(key, out var handlerList))
        {
            handlerList = new List<RegisteredCallbackHandler>();
            _boundActions[key] = handlerList;
        }

        if (!handlerList.Exists(h => h.ActionDelegate == callback && h.FilterTag == identifyingTag))
        {
            handlerList.Add(new RegisteredCallbackHandler(identifyingTag, callback));
        }
    }
    
    /// <summary>
    /// Safely unbinds a specific callback handler sequence.
    /// </summary>
    public void UnbindAction(InputAction action, EnhancedInputActionState targetState, Action<Tag> callback)
    {
        if (action == null || callback == null) return;

        var key = new ActionBindingKey(action, targetState);

        if (_boundActions.TryGetValue(key, out var handlerList))
        {
            handlerList.RemoveAll(h => h.ActionDelegate == callback);
        }
    }
}