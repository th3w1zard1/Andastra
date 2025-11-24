using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using TSLPatcher.Core.Common;
using TSLPatcher.Core.Formats;

namespace TSLPatcher.Core.Formats.GFF;

/// <summary>
/// Reads GFF (General File Format) binary data.
/// 1:1 port of Python GFFBinaryReader from pykotor/resource/formats/gff/io_gff.py
/// </summary>
public class GFFBinaryReader : BinaryFormatReaderBase
{
    private GFF? _gff;
    private List<string> _labels = new();
    private int _fieldDataOffset;
    private int _fieldIndicesOffset;
    private int _listIndicesOffset;
    private int _structOffset;
    private int _fieldOffset;

    // Complex fields that are stored in the field data section
    private static readonly HashSet<GFFFieldType> _complexFields = new()
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

    public GFFBinaryReader(byte[] data) : base(data)
    {
    }

    public GFFBinaryReader(string filepath) : base(filepath)
    {
    }

    public GFFBinaryReader(Stream source) : base(source)
    {
    }

    public GFF Load()
    {
        try
        {
            _gff = new GFF();

            Reader.BaseStream.Seek(0, SeekOrigin.Begin);

            // Read header
            string fileType = Encoding.ASCII.GetString(Reader.ReadBytes(4));
            string fileVersion = Encoding.ASCII.GetString(Reader.ReadBytes(4));

            // Validate content type
            if (!IsValidGFFContent(fileType))
            {
                throw new InvalidDataException("Not a valid binary GFF file.");
            }

            if (fileVersion != "V3.2")
            {
                throw new InvalidDataException("The GFF version of the file is unsupported.");
            }

            _gff.Content = GFFContentExtensions.FromFourCC(fileType);

            _structOffset = (int)Reader.ReadUInt32();
            Reader.ReadUInt32(); // struct count (unused during reading)
            _fieldOffset = (int)Reader.ReadUInt32();
            Reader.ReadUInt32(); // field count (unused)
            int labelOffset = (int)Reader.ReadUInt32();
            int labelCount = (int)Reader.ReadUInt32();
            _fieldDataOffset = (int)Reader.ReadUInt32();
            Reader.ReadUInt32(); // field data count (unused)
            _fieldIndicesOffset = (int)Reader.ReadUInt32();
            Reader.ReadUInt32(); // field indices count (unused)
            _listIndicesOffset = (int)Reader.ReadUInt32();
            Reader.ReadUInt32(); // list indices count (unused)

            // Read labels
            _labels = new List<string>();
            Reader.BaseStream.Seek(labelOffset, SeekOrigin.Begin);
            for (int i = 0; i < labelCount; i++)
            {
                string label = Encoding.ASCII.GetString(Reader.ReadBytes(16)).TrimEnd('\0');
                _labels.Add(label);
            }

            // Load root struct
            LoadStruct(_gff.Root, 0);

            return _gff;
        }
        catch (EndOfStreamException)
        {
            throw new InvalidDataException("Invalid GFF file format - unexpected end of file.");
        }
    }

    private static bool IsValidGFFContent(string fourCC)
    {
        // Check if fourCC matches any GFFContent enum value
        string trimmedFourCC = fourCC.Trim();
        return Enum.TryParse<GFFContent>(trimmedFourCC, ignoreCase: true, out _);
    }

    private void LoadStruct(GFFStruct gffStruct, int structIndex)
    {
        Reader.BaseStream.Seek(_structOffset + structIndex * 12, SeekOrigin.Begin);

        int structId = Reader.ReadInt32();
        uint data = Reader.ReadUInt32();
        uint fieldCount = Reader.ReadUInt32();

        gffStruct.StructId = structId;

        if (fieldCount == 1)
        {
            LoadField(gffStruct, (int)data);
        }
        else if (fieldCount > 1)
        {
            Reader.BaseStream.Seek(_fieldIndicesOffset + data, SeekOrigin.Begin);
            var indices = new List<int>();
            for (int i = 0; i < fieldCount; i++)
            {
                indices.Add((int)Reader.ReadUInt32());
            }

            foreach (int index in indices)
            {
                LoadField(gffStruct, index);
            }
        }
    }

