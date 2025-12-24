// Matching PyKotor implementation at vendor/PyKotor/Libraries/PyKotor/src/pykotor/tslpatcher/diff/formatters.py:217-287
// Original: class SideBySideFormatter(DiffFormatter): ...
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using KotorDiff.Diff.Objects;

namespace KotorDiff.Formatters
{
    /// <summary>
    /// Side-by-side diff formatter.
    /// 1:1 port of SideBySideFormatter from vendor/PyKotor/Libraries/PyKotor/src/pykotor/tslpatcher/diff/formatters.py:217-287
    /// </summary>
    public class SideBySideFormatter : DiffFormatter
    {
        private readonly int _width;

        public SideBySideFormatter(int width = 80, Action<string> outputFunc = null) : base(outputFunc)
        {
            _width = width;
        }

        public override string FormatDiff<T>(DiffResult<T> diffResult)
        {
            if (diffResult.HasError)
            {
                return $"diff: {diffResult.ErrorMessage}";
            }

            if (diffResult.DiffType == DiffType.Identical)
            {
                return "";
            }

            if (diffResult.DiffType == DiffType.Added)
            {
                return $"< {diffResult.LeftIdentifier ?? "/dev/null"} | > {diffResult.RightIdentifier}";
            }

            if (diffResult.DiffType == DiffType.Removed)
            {
                return $"< {diffResult.LeftIdentifier} | > {diffResult.RightIdentifier ?? "/dev/null"}";
            }

            // For modified files
            string header = $"< {diffResult.LeftIdentifier} | > {diffResult.RightIdentifier}";

            // Handle text-like content
            if (diffResult is ResourceDiffResult resourceDiff &&
                (resourceDiff.ResourceType == "txt" || resourceDiff.ResourceType == "nss"))
            {
                try
                {
                    if (resourceDiff.LeftValue != null && resourceDiff.RightValue != null)
                    {
                        string leftText = Encoding.UTF8.GetString(resourceDiff.LeftValue);
                        string rightText = Encoding.UTF8.GetString(resourceDiff.RightValue);

                        var leftLines = leftText.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);
                        var rightLines = rightText.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);

                        var diffLines = GenerateSideBySideDiff(leftLines, rightLines);

                        return header + "\n" + string.Join("\n", diffLines);
                    }
                }
                catch (Exception e)
                {
                    return $"{header}\nError formatting side-by-side diff: {e.GetType().Name}: {e.Message}";
                }
            }

            return header + "\nBinary files differ";
        }

        private string[] GenerateSideBySideDiff(string[] leftLines, string[] rightLines)
        {
            var result = new List<string>();
            int columnWidth = (_width - 3) / 2; // Subtract 3 for separator " | "

            int maxLines = Math.Max(leftLines.Length, rightLines.Length);
            for (int i = 0; i < maxLines; i++)
            {
                string leftLine = i < leftLines.Length ? TruncateLine(leftLines[i], columnWidth) : "";
                string rightLine = i < rightLines.Length ? TruncateLine(rightLines[i], columnWidth) : "";

                string marker = "";
                if (i >= leftLines.Length)
                {
                    marker = "+";
                }
                else if (i >= rightLines.Length)
                {
                    marker = "-";
                }
                else if (leftLines[i] != rightLines[i])
                {
                    marker = "*";
                }

                result.Add($"{leftLine.PadRight(columnWidth)} | {marker} {rightLine.PadRight(columnWidth)}");
            }

            return result.ToArray();
        }

        private static string TruncateLine(string line, int maxLength)
        {
            if (line.Length > maxLength)
            {
                return line.Substring(0, maxLength - 3) + "...";
            }
            return line;
        }
    }
}

