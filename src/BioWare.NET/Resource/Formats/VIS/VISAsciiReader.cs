using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using BioWare.NET.Resource.Formats.VIS;

namespace BioWare.NET.Resource.Formats.VIS
{
    // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/resource/formats/vis/io_vis.py:14-73
    // Original: class VISAsciiReader(ResourceReader)
    public class VISAsciiReader : IDisposable
    {
        private readonly BioWare.NET.Common.RawBinaryReader _reader;
        private VIS _vis;
        private List<string> _lines;

        public VISAsciiReader(byte[] data, int offset = 0, int size = 0)
        {
            int? sizeNullable = size > 0 ? (int?)size : null;
            _reader = BioWare.NET.Common.RawBinaryReader.FromBytes(data, offset, sizeNullable);
        }

        public VISAsciiReader(string filepath, int offset = 0, int size = 0)
        {
            int? sizeNullable = size > 0 ? (int?)size : null;
            _reader = BioWare.NET.Common.RawBinaryReader.FromFile(filepath, offset, sizeNullable);
        }

        public VISAsciiReader(Stream source, int offset = 0, int size = 0)
        {
            int? sizeNullable = size > 0 ? (int?)size : null;
            _reader = BioWare.NET.Common.RawBinaryReader.FromStream(source, offset, sizeNullable);
        }

        // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/resource/formats/vis/io_vis.py:29-73
        // Original: @autoclose def load(self, *, auto_close: bool = True) -> VIS
        public VIS Load(bool autoClose = true)
        {
            try
            {
                _vis = new VIS();
                byte[] allBytes = _reader.ReadAll();
                string text = Encoding.ASCII.GetString(allBytes);
                _lines = text.Split(new[] { "\r\n", "\n", "\r" }, StringSplitOptions.None).ToList();

                List<Tuple<string, string>> pairs = new List<Tuple<string, string>>();

                using (var enumerator = _lines.GetEnumerator())
                {
                    while (enumerator.MoveNext())
                    {
                        string line = enumerator.Current;
                        string[] tokens = line.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);

                        // Skip empty lines
                        if (tokens.Length == 0)
                        {
                            continue;
                        }

                        // Check if this is a version header line (e.g., "room V3.28")
                        if (tokens.Length >= 2 && tokens[1].StartsWith("V", StringComparison.Ordinal))
                        {
                            // This is a version header, skip it
                            continue;
                        }

                        string whenInside = tokens[0];
                        _vis.AddRoom(whenInside);

                        // Try to parse the count
                        if (tokens.Length < 2 || !int.TryParse(tokens[1], out int count))
                        {
                            throw new ArgumentException($"Invalid VIS format: expected room count, got '{(tokens.Length > 1 ? tokens[1] : "(missing)")}' for room '{whenInside}'");
                        }

                        for (int i = 0; i < count; i++)
                        {
                            if (!enumerator.MoveNext())
                            {
                                throw new ArgumentException($"Invalid VIS format: expected {count} child rooms for '{whenInside}', but reached end of file");
                            }
                            string childLine = enumerator.Current;
                            string[] childTokens = childLine.TrimStart().Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                            string show = childTokens.Length > 0 ? childTokens[0] : "";
                            pairs.Add(Tuple.Create(whenInside, show));
                        }
                    }
                }

                foreach (var pair in pairs)
                {
                    string whenInside = pair.Item1;
                    string show = pair.Item2;

                    if (!_vis.RoomExists(whenInside))
                    {
                        _vis.AddRoom(whenInside);
                    }
                    if (!_vis.RoomExists(show))
                    {
                        _vis.AddRoom(show);
                    }
                    _vis.SetVisible(whenInside, show, visible: true);
                }

                return _vis;
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

