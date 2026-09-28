using System;
using FlaxAIM.State;
using FlaxEngine;

namespace FlaxAIM.Examples;

/// <summary>
/// Moves the actor it's on with FlaxAIM. Shows typed callbacks (Move), Pressed (Jump), polling (Sprint),
/// a chord (Slide = Sprint + C/B), a hold (Spin = hold E/Y) and a higher-priority context that consumes
/// the movement keys while paused (Pause = Esc/Start).
/// </summary>
public class DemoPlayer : Script
{
    [Header("Input")]
    public JsonAssetReference<InputMappingContext> GameplayContext;
    public JsonAssetReference<InputMappingContext> MenuContext;
    public JsonAssetReference<InputAction> MoveAction;
    public JsonAssetReference<InputAction> JumpAction;
    public JsonAssetReference<InputAction> SprintAction;
    public JsonAssetReference<InputAction> SlideAction;
    public JsonAssetReference<InputAction> SpinAction;
    public JsonAssetReference<InputAction> PauseAction;
    public JsonAssetReference<InputAction> MenuConfirmAction;

    [Header("Movement")]
    public float WalkSpeed = 400f;
    public float SprintMultiplier = 2f;
    public float JumpSpeed = 700f;
    public float Gravity = 2000f;
    public float SlideSpeed = 1400f;
    public float SlideDuration = 0.3f;
    public float SpinSpeed = 720f;

    [Tooltip("Half the size of the area the player can move in.")]
    public float Bounds = 950f;

    [Tooltip("The visual that turns to face the movement direction and spins.")]
    public Actor Body;

    /// <summary>
    /// True while the menu context is active.
    /// </summary>
    public bool IsPaused { get; private set; }

    private InputManager _input;
    private Float2 _move;
    private float _groundHeight;
    private float _height;
    private float _verticalSpeed;
    private float _slideTimeLeft;
    private Float3 _slideDirection;
    private float _yaw;
    private float _spin;

    public override void OnStart()
    {
        _groundHeight = Actor.Position.Y;
        _input = Actor.GetScript<InputManager>() ?? Actor.AddScript<InputManager>();
        _input.AddInputContext(GameplayContext.Instance);

        // Typed callback: the Axis2D value directly
        _input.BindAction<Float2>(MoveAction.Instance, EnhancedInputActionState.Triggered, OnMove);
        // Completed fires when the stick/keys are released, or when the menu consumes them
        _input.BindAction(MoveAction.Instance, EnhancedInputActionState.Completed, OnMoveStopped);
        _input.BindAction(JumpAction.Instance, EnhancedInputActionState.Triggered, OnJump);
        _input.BindAction(SlideAction.Instance, EnhancedInputActionState.Triggered, OnSlide);
        _input.BindAction(SpinAction.Instance, EnhancedInputActionState.Triggered, OnSpin);
        _input.BindAction(PauseAction.Instance, EnhancedInputActionState.Started, OnPause);
        _input.BindAction(MenuConfirmAction.Instance, EnhancedInputActionState.Triggered, OnMenuConfirm);
    }

    public override void OnDestroy()
    {
        // Callbacks owned by a destroyed script are dropped automatically, but unbinding explicitly is tidier
        _input?.UnbindAll(this);
    }

    public override void OnUpdate()
    {
        var deltaTime = Time.DeltaTime;

        // Polling works too: Sprint has no callbacks
        var sprinting = _input.GetActionState(SprintAction.Instance) == EnhancedInputActionState.Triggered;
        var velocity = new Float3(_move.X, 0f, _move.Y) * WalkSpeed * (sprinting ? SprintMultiplier : 1f);

        if (_slideTimeLeft > 0f)
        {
            _slideTimeLeft -= deltaTime;
            velocity = _slideDirection * SlideSpeed;
        }

        if (velocity.LengthSquared > 1f)
        {
            _yaw = Mathf.RadiansToDegrees * MathF.Atan2(velocity.X, velocity.Z);
        }

        _verticalSpeed -= Gravity * deltaTime;
        _height += _verticalSpeed * deltaTime;
        if (_height <= 0f)
        {
            _height = 0f;
            _verticalSpeed = 0f;
        }

        var position = Actor.Position;
        position.X = Math.Clamp(position.X + velocity.X * deltaTime, -Bounds, Bounds);
        position.Z = Math.Clamp(position.Z + velocity.Z * deltaTime, -Bounds, Bounds);
        position.Y = _groundHeight + _height;
        Actor.Position = position;

        if (_spin > 0f) _spin = Math.Max(0f, _spin - SpinSpeed * deltaTime);
        if (Body) Body.Orientation = Quaternion.Euler(0f, _yaw + _spin, 0f);
    }

    private void OnMove(Float2 value) => _move = value;

    private void OnMoveStopped() => _move = Float2.Zero;

    private void OnJump()
    {
        if (_height <= 0f) _verticalSpeed = JumpSpeed;
    }

    private void OnSlide()
    {
        if (_slideTimeLeft > 0f || _move.LengthSquared < 0.01f) return;

        _slideDirection = Float3.Normalize(new Float3(_move.X, 0f, _move.Y));
        _slideTimeLeft = SlideDuration;
    }

    // Hold triggers every frame once the hold time is reached; keep spinning while held
    private void OnSpin() => _spin = 360f;

    private void OnPause()
    {
        IsPaused = !IsPaused;

        // The menu context maps the movement keys and Space/Enter/A too. Its higher priority consumes them,
        // so Move and Jump stop while it's active and MenuConfirm gets Space instead.
        if (IsPaused) _input.AddInputContext(MenuContext.Instance, 10);
        else _input.RemoveInputContext(MenuContext.Instance);

        Debug.Log(IsPaused ? "[Demo] Paused" : "[Demo] Resumed");
    }

    private void OnMenuConfirm() => Debug.Log("[Demo] Menu confirm");
}
