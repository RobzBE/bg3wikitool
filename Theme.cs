namespace BG3ItemExplorer;

internal static class Theme
{
    public static readonly Color Ink = Color.FromArgb(38, 30, 26);
    public static readonly Color Parchment = Color.FromArgb(251, 242, 220);
    public static readonly Color ParchmentAlt = Color.FromArgb(244, 231, 203);
    public static readonly Color Crimson = Color.FromArgb(103, 27, 35);
    public static readonly Color CrimsonDark = Color.FromArgb(54, 13, 18);
    public static readonly Color Gold = Color.FromArgb(185, 138, 61);
    public static readonly Color GoldLight = Color.FromArgb(229, 198, 124);
    public static readonly Color Muted = Color.FromArgb(106, 88, 70);
    public static readonly Color Grid = Color.FromArgb(207, 185, 143);

    public static Font Body(float size = 9f, FontStyle style = FontStyle.Regular) =>
        FontManager.Create(size, style);

    public static Font Heading(float size = 12f, FontStyle style = FontStyle.Bold) =>
        FontManager.Create(size, style);

    public static Color RarityBackground(string rarity) => rarity switch
    {
        "Uncommon" => Color.FromArgb(216, 231, 208),
        "Rare" => Color.FromArgb(212, 226, 237),
        "Very Rare" => Color.FromArgb(229, 212, 232),
        "Legendary" => Color.FromArgb(242, 223, 192),
        _ => Parchment
    };

    public static Color RarityForeground(string rarity) => rarity switch
    {
        "Uncommon" => Color.FromArgb(45, 103, 45),
        "Rare" => Color.FromArgb(36, 90, 134),
        "Very Rare" => Color.FromArgb(123, 57, 126),
        "Legendary" => Color.FromArgb(139, 90, 18),
        _ => Ink
    };

    public static void ConfigureModernCombo(ComboBox combo, float fontSize = 9.5f)
    {
        combo.FlatStyle = FlatStyle.Flat;
        combo.BackColor = Color.White;
        combo.ForeColor = Ink;
        combo.Font = Body(fontSize);
        combo.DrawMode = DrawMode.OwnerDrawFixed;
        combo.ItemHeight = 30;
        combo.IntegralHeight = false;
        combo.DropDownHeight = 300;
        combo.DrawItem += DrawComboItem;
    }

    private static void DrawComboItem(object? sender, DrawItemEventArgs e)
    {
        if (sender is not ComboBox combo)
            return;
        var selected = (e.State & DrawItemState.Selected) != 0;
        using var background = new SolidBrush(selected ? Color.FromArgb(232, 216, 181) : Color.White);
        e.Graphics.FillRectangle(background, e.Bounds);
        if (e.Index >= 0)
        {
            var text = combo.GetItemText(combo.Items[e.Index]);
            var bounds = new Rectangle(e.Bounds.X + 9, e.Bounds.Y, Math.Max(0, e.Bounds.Width - 13), e.Bounds.Height);
            TextRenderer.DrawText(e.Graphics, text, combo.Font, bounds, Ink,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);
        }
        if ((e.State & DrawItemState.Focus) != 0)
            e.DrawFocusRectangle();
    }
}
