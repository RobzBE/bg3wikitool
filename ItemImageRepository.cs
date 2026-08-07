using System.IO.Compression;
using System.Reflection;

namespace BG3ItemExplorer;

internal sealed class ItemImageRepository : IDisposable
{
    private readonly MemoryStream _archiveStream;
    private readonly ZipArchive _archive;

    public ItemImageRepository()
    {
        var assembly = Assembly.GetExecutingAssembly();
        var resourceName = assembly.GetManifestResourceNames()
            .Single(name => name.EndsWith("Data.item-images.zip", StringComparison.OrdinalIgnoreCase));
        using var resource = assembly.GetManifestResourceStream(resourceName)
            ?? throw new InvalidOperationException("Het ingebedde afbeeldingsarchief ontbreekt.");
        _archiveStream = new MemoryStream();
        resource.CopyTo(_archiveStream);
        _archiveStream.Position = 0;
        _archive = new ZipArchive(_archiveStream, ZipArchiveMode.Read, leaveOpen: true);
    }

    public Image? Load(string imageKey)
    {
        if (string.IsNullOrWhiteSpace(imageKey))
            return null;
        var entry = _archive.GetEntry(imageKey + ".jpg");
        if (entry is null)
            return null;
        using var stream = entry.Open();
        using var source = Image.FromStream(stream);
        return new Bitmap(source);
    }

    public void Dispose()
    {
        _archive.Dispose();
        _archiveStream.Dispose();
    }
}
