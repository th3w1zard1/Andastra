using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using BioWareCSharp.Common;
using BioWareCSharp.Common.LZMA;
using BioWareCSharp.Common.Extract;
using JetBrains.Annotations;

namespace BioWareCSharp.Common.Extract
{
    // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/extract/bzf.py:12-15
    // Original: BZF_ID = b"BIFF", VERSION_1 = b"V1  "
    internal static class BZFConstants
    {
        public static readonly byte[] BzfId = { 0x42, 0x49, 0x46, 0x46 }; // "BIFF"
        public static readonly byte[] Version1 = { 0x56, 0x31, 0x20, 0x20 }; // "V1  "
    }

    // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/extract/bzf.py:17-22
    // Original: @dataclass class IResource:
    public class IResource
    {
        public int Offset { get; set; }
        public int Size { get; set; }
        public int Type { get; set; }
        public int PackedSize { get; set; }
    }

    // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/extract/bzf.py:25-29
    // Original: @dataclass class Resource:
    public class BZFResource
    {
        public string Name { get; set; } = "";
        public int Type { get; set; }
        public int Index { get; set; }
    }

    // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/extract/bzf.py:32-135
    // Original: class BZFFile:
    /// <summary>
    /// Reads BZF (compressed BIF) files.
    ///
    /// BZF files are LZMA-compressed BIF archives used in KotOR. This class handles
    /// decompression and resource indexing for compressed BIF files.
    ///
    /// References:
    /// ----------
    ///     vendor/reone/src/libs/resource/format/bifreader.cpp (BIF/BZF reading)
    ///     vendor/xoreos-tools/src/unkeybif.cpp (BIF/BZF extraction)
    ///     vendor/KotOR-Bioware-Libs/BIF.pm (Perl BIF/BZF implementation)
    ///
    /// Missing Features:
    /// ----------------
    ///     - Fixed resources not yet supported (see line 67)
    /// </summary>
    public class BZFFile
    {
        private readonly Stream _bzf;
        private readonly List<BZFResource> _resources = new List<BZFResource>();
        private readonly List<IResource> _iResources = new List<IResource>();
        private byte[] _id;
        private byte[] _version;

        // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/extract/bzf.py:48-52
        // Original: def __init__(self, bzf: BinaryIO):
        public BZFFile(Stream bzf)
        {
            _bzf = bzf ?? throw new ArgumentNullException(nameof(bzf));
            Load(bzf);
        }

        // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/extract/bzf.py:54-73
        // Original: def load(self, bzf: BinaryIO):
        private void Load(Stream bzf)
        {
            ReadHeader(bzf);

            if (!_id.SequenceEqual(BZFConstants.BzfId))
            {
                string idStr = System.Text.Encoding.ASCII.GetString(_id);
                throw new ArgumentException($"Not a BZF file ({idStr})");
            }

            if (!_version.SequenceEqual(BZFConstants.Version1))
            {
                string versionStr = System.Text.Encoding.ASCII.GetString(_version);
                throw new ArgumentException($"Unsupported BZF file version {versionStr}");
            }

            byte[] varResCountBytes = new byte[4];
            bzf.Read(varResCountBytes, 0, 4);
            int varResCount = BitConverter.ToInt32(varResCountBytes, 0);
            if (BitConverter.IsLittleEndian == false)
            {
                Array.Reverse(varResCountBytes);
                varResCount = BitConverter.ToInt32(varResCountBytes, 0);
            }

            byte[] fixResCountBytes = new byte[4];
            bzf.Read(fixResCountBytes, 0, 4);
            int fixResCount = BitConverter.ToInt32(fixResCountBytes, 0);
            if (BitConverter.IsLittleEndian == false)
            {
                Array.Reverse(fixResCountBytes);
                fixResCount = BitConverter.ToInt32(fixResCountBytes, 0);
            }

            if (fixResCount != 0)
            {
                throw new NotImplementedException("Fixed BZF resources are not supported yet");
            }

            _iResources.Clear();
            for (int i = 0; i < varResCount; i++)
            {
                _iResources.Add(new IResource());
            }

            byte[] offVarResTableBytes = new byte[4];
            bzf.Read(offVarResTableBytes, 0, 4);
            int offVarResTable = BitConverter.ToInt32(offVarResTableBytes, 0);
            if (BitConverter.IsLittleEndian == false)
            {
                Array.Reverse(offVarResTableBytes);
                offVarResTable = BitConverter.ToInt32(offVarResTableBytes, 0);
            }

            ReadVarResTable(bzf, offVarResTable);
        }

        // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/extract/bzf.py:75-77
        // Original: def _read_header(self, bzf: BinaryIO):
        private void ReadHeader(Stream bzf)
        {
            _id = new byte[4];
            bzf.Read(_id, 0, 4);
            _version = new byte[4];
            bzf.Read(_version, 0, 4);
        }

        // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/extract/bzf.py:79-93
        // Original: def _read_var_res_table(self, bzf: BinaryIO, offset: int):
        private void ReadVarResTable(Stream bzf, int offset)
        {
            bzf.Seek(offset, SeekOrigin.Begin);

            for (int i = 0; i < _iResources.Count; i++)
            {
                bzf.Seek(4, SeekOrigin.Current); // Skip ID

                byte[] offsetBytes = new byte[4];
                bzf.Read(offsetBytes, 0, 4);
                int resourceOffset = BitConverter.ToInt32(offsetBytes, 0);
                if (BitConverter.IsLittleEndian == false)
                {
                    Array.Reverse(offsetBytes);
                    resourceOffset = BitConverter.ToInt32(offsetBytes, 0);
                }

                byte[] sizeBytes = new byte[4];
                bzf.Read(sizeBytes, 0, 4);
                int resourceSize = BitConverter.ToInt32(sizeBytes, 0);
                if (BitConverter.IsLittleEndian == false)
                {
                    Array.Reverse(sizeBytes);
                    resourceSize = BitConverter.ToInt32(sizeBytes, 0);
                }

                byte[] typeBytes = new byte[4];
                bzf.Read(typeBytes, 0, 4);
                int resourceType = BitConverter.ToInt32(typeBytes, 0);
                if (BitConverter.IsLittleEndian == false)
                {
                    Array.Reverse(typeBytes);
                    resourceType = BitConverter.ToInt32(typeBytes, 0);
                }

                _iResources[i].Offset = resourceOffset;
                _iResources[i].Size = resourceSize;
                _iResources[i].Type = resourceType;

                if (i > 0)
                {
                    _iResources[i - 1].PackedSize = _iResources[i].Offset - _iResources[i - 1].Offset;
                }
            }

            if (_iResources.Count > 0)
            {
                long endPosition = bzf.Seek(0, SeekOrigin.End);
                _iResources[_iResources.Count - 1].PackedSize = (int)(endPosition - _iResources[_iResources.Count - 1].Offset);
            }
        }

        // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/extract/bzf.py:95-114
        // Original: def merge_KEY(self, key: KEYFile, data_file_index: int):
        public void MergeKey(KeyFileWrapper key, int dataFileIndex)
        {
            var keyResList = key.GetResources();

            foreach (var keyRes in keyResList)
            {
                if (keyRes.BifIndex != dataFileIndex)
                {
                    continue;
                }

                if (keyRes.ResIndex >= _iResources.Count)
                {
                    Console.WriteLine($"Resource index out of range ({keyRes.ResIndex}/{_iResources.Count})");
                    continue;
                }

                if (keyRes.Type != _iResources[keyRes.ResIndex].Type)
                {
                    Console.WriteLine($"KEY and BZF disagree on the type of the resource \"{keyRes.Name}\" ({keyRes.Type}, {_iResources[keyRes.ResIndex].Type}). Trusting the BZF");
                }

                var res = new BZFResource
                {
                    Name = keyRes.Name,
                    Type = _iResources[keyRes.ResIndex].Type,
                    Index = keyRes.ResIndex
                };

                _resources.Add(res);
            }
        }

        // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/extract/bzf.py:116-117
        // Original: def get_internal_resource_count(self) -> int:
        public int GetInternalResourceCount()
        {
            return _iResources.Count;
        }

        // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/extract/bzf.py:119-120
        // Original: def get_resources(self) -> list[Resource]:
        public List<BZFResource> GetResources()
        {
            return _resources;
        }

        // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/extract/bzf.py:122-125
        // Original: def get_iresource(self, index: int) -> IResource:
        public IResource GetIResource(int index)
        {
            if (index >= _iResources.Count)
            {
                throw new IndexOutOfRangeException($"Resource index out of range ({index}/{_iResources.Count})");
            }
            return _iResources[index];
        }

        // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/extract/bzf.py:127-128
        // Original: def get_resource_size(self, index: int) -> int:
        public int GetResourceSize(int index)
        {
            return GetIResource(index).Size;
        }

        // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/extract/bzf.py:130-134
        // Original: def get_resource(self, index: int) -> bytes:
        public byte[] GetResource(int index)
        {
            IResource res = GetIResource(index);
            _bzf.Seek(res.Offset, SeekOrigin.Begin);
            byte[] compressedData = new byte[res.PackedSize];
            int bytesRead = _bzf.Read(compressedData, 0, res.PackedSize);
            if (bytesRead < res.PackedSize)
            {
                Array.Resize(ref compressedData, bytesRead);
            }
            return LzmaHelper.Decompress(compressedData, res.Size);
        }
    }
}

