using K4os.Compression.LZ4.Streams;

namespace LSLib.LS.Native;

public static class LZ4FrameCompressor
{
    public static byte[] Decompress(byte[] compressed)
    {
        using var input = new MemoryStream(compressed);
        using var decoder = LZ4Stream.Decode(input);
        using var output = new MemoryStream();
        decoder.CopyTo(output);
        return output.ToArray();
    }
}

public static class FastLZCompressor
{
    public static byte[] Compress(byte[] input, int level) => throw new NotSupportedException();
    public static byte[] Decompress(byte[] compressed, int maxOutput) => throw new NotSupportedException();
}
