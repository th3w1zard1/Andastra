using System;
using System.IO;
using System.Text;
using AuroraEngine.Common.Common;
using AuroraEngine.Common.Formats.MDLData;

namespace AuroraEngine.Common.Formats.MDL
{
    // Simplified port of PyKotor io_mdl_ascii.MDLAsciiWriter
    public class MDLAsciiWriter : IDisposable
    {
        private readonly MDLData.MDL _mdl;
        private readonly RawBinaryWriter _writer;

        public MDLAsciiWriter(MDLData.MDL mdl, string filepath)
        {
            _mdl = mdl ?? throw new ArgumentNullException(nameof(mdl));
            _writer = RawBinaryWriter.ToFile(filepath);
        }

        public MDLAsciiWriter(MDLData.MDL mdl, Stream target)
        {
            _mdl = mdl ?? throw new ArgumentNullException(nameof(mdl));
            _writer = RawBinaryWriter.ToStream(target);
        }

        public MDLAsciiWriter(MDLData.MDL mdl)
        {
            _mdl = mdl ?? throw new ArgumentNullException(nameof(mdl));
            _writer = RawBinaryWriter.ToByteArray();
        }

        public void Write(bool autoClose = true)
        {
            try
            {
                // Minimal ASCII writer placeholder
                string text = "# MDL ASCII export placeholder\n";
                _writer.WriteBytes(Encoding.ASCII.GetBytes(text));
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
            _writer?.Dispose();
        }
    }
}

