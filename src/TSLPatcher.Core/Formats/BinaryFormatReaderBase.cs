using System.IO;
using System.Text;

namespace TSLPatcher.Core.Formats;

/// <summary>
/// Base class for binary format readers to eliminate duplicate constructor patterns.
/// </summary>
public abstract class BinaryFormatReaderBase
{
    protected readonly byte[] Data;
    protected readonly BinaryReader Reader;

    protected BinaryFormatReaderBase(byte[] data, Encoding? encoding = null)
    {
        Data = data;
        Reader = encoding != null 
            ? new BinaryReader(new MemoryStream(data), encoding)
            : new BinaryReader(new MemoryStream(data));
    }

    protected BinaryFormatReaderBase(string filepath, Encoding? encoding = null)
    {
        Data = File.ReadAllBytes(filepath);
        Reader = encoding != null
            ? new BinaryReader(new MemoryStream(Data), encoding)
            : new BinaryReader(new MemoryStream(Data));
    }

    protected BinaryFormatReaderBase(Stream source, Encoding? encoding = null)
    {
        using var ms = new MemoryStream();
        source.CopyTo(ms);
        Data = ms.ToArray();
        Reader = encoding != null
            ? new BinaryReader(new MemoryStream(Data), encoding)
            : new BinaryReader(new MemoryStream(Data));
    }
}

