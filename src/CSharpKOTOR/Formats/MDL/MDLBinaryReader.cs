using System;
using System.IO;
using AuroraEngine.Common.Common;
using AuroraEngine.Common.Formats.MDLData;
using AuroraEngine.Common.Formats.MDL;

namespace AuroraEngine.Common.Formats.MDL
{
    // Simplified binary reader. Full parity with PyKotor io_mdl.py is pending.
    public class MDLBinaryReader : IDisposable
    {
        private readonly RawBinaryReader _reader;
        private readonly object _mdxSource;
        private readonly int _mdxOffset;
        private readonly int _mdxSize;
        private readonly bool _fastLoad;

        private static RawBinaryReader CreateReader(object source, int offset, int? size = null)
        {
            if (source is string path)
            {
                return RawBinaryReader.FromFile(path, offset, size);
            }
            if (source is byte[] bytes)
            {
                return RawBinaryReader.FromBytes(bytes, offset, size);
            }
            if (source is Stream stream)
            {
                return RawBinaryReader.FromStream(stream, offset, size);
            }
            throw new ArgumentException("Unsupported source type for MDL");
        }

        public MDLBinaryReader(object source, int offset = 0, int size = 0, object mdxSource = null, int mdxOffset = 0, int mdxSize = 0, bool fastLoad = false)
        {
            _reader = CreateReader(source, offset, size > 0 ? (int?)size : null);
            _mdxSource = mdxSource;
            _mdxOffset = mdxOffset;
            _mdxSize = mdxSize;
            _fastLoad = fastLoad;
        }

        public MDLData.MDL Load(bool autoClose = true)
        {
            try
            {
                // Minimal stub: real parsing to be implemented for full parity.
                var mdl = new MDLData.MDL();
                // consume size
                _reader.Seek(0);
                return mdl;
            }
            finally
            {
                if (autoClose)
                {
                    Dispose();
                }
            }
        }

        public void Dispose()
        {
            _reader?.Dispose();
        }
    }
}

