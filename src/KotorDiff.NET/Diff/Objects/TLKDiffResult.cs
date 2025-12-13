// Matching PyKotor implementation at vendor/PyKotor/Libraries/PyKotor/src/pykotor/tslpatcher/diff/objects.py:160-195
// Original: @dataclass class TLKDiffResult, TLKEntryDiff: ...
using System.Collections.Generic;
using JetBrains.Annotations;

namespace KotorDiff.NET.Diff.Objects
{
    /// <summary>
    /// Result of comparing two TLK files.
    /// 1:1 port of TLKDiffResult from vendor/PyKotor/Libraries/PyKotor/src/pykotor/tslpatcher/diff/objects.py:160-164
    /// </summary>
    public class TLKDiffResult : DiffResult<object>
    {
        [CanBeNull] public List<TLKEntryDiff> EntryDiffs { get; set; }
    }

    /// <summary>
    /// Difference in a TLK entry.
    /// 1:1 port of TLKEntryDiff from vendor/PyKotor/Libraries/PyKotor/src/pykotor/tslpatcher/diff/objects.py:186-195
    /// </summary>
    public class TLKEntryDiff
    {
        public int EntryId { get; set; }
        public DiffType DiffType { get; set; }
        [CanBeNull] public string LeftText { get; set; }
        [CanBeNull] public string RightText { get; set; }
        [CanBeNull] public string LeftVoiceover { get; set; }
        [CanBeNull] public string RightVoiceover { get; set; }
    }
}

