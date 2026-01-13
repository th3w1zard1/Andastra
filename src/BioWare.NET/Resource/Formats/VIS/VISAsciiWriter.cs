using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using BioWare.NET.Common;
using BioWare.NET.Resource.Formats.VIS;

namespace BioWare.NET.Resource.Formats.VIS
{
    // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/resource/formats/vis/io_vis.py:76-87
    // Original: class VISAsciiWriter(ResourceWriter)
    public class VISAsciiWriter : IDisposable
    {
        private readonly VIS _vis;
        private readonly RawBinaryWriter _writer;

        public VISAsciiWriter(VIS vis, string filepath)
        {
            _vis = vis ?? throw new ArgumentNullException(nameof(vis));
            _writer = RawBinaryWriter.ToFile(filepath);
        }

        public VISAsciiWriter(VIS vis, Stream target)
        {
            _vis = vis ?? throw new ArgumentNullException(nameof(vis));
            _writer = RawBinaryWriter.ToStream(target);
        }

        public VISAsciiWriter(VIS vis)
        {
            _vis = vis ?? throw new ArgumentNullException(nameof(vis));
            _writer = RawBinaryWriter.ToByteArray();
        }

        // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/resource/formats/vis/io_vis.py:82-87
        // Original: @autoclose def write(self, *, auto_close: bool = True)
        public void Write(bool autoClose = true)
        {
            try
            {
                string newline = Environment.NewLine;
                foreach (var pair in _vis.GetEnumerator())
                {
                    string observer = pair.Item1;
                    HashSet<string> observed = pair.Item2;
                    _writer.WriteString($"{observer} {observed.Count}{newline}", Encoding.ASCII.WebName);
                    foreach (string room in observed)
                    {
                        _writer.WriteString($"  {room}{newline}", Encoding.ASCII.WebName);
                    }
                }
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
