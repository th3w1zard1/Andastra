using System.IO;
using System.Text;
using CSharpKOTOR.Common;
using JetBrains.Annotations;

namespace CSharpKOTOR.Formats
{

    /// <summary>
    /// Base class for binary format readers to eliminate duplicate constructor patterns.
    /// </summary>
    public abstract class BinaryFormatReaderBase
    {
        protected readonly byte[] Data;
        protected readonly CSharpKOTOR.Common.BinaryReader Reader;

        protected BinaryFormatReaderBase(byte[] data, [CanBeNull] Encoding encoding = null)
        {
            Data = data;
            Reader = CSharpKOTOR.Common.BinaryReader.FromBytes(data, 0, null);
        }

        protected BinaryFormatReaderBase(string filepath, [CanBeNull] Encoding encoding = null)
        {
            Data = File.ReadAllBytes(filepath);
            Reader = CSharpKOTOR.Common.BinaryReader.FromBytes(Data, 0, null);
        }

        protected BinaryFormatReaderBase(Stream source, [CanBeNull] Encoding encoding = null)
        {
            using (var ms = new MemoryStream())
            {
                source.CopyTo(ms);
                Data = ms.ToArray();
                Reader = CSharpKOTOR.Common.BinaryReader.FromBytes(Data, 0, null);
            }
        }
    }
}

