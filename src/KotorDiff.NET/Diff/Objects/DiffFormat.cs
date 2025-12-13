// Matching PyKotor implementation at vendor/PyKotor/Libraries/PyKotor/src/pykotor/tslpatcher/diff/objects.py:35-41
// Original: class DiffFormat(Enum): ...
namespace KotorDiff.NET.Diff.Objects
{
    /// <summary>
    /// Supported diff output formats.
    /// 1:1 port of DiffFormat from vendor/PyKotor/Libraries/PyKotor/src/pykotor/tslpatcher/diff/objects.py:35-41
    /// </summary>
    public enum DiffFormat
    {
        Default,      // KotorDiff's native format
        Unified,      // Standard unified diff format
        Context,      // Context diff format
        SideBySide    // Side-by-side comparison
    }
}

