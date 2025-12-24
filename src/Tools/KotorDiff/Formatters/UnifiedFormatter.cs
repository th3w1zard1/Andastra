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
            // Use Myers diff algorithm to find minimal set of changes
            var operations = MyersDiff(leftLines, rightLines);

            // Format as unified diff with proper hunks and context
            return FormatUnifiedDiff(operations, leftLines, rightLines, leftFile, rightFile);
        }

        /// <summary>
        /// Represents a single diff operation
        /// </summary>
        private class DiffOperation
        {
            public enum OperationType { Equal, Insert, Delete }

            public OperationType Type { get; }
            public int OldIndex { get; }
            public int NewIndex { get; }
            public string Line { get; }

            public DiffOperation(OperationType type, int oldIndex, int newIndex, string line)
            {
                Type = type;
                OldIndex = oldIndex;
                NewIndex = newIndex;
                Line = line;
            }
        }

        /// <summary>
        /// Myers diff algorithm implementation - finds minimal set of changes
        /// </summary>
        private static System.Collections.Generic.List<DiffOperation> MyersDiff(string[] oldLines, string[] newLines)
        {
            int n = oldLines.Length;
            int m = newLines.Length;
            int max = n + m;

            var trace = new System.Collections.Generic.List<int[]>();
            var operations = new System.Collections.Generic.List<DiffOperation>();

            // Initialize the trace for backtracking
            for (int d = 0; d <= max; d++)
            {
                trace.Add(new int[2 * d + 1]);
                for (int k = -d; k <= d; k += 2)
                {
                    int x;
                    if (d == 0)
                    {
                        x = 0;
                    }
                    else if (k == -d || (k != d && trace[d - 1][k - 1 + d - 1] < trace[d - 1][k + 1 + d - 1]))
                    {
                        x = trace[d - 1][k + 1 + d - 1];
                    }
                    else
                    {
                        x = trace[d - 1][k - 1 + d - 1] + 1;
                    }

                    int y = x - k;

                    // Move diagonally as far as possible (find equal lines)
                    while (x < n && y < m && oldLines[x] == newLines[y])
                    {
                        x++;
                        y++;
                    }

                    trace[d][k + d] = x;

                    if (x >= n && y >= m)
                    {
                        // Found the end, reconstruct the path
                        return ReconstructPath(trace, d, k, oldLines, newLines);
                    }
                }
            }

            // This should never happen for valid inputs
            throw new System.InvalidOperationException("Diff algorithm failed to find a path");
        }

        /// <summary>
        /// Reconstruct the diff operations from the Myers algorithm trace
        /// </summary>
        private static System.Collections.Generic.List<DiffOperation> ReconstructPath(System.Collections.Generic.List<int[]> trace, int d, int k, string[] oldLines, string[] newLines)
        {
            var operations = new System.Collections.Generic.List<DiffOperation>();
            int x = oldLines.Length;
            int y = newLines.Length;

            for (int currentD = d; currentD > 0; currentD--)
            {
                int prevK = k;
                int prevX = trace[currentD - 1][k + currentD - 1];

                if (k == -currentD || (k != currentD && trace[currentD - 1][k - 1 + currentD - 1] < trace[currentD - 1][k + 1 + currentD - 1]))
                {
                    prevK = k + 1;
                }
                else
                {
                    prevK = k - 1;
                }

                int prevY = prevX - prevK;

                // Add operations for the diagonal move (equal lines)
                while (x > prevX && y > prevY)
                {
                    x--;
                    y--;
                    operations.Insert(0, new DiffOperation(DiffOperation.OperationType.Equal, x, y, oldLines[x]));
                }

                if (currentD > 0)
                {
                    if (x > prevX)
                    {
                        // Delete operation
                        x--;
                        operations.Insert(0, new DiffOperation(DiffOperation.OperationType.Delete, x, y, oldLines[x]));
                    }
                    else if (y > prevY)
                    {
                        // Insert operation
                        y--;
                        operations.Insert(0, new DiffOperation(DiffOperation.OperationType.Insert, x, y, newLines[y]));
                    }
                }

                k = prevK;
            }

            // Add remaining equal operations at the beginning
            while (x > 0 && y > 0 && oldLines[x - 1] == newLines[y - 1])
            {
                x--;
                y--;
                operations.Insert(0, new DiffOperation(DiffOperation.OperationType.Equal, x, y, oldLines[x]));
            }

            return operations;
        }

        /// <summary>
        /// Represents a hunk of changes in the diff
        /// </summary>
        private class Hunk
        {
            public int OldStart { get; set; }
            public int OldCount { get; set; }
            public int NewStart { get; set; }
            public int NewCount { get; set; }
            public int StartOpIndex { get; set; }
            public int EndOpIndex { get; set; }
        }

        /// <summary>
        /// Find hunks of changes with context lines
        /// </summary>
        private static System.Collections.Generic.List<Hunk> FindHunks(System.Collections.Generic.List<DiffOperation> operations, int oldCount, int newCount)
        {
            var hunks = new System.Collections.Generic.List<Hunk>();
            const int CONTEXT_LINES = 3;

            int i = 0;
            while (i < operations.Count)
            {
                // Skip equal operations until we find a change
                while (i < operations.Count && operations[i].Type == DiffOperation.OperationType.Equal)
                {
                    i++;
                }

                if (i >= operations.Count)
                {
                    break;
                }

                // Found the start of a change hunk
                var hunk = new Hunk();
                hunk.StartOpIndex = System.Math.Max(0, i - CONTEXT_LINES);

                // Find the end of this change hunk
                int changeStart = i;
                while (i < operations.Count && operations[i].Type != DiffOperation.OperationType.Equal)
                {
                    i++;
                }
                int changeEnd = i - 1;

                hunk.EndOpIndex = System.Math.Min(operations.Count - 1, changeEnd + CONTEXT_LINES);

                // Calculate line numbers (1-based)
                hunk.OldStart = 1;
                hunk.NewStart = 1;

                for (int j = 0; j < hunk.StartOpIndex; j++)
                {
                    if (operations[j].Type == DiffOperation.OperationType.Equal || operations[j].Type == DiffOperation.OperationType.Delete)
                    {
                        hunk.OldStart++;
                    }
                    if (operations[j].Type == DiffOperation.OperationType.Equal || operations[j].Type == DiffOperation.OperationType.Insert)
                    {
                        hunk.NewStart++;
                    }
                }

                // Count lines in hunk
                hunk.OldCount = 0;
                hunk.NewCount = 0;

                for (int j = hunk.StartOpIndex; j <= hunk.EndOpIndex; j++)
                {
                    if (operations[j].Type == DiffOperation.OperationType.Equal || operations[j].Type == DiffOperation.OperationType.Delete)
                    {
                        hunk.OldCount++;
                    }
                    if (operations[j].Type == DiffOperation.OperationType.Equal || operations[j].Type == DiffOperation.OperationType.Insert)
                    {
                        hunk.NewCount++;
                    }
                }

                hunks.Add(hunk);
            }

            return hunks;
        }

        /// <summary>
        /// Format diff operations as unified diff output with proper hunks
        /// </summary>
        private static string[] FormatUnifiedDiff(System.Collections.Generic.List<DiffOperation> operations, string[] oldLines, string[] newLines, string fromFile, string toFile)
        {
            var result = new System.Collections.Generic.List<string>();

            if (operations.Count == 0)
            {
                return result.ToArray();
            }

            // Find hunks (groups of changes with context)
            var hunks = FindHunks(operations, oldLines.Length, newLines.Length);

            if (hunks.Count == 0)
            {
                return result.ToArray();
            }

            // Add diff headers
            result.Add($"--- {fromFile}");
            result.Add($"+++ {toFile}");

            foreach (var hunk in hunks)
            {
                result.AddRange(FormatHunk(hunk, operations, oldLines, newLines));
            }

            return result.ToArray();
        }

        /// <summary>
        /// Format a single hunk as unified diff lines
        /// </summary>
        private static System.Collections.Generic.List<string> FormatHunk(Hunk hunk, System.Collections.Generic.List<DiffOperation> operations, string[] oldLines, string[] newLines)
        {
            var lines = new System.Collections.Generic.List<string>();

            // Add hunk header: @@ -oldStart,oldCount +newStart,newCount @@
            lines.Add($"@@ -{hunk.OldStart},{hunk.OldCount} +{hunk.NewStart},{hunk.NewCount} @@");

            // Add the lines in the hunk
            for (int i = hunk.StartOpIndex; i <= hunk.EndOpIndex; i++)
            {
                var op = operations[i];
                switch (op.Type)
                {
                    case DiffOperation.OperationType.Equal:
                        lines.Add($" {op.Line.TrimEnd('\r', '\n')}");
                        break;
                    case DiffOperation.OperationType.Delete:
                        lines.Add($"-{op.Line.TrimEnd('\r', '\n')}");
                        break;
                    case DiffOperation.OperationType.Insert:
                        lines.Add($"+{op.Line.TrimEnd('\r', '\n')}");
                        break;
                }
            }

            return lines;
        }
    }
}

