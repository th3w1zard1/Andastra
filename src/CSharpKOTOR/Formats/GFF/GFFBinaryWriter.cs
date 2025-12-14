using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using CSharpKOTOR.Common;

namespace CSharpKOTOR.Formats.GFF
{

    /// <summary>
    /// Writes GFF (General File Format) binary data.
    /// 1:1 port of Python GFFBinaryWriter from pykotor/resource/formats/gff/io_gff.py
    /// </summary>
    public class GFFBinaryWriter
    {
        private readonly GFF _gff;
        private readonly CSharpKOTOR.Common.RawBinaryWriter _structWriter;
        private readonly CSharpKOTOR.Common.RawBinaryWriter _fieldWriter;
        private readonly CSharpKOTOR.Common.RawBinaryWriter _fieldDataWriter;
        private readonly CSharpKOTOR.Common.RawBinaryWriter _fieldIndicesWriter;
        private readonly CSharpKOTOR.Common.RawBinaryWriter _listIndicesWriter;
        private readonly List<string> _labels = new List<string>();
        private int _structCount = 0;
        private int _fieldCount = 0;

        // Complex fields that are stored in the field data section
        private static readonly HashSet<GFFFieldType> _complexFields = new HashSet<GFFFieldType>
        {
            GFFFieldType.UInt64,
            GFFFieldType.Int64,
            GFFFieldType.Double,
            GFFFieldType.String,
            GFFFieldType.ResRef,
            GFFFieldType.LocalizedString,
            GFFFieldType.Binary,
            GFFFieldType.Vector3,
            GFFFieldType.Vector4
        };

        public GFFBinaryWriter(GFF gff)
        {
            _gff = gff;
            _structWriter = CSharpKOTOR.Common.RawBinaryWriter.ToByteArray();
            _fieldWriter = CSharpKOTOR.Common.RawBinaryWriter.ToByteArray();
            _fieldDataWriter = CSharpKOTOR.Common.RawBinaryWriter.ToByteArray();
            _fieldIndicesWriter = CSharpKOTOR.Common.RawBinaryWriter.ToByteArray();
            _listIndicesWriter = CSharpKOTOR.Common.RawBinaryWriter.ToByteArray();
        }

        public byte[] Write()
        {
            // Build all sections
            BuildStruct(_gff.Root);

            // Calculate offsets
            int structOffset = 56; // Header size
            int structCount = _structWriter.Size() / 12;
            int fieldOffset = structOffset + _structWriter.Size();
            int fieldCount = _fieldWriter.Size() / 12;
            int labelOffset = fieldOffset + _fieldWriter.Size();
            int labelCount = _labels.Count;
            int fieldDataOffset = labelOffset + _labels.Count * 16;
            int fieldDataCount = _fieldDataWriter.Size();
            int fieldIndicesOffset = fieldDataOffset + _fieldDataWriter.Size();
            int fieldIndicesCount = _fieldIndicesWriter.Size();
            int listIndicesOffset = fieldIndicesOffset + _fieldIndicesWriter.Size();
            int listIndicesCount = _listIndicesWriter.Size();

            // Write the file
            using (var ms = new MemoryStream())
            using (var writer = new BinaryWriter(ms))
            {
                // Write header
                writer.Write(Encoding.ASCII.GetBytes(_gff.Content.ToFourCC()));
                writer.Write(Encoding.ASCII.GetBytes("V3.2"));
                writer.Write((uint)structOffset);
                writer.Write((uint)structCount);
                writer.Write((uint)fieldOffset);
                writer.Write((uint)fieldCount);
                writer.Write((uint)labelOffset);
                writer.Write((uint)labelCount);
                writer.Write((uint)fieldDataOffset);
                writer.Write((uint)fieldDataCount);
                writer.Write((uint)fieldIndicesOffset);
                writer.Write((uint)fieldIndicesCount);
                writer.Write((uint)listIndicesOffset);
                writer.Write((uint)listIndicesCount);

                // Write all sections
                byte[] structData = _structWriter.Data();
                writer.Write(structData);

                byte[] fieldData = _fieldWriter.Data();
                writer.Write(fieldData);

                // Write labels (16 bytes each)
                foreach (string label in _labels)
                {
                    byte[] labelBytes = Encoding.ASCII.GetBytes(label.PadRight(16, '\0'));
                    writer.Write(labelBytes, 0, 16);
                }

                byte[] fieldDataSection = _fieldDataWriter.Data();
                writer.Write(fieldDataSection);

                byte[] fieldIndicesData = _fieldIndicesWriter.Data();
                writer.Write(fieldIndicesData);

                byte[] listIndicesData = _listIndicesWriter.Data();
                writer.Write(listIndicesData);

                return ms.ToArray();
            }
        }

        private void BuildStruct(GFFStruct gffStruct)
        {
            _structCount++;
            int structId = gffStruct.StructId;
            int fieldCount = gffStruct.Count;

            // Write struct ID (handle -1 as 0xFFFFFFFF)
            if (structId == -1)
            {
                _structWriter.Write(0xFFFFFFFFu);
            }
            else
            {
                _structWriter.Write(structId);
            }

            if (fieldCount == 0)
            {
                _structWriter.Write(0xFFFFFFFFu);
                _structWriter.Write(0u);
            }
            else if (fieldCount == 1)
            {
                _structWriter.Write((uint)_fieldCount);
                _structWriter.Write((uint)fieldCount);

                foreach ((string label, GFFFieldType fieldType, object value) in gffStruct)
                {
                    BuildField(label, value, fieldType);
                }
            }
            else if (fieldCount > 1)
            {
                WriteLargeStruct(fieldCount, gffStruct);
            }
        }

