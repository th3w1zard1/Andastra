// Matching PyKotor implementation at vendor/PyKotor/Libraries/PyKotor/src/pykotor/tslpatcher/diff/objects.py:167-184
// Original: @dataclass class LIPDiffResult, LIPEntryDiff: ...
using System.Collections.Generic;
using BioWare.NET.Resource.Formats.LIP;
using JetBrains.Annotations;

namespace KotorDiff.Diff.Objects
{
    /// <summary>
    /// Result of comparing two LIP files.
    /// 1:1 port of LIPDiffResult from vendor/PyKotor/Libraries/PyKotor/src/pykotor/tslpatcher/diff/objects.py:167-171
    /// </summary>
    public class LIPDiffResult : DiffResult<object>
    {
        [CanBeNull] public List<LIPEntryDiff> EntryDiffs { get; set; }
    }

    /// <summary>
    /// Difference in a LIP entry.
    /// 1:1 port of LIPEntryDiff from vendor/PyKotor/Libraries/PyKotor/src/pykotor/tslpatcher/diff/objects.py:174-183
    /// </summary>
    public class LIPEntryDiff
    {
        public int EntryId { get; set; }
        public DiffType DiffType { get; set; }
        public float? LeftTime { get; set; }
        public float? RightTime { get; set; }
        [CanBeNull] public LIPShape? LeftShape { get; set; }
        [CanBeNull] public LIPShape? RightShape { get; set; }
    }
}

