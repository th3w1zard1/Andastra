// Matching PyKotor implementation at vendor/PyKotor/Libraries/PyKotor/src/pykotor/tslpatcher/diff/engine.py:819-826
// Original: class CachedFileComparison: ...
using JetBrains.Annotations;

namespace KotorDiff.NET.Diff
{
    /// <summary>
    /// Represents a single file comparison for caching.
    /// 1:1 port of CachedFileComparison from vendor/PyKotor/Libraries/PyKotor/src/pykotor/tslpatcher/diff/engine.py:819-826
    /// </summary>
    public class CachedFileComparison
    {
        /// <summary>
        /// Relative path of the file.
        /// </summary>
        public string RelPath { get; set; }

        /// <summary>
        /// Status: "identical", "modified", "missing_left", "missing_right".
        /// </summary>
        public string Status { get; set; }

        /// <summary>
        /// File extension.
        /// </summary>
        public string Ext { get; set; }

        /// <summary>
        /// Whether the file exists in the left/base installation.
        /// </summary>
        public bool LeftExists { get; set; }

        /// <summary>
        /// Whether the file exists in the right/target installation.
        /// </summary>
        public bool RightExists { get; set; }
    }
}

