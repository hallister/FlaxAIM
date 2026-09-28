using System;
using System.Collections.Generic;
using FlaxAIM.State;
using FlaxEngine;

namespace FlaxAIM;

public partial class InputManager
{
    private readonly Dictionary<ActionBindingKey, List<RegisteredCallbackHandler>> _boundActions = new();

    // Resolves GetActionValue(Tag) to the action the tag was bound to.
    private readonly Dictionary<Tag, Guid> _tagActions = new();

    /// <summary>
    /// Registers an action callback routed strictly by its hardware execution state enum,
    /// with an explicit context tag returned as a parameter when fired.
    /// </summary>
    public void BindAction(InputAction action, EnhancedInputActionState targetState, Action<Tag> callback, Tag identifyingTag)
    {
        if (action == null || callback == null) return;

        Debug.Log($"[InputManager] Binding {action.Name} in state {targetState} to tag {identifyingTag}");

        var key = new ActionBindingKey(action.ID, targetState);

        if (!_boundActions.TryGetValue(key, out var handlerList))
        {
            handlerList = new List<RegisteredCallbackHandler>();
            _boundActions[key] = handlerList;
        }

        if (!handlerList.Exists(h => h.ActionDelegate == callback && h.FilterTag == identifyingTag))
        {
            handlerList.Add(new RegisteredCallbackHandler(identifyingTag, callback));
        }

        if (identifyingTag != Tag.Default)
        {
            if (_tagActions.TryGetValue(identifyingTag, out var existingActionId) && existingActionId != action.ID)
                Debug.LogWarning($"[InputManager] Tag {identifyingTag} was already bound to another action. GetActionValue({identifyingTag}) now returns {action.Name}.");

            _tagActions[identifyingTag] = action.ID;
        }
    }

    /// <summary>
    /// Unbinds a callback from an action state, for every tag it was bound with.
    /// </summary>
    public void UnbindAction(InputAction action, EnhancedInputActionState targetState, Action<Tag> callback)
    {
        UnbindAction(action, targetState, h => h.ActionDelegate == callback, callback);
    }

    /// <summary>
    /// Unbinds a callback from an action state for one tag only.
    /// </summary>
    public void UnbindAction(InputAction action, EnhancedInputActionState targetState, Action<Tag> callback, Tag identifyingTag)
    {
        UnbindAction(action, targetState, h => h.ActionDelegate == callback && h.FilterTag == identifyingTag, callback);
    }

    private void UnbindAction(InputAction action, EnhancedInputActionState targetState, Predicate<RegisteredCallbackHandler> match, Action<Tag> callback)
    {
        if (action == null || callback == null) return;

        if (!_boundActions.TryGetValue(new ActionBindingKey(action.ID, targetState), out var handlerList)) return;

        var removedTags = new List<Tag>();
        foreach (var handler in handlerList)
        {
            if (match(handler)) removedTags.Add(handler.FilterTag);
        }
        if (removedTags.Count == 0) return;

        handlerList.RemoveAll(match);

        foreach (var tag in removedTags)
        {
            if (!IsTagStillBound(tag, action.ID)) _tagActions.Remove(tag);
        }
    }

    private bool IsTagStillBound(Tag tag, Guid actionId)
    {
        if (!_tagActions.TryGetValue(tag, out var mappedActionId) || mappedActionId != actionId) return true;

        foreach (var (key, handlers) in _boundActions)
        {
            if (key.ActionId == actionId && handlers.Exists(h => h.FilterTag == tag)) return true;
        }
        return false;
    }
}
