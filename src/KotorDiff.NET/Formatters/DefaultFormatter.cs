// Matching PyKotor implementation at vendor/PyKotor/Libraries/PyKotor/src/pykotor/tslpatcher/diff/formatters.py:43-117
// Original: class DefaultFormatter(DiffFormatter): ...
using System;
using System.Linq;
using KotorDiff.NET.Diff.Objects;

namespace KotorDiff.NET.Formatters
{
    /// <summary>
    /// Default formatter that maintains existing KotorDiff behavior.
    /// 1:1 port of DefaultFormatter from vendor/PyKotor/Libraries/PyKotor/src/pykotor/tslpatcher/diff/formatters.py:43-117
    /// </summary>
    public class DefaultFormatter : DiffFormatter
    {
        public DefaultFormatter(Action<string> outputFunc = null) : base(outputFunc)
        {
        }

        public override string FormatDiff<T>(DiffResult<T> diffResult)
        {
            if (diffResult.HasError)
            {
                return $"[Error] {diffResult.ErrorMessage}";
            }

            if (diffResult.DiffType == DiffType.Identical)
            {
                return ""; // No output for identical files
            }

            if (diffResult.DiffType == DiffType.Added)
            {
                return $"Resource added: '{diffResult.RightIdentifier}'";
            }

            if (diffResult.DiffType == DiffType.Removed)
            {
                return $"Resource removed: '{diffResult.LeftIdentifier}'";
            }

            // Handle specific diff types
            if (diffResult is ResourceDiffResult resourceDiff)
            {
                return FormatResourceDiff(resourceDiff);
            }
            if (diffResult is GFFDiffResult gffDiff)
            {
                return FormatGffDiff(gffDiff);
            }
            if (diffResult is TwoDADiffResult twodaDiff)
            {
                return Format2DaDiff(twodaDiff);
            }
            if (diffResult is TLKDiffResult tlkDiff)
            {
                return FormatTlkDiff(tlkDiff);
            }

            return $"'{diffResult.LeftIdentifier}' differs from '{diffResult.RightIdentifier}'";
        }

        private string FormatResourceDiff(ResourceDiffResult diffResult)
        {
            string sizeInfo = "";
            if (diffResult.LeftSize.HasValue && diffResult.RightSize.HasValue)
            {
                sizeInfo = $" (sizes: {diffResult.LeftSize} vs {diffResult.RightSize})";
            }

            string resourceType = diffResult.ResourceType != null ? $" [{diffResult.ResourceType}]" : "";

            return $"'{diffResult.LeftIdentifier}'{resourceType} differs from '{diffResult.RightIdentifier}'{sizeInfo}";
        }

        private string FormatGffDiff(GFFDiffResult diffResult)
        {
            string baseMsg = $"GFF '{diffResult.LeftIdentifier}' differs from '{diffResult.RightIdentifier}'";

            if (diffResult.FieldDiffs != null && diffResult.FieldDiffs.Count > 0)
            {
                int fieldCount = diffResult.FieldDiffs.Count;
                baseMsg += $" ({fieldCount} field differences)";
            }

            return baseMsg;
        }

        private string Format2DaDiff(TwoDADiffResult diffResult)
        {
            string baseMsg = $"2DA '{diffResult.LeftIdentifier}' differs from '{diffResult.RightIdentifier}'";

            var diffCounts = new System.Collections.Generic.List<string>();
            if (diffResult.HeaderDiffs != null && diffResult.HeaderDiffs.Count > 0)
            {
                diffCounts.Add($"{diffResult.HeaderDiffs.Count} header");
            }
            if (diffResult.RowDiffs != null && diffResult.RowDiffs.Count > 0)
            {
                diffCounts.Add($"{diffResult.RowDiffs.Count} row");
            }
            if (diffResult.ColumnDiffs != null && diffResult.ColumnDiffs.Count > 0)
            {
                diffCounts.Add($"{diffResult.ColumnDiffs.Count} column");
            }

            if (diffCounts.Count > 0)
            {
                baseMsg += $" ({string.Join(", ", diffCounts)} differences)";
            }

            return baseMsg;
        }

        private string FormatTlkDiff(TLKDiffResult diffResult)
        {
            string baseMsg = $"TLK '{diffResult.LeftIdentifier}' differs from '{diffResult.RightIdentifier}'";

            if (diffResult.EntryDiffs != null && diffResult.EntryDiffs.Count > 0)
            {
                int entryCount = diffResult.EntryDiffs.Count;
                baseMsg += $" ({entryCount} entry differences)";
            }

            return baseMsg;
        }
    }
}

