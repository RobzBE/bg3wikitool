namespace BG3ItemExplorer;

internal sealed class ModernCheckedListBox : CheckedListBox
{
    public ModernCheckedListBox()
    {
        DrawMode = DrawMode.OwnerDrawFixed;
        ItemHeight = 30;
        BorderStyle = BorderStyle.FixedSingle;
        BackColor = Theme.Parchment;
        ForeColor = Theme.Ink;
        Font = Theme.Body(9.5f);
        CheckOnClick = true;
        IntegralHeight = false;
        ItemCheck += (_, eventArgs) =>
        {
            if (IsHandleCreated)
                BeginInvoke(() => Invalidate(GetItemRectangle(eventArgs.Index)));
        };
    }

    protected override void OnDrawItem(DrawItemEventArgs e)
    {
        if (e.Index < 0 || e.Index >= Items.Count)
            return;
        var selected = (e.State & DrawItemState.Selected) != 0;
        using var background = new SolidBrush(selected ? Color.FromArgb(232, 216, 181) : BackColor);
        e.Graphics.FillRectangle(background, e.Bounds);
        var box = new Rectangle(e.Bounds.Left + 7, e.Bounds.Top + (e.Bounds.Height - 18) / 2, 18, 18);
        var isChecked = GetItemChecked(e.Index);
        using var boxFill = new SolidBrush(isChecked ? Theme.Crimson : Color.White);
        using var border = new Pen(isChecked ? Theme.Crimson : Theme.Gold, 2f);
        e.Graphics.FillRectangle(boxFill, box);
        e.Graphics.DrawRectangle(border, box);
        if (isChecked)
        {
            using var check = new Pen(Color.White, 2.2f);
            e.Graphics.DrawLines(check,
            [
                new Point(box.Left + 4, box.Top + 9),
                new Point(box.Left + 8, box.Top + 13),
                new Point(box.Left + 15, box.Top + 5)
            ]);
        }
        var text = GetItemText(Items[e.Index]);
        var textBounds = new Rectangle(box.Right + 8, e.Bounds.Top, Math.Max(0, e.Bounds.Right - box.Right - 12), e.Bounds.Height);
        TextRenderer.DrawText(e.Graphics, text, Font, textBounds, Enabled ? ForeColor : Theme.Muted,
            TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);
        if ((e.State & DrawItemState.Focus) != 0)
            ControlPaint.DrawFocusRectangle(e.Graphics, textBounds);
    }
}
