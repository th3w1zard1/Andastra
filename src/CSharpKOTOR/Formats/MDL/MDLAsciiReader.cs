using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using AuroraEngine.Common.Common;
using AuroraEngine.Common.Formats.MDLData;
using AuroraEngine.Common.Formats.MDL;

namespace AuroraEngine.Common.Formats.MDL
{
    // Simplified port of PyKotor io_mdl_ascii.MDLAsciiReader
    public class MDLAsciiReader : IDisposable
    {
        private readonly RawBinaryReader _reader;
        private MDLData.MDL _mdl;

        public MDLAsciiReader(byte[] data, int offset = 0, int size = 0)
        {
            _reader = RawBinaryReader.FromBytes(data, offset, size > 0 ? size : (int?)null);
        }

        public MDLAsciiReader(string filepath, int offset = 0, int size = 0)
        {
            _reader = RawBinaryReader.FromFile(filepath, offset, size > 0 ? size : (int?)null);
        }

        public MDLAsciiReader(Stream source, int offset = 0, int size = 0)
        {
            _reader = RawBinaryReader.FromStream(source, offset, size > 0 ? size : (int?)null);
        }

        public MDLData.MDL Load(bool autoClose = true)
        {
            try
            {
                // Minimal ASCII loader: reads entire file as text, not fully parsed.
                _mdl = new MDLData.MDL();
                string text = Encoding.ASCII.GetString(_reader.ReadAll());
                _mdl.Name = ""; // placeholder; full parsing not implemented
                return _mdl;
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

