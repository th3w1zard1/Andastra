// Matching PyKotor implementation at vendor/PyKotor/Libraries/PyKotor/src/pykotor/tslpatcher/diff/formatters.py:169-214
// Original: class ContextFormatter(DiffFormatter): ...
using System;
using System.Text;
using KotorDiff.Diff.Objects;

namespace KotorDiff.Formatters
{
    /// <summary>
    /// Context diff formatter (similar to `diff -c`).
    /// 1:1 port of ContextFormatter from vendor/PyKotor/Libraries/PyKotor/src/pykotor/tslpatcher/diff/formatters.py:169-214
    /// </summary>
    public class ContextFormatter : DiffFormatter
    {
        public ContextFormatter(Action<string> outputFunc = null) : base(outputFunc)
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
                return "";
            }

            if (diffResult.DiffType == DiffType.Added)
            {
                return $"*** /dev/null\n--- {diffResult.RightIdentifier}";
            }

            if (diffResult.DiffType == DiffType.Removed)
            {
                return $"*** {diffResult.LeftIdentifier}\n--- /dev/null";
            }

            // For modified files
            string header = $"*** {diffResult.LeftIdentifier}\n--- {diffResult.RightIdentifier}";

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

                        // Simple context diff implementation
                        var diffLines = GenerateContextDiff(leftLines, rightLines,
                            resourceDiff.LeftIdentifier, resourceDiff.RightIdentifier);

                        return string.Join("\n", diffLines);
                    }
                }
                catch (Exception e)
                {
                    return $"{header}\nError formatting context diff: {e.GetType().Name}: {e.Message}";
                }
            }

            return header + "\nBinary files differ";
        }

        private static string[] GenerateContextDiff(string[] leftLines, string[] rightLines, string leftFile, string rightFile)
        {
            // TODO:  Simplified context diff - in a full implementation, would use a proper diff algorithm
            var result = new System.Collections.Generic.List<string>();
            result.Add($"*** {leftFile}");
            result.Add($"--- {rightFile}");

            // Simple line-by-line comparison with context
            int maxLines = Math.Max(leftLines.Length, rightLines.Length);
            for (int i = 0; i < maxLines; i++)
            {
                string leftLine = i < leftLines.Length ? leftLines[i] : "";
                string rightLine = i < rightLines.Length ? rightLines[i] : "";

                if (leftLine != rightLine)
                {
                    if (i < leftLines.Length)
                    {
                        result.Add($"- {leftLine}");
                    }
                    if (i < rightLines.Length)
                    {
                        result.Add($"+ {rightLine}");
                    }
                }
                else
                {
                    result.Add($"  {leftLine}");
                }
            }

            return result.ToArray();
        }
    }
}

