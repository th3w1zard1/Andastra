// Matching PyKotor implementation at vendor/PyKotor/Libraries/PyKotor/src/pykotor/tslpatcher/diff/objects.py:83-108
// Original: @dataclass class GFFDiffResult, FieldDiff, StructDiff: ...
using System.Collections.Generic;
using JetBrains.Annotations;

namespace KotorDiff.NET.Diff.Objects
{
    /// <summary>
    /// Result of comparing two GFF files.
    /// 1:1 port of GFFDiffResult from vendor/PyKotor/Libraries/PyKotor/src/pykotor/tslpatcher/diff/objects.py:83-89
    /// </summary>
    public class GFFDiffResult : DiffResult<object>
    {
        [CanBeNull] public List<FieldDiff> FieldDiffs { get; set; }
        [CanBeNull] public List<StructDiff> StructDiffs { get; set; }
    }

    /// <summary>
    /// Difference in a GFF field.
    /// 1:1 port of FieldDiff from vendor/PyKotor/Libraries/PyKotor/src/pykotor/tslpatcher/diff/objects.py:91-99
    /// </summary>
    public class FieldDiff
    {
        public string FieldPath { get; set; }
        public DiffType DiffType { get; set; }
        [CanBeNull] public object LeftValue { get; set; }
        [CanBeNull] public object RightValue { get; set; }
        [CanBeNull] public string FieldType { get; set; }
    }

    /// <summary>
    /// Difference in a GFF struct.
    /// 1:1 port of StructDiff from vendor/PyKotor/Libraries/PyKotor/src/pykotor/tslpatcher/diff/objects.py:102-108
    /// </summary>
    public class StructDiff
    {
        public string StructPath { get; set; }
        public DiffType DiffType { get; set; }
        [CanBeNull] public List<FieldDiff> FieldDiffs { get; set; }
    }
}

