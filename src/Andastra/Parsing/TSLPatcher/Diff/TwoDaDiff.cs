using System.Collections.Generic;
using System.Linq;
using System.Text;
using Andastra.Parsing.Formats.TwoDA;

namespace Andastra.Parsing.Diff
{

    public class TwoDaCompareResult
    {
        public List<string> AddedColumns { get; } = new List<string>();
        public Dictionary<int, Dictionary<string, string>> ChangedRows { get; } = new Dictionary<int, Dictionary<string, string>>();
        public List<(string Label, Dictionary<string, string> Cells)> AddedRows { get; } = new List<(string Label, Dictionary<string, string> Cells)>();
    }

    public static class TwoDaDiff
    {
        public static TwoDaCompareResult Compare(TwoDA original, TwoDA modified)
        {
            var result = new TwoDaCompareResult();

            // Detect added columns
            var origHeaders = new HashSet<string>(original.GetHeaders());
            foreach (string header in modified.GetHeaders())
            {
                if (!origHeaders.Contains(header))
                {
                    result.AddedColumns.Add(header);
                }
            }

            // Detect modified/added rows
            int origHeight = original.GetHeight();
            int modHeight = modified.GetHeight();

            for (int i = 0; i < modHeight; i++)
            {
                TwoDARow modRow = modified.GetRow(i);

                if (i >= origHeight)
                {
                    // Added row
                    result.AddedRows.Add((modRow.Label(), modRow.GetData()));
                }
                else
                {
                    // Check for changes in existing rows
                    TwoDARow origRow = original.GetRow(i);
                    var changes = new Dictionary<string, string>();

                    foreach (string header in modified.GetHeaders())
                    {
                        string modVal = modRow.GetString(header);

                        if (origHeaders.Contains(header))
                        {
                            string origVal = origRow.GetString(header);
                            if (modVal != origVal)
                            {
                                changes[header] = modVal;
                            }
                        }
                        else
                        {
                            // New column.
                            changes[header] = modVal;
                        }
                    }

                    if (changes.Any())
                    {
                        result.ChangedRows[i] = changes;
                    }
                }
            }

            return result;
        }

