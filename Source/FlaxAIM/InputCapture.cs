using System;
using FlaxEngine;

namespace FlaxAIM;

/// <summary>
/// Reads the control a player presses, for a rebinding screen waiting for the new key. The keyboard and mouse reach
/// GUI controls as events (<c>OnKeyDown</c>, <c>OnMouseDown</c>); gamepad buttons don't, so they're read here.
/// </summary>
public static class InputCapture
{
    private static readonly GamepadButton[] Buttons = (GamepadButton[])Enum.GetValues(typeof(GamepadButton));

    /// <summary>
    /// The first button <paramref name="gamepad"/> pressed this frame, or <see cref="GamepadButton.None"/>.
    /// </summary>
    public static GamepadButton GamepadButtonDown(InputGamepadIndex gamepad = InputGamepadIndex.All)
    {
        foreach (var button in Buttons)
        {
            if (button != GamepadButton.None && Input.GetGamepadButtonDown(gamepad, button)) return button;
        }
        return GamepadButton.None;
    }
}
