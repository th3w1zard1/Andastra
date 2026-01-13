using System;
using System.IO;
using System.Text;
using BioWare.NET.Common;
using BioWare.NET.Resource;

namespace BioWare.NET.Resource.Formats.KEY
{
    // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/resource/formats/key/io_key.py:15-105
    // Original: class KEYBinaryReader(ResourceReader)
    public class KEYBinaryReader : IDisposable
    {
        private readonly BioWare.NET.Common.RawBinaryReader _reader;
        private KEY _key;

        public KEYBinaryReader(byte[] data, int offset = 0, int size = 0)
        {
            _reader = BioWare.NET.Common.RawBinaryReader.FromBytes(data, offset, size > 0 ? size : (int?)null);
            _key = new KEY();
        }

        public KEYBinaryReader(string filepath, int offset = 0, int size = 0)
        {
            _reader = BioWare.NET.Common.RawBinaryReader.FromFile(filepath, offset, size > 0 ? size : (int?)null);
            _key = new KEY();
        }

        public KEYBinaryReader(Stream source, int offset = 0, int size = 0)
        {
            _reader = BioWare.NET.Common.RawBinaryReader.FromStream(source, offset, size > 0 ? size : (int?)null);
            _key = new KEY();
        }

        public KEY Load(bool autoClose = true)
        {
            try
            {
                _key = new KEY();

                _key.FileType = _reader.ReadString(4);
                _key.FileVersion = _reader.ReadString(4);

                if (_key.FileType != KEY.FileTypeConst)
                {
                    throw new ArgumentException("Invalid KEY file type: " + _key.FileType);
                }
                if (_key.FileVersion != KEY.FileVersionConst && _key.FileVersion != "V1.1")
                {
                    throw new ArgumentException("Unsupported KEY version: " + _key.FileVersion);
                }

                int bifCount = (int)_reader.ReadUInt32();
                int keyCount = (int)_reader.ReadUInt32();
                int fileTableOffset = (int)_reader.ReadUInt32();
                int keyTableOffset = (int)_reader.ReadUInt32();

                _key.BuildYear = (int)_reader.ReadUInt32();
                _key.BuildDay = (int)_reader.ReadUInt32();

                _reader.Skip(32);

                _reader.Seek(fileTableOffset);
                for (int i = 0; i < bifCount; i++)
                {
                    BifEntry bif = new BifEntry();
                    bif.Filesize = (int)_reader.ReadUInt32();
                    int filenameOffset = (int)_reader.ReadUInt32();
                    ushort filenameSize = _reader.ReadUInt16();
                    bif.Drives = _reader.ReadUInt16();

                    int currentPos = _reader.Position;
                    _reader.Seek(filenameOffset);
                    string filename = _reader.ReadString(filenameSize).TrimEnd('\0').Replace("\\", "/").TrimStart('/');
                    bif.Filename = filename;
                    _reader.Seek(currentPos);

                    _key.BifEntries.Add(bif);
                }

                _reader.Seek(keyTableOffset);
                for (int i = 0; i < keyCount; i++)
                {
                    KeyEntry entry = new KeyEntry();
                    string resrefStr = _reader.ReadString(16).TrimEnd('\0').ToLowerInvariant();
                    entry.ResRef = new ResRef(resrefStr);
                    entry.ResType = ResourceType.FromId(_reader.ReadUInt16());
                    entry.ResourceId = _reader.ReadUInt32();
                    _key.KeyEntries.Add(entry);
                }

                _key.BuildLookupTables();
                return _key;
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
