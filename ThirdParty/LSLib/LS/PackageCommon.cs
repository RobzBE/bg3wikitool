using System.IO;
using System.IO.MemoryMappedFiles;
using System.Xml.Linq;
using LSLib.LS.Enums;

namespace LSLib.LS;

public class PackagedFileInfo : PackagedFileInfoCommon
{
    public Package Package;
    public MemoryMappedFile PackageFile;
    public MemoryMappedViewAccessor PackageView;
    public bool Solid;
    public ulong SolidOffset;
    public Stream SolidStream;

    public UInt64 Size() => Flags.Method() == CompressionMethod.None ? SizeOnDisk : UncompressedSize;

    public Stream CreateContentReader()
    {
        if (IsDeletion())
        {
            throw new InvalidOperationException("Cannot open file stream for a deleted file");
        }

        if (Solid)
        {
            SolidStream.Seek((long)SolidOffset, SeekOrigin.Begin);
            return new ReadOnlySubstream(SolidStream, (long)SolidOffset, (long)UncompressedSize);
        }
        else
        {
            return CompressionHelpers.Decompress(PackageFile, PackageView, (long)OffsetInFile, (int)SizeOnDisk, (int)UncompressedSize, Flags);
        }
    }

    internal static PackagedFileInfo CreateFromEntry(Package package, ILSPKFile entry, MemoryMappedFile file, MemoryMappedViewAccessor view)
    {
        var info = new PackagedFileInfo
        {
            Package = package,
            PackageFile = file,
            PackageView = view,
            Solid = false
        };

        entry.ToCommon(info);
        return info;
    }

    internal void MakeSolid(ulong solidOffset, Stream solidStream)
    {
        Solid = true;
        SolidOffset = solidOffset;
        SolidStream = solidStream;
    }

    public bool IsDeletion()
    {
        return (OffsetInFile & 0x0000ffffffffffff) == 0xbeefdeadbeef;
    }
}

