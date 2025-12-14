using System;
using System.Collections.Generic;
using CSharpKOTOR.Mods.GFF;
using CSharpKOTOR.Mods.SSF;
using CSharpKOTOR.Mods.TLK;
using CSharpKOTOR.Mods.TwoDA;
using CSharpKOTOR.Mods.Template;

namespace CSharpKOTOR.Diff
{
    // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tslpatcher/diff/analyzers.py:89-104
    // Original: class DiffAnalyzer(ABC):
    /// <summary>
    /// Abstract base interface for diff analyzers.
    /// </summary>
    public interface IDiffAnalyzer
    {
        /// <summary>
        /// Analyze differences and return a PatcherModifications object.
        /// TLK analyzers may return a tuple of (ModificationsTLK, strref_mappings).
        /// All other analyzers return PatcherModifications | None.
        /// </summary>
        object Analyze(byte[] leftData, byte[] rightData, string identifier);
    }

    // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tslpatcher/diff/analyzers.py:845-869
    // Original: class DiffAnalyzerFactory:
    /// <summary>
    /// Factory for creating appropriate diff analyzers.
    /// </summary>
    public static class DiffAnalyzerFactory
    {
        // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tslpatcher/diff/analyzers.py:848-869
        // Original: @staticmethod def get_analyzer(resource_type: str) -> DiffAnalyzer | None:
        /// <summary>
        /// Get the appropriate analyzer for a resource type.
        /// </summary>
        public static IDiffAnalyzer GetAnalyzer(string resourceType)
        {
            if (string.IsNullOrEmpty(resourceType))
            {
                return null;
            }

            string resourceTypeLower = resourceType.ToLowerInvariant();

            // 2DA analyzer
            if (resourceTypeLower == "2da" || resourceTypeLower == "twoda")
            {
                return new TwoDaDiffAnalyzerWrapper();
            }

            // GFF analyzer (handles all GFF-based formats)
            HashSet<string> gffTypes = new HashSet<string>
            {
                "gff", "utc", "uti", "utp", "ute", "utm", "utd", "utw",
                "dlg", "are", "git", "ifo", "gui", "jrl", "fac"
            };
            if (gffTypes.Contains(resourceTypeLower))
            {
                return new GffDiffAnalyzerWrapper();
            }

            // TLK analyzer
            if (resourceTypeLower == "tlk")
            {
                return new TlkDiffAnalyzerWrapper();
            }

            // SSF analyzer
            if (resourceTypeLower == "ssf")
            {
                return new SsfDiffAnalyzerWrapper();
            }

            return null;
        }

        // Wrapper classes to adapt existing analyzers to IDiffAnalyzer interface
        private class TwoDaDiffAnalyzerWrapper : IDiffAnalyzer
        {
            private readonly TwoDaDiffAnalyzer _analyzer = new TwoDaDiffAnalyzer();

            public object Analyze(byte[] leftData, byte[] rightData, string identifier)
            {
                return _analyzer.Analyze(leftData, rightData, identifier);
            }
        }

        private class GffDiffAnalyzerWrapper : IDiffAnalyzer
        {
            private readonly GffDiffAnalyzer _analyzer = new GffDiffAnalyzer();

            public object Analyze(byte[] leftData, byte[] rightData, string identifier)
            {
                return _analyzer.Analyze(leftData, rightData, identifier);
            }
        }

        private class TlkDiffAnalyzerWrapper : IDiffAnalyzer
        {
            public object Analyze(byte[] leftData, byte[] rightData, string identifier)
            {
                // TODO: Implement TLKDiffAnalyzer to match Python implementation
                // For now, return null as placeholder
                return null;
            }
        }

        private class SsfDiffAnalyzerWrapper : IDiffAnalyzer
        {
            public object Analyze(byte[] leftData, byte[] rightData, string identifier)
            {
                // TODO: Implement SSFDiffAnalyzer to match Python implementation
                // For now, return null as placeholder
                return null;
            }
        }
    }
}
