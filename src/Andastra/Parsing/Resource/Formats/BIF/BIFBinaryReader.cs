using System;
using System.IO;
using System.Text;
using Andastra.Parsing;
using Andastra.Parsing.Common;
using Andastra.Parsing.Formats;
using Andastra.Parsing.Resource;
using Andastra.Utility.LZMA;
using JetBrains.Annotations;

namespace Andastra.Parsing.Resource.Formats.BIF
{
    // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/resource/formats/bif/io_bif.py:49-161
    // Original: class BIFBinaryReader(ResourceReader):
    /// <summary>
    /// Reads BIF/BZF files.
    ///
    /// BIF (BioWare Index File) files contain game resources indexed by KEY files.
    /// BZF files are compressed BIF files using LZMA compression.
    /// </summary>
    public class BIFBinaryReader : BinaryFormatReaderBase
    {
        private BIF _bif;
        private int _varResCount;
        private int _fixedResCount;
        private int _dataOffset;

        public BIFBinaryReader(byte[] data) : base(data)
        {
        }

        public BIFBinaryReader(string filepath) : base(filepath)
        {
        }

        public BIFBinaryReader(Stream source) : base(source)
        {
        }

        // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/resource/formats/bif/io_bif.py:77-84
        // Original: def load(self, *, auto_close: bool = True) -> BIF:
        public BIF Load()
        {
            CheckSignature();
            ReadHeader();
            ReadResourceTable();
            ReadResourceData();
            return _bif;
        }

        // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/resource/formats/bif/io_bif.py:86-104
        // Original: def _check_signature(self) -> None:
        private void CheckSignature()
        {
            // vendor/reone/src/libs/resource/format/bifreader.cpp:26-30
            Reader.Seek(0);
            string signature = Encoding.ASCII.GetString(Reader.ReadBytes(8)); // "BIFFV1  " or "BZF V1.0"

            // Check file type
            string fileType = signature.Substring(0, 4);
            if (fileType == BIFTypeExtensions.ToFourCC(BIFType.BIF))
            {
                _bif = new BIF(BIFType.BIF);
            }
            else if (fileType == BIFTypeExtensions.ToFourCC(BIFType.BZF))
            {
                _bif = new BIF(BIFType.BZF);
            }
            else
            {
                throw new InvalidDataException($"Invalid BIF/BZF file type: {fileType}");
            }

            // Check version - PyKotor supports "V1  " and "V1.1", reone only checks "BIFFV1  "
            // vendor/reone/src/libs/resource/format/bifreader.cpp:27
            string version = signature.Substring(4);
            if (version != "V1  " && version != "V1.1")
            {
                throw new InvalidDataException($"Unsupported BIF/BZF version: {version}");
            }
        }

        // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/resource/formats/bif/io_bif.py:106-116
        // Original: def _read_header(self) -> None:
        private void ReadHeader()
        {
            _varResCount = (int)Reader.ReadUInt32();
            _fixedResCount = (int)Reader.ReadUInt32();
            _dataOffset = (int)Reader.ReadUInt32();

            // vendor/reone/src/libs/resource/format/bifreader.cpp:32-39
            // NOTE: reone reads fixed_res_count but doesn't use it. PyKotor explicitly rejects.
            if (_fixedResCount > 0)
            {
                throw new InvalidDataException("Fixed resources not supported");
            }
        }

        // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/resource/formats/bif/io_bif.py:118-142
        // Original: def _read_resource_table(self) -> None:
        private void ReadResourceTable()
        {
            Reader.Seek(_dataOffset);

            for (int i = 0; i < _varResCount; i++)
            {
                int keyId = (int)Reader.ReadUInt32();
                int offset = (int)Reader.ReadUInt32();
                int size = (int)Reader.ReadUInt32();
                ResourceType resType = ResourceType.FromId((int)Reader.ReadUInt32());

                // Create empty resource with placeholder data
                var resource = new BIFResource(ResRef.FromBlank(), resType, new byte[0], keyId, size);
                resource.Offset = offset;

                // For BZF, calculate packed size from offset differences
                if (_bif.BifType == BIFType.BZF && i > 0)
                {
                    BIFResource prevResource = _bif.Resources[_bif.Resources.Count - 1];
                    prevResource.PackedSize = offset - prevResource.Offset;
                }

                _bif.Resources.Add(resource);
            }

            // Set packed size for last resource in BZF
            if (_bif.BifType == BIFType.BZF && _bif.Resources.Count > 0)
            {
                BIFResource lastResource = _bif.Resources[_bif.Resources.Count - 1];
                int fileSize = Reader.Size;
                lastResource.PackedSize = fileSize - lastResource.Offset;
            }
        }

        // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/resource/formats/bif/io_bif.py:144-161
        // Original: def _read_resource_data(self) -> None:
        private void ReadResourceData()
        {
            foreach (BIFResource resource in _bif.Resources)
            {
                Reader.Seek(resource.Offset);

                if (_bif.BifType == BIFType.BZF)
                {
                    // For BZF, decompress the data
                    byte[] compressed = Reader.ReadBytes(resource.PackedSize);
                    try
                    {
                        resource.Data = DecompressBzfPayload(compressed, resource.Size);
                    }
                    catch (Exception ex)
                    {
                        throw new InvalidDataException($"Failed to decompress BZF resource: {ex.Message}", ex);
                    }
                }
                else
                {
                    // For BIF, read raw data
                    resource.Data = Reader.ReadBytes(resource.Size);
                }
            }

            _bif.BuildLookupTables();
        }

        // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/resource/formats/bif/io_bif.py:18-46
        // Original: def _decompress_bzf_payload(payload: bytes, expected_size: int) -> bytes:
        private byte[] DecompressBzfPayload(byte[] payload, int expectedSize)
        {
            return LzmaHelper.Decompress(payload, expectedSize);
        }
    }
}

