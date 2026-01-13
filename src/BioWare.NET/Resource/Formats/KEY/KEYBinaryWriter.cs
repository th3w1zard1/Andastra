using System;
using System.IO;
using System.Text;
using BioWare.NET.Common;
using BioWare.NET.Resource;

namespace BioWare.NET.Resource.Formats.KEY
{
    // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/resource/formats/key/io_key.py:108-171
    // Original: class KEYBinaryWriter(ResourceWriter)
    public class KEYBinaryWriter : IDisposable
    {
        private readonly KEY _key;
        private readonly BioWare.NET.Common.RawBinaryWriter _writer;

        public KEYBinaryWriter(KEY key, string filepath)
        {
            _key = key ?? throw new ArgumentNullException(nameof(key));
            _writer = BioWare.NET.Common.RawBinaryWriter.ToFile(filepath);
        }

        public KEYBinaryWriter(KEY key, Stream target)
        {
            _key = key ?? throw new ArgumentNullException(nameof(key));
            _writer = BioWare.NET.Common.RawBinaryWriter.ToStream(target);
        }

        public KEYBinaryWriter(KEY key)
        {
            _key = key ?? throw new ArgumentNullException(nameof(key));
            _writer = BioWare.NET.Common.RawBinaryWriter.ToByteArray();
        }

        public void Write(bool autoClose = true)
        {
            try
            {
                WriteHeader();
                WriteFileTable();
                WriteKeyTable();
            }
            finally
            {
                if (autoClose)
                {
                    Dispose();
                }
            }
        }

        private void WriteHeader()
        {
            _writer.WriteString(_key.FileType, Encoding.ASCII.WebName);
            _writer.WriteString(_key.FileVersion, Encoding.ASCII.WebName);

            _writer.WriteUInt32((uint)_key.BifEntries.Count);
            _writer.WriteUInt32((uint)_key.KeyEntries.Count);
            _writer.WriteUInt32((uint)_key.CalculateFileTableOffset());
            _writer.WriteUInt32((uint)_key.CalculateKeyTableOffset());

            _writer.WriteUInt32((uint)_key.BuildYear);
            _writer.WriteUInt32((uint)_key.BuildDay);

            _writer.WriteBytes(new byte[32]);
        }

        private void WriteFileTable()
        {
            for (int i = 0; i < _key.BifEntries.Count; i++)
            {
                BifEntry bif = _key.BifEntries[i];
                _writer.WriteUInt32((uint)bif.Filesize);
                _writer.WriteUInt32((uint)_key.CalculateFilenameOffset(i));
                _writer.WriteUInt16((ushort)(bif.Filename.Length + 1));
                _writer.WriteUInt16(bif.Drives);
            }

            foreach (var bif in _key.BifEntries)
            {
                _writer.WriteString(bif.Filename, Encoding.ASCII.WebName);
                _writer.WriteUInt8(0);
            }
        }

        private void WriteKeyTable()
        {
            foreach (var entry in _key.KeyEntries)
            {
                string resref = entry.ResRef.ToString();
                if (resref.Length > ResRef.MaxLength)
                {
                    resref = resref.Substring(0, ResRef.MaxLength);
                }
                _writer.WriteString(resref, Encoding.ASCII.WebName);
                if (resref.Length < ResRef.MaxLength)
                {
                    _writer.WriteBytes(new byte[ResRef.MaxLength - resref.Length]);
                }

                _writer.WriteUInt16((ushort)entry.ResType.TypeId);
                _writer.WriteUInt32(entry.ResourceId);
            }
        }

        public void Dispose()
        {
            _writer?.Dispose();
        }
    }
}
