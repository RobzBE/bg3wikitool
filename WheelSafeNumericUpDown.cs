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
    {
        ScrollableControl? scrollParent = null;

        // Prefer the closest scrolling container (normally the Build tab).
        for (var parent = Parent; parent is not null; parent = parent.Parent)
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
            : Math.Max(1, lines) * Math.Max(Font.Height, 16);
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
internal sealed class NumericWheelMessageFilter : IMessageFilter
{
    private const int WmMouseWheel = 0x020A;

    public bool PreFilterMessage(ref Message message)
    {
        if (message.Msg != WmMouseWheel)
            return false;
        for (var control = Control.FromHandle(message.HWnd); control is not null; control = control.Parent)
        {
            if (control is not WheelSafeNumericUpDown numeric)
                continue;
            numeric.RouteWheelToWindow((short)((long)message.WParam >> 16));
            return true;
        }
        return false;
    }
}
