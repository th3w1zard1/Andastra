using System;
using System.Collections.Generic;
using System.Linq;

// Simple test for the diff algorithm
class Program
{
    static void Main()
    {
        TestDiffAlgorithm();
    }

    static void TestDiffAlgorithm()
    {
        // Test case 1: Simple addition
        string text1 = "line1\nline2\nline3";
        string text2 = "line1\nline2\nline3\nline4";
        var result1 = GenerateUnifiedDiffLines(text1, text2, "original", "modified");
        Console.WriteLine("Test 1 - Simple addition:");
        foreach (var line in result1)
        {
            Console.WriteLine(line);
        }
        Console.WriteLine();

        // Test case 2: Simple deletion
        string text3 = "line1\nline2\nline3\nline4";
        string text4 = "line1\nline2\nline3";
        var result2 = GenerateUnifiedDiffLines(text3, text4, "original", "modified");
        Console.WriteLine("Test 2 - Simple deletion:");
        foreach (var line in result2)
        {
            Console.WriteLine(line);
        }
        Console.WriteLine();

        // Test case 3: Modification
        string text5 = "line1\nline2\nline3";
        string text6 = "line1\nmodified_line2\nline3";
        var result3 = GenerateUnifiedDiffLines(text5, text6, "original", "modified");
        Console.WriteLine("Test 3 - Modification:");
        foreach (var line in result3)
        {
            Console.WriteLine(line);
        }
        Console.WriteLine();

        // Test case 4: Complex changes with context
        string text7 = "common1\ncommon2\nold_line1\nold_line2\ncommon3\ncommon4";
        string text8 = "common1\ncommon2\nnew_line1\nnew_line2\ncommon3\ncommon4";
        var result4 = GenerateUnifiedDiffLines(text7, text8, "original", "modified");
        Console.WriteLine("Test 4 - Complex changes:");
        foreach (var line in result4)
        {
            Console.WriteLine(line);
        }
    }

    // Simplified version of the diff algorithm for testing
    static List<string> GenerateUnifiedDiffLines(string text1, string text2, string fromFile, string toFile)
    {
        var leftLines = SplitLines(text1);
        var rightLines = SplitLines(text2);

        // Use Myers diff algorithm to find differences
        var diff = MyersDiff(leftLines, rightLines);

        // Format as unified diff
        return FormatUnifiedDiff(diff, leftLines, rightLines, fromFile, toFile);
    }

