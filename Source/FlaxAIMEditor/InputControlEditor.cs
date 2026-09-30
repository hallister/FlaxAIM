using FlaxAIM;
using FlaxEditor.CustomEditors;
using FlaxEditor.CustomEditors.Elements;
using FlaxEditor.GUI.ContextMenu;
using FlaxEngine;

namespace FlaxAIMEditor;

/// <summary>
/// Picks an <see cref="InputControl"/> from a Device ▸ Control menu instead of one long enum dropdown.
/// </summary>
[CustomEditor(typeof(InputControl))]
public class InputControlEditor : CustomEditor
{
    private ButtonElement _button;

    /// <inheritdoc />
    public override DisplayStyle Style => DisplayStyle.Inline;

    /// <inheritdoc />
    public override void Initialize(LayoutElementsContainer layout)
    {
        _button = layout.Button(string.Empty);
        _button.Button.Clicked += ShowMenu;
    }

    /// <inheritdoc />
    public override void Refresh()
    {
        base.Refresh();

        _button.Button.Text = HasDifferentValues || Values[0] is not InputControl control
            ? "—"
            : $"{control.Device()} > {control.DisplayName()}";
    }

    private void ShowMenu()
    {
        var menu = new ContextMenu();
        foreach (var control in InputControls.All)
        {
            var value = control;
            var device = menu.GetOrAddChildMenu(control.Device().ToString());
            var item = device.ContextMenu.AddButton(control.DisplayName(), () => SetValue(value));
            item.TooltipText = Tooltip(control);
            item.Checked = !HasDifferentValues && Values[0] is InputControl current && current == control;
        }

        menu.Show(_button.Button, new Float2(0, _button.Button.Height));
    }

    private static string Tooltip(InputControl control)
    {
        var field = typeof(InputControl).GetField(control.ToString());
        return field?.GetCustomAttributes(typeof(TooltipAttribute), false) is [TooltipAttribute tooltip, ..] ? tooltip.Text : null;
    }
}
