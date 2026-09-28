using System;
using FlaxAIM.Modifiers;
using FlaxAIM.State;
using FlaxAIM.Triggers;
using FlaxEngine;

namespace FlaxAIM;

/// <summary>
/// Adds FlaxAIM input to an actor: owns an <see cref="InputProcessor"/>, enables it with the script and updates it
/// every frame. The methods here forward to <see cref="Processor"/>.
/// </summary>
// ReSharper disable once ClassNeverInstantiated.Global
public class InputManager : Script
{
    [Tooltip("Which gamepad this manager reads. Use a specific gamepad per player for local multiplayer.")]
    public InputGamepadIndex Gamepad = InputGamepadIndex.All;

    [Tooltip("Whether this manager reads keyboard and mouse bindings. Disable for players that only use a gamepad.")]
    public bool UseKeyboardAndMouse = true;

    [Tooltip("Magnitude a binding with no triggers must reach to trigger its action.")]
    public float DefaultActuationThreshold = 0.1f;

    /// <summary>
    /// The processor this script drives.
    /// </summary>
    public InputProcessor Processor { get; } = new();

    public override void OnEnable()
    {
        SyncSettings();
        Processor.Enable();
    }

    public override void OnDisable() => Processor.Disable();

    public override void OnDestroy() => Processor.Disable();

    public override void OnUpdate()
    {
        // Picks up edits made in the editor during play; the processor only recompiles when a device setting changes
        SyncSettings();
        Processor.Update(Time.DeltaTime);
    }

    private void SyncSettings()
    {
        Processor.Gamepad = Gamepad;
        Processor.UseKeyboardAndMouse = UseKeyboardAndMouse;
        Processor.DefaultActuationThreshold = DefaultActuationThreshold;
    }

    // ---- Contexts ---------------------------------------------------------------------------------------------------

    /// <inheritdoc cref="InputProcessor.AddInputContext(InputMappingContext)"/>
    public void AddInputContext(InputMappingContext context) => Processor.AddInputContext(context);

    /// <inheritdoc cref="InputProcessor.AddInputContext(InputMappingContext, int)"/>
    public void AddInputContext(InputMappingContext context, int priority) => Processor.AddInputContext(context, priority);

    /// <inheritdoc cref="InputProcessor.AddInputContext(InputMappingContext[])"/>
    public void AddInputContext(InputMappingContext[] contexts) => Processor.AddInputContext(contexts);

    /// <inheritdoc cref="InputProcessor.RemoveInputContext(InputMappingContext)"/>
    public void RemoveInputContext(InputMappingContext context) => Processor.RemoveInputContext(context);

    /// <inheritdoc cref="InputProcessor.RemoveInputContext(InputMappingContext[])"/>
    public void RemoveInputContext(InputMappingContext[] contexts) => Processor.RemoveInputContext(contexts);

    /// <inheritdoc cref="InputProcessor.ClearInputContexts"/>
    public void ClearInputContexts() => Processor.ClearInputContexts();

    /// <inheritdoc cref="InputProcessor.HasInputContext"/>
    public bool HasInputContext(InputMappingContext context) => Processor.HasInputContext(context);

    /// <inheritdoc cref="InputProcessor.RebuildMappings"/>
    public void RebuildMappings()
    {
        SyncSettings();
        Processor.RebuildMappings();
    }

    // ---- Queries ----------------------------------------------------------------------------------------------------

    /// <inheritdoc cref="InputProcessor.GetActionState"/>
    public EnhancedInputActionState GetActionState(InputAction action) => Processor.GetActionState(action);

    /// <inheritdoc cref="InputProcessor.GetActionEvents"/>
    public TriggerEvent GetActionEvents(InputAction action) => Processor.GetActionEvents(action);

    /// <inheritdoc cref="InputProcessor.GetActionValue(InputAction)"/>
    public ProcessedInputActionValue GetActionValue(InputAction action) => Processor.GetActionValue(action);

    /// <inheritdoc cref="InputProcessor.GetActionValue(Tag)"/>
    public ProcessedInputActionValue GetActionValue(Tag bindingTag) => Processor.GetActionValue(bindingTag);

    /// <inheritdoc cref="InputProcessor.GetPreviousFrameMagnitude"/>
    public float GetPreviousFrameMagnitude(InputAction action) => Processor.GetPreviousFrameMagnitude(action);

    // ---- Bindings ---------------------------------------------------------------------------------------------------

    /// <inheritdoc cref="InputProcessor.BindAction(InputAction, EnhancedInputActionState, Action{Tag}, Tag)"/>
    public InputBindingHandle BindAction(InputAction action, EnhancedInputActionState targetState, Action<Tag> callback, Tag identifyingTag)
        => Processor.BindAction(action, targetState, callback, identifyingTag);

    /// <inheritdoc cref="InputProcessor.BindAction(InputAction, EnhancedInputActionState, Action{ProcessedInputActionValue})"/>
    public InputBindingHandle BindAction(InputAction action, EnhancedInputActionState targetState, Action<ProcessedInputActionValue> callback)
        => Processor.BindAction(action, targetState, callback);

    /// <inheritdoc cref="InputProcessor.BindAction(InputAction, EnhancedInputActionState, Action)"/>
    public InputBindingHandle BindAction(InputAction action, EnhancedInputActionState targetState, Action callback)
        => Processor.BindAction(action, targetState, callback);

    /// <inheritdoc cref="InputProcessor.BindAction{T}(InputAction, EnhancedInputActionState, Action{T})"/>
    public InputBindingHandle BindAction<T>(InputAction action, EnhancedInputActionState targetState, Action<T> callback)
        => Processor.BindAction(action, targetState, callback);

    /// <inheritdoc cref="InputProcessor.BindAction(InputConfig, EnhancedInputActionState, Action{Tag})"/>
    public InputBindingHandle BindAction(InputConfig config, EnhancedInputActionState targetState, Action<Tag> callback)
        => Processor.BindAction(config, targetState, callback);

    /// <inheritdoc cref="InputProcessor.BindActions"/>
    public InputBindingHandle BindActions(InputConfigSet configSet, EnhancedInputActionState targetState, Action<Tag> callback)
        => Processor.BindActions(configSet, targetState, callback);

    /// <inheritdoc cref="InputProcessor.UnbindAction(InputAction, EnhancedInputActionState, Delegate)"/>
    public void UnbindAction(InputAction action, EnhancedInputActionState targetState, Delegate callback)
        => Processor.UnbindAction(action, targetState, callback);

    /// <inheritdoc cref="InputProcessor.UnbindAction(InputAction, EnhancedInputActionState, Action{Tag}, Tag)"/>
    public void UnbindAction(InputAction action, EnhancedInputActionState targetState, Action<Tag> callback, Tag identifyingTag)
        => Processor.UnbindAction(action, targetState, callback, identifyingTag);

    /// <inheritdoc cref="InputProcessor.UnbindAll"/>
    public void UnbindAll(object owner) => Processor.UnbindAll(owner);
}
