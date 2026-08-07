namespace BG3ItemExplorer;

internal sealed class WheelSafeNumericUpDown : NumericUpDown
{
    protected override void OnMouseWheel(MouseEventArgs e)
    {
        var scrollParent = Parent;
        while (scrollParent is not null && scrollParent is not ScrollableControl { AutoScroll: true })
            scrollParent = scrollParent.Parent;

        if (scrollParent is ScrollableControl scrollable)
        {
            var lines = SystemInformation.MouseWheelScrollLines;
            var distance = lines < 0
                ? scrollable.ClientSize.Height
                : Math.Max(1, lines) * Math.Max(Font.Height, 16);
            var currentY = -scrollable.AutoScrollPosition.Y;
            var maximumY = Math.Max(0, scrollable.DisplayRectangle.Height - scrollable.ClientSize.Height);
            var nextY = Math.Clamp(currentY - Math.Sign(e.Delta) * distance, 0, maximumY);
            scrollable.AutoScrollPosition = new Point(-scrollable.AutoScrollPosition.X, nextY);
        }
    }
}
