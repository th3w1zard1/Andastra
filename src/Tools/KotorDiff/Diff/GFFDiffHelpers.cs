// Matching PyKotor implementation at vendor/PyKotor/Libraries/PyKotor/src/pykotor/tslpatcher/diff/gff.py:9-77
// Original: def flatten_differences, build_hierarchy, serialize_to_ini: ...
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using BioWare.NET.Diff;
using GffCompareResult = BioWare.NET.Diff.GffCompareResult;

namespace KotorDiff.Diff
{
    /// <summary>
    /// Helper functions for GFF diff processing.
    /// 1:1 port of gff.py from vendor/PyKotor/Libraries/PyKotor/src/pykotor/tslpatcher/diff/gff.py:9-77
    /// </summary>
    public static class GFFDiffHelpers
    {
        /// <summary>
        /// Flattens the differences from GffCompareResult into a flat dictionary.
        /// Matching PyKotor implementation at vendor/PyKotor/Libraries/PyKotor/src/pykotor/tslpatcher/diff/gff.py:9-18
        /// </summary>
        public static Dictionary<string, object> FlattenDifferences(GffCompareResult compareResult)
        {
            var flatChanges = new Dictionary<string, object>();
            var differences = compareResult.Differences;

            foreach (var diff in differences)
            {
                string pathStr = diff.Path.Replace("\\", "/"); // Use forward slashes for INI compatibility
                flatChanges[pathStr] = diff.NewValue ?? (object)null;
            }

            return flatChanges;
        }

        /// <summary>
        /// Build a hierarchical structure suitable for INI serialization from flat changes.
        /// Matching PyKotor implementation at vendor/PyKotor/Libraries/PyKotor/src/pykotor/tslpatcher/diff/gff.py:21-32
        /// </summary>
        public static Dictionary<string, object> BuildHierarchy(Dictionary<string, object> flatChanges)
        {
            var hierarchy = new Dictionary<string, object>();

            foreach (var kvp in flatChanges)
            {
                string path = kvp.Key;
                object value = kvp.Value;

                string[] parts = path.Split('/');
                var currentLevel = hierarchy;

                for (int i = 0; i < parts.Length - 1; i++)
                {
                    string part = parts[i];
                    if (!currentLevel.ContainsKey(part))
                    {
                        currentLevel[part] = new Dictionary<string, object>();
                    }
                    currentLevel = currentLevel[part] as Dictionary<string, object>;
                }

                currentLevel[parts[parts.Length - 1]] = value;
            }

            return hierarchy;
        }

        /// <summary>
        /// Serializes a hierarchical dictionary into an INI-formatted string.
        /// Matching PyKotor implementation at vendor/PyKotor/Libraries/PyKotor/src/pykotor/tslpatcher/diff/gff.py:35-66
        /// </summary>
        public static string SerializeToIni(Dictionary<string, object> hierarchy)
        {
            var iniLines = new List<string>();

            SerializeSection("", hierarchy, 0, iniLines);

            return string.Join("\n", iniLines);
        }

        private static void SerializeSection(
            string name,
            Dictionary<string, object> content,
            int indentLevel,
            List<string> iniLines)
        {
            string prefix = new string(' ', indentLevel * 4);

            if (indentLevel == 0 && !string.IsNullOrEmpty(name))
            {
                iniLines.Add($"[{name}]");
            }
            else if (indentLevel > 0 && !string.IsNullOrEmpty(name))
            {
                iniLines.Add($"{prefix}{name}=");
            }

            foreach (var kvp in content)
            {
                string key = kvp.Key;
                object value = kvp.Value;

                if (value is Dictionary<string, object> nestedDict)
                {
                    SerializeSection(key, nestedDict, indentLevel + 1, iniLines);
                }
                else
                {
                    string valueStr;
                    if (value == null)
                    {
                        valueStr = "null";
                    }
                    else if (value is string strValue && strValue.Contains(" "))
                    {
                        valueStr = $"\"{strValue}\"";
                    }
                    else
                    {
                        valueStr = value.ToString();
                    }
                    iniLines.Add($"{prefix}{key}={valueStr}");
                }
            }
        }
    }
}

