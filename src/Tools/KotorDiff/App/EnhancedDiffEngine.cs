// Matching PyKotor implementation at vendor/PyKotor/Libraries/PyKotor/src/pykotor/tslpatcher/diff/enhanced_engine.py:20-89
// Original: class EnhancedDiffEngine: ...
using System;
using KotorDiff.Diff;
using KotorDiff.Diff.Objects;
using KotorDiff.Formatters;
using JetBrains.Annotations;

namespace KotorDiff.Diff
{
    /// <summary>
    /// Enhanced diff engine that returns structured diff results with formatted output.
    /// 1:1 port of EnhancedDiffEngine from vendor/PyKotor/Libraries/PyKotor/src/pykotor/tslpatcher/diff/enhanced_engine.py:20-89
    /// </summary>
    public class EnhancedDiffEngine
    {
        private readonly DiffEngineObjects _structuredEngine;
        private readonly DiffFormatter _formatter;

        /// <summary>
        /// Initialize with format and output function.
        /// Matching PyKotor implementation at vendor/PyKotor/Libraries/PyKotor/src/pykotor/tslpatcher/diff/enhanced_engine.py:23-35
        /// </summary>
        public EnhancedDiffEngine(
            DiffFormat diffFormat = DiffFormat.Default,
            [CanBeNull] Action<string> outputFunc = null)
        {
            _structuredEngine = new DiffEngineObjects();
            _formatter = FormatterFactory.CreateFormatter(diffFormat, outputFunc);
        }

        /// <summary>
        /// Compare two resources and output formatted results.
        /// Matching PyKotor implementation at vendor/PyKotor/Libraries/PyKotor/src/pykotor/tslpatcher/diff/enhanced_engine.py:37-67
        /// </summary>
        public bool CompareResources(
            ComparableResource resA,
            ComparableResource resB)
        {
            // Determine the resource type for structured comparison
            DiffResourceType resourceType = GetResourceType(resA.Ext);

            // Use structured diff engine
            // Matching Python: self.structured_engine.compare_resources(res_a.data, res_b.data, res_a.identifier, res_b.identifier, resource_type)
            DiffResult<object> diffResult = _structuredEngine.CompareResources(
                resA.Data,
                resB.Data,
                resA.Identifier,
                resB.Identifier,
                resourceType);

            // Format and output the result
            // Matching Python: self.formatter.output_diff(diff_result)
            _formatter.OutputDiff(diffResult);

            // Return whether they're identical
            // Matching Python: return not diff_result.is_different
            return !diffResult.IsDifferent;
        }

        /// <summary>
        /// Map file extension to resource type.
        /// Matching PyKotor implementation at vendor/PyKotor/Libraries/PyKotor/src/pykotor/tslpatcher/diff/enhanced_engine.py:69-88
        /// </summary>
        private DiffResourceType GetResourceType(string ext)
        {
            // Python uses: gff.GFFContent.get_extensions() to get GFF extensions
            // For C#, we check common GFF extensions manually
            if (IsGffExtension(ext))
            {
                return DiffResourceType.Gff;
            }
            if (ext == "2da" || ext == "twoda")
            {
                return DiffResourceType.TwoDa;
            }
            if (ext == "tlk")
            {
                return DiffResourceType.Tlk;
            }
            if (ext == "lip")
            {
                return DiffResourceType.Lip;
            }
            return DiffResourceType.Bytes;
        }

        private bool IsGffExtension(string ext)
        {
            string extLower = ext.ToLowerInvariant();
            return extLower == "utc" || extLower == "uti" || extLower == "utp" ||
                   extLower == "ute" || extLower == "utm" || extLower == "utd" ||
                   extLower == "utw" || extLower == "dlg" || extLower == "are" ||
                   extLower == "git" || extLower == "ifo" || extLower == "gui" ||
                   extLower == "jrl" || extLower == "fac" || extLower == "gff";
        }
    }
}

