using System;

namespace TSLPatcher.Core.Formats.GFF;

/// <summary>
/// Represents the data of a GFF file.
/// </summary>
public class GFF
{
    public GFFContent Content { get; set; }
    public GFFStruct Root { get; set; }

    public GFF(GFFContent content = GFFContent.GFF)
    {
        Content = content;
        Root = new GFFStruct(-1);
    }

    /// <summary>
    /// Print a tree representation of the GFF structure (for debugging).
    /// </summary>
    public void PrintTree(GFFStruct? root = null, int indent = 0, int columnLen = 40)
    {
        root ??= Root;

        foreach (var (label, fieldType, value) in root)
        {
            int lengthOrId = -2;

            if (fieldType == GFFFieldType.Struct && value is GFFStruct gffStruct)
            {
                lengthOrId = gffStruct.StructId;
            }
            else if (fieldType == GFFFieldType.List && value is GFFList gffList)
            {
                lengthOrId = gffList.Count;
            }

            string indentStr = new string(' ', indent * 2);
            string labelStr = (indentStr + label).PadRight(columnLen);
            Console.WriteLine($"{labelStr}  {lengthOrId}");

            if (fieldType == GFFFieldType.Struct && value is GFFStruct structValue)
            {
                PrintTree(structValue, indent + 1, columnLen);
            }
            else if (fieldType == GFFFieldType.List && value is GFFList listValue)
            {
                int i = 0;
                foreach (var item in listValue)
                {
                    string listIndentStr = new string(' ', indent * 2);
                    string listLabelStr = $"  {listIndentStr}[Struct {i}]".PadRight(columnLen);
                    Console.WriteLine($"{listLabelStr}  {item.StructId}");
                    PrintTree(item, indent + 2, columnLen);
                    i++;
                }
            }
        }
    }

    /// <summary>
    /// Serializes the GFF to a byte array.
    /// </summary>
    public byte[] ToBytes()
    {
        var writer = new GFFBinaryWriter(this);
        return writer.Write();
    }

    /// <summary>
    /// Deserializes a GFF from a byte array.
    /// </summary>
    public static GFF FromBytes(byte[] data)
    {
        var reader = new GFFBinaryReader(data);
        return reader.Load();
    }
}

