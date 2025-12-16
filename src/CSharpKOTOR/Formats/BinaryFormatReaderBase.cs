using System.IO;
using System.Text;
using AuroraEngine.Common.Common;
using JetBrains.Annotations;

namespace AuroraEngine.Common.Formats
{

    /// <summary>
    /// Base class for binary format readers to eliminate duplicate constructor patterns.
    /// </summary>
    public abstract class BinaryFormatReaderBase
    {
        protected readonly byte[] Data;
        protected readonly AuroraEngine.Common.Common.BinaryReader Reader;

        protected BinaryFormatReaderBase(byte[] data, [CanBeNull] Encoding encoding = null)
        {
            Data = data;
            Reader = AuroraEngine.Common.Common.BinaryReader.FromBytes(data, 0, null);
        }

        protected BinaryFormatReaderBase(string filepath, [CanBeNull] Encoding encoding = null)
        {
            Data = File.ReadAllBytes(filepath);
            Reader = AuroraEngine.Common.Common.BinaryReader.FromBytes(Data, 0, null);
        }

        protected BinaryFormatReaderBase(Stream source, [CanBeNull] Encoding encoding = null)
        {
            using (var ms = new MemoryStream())
            {
                source.CopyTo(ms);
                Data = ms.ToArray();
                Reader = AuroraEngine.Common.Common.BinaryReader.FromBytes(Data, 0, null);
            }
        }
    }
}