    private void LoadField(GFFStruct gffStruct, int fieldIndex)
    {
        Reader.BaseStream.Seek(_fieldOffset + fieldIndex * 12, SeekOrigin.Begin);

        uint fieldTypeId = Reader.ReadUInt32();
        uint labelId = Reader.ReadUInt32();

        var fieldType = (GFFFieldType)fieldTypeId;
        string label = _labels[(int)labelId];

        if (_complexFields.Contains(fieldType))
        {
            uint offset = Reader.ReadUInt32(); // Relative to field data
            Reader.BaseStream.Seek(_fieldDataOffset + offset, SeekOrigin.Begin);

            switch (fieldType)
            {
                case GFFFieldType.UInt64:
                    gffStruct.SetUInt64(label, Reader.ReadUInt64());
                    break;
                case GFFFieldType.Int64:
                    gffStruct.SetInt64(label, Reader.ReadInt64());
                    break;
                case GFFFieldType.Double:
                    gffStruct.SetDouble(label, Reader.ReadDouble());
                    break;
                case GFFFieldType.String:
                    uint stringLength = Reader.ReadUInt32();
                    string str = Encoding.ASCII.GetString(Reader.ReadBytes((int)stringLength)).TrimEnd('\0');
                    gffStruct.SetString(label, str);
                    break;
                case GFFFieldType.ResRef:
                    byte resrefLength = Reader.ReadByte();
                    string resrefStr = Encoding.ASCII.GetString(Reader.ReadBytes(resrefLength)).Trim();
                    gffStruct.SetResRef(label, new ResRef(resrefStr));
                    break;
                case GFFFieldType.LocalizedString:
                    gffStruct.SetLocString(label, Reader.ReadLocalizedString());
                    break;
                case GFFFieldType.Binary:
                    uint binaryLength = Reader.ReadUInt32();
                    gffStruct.SetBinary(label, Reader.ReadBytes((int)binaryLength));
                    break;
                case GFFFieldType.Vector3:
                    gffStruct.SetVector3(label, Reader.ReadVector3());
                    break;
                case GFFFieldType.Vector4:
                    gffStruct.SetVector4(label, Reader.ReadVector4());
                    break;
            }
        }
        else if (fieldType == GFFFieldType.Struct)
        {
            uint structIndex = Reader.ReadUInt32();
            var newStruct = new GFFStruct();
            LoadStruct(newStruct, (int)structIndex);
            gffStruct.SetStruct(label, newStruct);
        }
        else if (fieldType == GFFFieldType.List)
        {
            LoadList(gffStruct, label);
        }
        else
        {
            // Simple types (stored inline in the field data)
            switch (fieldType)
            {
                case GFFFieldType.UInt8:
                    gffStruct.SetUInt8(label, Reader.ReadByte());
                    break;
                case GFFFieldType.Int8:
                    gffStruct.SetInt8(label, Reader.ReadSByte());
                    break;
                case GFFFieldType.UInt16:
                    gffStruct.SetUInt16(label, Reader.ReadUInt16());
                    break;
                case GFFFieldType.Int16:
                    gffStruct.SetInt16(label, Reader.ReadInt16());
                    break;
                case GFFFieldType.UInt32:
                    gffStruct.SetUInt32(label, Reader.ReadUInt32());
                    break;
                case GFFFieldType.Int32:
                    gffStruct.SetInt32(label, Reader.ReadInt32());
                    break;
                case GFFFieldType.Single:
                    gffStruct.SetSingle(label, Reader.ReadSingle());
                    break;
            }
        }
    }

    private void LoadList(GFFStruct gffStruct, string label)
    {
        uint offset = Reader.ReadUInt32(); // Relative to list indices
        Reader.BaseStream.Seek(_listIndicesOffset + offset, SeekOrigin.Begin);

        var value = new GFFList();
        uint count = Reader.ReadUInt32();
        var listIndices = new List<int>();

        for (int i = 0; i < count; i++)
        {
            listIndices.Add((int)Reader.ReadUInt32());
        }

        foreach (int structIndex in listIndices)
        {
            value.Add(0);
            GFFStruct? child = value.At(value.Count - 1);
            if (child != null)
            {
                LoadStruct(child, structIndex);
            }
        }

        gffStruct.SetList(label, value);
    }
}

