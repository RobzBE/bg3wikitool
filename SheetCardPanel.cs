namespace BG3ItemExplorer;

internal sealed class SheetCardPanel : Panel
{
    public SheetCardPanel()
    {
        DoubleBuffered = true;
        BackColor = Color.FromArgb(255, 249, 235);
        Padding = new Padding(7);
        Margin = new Padding(3);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        var outer = new Rectangle(0, 0, Math.Max(0, Width - 1), Math.Max(0, Height - 1));
        var inner = Rectangle.Inflate(outer, -3, -3);
        using var outerPen = new Pen(Theme.Crimson, 2f);
        using var innerPen = new Pen(Theme.Gold, 1f);
        e.Graphics.DrawRectangle(outerPen, outer);
        if (inner.Width > 0 && inner.Height > 0)
            e.Graphics.DrawRectangle(innerPen, inner);
    }
}
