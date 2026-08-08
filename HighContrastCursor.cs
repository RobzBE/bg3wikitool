using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;

namespace BG3ItemExplorer;

internal static class HighContrastCursor
{
    private static readonly Lazy<Cursor> Instance = new(CreateCursor);
    public static Cursor Current => Instance.Value;

    private static Cursor CreateCursor()
    {
        using var colour = new Bitmap(32, 32, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
        using var mask = new Bitmap(32, 32);
        using (var graphics = Graphics.FromImage(colour))
        {
            graphics.Clear(Color.Transparent);
            graphics.SmoothingMode = SmoothingMode.AntiAlias;
            var arrow = new[]
            {
                new PointF(2, 1), new PointF(3, 24), new PointF(9, 18),
                new PointF(14, 29), new PointF(20, 26), new PointF(15, 16),
                new PointF(25, 15)
            };
            using var outline = new Pen(Theme.GoldLight, 4.2f) { LineJoin = LineJoin.Round };
            using var fill = new SolidBrush(Theme.CrimsonDark);
            graphics.FillPolygon(fill, arrow);
            graphics.DrawPolygon(outline, arrow);
        }
        using (var graphics = Graphics.FromImage(mask))
        {
            graphics.Clear(Color.White);
            using var fill = new SolidBrush(Color.Black);
            graphics.FillPolygon(fill,
            [
                new PointF(0, 0), new PointF(1, 28), new PointF(8, 21),
                new PointF(13, 32), new PointF(24, 27), new PointF(19, 18),
                new PointF(30, 16)
            ]);
        }
        var info = new IconInfo
        {
            IsIcon = false,
            HotspotX = 2,
            HotspotY = 2,
            ColourBitmap = colour.GetHbitmap(Color.Transparent),
            MaskBitmap = mask.GetHbitmap()
        };
        try
        {
            return new Cursor(CreateIconIndirect(ref info));
        }
        finally
        {
            DeleteObject(info.ColourBitmap);
            DeleteObject(info.MaskBitmap);
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct IconInfo
    {
        [MarshalAs(UnmanagedType.Bool)] public bool IsIcon;
        public uint HotspotX;
        public uint HotspotY;
        public IntPtr MaskBitmap;
        public IntPtr ColourBitmap;
    }

    [DllImport("user32.dll")]
    private static extern IntPtr CreateIconIndirect(ref IconInfo iconInfo);

    [DllImport("gdi32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DeleteObject(IntPtr handle);
}
