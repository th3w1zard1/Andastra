using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using Andastra.Parsing;
using Andastra.Parsing.Common;

namespace Andastra.Parsing.Formats.TXI
{
    // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/resource/formats/txi/io_txi.py:316-355
    // Original: class TXIBinaryWriter(ResourceWriter)
    public class TXIBinaryWriter : IDisposable
    {
        private readonly TXI _txi;
        private readonly RawBinaryWriter _writer;

        public TXIBinaryWriter(TXI txi, string filepath)
        {
            _txi = txi ?? throw new ArgumentNullException(nameof(txi));
            _writer = RawBinaryWriter.ToFile(filepath);
        }

        public TXIBinaryWriter(TXI txi, Stream target)
        {
            _txi = txi ?? throw new ArgumentNullException(nameof(txi));
            _writer = RawBinaryWriter.ToStream(target);
        }

        public TXIBinaryWriter(TXI txi)
        {
            _txi = txi ?? throw new ArgumentNullException(nameof(txi));
            _writer = RawBinaryWriter.ToByteArray();
        }

        // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/resource/formats/txi/io_txi.py:322-355
        // Original: @autoclose def write(self, *, auto_close: bool = True)
        public void Write(bool autoClose = true)
        {
            try
            {
                List<string> lines = new List<string>();
                var properties = typeof(TXIFeatures).GetProperties();
                foreach (var prop in properties)
                {
                    object value = prop.GetValue(_txi.Features);
                    if (value == null)
                    {
                        continue;
                    }
                    if (value is List<float> floatList && floatList.Count == 0)
                    {
                        continue;
                    }
                    if (value is List<int> intList && intList.Count == 0)
                    {
                        continue;
                    }
                    if (value is List<Tuple<float, float, int>> coordList && coordList.Count == 0)
                    {
                        continue;
                    }
                    if (value is string stringValue && string.IsNullOrEmpty(stringValue))
                    {
                        continue;
                    }

                    string attr = prop.Name;
                    string upperAttr = attr.ToUpperInvariant();
                    TXICommand? command = TXICommandExtensions.FromString(upperAttr);
                    if (!command.HasValue)
                    {
                        continue;
                    }

                    if (value is bool boolValue)
                    {
                        lines.Add($"{command.Value.GetValue()} {(boolValue ? 1 : 0)}");
                    }
                    else if (value is int || value is float || value is double)
                    {
                        lines.Add(string.Format(CultureInfo.InvariantCulture, "{0} {1}", command.Value.GetValue(), value));
                    }
                    else if (value is List<Tuple<float, float, int>> coordList2)
                    {
                        if (attr.ToLowerInvariant() == "upperleftcoords" || attr.ToLowerInvariant() == "lowerrightcoords")
                        {
                            lines.Add($"{command.Value.GetValue()} {coordList2.Count}");
                            foreach (var coord in coordList2)
                            {
                                lines.Add(string.Format(CultureInfo.InvariantCulture, "{0} {1} {2}", coord.Item1, coord.Item2, coord.Item3));
                            }
                        }
                    }
                    else if (value is List<float> floatList2)
                    {
                        lines.Add($"{command.Value.GetValue()} {string.Join(" ", floatList2.Select(v => v.ToString(CultureInfo.InvariantCulture)))}");
                    }
                    else if (value is List<int> intList2)
                    {
                        lines.Add($"{command.Value.GetValue()} {string.Join(" ", intList2.Select(v => v.ToString(CultureInfo.InvariantCulture)))}");
                    }
                    else if (value is string strValue)
                    {
                        lines.Add($"{command.Value.GetValue()} {strValue}");
                    }
                }

                string txiString = string.Join("\n", lines);
                byte[] bytes = Encoding.ASCII.GetBytes(txiString);
                _writer.WriteBytes(bytes);
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
