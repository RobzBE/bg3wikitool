namespace BG3ItemExplorer;

internal sealed class ModernCheckBox : CheckBox
{
    private bool _hovered;

    public ModernCheckBox()
    {
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                 ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
        AutoSize = true;
        Cursor = Cursors.Hand;
        Font = Theme.Body(9.5f);
        ForeColor = Theme.Ink;
        Padding = new Padding(0, 2, 0, 2);
    }

    public override Size GetPreferredSize(Size proposedSize)
    {
        var text = TextRenderer.MeasureText(Text, Font, proposedSize, TextFormatFlags.NoPadding);
        return new Size(text.Width + 34 + Padding.Horizontal, Math.Max(28, text.Height + 8) + Padding.Vertical);
    }

    protected override void OnMouseEnter(EventArgs eventArgs)
    {
        _hovered = true;
        Invalidate();
        base.OnMouseEnter(eventArgs);
    }

    protected override void OnMouseLeave(EventArgs eventArgs)
    {
        _hovered = false;
        Invalidate();
        base.OnMouseLeave(eventArgs);
    }

    protected override void OnPaint(PaintEventArgs eventArgs)
    {
        eventArgs.Graphics.Clear(Parent?.BackColor ?? BackColor);
        var box = new Rectangle(2, (Height - 20) / 2, 20, 20);
        using var fill = new SolidBrush(Checked ? Theme.Crimson : (_hovered ? Theme.ParchmentAlt : Color.White));
        using var border = new Pen(Checked ? Theme.Crimson : Theme.Gold, 2f);
        eventArgs.Graphics.FillRectangle(fill, box);
        eventArgs.Graphics.DrawRectangle(border, box);
        if (Checked)
        {
            using var check = new Pen(Color.White, 2.4f) { StartCap = System.Drawing.Drawing2D.LineCap.Round, EndCap = System.Drawing.Drawing2D.LineCap.Round };
            eventArgs.Graphics.DrawLines(check,
            [
                new Point(box.Left + 5, box.Top + 10),
                new Point(box.Left + 9, box.Top + 14),
                new Point(box.Left + 16, box.Top + 6)
            ]);
        }
        var textBounds = new Rectangle(box.Right + 9, 0, Math.Max(0, Width - box.Right - 9), Height);
        TextRenderer.DrawText(eventArgs.Graphics, Text, Font, textBounds, Enabled ? ForeColor : Theme.Muted,
            TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);
        if (Focused && ShowFocusCues)
            ControlPaint.DrawFocusRectangle(eventArgs.Graphics, textBounds);
    }
}
