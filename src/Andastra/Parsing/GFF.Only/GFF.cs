using System;
using System.Collections.Generic;
using Andastra.Parsing;
using JetBrains.Annotations;

namespace Andastra.Parsing.Formats.GFF
{

    /// <summary>
    /// Represents the data of a GFF file.
    /// </summary>
    public class GFF
    {
        public GFFContent Content { get; set; }
        public GFFStruct Root { get; set; }

        /// <summary>
        /// GFF file header containing file type, version, and section offsets/counts.
        /// </summary>
        public GFFHeader Header { get; set; }

        /// <summary>
        /// Array of all structs in the GFF file (read-only, populated during Load).
        /// </summary>
        public IReadOnlyList<GFFStruct> Structs { get; internal set; }

        /// <summary>
        /// Array of all field entries in the GFF file (read-only, populated during Load).
        /// Each field entry contains type, label index, and data/offset.
        /// </summary>
        public IReadOnlyList<GFFFieldEntry> Fields { get; internal set; }

        /// <summary>
        /// Array of all label strings in the GFF file (read-only, populated during Load).
        /// </summary>
        public IReadOnlyList<string> Labels { get; internal set; }

        /// <summary>
        /// Raw field data section bytes (read-only, populated during Load).
        /// </summary>
        public IReadOnlyList<byte> FieldData { get; internal set; }

        /// <summary>
        /// Field indices array bytes (read-only, populated during Load).
        /// </summary>
        public IReadOnlyList<byte> FieldIndices { get; internal set; }

        /// <summary>
        /// List indices array bytes (read-only, populated during Load).
        /// </summary>
        public IReadOnlyList<byte> ListIndices { get; internal set; }

        public GFF(GFFContent content = GFFContent.GFF)
        {
            Content = content;
            Root = new GFFStruct(-1);
            Header = new GFFHeader
            {
                FileType = content.ToFourCC(),
                FileVersion = "V3.2"
            };
            Structs = new List<GFFStruct>().AsReadOnly();
            Fields = new List<GFFFieldEntry>().AsReadOnly();
            Labels = new List<string>().AsReadOnly();
            FieldData = new List<byte>().AsReadOnly();
            FieldIndices = new List<byte>().AsReadOnly();
            ListIndices = new List<byte>().AsReadOnly();
        }

        /// <summary>
        /// Print a tree representation of the GFF structure (for debugging).
        /// </summary>
        public void PrintTree([CanBeNull] GFFStruct root = null, int indent = 0, int columnLen = 40)
        {
            if (root is null)
            {
                root = Root;
            }

            foreach ((string label, GFFFieldType fieldType, object value) in root)
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
                    foreach (GFFStruct item in listValue)
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

        // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/resource/formats/gff/gff_data.py:348-388
        // Original: def compare(self, other: object, log_func: Callable = print, path: PureWindowsPath | None = None, ignore_default_changes: bool = False, comparison_result: GFFComparisonResult | None = None) -> bool:
        public bool Compare(GFF other, Action<string> logFunc, string path = null, bool ignoreDefaultChanges = false, GFFComparisonResult comparisonResult = null)
        {
            if (other == null)
            {
                logFunc($"GFF counts have changed at '{path ?? "GFFRoot"}': '<unknown>' --> '<unknown>'");
                logFunc("");
                return false;
            }
            // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/resource/formats/gff/gff_data.py:382-386
            // Original: if len(self.root) != len(other.root): ... return is_same
            // Note: Python returns False immediately, but we allow comparison to continue to GFFStruct.Compare
            // which will handle ignore_default_changes properly for field-level differences
            if (Root.Count != other.Root.Count && !ignoreDefaultChanges)
            {
                logFunc($"GFF counts have changed at '{path ?? "GFFRoot"}': '{Root.Count}' --> '{other.Root.Count}'");
                logFunc("");
                return false;
            }
            comparisonResult = comparisonResult ?? new GFFComparisonResult();
            Dictionary<string, HashSet<object>> ignoreValues = null;
            // Root is Resource project's GFFStruct (since GFF.Only uses Resource types)
            // But we need to use GFF.Only's GFFComparisonResult, so we'll pass null and ignore the result
            // TODO: Implement proper conversion between GFF.Only and Resource GFFComparisonResult if needed
            return Root.Compare(other.Root, logFunc, path ?? "GFFRoot", ignoreDefaultChanges, ignoreValues, null);
        }
    }

    // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/resource/formats/gff/gff_data.py:253-292
    // Original: class GFFComparisonResult:
    public class GFFComparisonResult
    {
        public Dictionary<string, Dictionary<string, int>> FieldStats { get; } = new Dictionary<string, Dictionary<string, int>>
        {
            ["used"] = new Dictionary<string, int>(),
            ["missing"] = new Dictionary<string, int>(),
            ["extra"] = new Dictionary<string, int>(),
            ["mismatched"] = new Dictionary<string, int>()
        };

        public List<(string Path, int SourceId, int TargetId)> StructIdMismatches { get; } = new List<(string, int, int)>();
        public List<(string Path, int SourceCount, int TargetCount)> FieldCountMismatches { get; } = new List<(string, int, int)>();
        public List<(string Path, string FieldType, object SourceVal, object TargetVal)> ValueMismatches { get; } = new List<(string, string, object, object)>();

        public bool IsIdentical => StructIdMismatches.Count == 0 && FieldCountMismatches.Count == 0 && ValueMismatches.Count == 0;

        public void AddFieldStat(string category, string fieldName)
        {
            if (!FieldStats.ContainsKey(category))
            {
                FieldStats[category] = new Dictionary<string, int>();
            }
            if (!FieldStats[category].ContainsKey(fieldName))
            {
                FieldStats[category][fieldName] = 0;
            }
            FieldStats[category][fieldName]++;
        }

        public void AddStructIdMismatch(string path, int sourceId, int targetId)
        {
            StructIdMismatches.Add((path, sourceId, targetId));
        }

        public void AddFieldCountMismatch(string path, int sourceCount, int targetCount)
        {
            FieldCountMismatches.Add((path, sourceCount, targetCount));
        }

        public void AddValueMismatch(string path, string fieldType, object sourceVal, object targetVal)
        {
            ValueMismatches.Add((path, fieldType, sourceVal, targetVal));
        }
    }
}

