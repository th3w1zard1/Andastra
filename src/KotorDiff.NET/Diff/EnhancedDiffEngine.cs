// Matching PyKotor implementation at vendor/PyKotor/Libraries/PyKotor/src/pykotor/tslpatcher/diff/enhanced_engine.py:20-89
// Original: class EnhancedDiffEngine: ...
using System;
using KotorDiff.NET.Diff.Objects;
using KotorDiff.NET.Formatters;
using JetBrains.Annotations;

namespace KotorDiff.NET.Diff
{
    /// <summary>
    /// Enhanced diff engine that returns structured diff results with formatted output.
    /// 1:1 port of EnhancedDiffEngine from vendor/PyKotor/Libraries/PyKotor/src/pykotor/tslpatcher/diff/enhanced_engine.py:20-89
    /// </summary>
    public class EnhancedDiffEngine
    {
        private readonly StructuredDiffEngine _structuredEngine;
        private readonly DiffFormatter _formatter;

        /// <summary>
        /// Initialize with format and output function.
        /// Matching PyKotor implementation at vendor/PyKotor/Libraries/PyKotor/src/pykotor/tslpatcher/diff/enhanced_engine.py:23-35
        /// </summary>
        public EnhancedDiffEngine(
            DiffFormat diffFormat = DiffFormat.Default,
            [CanBeNull] Action<string> outputFunc = null)
        {
            _structuredEngine = new StructuredDiffEngine();
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
            DiffResult<object> diffResult = null;
            string ext = resA.Ext.ToLowerInvariant();

            if (ext == "2da" || ext == "twoda")
            {
                var result = _structuredEngine.Compare2Da(resA.Data, resB.Data, resA.Identifier, resB.Identifier);
                diffResult = new DiffResult<object>
                {
                    DiffType = result.DiffType,
                    LeftIdentifier = result.LeftIdentifier,
                    RightIdentifier = result.RightIdentifier,
                    LeftValue = result.LeftValue,
                    RightValue = result.RightValue,
                    ErrorMessage = result.ErrorMessage
                };
            }
            else if (ext == "tlk")
            {
                var result = _structuredEngine.CompareTlk(resA.Data, resB.Data, resA.Identifier, resB.Identifier);
                diffResult = new DiffResult<object>
                {
                    DiffType = result.DiffType,
                    LeftIdentifier = result.LeftIdentifier,
                    RightIdentifier = result.RightIdentifier,
                    LeftValue = result.LeftValue,
                    RightValue = result.RightValue,
                    ErrorMessage = result.ErrorMessage
                };
            }
            else if (IsGffExtension(ext))
            {
                var result = _structuredEngine.CompareGff(resA.Data, resB.Data, resA.Identifier, resB.Identifier);
                diffResult = new DiffResult<object>
                {
                    DiffType = result.DiffType,
                    LeftIdentifier = result.LeftIdentifier,
                    RightIdentifier = result.RightIdentifier,
                    LeftValue = result.LeftValue,
                    RightValue = result.RightValue,
                    ErrorMessage = result.ErrorMessage
                };
            }
            else
            {
                // Default: byte comparison
                bool areEqual = resA.Data.Length == resB.Data.Length;
                if (areEqual)
                {
                    for (int i = 0; i < resA.Data.Length; i++)
                    {
                        if (resA.Data[i] != resB.Data[i])
                        {
                            areEqual = false;
                            break;
                        }
                    }
                }

                diffResult = new DiffResult<object>
                {
                    DiffType = areEqual ? DiffType.Identical : DiffType.Modified,
                    LeftIdentifier = resA.Identifier,
                    RightIdentifier = resB.Identifier,
                    LeftValue = resA.Data,
                    RightValue = resB.Data
                };
            }

            // Format and output the result
            _formatter.OutputDiff(diffResult);

            // Return whether they're identical
            return !diffResult.IsDifferent;
        }

        /// <summary>
        /// Map file extension to resource type.
        /// Matching PyKotor implementation at vendor/PyKotor/Libraries/PyKotor/src/pykotor/tslpatcher/diff/enhanced_engine.py:69-88
        /// </summary>
        private DiffResourceType GetResourceType(string ext)
        {
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

    /// <summary>
    /// Resource type for structured diff engine.
    /// </summary>
    public enum DiffResourceType
    {
        Gff,
        TwoDa,
        Tlk,
        Lip,
        Bytes
    }
}