        /// <summary>
        /// Serializes a TwoDaCompareResult to TSLPatcher INI format.
        /// Generates complete [2DAList] section with AddColumn, ChangeRow, and AddRow instructions.
        /// </summary>
        /// <param name="result">The diff result from Compare()</param>
        /// <param name="filename">The 2DA filename (e.g., "appearance.2da")</param>
        /// <param name="modified">The modified 2DA file (required for row labels)</param>
        /// <returns>TSLPatcher INI format string</returns>
        public static string SerializeToIni(TwoDaCompareResult result, string filename, TwoDA modified)
        {
            if (result == null)
            {
                throw new System.ArgumentNullException(nameof(result));
            }
            if (string.IsNullOrEmpty(filename))
            {
                throw new System.ArgumentException("Filename cannot be null or empty", nameof(filename));
            }
            if (modified == null)
            {
                throw new System.ArgumentNullException(nameof(modified));
            }

            var lines = new List<string>();

            // Generate [2DAList] section
            bool hasModifications = result.AddedColumns.Count > 0 ||
                                   result.ChangedRows.Count > 0 ||
                                   result.AddedRows.Count > 0;

            if (!hasModifications)
            {
                return string.Empty;
            }

            lines.Add("[2DAList]");
            lines.Add($"Table0={filename}");
            lines.Add("");

            // Generate [filename] section with modifier declarations
            lines.Add($"[{filename}]");

            int modifierIndex = 0;

            // Declare AddColumn modifiers
            foreach (string col in result.AddedColumns)
            {
                string sectionName = $"{filename}_addcol_{modifierIndex}";
                lines.Add($"AddColumn{modifierIndex}={sectionName}");
                modifierIndex++;
            }

            // Declare ChangeRow modifiers
            foreach (int rowIndex in result.ChangedRows.Keys.OrderBy(x => x))
            {
                // Only include if there are changes to existing columns (not just new column values)
                var changes = result.ChangedRows[rowIndex];
                bool hasExistingColumnChanges = changes.Any(kvp => !result.AddedColumns.Contains(kvp.Key));

                if (hasExistingColumnChanges)
                {
                    string sectionName = $"{filename}_changerow_{modifierIndex}";
                    lines.Add($"ChangeRow{modifierIndex}={sectionName}");
                    modifierIndex++;
                }
            }

            // Declare AddRow modifiers
            foreach (var addedRow in result.AddedRows)
            {
                string sectionName = $"{filename}_addrow_{modifierIndex}";
                lines.Add($"AddRow{modifierIndex}={sectionName}");
                modifierIndex++;
            }

            lines.Add("");

            // Generate detailed sections for each modifier
            modifierIndex = 0;

            // Generate AddColumn sections
            foreach (string col in result.AddedColumns)
            {
                string sectionName = $"{filename}_addcol_{modifierIndex}";
                lines.Add($"[{sectionName}]");
                lines.Add($"ColumnLabel={FormatIniValue(col)}");

                // Determine default value: use "****" (empty) as default
                // Check ChangedRows to see if there are any values for this column
                var columnValues = new List<string>();
                var indexInserts = new Dictionary<int, string>();
                var labelInserts = new Dictionary<string, string>();

                foreach (var kvp in result.ChangedRows)
                {
                    int rowIndex = kvp.Key;
                    var changes = kvp.Value;
                    if (changes.TryGetValue(col, out string cellValue))
                    {
                        columnValues.Add(cellValue);
                        indexInserts[rowIndex] = cellValue;

                        // Try to get row label for Llabel format
                        try
                        {
                            string rowLabel = modified.GetLabel(rowIndex);
                            labelInserts[rowLabel] = cellValue;
                        }
                        catch
                        {
                            // Row index out of range, skip label insert
                        }
                    }
                }

                // Also check AddedRows for this column
                foreach (var addedRow in result.AddedRows)
                {
                    if (addedRow.Cells.TryGetValue(col, out string cellValue))
                    {
                        columnValues.Add(cellValue);
                        labelInserts[addedRow.Label] = cellValue;
                    }
                }

                // Determine default: use most common value, or "****" if none
                string defaultValue = "****";
                if (columnValues.Count > 0)
                {
                    var mostCommon = columnValues.GroupBy(v => v)
                                                 .OrderByDescending(g => g.Count())
                                                 .First();
                    defaultValue = mostCommon.Key;
                }

                lines.Add($"DefaultValue={FormatIniValue(defaultValue)}");

                // Add index-based inserts (I#=value)
                foreach (var kvp in indexInserts.OrderBy(x => x.Key))
                {
                    // Only include if value differs from default
                    if (kvp.Value != defaultValue)
                    {
                        lines.Add($"I{kvp.Key}={FormatIniValue(kvp.Value)}");
                    }
                }

                // Add label-based inserts (Llabel=value)
                foreach (var kvp in labelInserts.OrderBy(x => x.Key))
                {
                    // Only include if value differs from default and not already covered by index insert
                    int? rowIndex = null;
                    try
                    {
                        rowIndex = modified.GetRowIndex(kvp.Key);
                    }
                    catch
                    {
                        // Label not found, skip
                    }

                    bool coveredByIndex = rowIndex.HasValue && indexInserts.ContainsKey(rowIndex.Value);
                    if (!coveredByIndex && kvp.Value != defaultValue)
                    {
                        lines.Add($"L{kvp.Key}={FormatIniValue(kvp.Value)}");
                    }
                }

                lines.Add("");
                modifierIndex++;
            }

            // Generate ChangeRow sections
            modifierIndex = 0;
            int changeRowModifierIndex = 0;

            foreach (int rowIndex in result.ChangedRows.Keys.OrderBy(x => x))
            {
                var changes = result.ChangedRows[rowIndex];

                // Only create ChangeRow for changes to existing columns
                var existingColumnChanges = changes.Where(kvp => !result.AddedColumns.Contains(kvp.Key))
                                                   .ToDictionary(kvp => kvp.Key, kvp => kvp.Value);

                if (existingColumnChanges.Count > 0)
                {
                    string sectionName = $"{filename}_changerow_{changeRowModifierIndex}";
                    lines.Add($"[{sectionName}]");

                    // Use RowIndex for targeting (we have the index from ChangedRows)
                    lines.Add($"RowIndex={FormatIniValue(rowIndex.ToString())}");

                    // Add cell modifications
                    foreach (var kvp in existingColumnChanges.OrderBy(x => x.Key))
                    {
                        lines.Add($"{kvp.Key}={FormatIniValue(kvp.Value)}");
                    }

                    lines.Add("");
                    changeRowModifierIndex++;
                }
            }

            // Generate AddRow sections
            modifierIndex = 0;
            int addRowModifierIndex = 0;

            foreach (var addedRow in result.AddedRows)
            {
                string sectionName = $"{filename}_addrow_{addRowModifierIndex}";
                lines.Add($"[{sectionName}]");

                // Add row label
                if (!string.IsNullOrEmpty(addedRow.Label))
                {
                    lines.Add($"RowLabel={FormatIniValue(addedRow.Label)}");
                }

                // Add cell values (only for existing columns, new columns are handled by AddColumn)
                foreach (var kvp in addedRow.Cells.OrderBy(x => x.Key))
                {
                    // Only include cells for existing columns (not added columns)
                    if (!result.AddedColumns.Contains(kvp.Key))
                    {
                        lines.Add($"{kvp.Key}={FormatIniValue(kvp.Value)}");
                    }
                }

                lines.Add("");
                addRowModifierIndex++;
            }

            return string.Join("\n", lines);
        }

        /// <summary>
        /// Formats a value for INI output. Quotes values containing single quotes.
        /// Matches TSLPatcherINISerializer.FormatIniValue behavior.
        /// </summary>
        private static string FormatIniValue(string value)
        {
            if (value == null)
            {
                return "";
            }
            if (value.Contains("'"))
            {
                return $"\"{value}\"";
            }
            return value;
        }
    }
}
