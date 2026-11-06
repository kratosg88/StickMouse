namespace StickMouse;

// Control names and numeric enum values are persisted; append actions without renumbering.
enum PadControl { LeftStick, RightStick, A, B, X, Y, LeftShoulder, RightShoulder, LeftTrigger, RightTrigger, DpadUp, DpadDown, DpadLeft, DpadRight, Menu, View }
enum MouseAction { None, LeftClick, RightClick, MiddleClick, Back, Forward, DoubleLeftClick, DoubleRightClick, ToggleDrag, ScrollUp, ScrollDown, ScrollLeft, ScrollRight, MoveUp, MoveDown, MoveLeft, MoveRight, Precision, Pause }
enum StickRole { Cursor, Scroll, Disabled }

static class Bindings
{
    internal static readonly PadControl[] Controls = Enum.GetValues<PadControl>();
    internal static readonly MouseAction[] Actions = Enum.GetValues<MouseAction>();
    internal static Dictionary<string, MouseAction> Defaults() => Controls.ToDictionary(c => c.ToString(), c => c switch {
        PadControl.LeftStick => MouseAction.LeftClick, PadControl.RightStick => MouseAction.RightClick, _ => MouseAction.None });
    internal static MouseAction Action(Preferences p, PadControl c) => p.Buttons.TryGetValue(c.ToString(), out var action) ? action : MouseAction.None;
    internal static Dictionary<string, MouseAction> Normalize(Dictionary<string, MouseAction>? buttons)
    {
        var result = Defaults();
        if (buttons is not null) foreach (var c in Controls)
            if (buttons.TryGetValue(c.ToString(), out var action)) result[c.ToString()] = Enum.IsDefined(action) ? action : MouseAction.None;
        return result;
    }
    internal static string Label(PadControl c) => c switch {
        PadControl.LeftStick => "L3 · Left stick press", PadControl.RightStick => "R3 · Right stick press",
        PadControl.LeftShoulder => "LB / L1", PadControl.RightShoulder => "RB / R1",
        PadControl.LeftTrigger => "LT / L2", PadControl.RightTrigger => "RT / R2",
        PadControl.DpadUp => "D-pad up", PadControl.DpadDown => "D-pad down", PadControl.DpadLeft => "D-pad left", PadControl.DpadRight => "D-pad right",
        PadControl.Menu => "Menu / Start", PadControl.View => "View / Back", _ => c.ToString() };
    internal static string Label(MouseAction a) => a switch {
        MouseAction.None => "Unassigned", MouseAction.LeftClick => "Left click / hold to drag", MouseAction.RightClick => "Right click / hold",
        MouseAction.MiddleClick => "Middle click / hold", MouseAction.Back => "Mouse Back (button 4)", MouseAction.Forward => "Mouse Forward (button 5)",
        MouseAction.DoubleLeftClick => "Double left-click", MouseAction.DoubleRightClick => "Double right-click", MouseAction.ToggleDrag => "Toggle left drag",
        MouseAction.ScrollUp => "Scroll up", MouseAction.ScrollDown => "Scroll down", MouseAction.ScrollLeft => "Scroll left", MouseAction.ScrollRight => "Scroll right",
        MouseAction.MoveUp => "Move cursor up", MouseAction.MoveDown => "Move cursor down", MouseAction.MoveLeft => "Move cursor left", MouseAction.MoveRight => "Move cursor right",
        MouseAction.Precision => "Precision mode (hold)", MouseAction.Pause => "Pause mouse control", _ => "Unassigned" };
    internal static string Label(StickRole r) => r switch { StickRole.Cursor => "Move cursor", StickRole.Scroll => "Scroll in both directions", _ => "Disabled" };
}
