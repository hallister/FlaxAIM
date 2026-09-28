using System;
using System.Collections.Generic;
using FlaxAIM.Modifiers;
using FlaxAIM.State;
using FlaxEngine;

namespace FlaxAIM;

public partial class InputProcessor
{
    private readonly Dictionary<ActionBindingKey, List<RegisteredCallbackHandler>> _boundActions = new();

    // Resolves GetActionValue(Tag) to the action the tag was bound to.
    private readonly Dictionary<Tag, Guid> _tagActions = new();

    private long _nextHandlerId;

    /// <summary>
    /// Registers an action callback routed strictly by its hardware execution state enum,
    /// with an explicit context tag returned as a parameter when fired.
    /// </summary>
    public InputBindingHandle BindAction(InputAction action, EnhancedInputActionState targetState, Action<Tag> callback, Tag identifyingTag)
    {
        if (action == null || callback == null) return InputBindingHandle.Empty;

        var handle = AddHandler(action, targetState, identifyingTag, callback, _ => callback(identifyingTag));

        if (identifyingTag != Tag.Default)
        {
            var actionId = ActionIdentity.Of(action);
            if (_tagActions.TryGetValue(identifyingTag, out var existingActionId) && existingActionId != actionId)
                InputLog.Warning($"Tag {InputLog.TagName(identifyingTag)} was already bound to another action. GetActionValue now returns {action.Name} for it.");

            _tagActions[identifyingTag] = actionId;
        }

        return handle;
    }

    /// <summary>
    /// Binds a callback that receives the action's full value.
    /// </summary>
    public InputBindingHandle BindAction(InputAction action, EnhancedInputActionState targetState, Action<ProcessedInputActionValue> callback)
    {
        if (action == null || callback == null) return InputBindingHandle.Empty;
        return AddHandler(action, targetState, Tag.Default, callback, callback);
    }

    /// <summary>
    /// Binds a callback with no parameters.
    /// </summary>
    public InputBindingHandle BindAction(InputAction action, EnhancedInputActionState targetState, Action callback)
    {
        if (action == null || callback == null) return InputBindingHandle.Empty;
        return AddHandler(action, targetState, Tag.Default, callback, _ => callback());
    }

    /// <summary>
    /// Binds a callback that receives the action's value as <typeparamref name="T"/>:
    /// <see cref="bool"/> (Digital), <see cref="float"/> (Axis1D), <see cref="Float2"/> (Axis2D) or <see cref="Float3"/> (Axis3D).
    /// </summary>
    public InputBindingHandle BindAction<T>(InputAction action, EnhancedInputActionState targetState, Action<T> callback)
    {
        if (action == null || callback == null) return InputBindingHandle.Empty;

        Action<ProcessedInputActionValue> invoke = callback switch
        {
            Action<bool> c   => v => c(v.Digital),
            Action<float> c  => v => c(v.Axis1D),
            Action<Float2> c => v => c(v.Axis2D),
            Action<Float3> c => v => c(v.Axis3D),
            Action<ProcessedInputActionValue> c => c,
            _ => throw new ArgumentException($"BindAction<{typeof(T).Name}> is not supported. Use bool, float, Float2, Float3 or ProcessedInputActionValue."),
        };

        return AddHandler(action, targetState, Tag.Default, callback, invoke);
    }

    /// <summary>
    /// Binds the action and tag from an <see cref="InputConfig"/>.
    /// </summary>
    public InputBindingHandle BindAction(InputConfig config, EnhancedInputActionState targetState, Action<Tag> callback)
    {
        if (config == null) return InputBindingHandle.Empty;
        return BindAction(config.InputAction.Instance, targetState, callback, config.InputTag);
    }

    /// <summary>
    /// Binds every action in an <see cref="InputConfigSet"/> to one callback, which receives the matching tag.
    /// </summary>
    public InputBindingHandle BindActions(InputConfigSet configSet, EnhancedInputActionState targetState, Action<Tag> callback)
    {
        var handle = new InputBindingHandle(this);
        if (configSet?.Configs == null || callback == null) return handle;

        foreach (var config in configSet.Configs)
        {
            handle.AddRange(BindAction(config, targetState, callback));
        }
        return handle;
    }

    /// <summary>
    /// Unbinds a callback from an action state, for every tag it was bound with.
    /// </summary>
    public void UnbindAction(InputAction action, EnhancedInputActionState targetState, Delegate callback)
    {
        if (action == null || callback == null) return;
        RemoveHandlers(new ActionBindingKey(ActionIdentity.Of(action), targetState), h => Equals(h.Callback, callback));
    }

    /// <summary>
    /// Unbinds a callback from an action state for one tag only.
    /// </summary>
    public void UnbindAction(InputAction action, EnhancedInputActionState targetState, Action<Tag> callback, Tag identifyingTag)
    {
        if (action == null || callback == null) return;
        RemoveHandlers(new ActionBindingKey(ActionIdentity.Of(action), targetState), h => Equals(h.Callback, callback) && h.FilterTag == identifyingTag);
    }

    /// <summary>
    /// Removes every binding whose callback belongs to <paramref name="owner"/> (typically a script calling
    /// <c>UnbindAll(this)</c> in its OnDestroy). Callbacks owned by destroyed Flax objects are also removed
    /// automatically the next time they would fire.
    /// </summary>
    public void UnbindAll(object owner)
    {
        if (owner == null) return;

        foreach (var key in new List<ActionBindingKey>(_boundActions.Keys))
            RemoveHandlers(key, h => ReferenceEquals(h.Callback.Target, owner));
    }

    internal void RemoveHandler(ActionBindingKey key, long id)
    {
        RemoveHandlers(key, h => h.Id == id);
    }

    private InputBindingHandle AddHandler(InputAction action, EnhancedInputActionState targetState, Tag tag, Delegate callback, Action<ProcessedInputActionValue> invoke)
    {
        var key = new ActionBindingKey(ActionIdentity.Of(action), targetState);
        var handle = new InputBindingHandle(this);

        if (!_boundActions.TryGetValue(key, out var handlerList))
        {
            handlerList = new List<RegisteredCallbackHandler>();
            _boundActions[key] = handlerList;
        }

        var existing = handlerList.FindIndex(h => Equals(h.Callback, callback) && h.FilterTag == tag);
        if (existing >= 0)
        {
            handle.Add(key, handlerList[existing].Id);
            return handle;
        }

        var id = ++_nextHandlerId;
        handlerList.Add(new RegisteredCallbackHandler(id, tag, callback, invoke));
        handle.Add(key, id);
        return handle;
    }

    private void RemoveHandlers(ActionBindingKey key, Predicate<RegisteredCallbackHandler> match)
    {
        if (!_boundActions.TryGetValue(key, out var handlerList)) return;

        List<Tag> removedTags = null;
        foreach (var handler in handlerList)
        {
            if (match(handler) && handler.FilterTag != Tag.Default) (removedTags ??= []).Add(handler.FilterTag);
        }

        if (handlerList.RemoveAll(match) == 0) return;
        if (handlerList.Count == 0) _boundActions.Remove(key);

        if (removedTags == null) return;
        foreach (var tag in removedTags)
        {
            if (!IsTagStillBound(tag, key.ActionId)) _tagActions.Remove(tag);
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
