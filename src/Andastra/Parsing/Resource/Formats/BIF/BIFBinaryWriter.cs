using System;
using System.IO;
using System.Text;
using Andastra.Parsing;
using Andastra.Parsing.Common;
using Andastra.Parsing.Resource;
using Andastra.Utility.LZMA;

namespace Andastra.Parsing.Resource.Formats.BIF
{
    // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/resource/formats/bif/io_bif.py:164-238
    // Original: class BIFBinaryWriter(ResourceWriter):
    /// <summary>
    /// Writes BIF/BZF files.
    /// </summary>
    public class BIFBinaryWriter
    {
        private readonly BIF _bif;

        public BIFBinaryWriter(BIF bif)
        {
            _bif = bif ?? throw new ArgumentNullException(nameof(bif));
        }

        // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/resource/formats/bif/io_bif.py:176-181
        // Original: def write(self, *, auto_close: bool = True) -> None:
        public byte[] Write()
        {
            using (var ms = new MemoryStream())
            {
                Write(ms);
                return ms.ToArray();
            }
        }

        public void Write(Stream stream)
        {
            using (var writer = new System.IO.BinaryWriter(stream, Encoding.ASCII, true))
            {
                WriteHeader(writer);
                WriteResourceTable(writer);
                WriteResourceData(writer);
            }
        }

        // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/resource/formats/bif/io_bif.py:183-192
        // Original: def _write_header(self) -> None:
        private void WriteHeader(System.IO.BinaryWriter writer)
        {
            // Write signature
            writer.Write(Encoding.ASCII.GetBytes(BIFTypeExtensions.ToFourCC(_bif.BifType)));
            writer.Write(Encoding.ASCII.GetBytes(BIF.FileVersion));

            // Write counts and offset to resource table (always right after header)
            writer.Write((uint)_bif.VarCount);
            writer.Write((uint)_bif.FixedCount);
            writer.Write((uint)BIF.HeaderSize); // Offset to variable resource table (20 bytes)
        }

        // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/resource/formats/bif/io_bif.py:194-220
        // Original: def _write_resource_table(self) -> None:
        private void WriteResourceTable(System.IO.BinaryWriter writer)
        {
            // Calculate absolute file offsets for resource data
            // Data section starts after header and resource table
            int dataSectionOffset = BIF.HeaderSize + (_bif.VarCount * BIF.VarEntrySize);
            int currentOffset = dataSectionOffset;

            foreach (BIFResource resource in _bif.Resources)
            {
                // Align resource data to 4-byte boundary
                if (currentOffset % 4 != 0)
                {
                    currentOffset += 4 - (currentOffset % 4);
                }
                resource.Offset = currentOffset;

                if (_bif.BifType == BIFType.BZF)
                {
                    // For BZF, compress the data to get size using raw LZMA1 format
                    byte[] compressed = LzmaHelper.Compress(resource.Data);
                    resource.PackedSize = compressed.Length;
                    currentOffset += resource.PackedSize;
                }
                else
                {
                    currentOffset += resource.Size;
                }
            }

            // Write resource table entries with absolute file offsets
            foreach (BIFResource resource in _bif.Resources)
            {
                writer.Write((uint)resource.ResnameKeyIndex);
                writer.Write((uint)resource.Offset); // Absolute file offset
                writer.Write((uint)resource.Size);
                writer.Write((uint)resource.ResType.TypeId);
            }
        }

        // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/resource/formats/bif/io_bif.py:222-238
        // Original: def _write_resource_data(self) -> None:
        private void WriteResourceData(System.IO.BinaryWriter writer)
        {
            foreach (BIFResource resource in _bif.Resources)
            {
                // Align to 4-byte boundary
                long currentPos = writer.BaseStream.Position;
                int calc = (int)(currentPos % 4);
                if (calc != 0)
                {
                    writer.Write(new byte[4 - calc]);
                }

                if (_bif.BifType == BIFType.BZF)
                {
                    // Write compressed data for BZF using raw LZMA1 format
                    byte[] compressed = LzmaHelper.Compress(resource.Data);
                    writer.Write(compressed);
                }
                else
                {
                    // Write raw data for BIF
                    writer.Write(resource.Data);
                }
            }
        }
    }
}
