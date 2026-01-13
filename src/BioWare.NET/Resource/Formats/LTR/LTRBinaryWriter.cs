using System;
using System.IO;
using BioWare.NET.Common;
using BioWare.NET.Resource.Formats.LTR;

namespace BioWare.NET.Resource.Formats.LTR
{
    // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/resource/formats/ltr/io_ltr.py:107-147
    // Original: class LTRBinaryWriter(ResourceWriter)
    public class LTRBinaryWriter : IDisposable
    {
        private readonly LTR _ltr;
        private readonly RawBinaryWriter _writer;

        public LTRBinaryWriter(LTR ltr, string filepath)
        {
            _ltr = ltr ?? throw new ArgumentNullException(nameof(ltr));
            _writer = RawBinaryWriter.ToFile(filepath);
        }

        public LTRBinaryWriter(LTR ltr, Stream target)
        {
            _ltr = ltr ?? throw new ArgumentNullException(nameof(ltr));
            _writer = RawBinaryWriter.ToStream(target);
        }

        public LTRBinaryWriter(LTR ltr)
        {
            _ltr = ltr ?? throw new ArgumentNullException(nameof(ltr));
            _writer = RawBinaryWriter.ToByteArray();
        }

        // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/resource/formats/ltr/io_ltr.py:116-147
        // Original: def write(self, auto_close: bool = True)
        public void Write(bool autoClose = true)
        {
            try
            {
                _writer.WriteString("LTR ", stringLength: 4);
                _writer.WriteString("V1.0", stringLength: 4);
                _writer.WriteUInt8(28);

                foreach (float chance in _ltr.Singles.Start)
                {
                    _writer.WriteSingle(chance);
                }
                foreach (float chance in _ltr.Singles.Middle)
                {
                    _writer.WriteSingle(chance);
                }
                foreach (float chance in _ltr.Singles.End)
                {
                    _writer.WriteSingle(chance);
                }

                for (int i = 0; i < 28; i++)
                {
                    foreach (float chance in _ltr.Doubles[i].Start)
                    {
                        _writer.WriteSingle(chance);
                    }
                    foreach (float chance in _ltr.Doubles[i].Middle)
                    {
                        _writer.WriteSingle(chance);
                    }
                    foreach (float chance in _ltr.Doubles[i].End)
                    {
                        _writer.WriteSingle(chance);
                    }
                }

                for (int i = 0; i < 28; i++)
                {
                    for (int j = 0; j < 28; j++)
                    {
                        foreach (float chance in _ltr.Triples[i][j].Start)
                        {
                            _writer.WriteSingle(chance);
                        }
                        foreach (float chance in _ltr.Triples[i][j].Middle)
                        {
                            _writer.WriteSingle(chance);
                        }
                        foreach (float chance in _ltr.Triples[i][j].End)
                        {
                            _writer.WriteSingle(chance);
                        }
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
