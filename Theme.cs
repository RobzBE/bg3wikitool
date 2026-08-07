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
}
