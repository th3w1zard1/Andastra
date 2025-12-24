// Matching PyKotor implementation at vendor/PyKotor/Libraries/PyKotor/src/pykotor/tslpatcher/diff/formatters.py:120-166
// Original: class UnifiedFormatter(DiffFormatter): ...
using System;
using System.Linq;
using System.Text;
using KotorDiff.Diff.Objects;

namespace KotorDiff.Formatters
{
    /// <summary>
    /// Unified diff formatter (similar to `diff -u`).
    /// 1:1 port of UnifiedFormatter from vendor/PyKotor/Libraries/PyKotor/src/pykotor/tslpatcher/diff/formatters.py:120-166
    /// </summary>
    public class UnifiedFormatter : DiffFormatter
    {
        public UnifiedFormatter(Action<string> outputFunc = null) : base(outputFunc)
        {
        }

        public override string FormatDiff<T>(DiffResult<T> diffResult)
        {
            if (diffResult.HasError)
            {
                return $"diff: {diffResult.ErrorMessage}";
            }

            if (diffResult.DiffType == DiffType.Identical)
            {
                return ""; // No output for identical files in unified diff
            }

            if (diffResult.DiffType == DiffType.Added)
            {
                return $"--- /dev/null\n+++ {diffResult.RightIdentifier}";
            }

            if (diffResult.DiffType == DiffType.Removed)
            {
                return $"--- {diffResult.LeftIdentifier}\n+++ /dev/null";
            }

            // For modified files, try to create a meaningful unified diff
            string header = $"--- {diffResult.LeftIdentifier}\n+++ {diffResult.RightIdentifier}";

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

                        // Simple unified diff implementation
                        var diffLines = GenerateUnifiedDiff(leftLines, rightLines,
                            resourceDiff.LeftIdentifier, resourceDiff.RightIdentifier);

                        return string.Join("\n", diffLines);
                    }
                }
                catch (Exception e)
                {
                    return $"{header}\nError formatting unified diff: {e.GetType().Name}: {e.Message}";
                }
            }

            // For binary or structured files, just show the header
            return header + "\nBinary files differ";
        }

        private static string[] GenerateUnifiedDiff(string[] leftLines, string[] rightLines, string leftFile, string rightFile)
        {
            // TODO:  Simplified unified diff - in a full implementation, would use a proper diff algorithm
            var result = new System.Collections.Generic.List<string>();
            result.Add($"--- {leftFile}");
            result.Add($"+++ {rightFile}");

            // Simple line-by-line comparison
            int maxLines = Math.Max(leftLines.Length, rightLines.Length);
            for (int i = 0; i < maxLines; i++)
            {
                string leftLine = i < leftLines.Length ? leftLines[i] : "";
                string rightLine = i < rightLines.Length ? rightLines[i] : "";

                if (leftLine != rightLine)
                {
                    if (i < leftLines.Length)
                    {
                        result.Add($"-{leftLine}");
                    }
                    if (i < rightLines.Length)
                    {
                        result.Add($"+{rightLine}");
                    }
                }
                else
                {
                    result.Add($" {leftLine}");
                }
            }

            return result.ToArray();
        }
    }
}

