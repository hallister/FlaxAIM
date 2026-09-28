using System.Collections.Generic;
using System.Text;
using FlaxAIM.State;
using FlaxAIM.Triggers;
using FlaxEngine;
using FlaxEngine.GUI;

namespace FlaxAIM.Examples;

/// <summary>
/// Shows the state, events and value of a list of actions on screen.
/// </summary>
public class InputHud : Script
{
    [Tooltip("The manager to read. Uses the one on this actor when empty.")]
    public InputManager Manager;

    [Tooltip("Optional: shows whether the demo is paused.")]
    public DemoPlayer Player;

    public List<JsonAssetReference<InputAction>> Actions = [];

    [Tooltip("Seconds an event stays on screen after it's raised.")]
    public float EventDisplayTime = 0.75f;

    private readonly Dictionary<InputAction, (TriggerEvent Events, float Time)> _lastEvents = new();
    private UICanvas _canvas;
    private Label _label;

    public override void OnStart()
    {
        if (Manager == null) Manager = Actor.GetScript<InputManager>();

        _canvas = new UICanvas { Name = "InputHudCanvas", RenderMode = CanvasRenderMode.ScreenSpace, Parent = Actor };
        _label = new Label
        {
            TextColor = Color.White,
            HorizontalAlignment = TextAlignment.Near,
            VerticalAlignment = TextAlignment.Near,
        };
        new UIControl { Name = "InputHudText", Parent = _canvas, Control = _label };
        _label.SetAnchorPreset(AnchorPresets.StretchAll, false, false);
        _label.Offsets = new Margin(10, 10, 10, 10);
    }

    public override void OnDestroy()
    {
        if (_canvas) Destroy(_canvas);
    }

    public override void OnUpdate()
    {
        if (Manager == null || _label == null) return;

        var text = new StringBuilder();
        text.AppendLine("FlaxAIM demo   WASD/left stick: move   Space/A: jump   Shift/LB: sprint   Shift+C / LB+B: slide");
        text.AppendLine("               hold E/Y: spin   Esc/Start: pause (the menu context takes over WASD and Space)");
        if (Player != null) text.AppendLine(Player.IsPaused ? "PAUSED" : "");
        text.AppendLine();

        var now = Time.GameTime;
        foreach (var reference in Actions)
        {
            var action = reference.Instance;
            if (action == null) continue;

            var events = Manager.GetActionEvents(action);
            if (events != TriggerEvent.None) _lastEvents[action] = (events, now);

            var recent = _lastEvents.TryGetValue(action, out var last) && now - last.Time < EventDisplayTime ? last.Events.ToString() : "";
            text.AppendLine($"{action.Name,-14} {Manager.GetActionState(action),-10} {FormatValue(action, Manager.GetActionValue(action)),-18} {recent}");
        }

        _label.Text = text.ToString();
    }

    private static string FormatValue(InputAction action, Modifiers.ProcessedInputActionValue value)
    {
        return action.ActionType switch
        {
            InputActionType.Axis1D => $"{value.Axis1D:0.00}",
            InputActionType.Axis2D => $"({value.Axis2D.X:0.00}, {value.Axis2D.Y:0.00})",
            InputActionType.Axis3D => $"({value.Axis3D.X:0.00}, {value.Axis3D.Y:0.00}, {value.Axis3D.Z:0.00})",
            _ => value.Digital ? "on" : "off",
        };
    }
}
