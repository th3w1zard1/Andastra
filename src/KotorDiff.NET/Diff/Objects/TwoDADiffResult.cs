// Matching PyKotor implementation at vendor/PyKotor/Libraries/PyKotor/src/pykotor/tslpatcher/diff/objects.py:111-157
// Original: @dataclass class TwoDADiffResult, HeaderDiff, RowDiff, CellDiff, ColumnDiff: ...
using System.Collections.Generic;
using JetBrains.Annotations;

namespace KotorDiff.NET.Diff.Objects
{
    /// <summary>
    /// Result of comparing two 2DA files.
    /// 1:1 port of TwoDADiffResult from vendor/PyKotor/Libraries/PyKotor/src/pykotor/tslpatcher/diff/objects.py:111-117
    /// </summary>
    public class TwoDADiffResult : DiffResult<object>
    {
        [CanBeNull] public List<HeaderDiff> HeaderDiffs { get; set; }
        [CanBeNull] public List<RowDiff> RowDiffs { get; set; }
        [CanBeNull] public List<ColumnDiff> ColumnDiffs { get; set; }
    }

    /// <summary>
    /// Difference in 2DA headers.
    /// 1:1 port of HeaderDiff from vendor/PyKotor/Libraries/PyKotor/src/pykotor/tslpatcher/diff/objects.py:120-127
    /// </summary>
    public class HeaderDiff
    {
        public int ColumnIndex { get; set; }
        public DiffType DiffType { get; set; }
        [CanBeNull] public string LeftHeader { get; set; }
        [CanBeNull] public string RightHeader { get; set; }
    }

    /// <summary>
    /// Difference in a 2DA row.
    /// 1:1 port of RowDiff from vendor/PyKotor/Libraries/PyKotor/src/pykotor/tslpatcher/diff/objects.py:130-136
    /// </summary>
    public class RowDiff
    {
        public int RowIndex { get; set; }
        public DiffType DiffType { get; set; }
        [CanBeNull] public List<CellDiff> CellDiffs { get; set; }
    }

    /// <summary>
    /// Difference in a 2DA cell.
    /// 1:1 port of CellDiff from vendor/PyKotor/Libraries/PyKotor/src/pykotor/tslpatcher/diff/objects.py:139-148
    /// </summary>
    public class CellDiff
    {
        public int RowIndex { get; set; }
        public int ColumnIndex { get; set; }
        [CanBeNull] public string ColumnName { get; set; }
        public DiffType DiffType { get; set; }
        [CanBeNull] public string LeftValue { get; set; }
        [CanBeNull] public string RightValue { get; set; }
    }

    /// <summary>
    /// Difference in a 2DA column.
    /// 1:1 port of ColumnDiff from vendor/PyKotor/Libraries/PyKotor/src/pykotor/tslpatcher/diff/objects.py:151-157
    /// </summary>
    public class ColumnDiff
    {
        public int ColumnIndex { get; set; }
        [CanBeNull] public string ColumnName { get; set; }
        public DiffType DiffType { get; set; }
    }
}

