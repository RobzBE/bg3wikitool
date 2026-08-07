namespace BG3ItemExplorer;

internal sealed class WheelSafeNumericUpDown : NumericUpDown
{
    private const int WmMouseWheel = 0x020A;

    protected override void WndProc(ref Message message)
    {
        if (message.Msg == WmMouseWheel)
        {
            RouteWheelToWindow((short)((long)message.WParam >> 16));
            return;
        }

        base.WndProc(ref message);
    }

    protected override void OnMouseWheel(MouseEventArgs e)
    {
        RouteWheelToWindow(e.Delta);
        // Deliberately do not call base: the wheel may never alter this value.
    }

    internal void RouteWheelToWindow(int delta)
        => MouseWheelRouter.RouteToWindow(this, delta);
}

internal static class MouseWheelRouter
{
    internal static void RouteToWindow(Control origin, int delta)
    {
        ScrollableControl? scrollParent = null;

        // Prefer the closest scrolling container (normally the Build tab).
        for (var parent = origin.Parent; parent is not null; parent = parent.Parent)
        {
            if (parent is ScrollableControl { AutoScroll: true } candidate)
            {
                scrollParent = candidate;
                break;
            }
        }

        if (scrollParent is null || delta == 0)
            return;

        var lines = SystemInformation.MouseWheelScrollLines;
        var distance = lines < 0
            ? scrollParent.ClientSize.Height
            : Math.Max(1, lines) * Math.Max(origin.Font.Height, 16);
        var currentY = -scrollParent.AutoScrollPosition.Y;
        var maximumY = Math.Max(0, scrollParent.DisplayRectangle.Height - scrollParent.ClientSize.Height);
        var nextY = Math.Clamp(currentY - Math.Sign(delta) * distance, 0, maximumY);
        scrollParent.AutoScrollPosition = new Point(-scrollParent.AutoScrollPosition.X, nextY);
    }
}

/// <summary>
/// NumericUpDown owns a native edit child which can receive the wheel message
/// before the parent control. Filtering at application level guarantees that
/// no numeric field changes while the user is scrolling the build page.
/// </summary>
internal sealed class SafeOptionWheelMessageFilter : IMessageFilter
{
    private const int WmMouseWheel = 0x020A;

    public bool PreFilterMessage(ref Message message)
    {
        if (message.Msg != WmMouseWheel)
            return false;
        for (var control = Control.FromHandle(message.HWnd); control is not null; control = control.Parent)
        {
            var delta = (short)((long)message.WParam >> 16);
            if (control is WheelSafeNumericUpDown numeric)
            {
                numeric.RouteWheelToWindow(delta);
                return true;
            }
            if (control is ComboBox combo)
            {
                // A closed combo must never change merely because the pointer
                // happens to be over it. Once explicitly opened, native list
                // scrolling remains available for choosing an option.
                if (combo.DroppedDown)
                    return false;
                MouseWheelRouter.RouteToWindow(combo, delta);
                return true;
            }
        }
        return false;
    }
}