        private void WriteLargeStruct(int fieldCount, GFFStruct gffStruct)
        {
            _structWriter.Write((uint)_fieldIndicesWriter.Size());
            _structWriter.Write((uint)fieldCount);

            int pos = _fieldIndicesWriter.Position();
            _fieldIndicesWriter.Seek(_fieldIndicesWriter.Size());

            // Reserve space for field indices
            for (int i = 0; i < fieldCount; i++)
            {
                _fieldIndicesWriter.Write(0u);
            }

            int index = 0;
            foreach ((string label, GFFFieldType fieldType, object value) in gffStruct)
            {
                int currentPos = _fieldIndicesWriter.Position();
                _fieldIndicesWriter.Seek(pos + index * 4);
                _fieldIndicesWriter.Write((uint)_fieldCount);
                _fieldIndicesWriter.Seek(currentPos);
                BuildField(label, value, fieldType);
                index++;
            }
        }

        private void BuildList(GFFList gffList)
        {
            int pos = _listIndicesWriter.Position();
            _listIndicesWriter.Seek(_listIndicesWriter.Size());

            _listIndicesWriter.Write((uint)gffList.Count);
            int indexStartPos = _listIndicesWriter.Position();

            // Reserve space for struct indices
            for (int i = 0; i < gffList.Count; i++)
            {
                _listIndicesWriter.Write(0u);
            }

            int index = 0;
            foreach (GFFStruct gffStruct in gffList)
            {
                int currentPos = _listIndicesWriter.Position();
                _listIndicesWriter.Seek(indexStartPos + index * 4);
                _listIndicesWriter.Write((uint)_structCount);
                _listIndicesWriter.Seek(currentPos);
                BuildStruct(gffStruct);
                index++;
            }
        }

        private void BuildField(string label, object value, GFFFieldType fieldType)
        {
            _fieldCount++;
            uint fieldTypeId = (uint)fieldType;
            uint labelIndex = (uint)GetLabelIndex(label);

            _fieldWriter.Write(fieldTypeId);
            _fieldWriter.Write(labelIndex);

            if (_complexFields.Contains(fieldType))
            {
                _fieldWriter.Write((uint)_fieldDataWriter.Size());
                _fieldDataWriter.Seek(_fieldDataWriter.Size());

                switch (fieldType)
                {
                    case GFFFieldType.UInt64:
                        _fieldDataWriter.Write(Convert.ToUInt64(value));
                        break;
                    case GFFFieldType.Int64:
                        _fieldDataWriter.Write(Convert.ToInt64(value));
                        break;
                    case GFFFieldType.Double:
                        _fieldDataWriter.Write(Convert.ToDouble(value));
                        break;
                    case GFFFieldType.String:
                        string str = value.ToString() ?? "";
                        byte[] strBytes = Encoding.ASCII.GetBytes(str);
                        _fieldDataWriter.Write((uint)strBytes.Length);
                        _fieldDataWriter.Write(strBytes);
                        break;
                    case GFFFieldType.ResRef:
                        string resrefStr = value.ToString() ?? "";
                        byte[] resrefBytes = Encoding.ASCII.GetBytes(resrefStr);
                        _fieldDataWriter.Write((byte)resrefBytes.Length);
                        _fieldDataWriter.Write(resrefBytes);
                        break;
                    case GFFFieldType.LocalizedString:
                        _fieldDataWriter.WriteLocalizedString((LocalizedString)value);
                        break;
                    case GFFFieldType.Binary:
                        byte[] binaryData = (byte[])value;
                        _fieldDataWriter.Write((uint)binaryData.Length);
                        _fieldDataWriter.Write(binaryData);
                        break;
                    case GFFFieldType.Vector4:
                        _fieldDataWriter.WriteVector4((Vector4)value);
                        break;
                    case GFFFieldType.Vector3:
                        _fieldDataWriter.WriteVector3((Vector3)value);
                        break;
                }
            }
            else if (fieldType == GFFFieldType.Struct)
            {
                _fieldWriter.Write((uint)_structCount);
                BuildStruct((GFFStruct)value);
            }
            else if (fieldType == GFFFieldType.List)
            {
                _fieldWriter.Write((uint)_listIndicesWriter.Size());
                BuildList((GFFList)value);
            }
            else
            {
                // Simple types (stored inline)
                switch (fieldType)
                {
                    case GFFFieldType.UInt8:
                        uint uint8Val = Convert.ToUInt32(value);
                        _fieldWriter.Write(uint8Val == 0xFFFFFFFF ? 0xFFFFFFFFu : uint8Val);
                        break;
                    case GFFFieldType.Int8:
                        _fieldWriter.Write(Convert.ToInt32(value));
                        break;
                    case GFFFieldType.UInt16:
                        uint uint16Val = Convert.ToUInt32(value);
                        _fieldWriter.Write(uint16Val == 0xFFFFFFFF ? 0xFFFFFFFFu : uint16Val);
                        break;
                    case GFFFieldType.Int16:
                        _fieldWriter.Write(Convert.ToInt32(value));
                        break;
                    case GFFFieldType.UInt32:
                        uint uint32Val = Convert.ToUInt32(value);
                        _fieldWriter.Write(uint32Val == 0xFFFFFFFF ? 0xFFFFFFFFu : uint32Val);
                        break;
                    case GFFFieldType.Int32:
                        _fieldWriter.Write(Convert.ToInt32(value));
                        break;
                    case GFFFieldType.Single:
                        _fieldWriter.Write(Convert.ToSingle(value));
                        break;
                    default:
                        throw new InvalidOperationException($"Unknown field type '{fieldType}'");
                }
            }
        }

        private int GetLabelIndex(string label)
        {
            int index = _labels.IndexOf(label);
            if (index >= 0)
            {
                return index;
            }

            _labels.Add(label);
            return _labels.Count - 1;
        }
    }
}

