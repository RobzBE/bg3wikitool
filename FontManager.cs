using System.Drawing.Text;
using System.Reflection;
using System.Runtime.InteropServices;

namespace BG3ItemExplorer;

internal static class FontManager
{
    private static readonly PrivateFontCollection Collection = new();
    private static readonly List<IntPtr> FontMemory = [];
    private static FontFamily? _alegreya;

    public static bool IsAlegreyaLoaded => _alegreya is not null;

    public static void Initialize()
    {
        try
        {
            LoadEmbeddedFont("Fonts.Alegreya.ttf");
            LoadEmbeddedFont("Fonts.Alegreya-Italic.ttf");
            _alegreya = Collection.Families.FirstOrDefault(family =>
                family.Name.Contains("Alegreya", StringComparison.OrdinalIgnoreCase));
        }
        catch
        {
            _alegreya = null;
        }
    }

    public static Font Create(float size, FontStyle style = FontStyle.Regular)
    {
        if (_alegreya is not null)
        {
            try
            {
                return new Font(_alegreya, size, style, GraphicsUnit.Point);
            }
            catch
            {
                // GDI may emulate unsupported styles differently on older Windows versions.
            }
        }
        return new Font("Georgia", size, style, GraphicsUnit.Point);
    }

    private static void LoadEmbeddedFont(string suffix)
    {
        var assembly = Assembly.GetExecutingAssembly();
        var resourceName = assembly.GetManifestResourceNames()
            .Single(name => name.EndsWith(suffix, StringComparison.OrdinalIgnoreCase));
        using var stream = assembly.GetManifestResourceStream(resourceName)
            ?? throw new InvalidOperationException($"Embedded font missing: {suffix}");
        using var memory = new MemoryStream();
        stream.CopyTo(memory);
        var bytes = memory.ToArray();
        var pointer = Marshal.AllocCoTaskMem(bytes.Length);
        Marshal.Copy(bytes, 0, pointer, bytes.Length);
        Collection.AddMemoryFont(pointer, bytes.Length);
        FontMemory.Add(pointer);
    }
}