    static List<string> SplitLines(string text)
    {
        if (string.IsNullOrEmpty(text)) return new List<string>();
        return text.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None).ToList();
    }

    class DiffOperation
    {
        public enum OperationType { Equal, Insert, Delete }
        public OperationType Type;
        public int OldIndex, NewIndex;
        public string Line;
    }

    static List<DiffOperation> MyersDiff(List<string> oldLines, List<string> newLines)
    {
        int n = oldLines.Count;
        int m = newLines.Count;
        int max = n + m;
        var trace = new List<int[]>();
        var operations = new List<DiffOperation>();

        for (int d = 0; d <= max; d++)
        {
            trace.Add(new int[2 * d + 1]);
            for (int k = -d; k <= d; k += 2)
            {
                int x;
                if (d == 0) x = 0;
                else if (k == -d || (k != d && trace[d - 1][k - 1 + d - 1] < trace[d - 1][k + 1 + d - 1]))
                    x = trace[d - 1][k + 1 + d - 1];
                else
                    x = trace[d - 1][k - 1 + d - 1] + 1;

                int y = x - k;

                while (x < n && y < m && oldLines[x] == newLines[y])
                {
                    x++;
                    y++;
                }

                trace[d][k + d] = x;

                if (x >= n && y >= m)
                {
                    return ReconstructPath(trace, d, k, oldLines, newLines);
                }
            }
        }
        throw new InvalidOperationException("Diff algorithm failed");
    }

    static List<DiffOperation> ReconstructPath(List<int[]> trace, int d, int k, List<string> oldLines, List<string> newLines)
    {
        var operations = new List<DiffOperation>();
        int x = oldLines.Count;
        int y = newLines.Count;

        for (int currentD = d; currentD > 0; currentD--)
        {
            int prevK = k;
            int prevX = trace[currentD - 1][k + currentD - 1];

            if (k == -currentD || (k != currentD && trace[currentD - 1][k - 1 + currentD - 1] < trace[currentD - 1][k + 1 + currentD - 1]))
                prevK = k + 1;
            else
                prevK = k - 1;

            int prevY = prevX - prevK;

            while (x > prevX && y > prevY)
            {
                x--;
                y--;
                operations.Insert(0, new DiffOperation { Type = DiffOperation.OperationType.Equal, OldIndex = x, NewIndex = y, Line = oldLines[x] });
            }

            if (currentD > 0)
            {
                if (x > prevX)
                {
                    x--;
                    operations.Insert(0, new DiffOperation { Type = DiffOperation.OperationType.Delete, OldIndex = x, NewIndex = y, Line = oldLines[x] });
                }
                else if (y > prevY)
                {
                    y--;
                    operations.Insert(0, new DiffOperation { Type = DiffOperation.OperationType.Insert, OldIndex = x, NewIndex = y, Line = newLines[y] });
                }
            }

            k = prevK;
        }

        while (x > 0 && y > 0 && oldLines[x - 1] == newLines[y - 1])
        {
            x--;
            y--;
            operations.Insert(0, new DiffOperation { Type = DiffOperation.OperationType.Equal, OldIndex = x, NewIndex = y, Line = oldLines[x] });
        }

        return operations;
    }

    static List<string> FormatUnifiedDiff(List<DiffOperation> operations, List<string> oldLines, List<string> newLines, string fromFile, string toFile)
    {
        var result = new List<string>();
        if (operations.Count == 0) return result;

        var hunks = FindHunks(operations, oldLines.Count, newLines.Count);
        if (hunks.Count == 0) return result;

        result.Add($"--- {fromFile}");
        result.Add($"+++ {toFile}");

        foreach (var hunk in hunks)
        {
            result.AddRange(FormatHunk(hunk, operations, oldLines, newLines));
        }

        return result;
    }

    class Hunk
    {
        public int OldStart, OldCount, NewStart, NewCount, StartOpIndex, EndOpIndex;
    }

    static List<Hunk> FindHunks(List<DiffOperation> operations, int oldCount, int newCount)
    {
        var hunks = new List<Hunk>();
        const int CONTEXT_LINES = 3;

        int i = 0;
        while (i < operations.Count)
        {
            while (i < operations.Count && operations[i].Type == DiffOperation.OperationType.Equal) i++;
            if (i >= operations.Count) break;

            var hunk = new Hunk();
            hunk.StartOpIndex = Math.Max(0, i - CONTEXT_LINES);

            int changeStart = i;
            while (i < operations.Count && operations[i].Type != DiffOperation.OperationType.Equal) i++;
            int changeEnd = i - 1;

            hunk.EndOpIndex = Math.Min(operations.Count - 1, changeEnd + CONTEXT_LINES);

            hunk.OldStart = 1;
            hunk.NewStart = 1;

            for (int j = 0; j < hunk.StartOpIndex; j++)
            {
                if (operations[j].Type == DiffOperation.OperationType.Equal || operations[j].Type == DiffOperation.OperationType.Delete)
                    hunk.OldStart++;
                if (operations[j].Type == DiffOperation.OperationType.Equal || operations[j].Type == DiffOperation.OperationType.Insert)
                    hunk.NewStart++;
            }

            hunk.OldCount = 0;
            hunk.NewCount = 0;

            for (int j = hunk.StartOpIndex; j <= hunk.EndOpIndex; j++)
            {
                if (operations[j].Type == DiffOperation.OperationType.Equal || operations[j].Type == DiffOperation.OperationType.Delete)
                    hunk.OldCount++;
                if (operations[j].Type == DiffOperation.OperationType.Equal || operations[j].Type == DiffOperation.OperationType.Insert)
                    hunk.NewCount++;
            }

            hunks.Add(hunk);
        }

        return hunks;
    }

    static List<string> FormatHunk(Hunk hunk, List<DiffOperation> operations, List<string> oldLines, List<string> newLines)
    {
        var lines = new List<string>();
        lines.Add($"@@ -{hunk.OldStart},{hunk.OldCount} +{hunk.NewStart},{hunk.NewCount} @@");

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
