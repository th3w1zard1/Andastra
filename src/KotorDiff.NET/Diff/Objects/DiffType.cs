// Matching PyKotor implementation at vendor/PyKotor/Libraries/PyKotor/src/pykotor/tslpatcher/diff/objects.py:25-32
// Original: class DiffType(Enum): ...
namespace KotorDiff.NET.Diff.Objects
{
    /// <summary>
    /// Types of differences that can be detected.
    /// 1:1 port of DiffType from vendor/PyKotor/Libraries/PyKotor/src/pykotor/tslpatcher/diff/objects.py:25-32
    /// </summary>
    public enum DiffType
    {
        Identical,
        Modified,
        Added,
        Removed,
        Error
    }
}

