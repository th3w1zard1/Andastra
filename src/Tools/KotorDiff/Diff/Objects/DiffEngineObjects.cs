// Matching PyKotor implementation at vendor/PyKotor/Libraries/PyKotor/src/pykotor/tslpatcher/diff/objects.py:394-497
// Original: class DiffEngine: ... class DiffResourceType(Enum): ...
using System;
using System.Collections.Generic;
using BioWare.NET.Resource.Formats.GFF;
using BioWare.NET.Resource.Formats.TLK;
using BioWare.NET.Resource.Formats.TwoDA;
using BioWare.NET.Resource;

namespace KotorDiff.Diff.Objects
{
    /// <summary>
    /// Resource type for diff operations.
    /// 1:1 port of DiffResourceType from vendor/PyKotor/Libraries/PyKotor/src/pykotor/tslpatcher/diff/objects.py:491-496
    /// </summary>
    public enum DiffResourceType
    {
        Gff,
        TwoDa,
        Tlk,
        Lip,
        Bytes
    }

    /// <summary>
    /// Main diff engine that coordinates comparisons and returns structured results.
    /// 1:1 port of DiffEngine from vendor/PyKotor/Libraries/PyKotor/src/pykotor/tslpatcher/diff/objects.py:394-488
    /// </summary>
    public class DiffEngineObjects
    {
        private readonly Dictionary<DiffResourceType, DiffComparator<object>> _comparators;
        private StructuredDiffEngine _structuredEngine;

        public DiffEngineObjects()
        {
            _comparators = new Dictionary<DiffResourceType, DiffComparator<object>>
            {
                { DiffResourceType.Gff, new GFFDiffComparator() },
                { DiffResourceType.TwoDa, new TwoDADiffComparator() },
                { DiffResourceType.Tlk, new TLKDiffComparator() },
                { DiffResourceType.Lip, new LIPDiffComparator() },
                { DiffResourceType.Bytes, new BytesDiffComparator() }
            };
        }

        /// <summary>
        /// Lazy load structured engine.
        /// </summary>
        private StructuredDiffEngine StructuredEngine
        {
            get
            {
                if (_structuredEngine == null)
                {
                    _structuredEngine = new StructuredDiffEngine();
                }
                return _structuredEngine;
            }
        }

        /// <summary>
        /// Compare two resources and return structured diff results.
        /// </summary>
        public DiffResult<object> CompareResources(
            byte[] leftData,
            byte[] rightData,
            string leftId,
            string rightId,
            DiffResourceType resourceType)
        {
            // Handle missing data
            if (leftData == null && rightData != null)
            {
                var result = new ResourceDiffResult
                {
                    DiffType = DiffType.Added,
                    LeftIdentifier = leftId,
                    RightIdentifier = rightId,
                    RightValue = rightData,
                    ResourceType = resourceType.ToString().ToLowerInvariant()
                };
                return new DiffResult<object>
                {
                    DiffType = result.DiffType,
                    LeftIdentifier = result.LeftIdentifier,
                    RightIdentifier = result.RightIdentifier,
                    RightValue = result.RightValue,
                    ErrorMessage = result.ErrorMessage,
                    Details = result.Details
                };
            }
            if (leftData != null && rightData == null)
            {
                var result = new ResourceDiffResult
                {
                    DiffType = DiffType.Removed,
                    LeftIdentifier = leftId,
                    RightIdentifier = rightId,
                    LeftValue = leftData,
                    ResourceType = resourceType.ToString().ToLowerInvariant()
                };
                return new DiffResult<object>
                {
                    DiffType = result.DiffType,
                    LeftIdentifier = result.LeftIdentifier,
                    RightIdentifier = result.RightIdentifier,
                    LeftValue = result.LeftValue,
                    ErrorMessage = result.ErrorMessage,
                    Details = result.Details
                };
            }
            if (leftData == null && rightData == null)
            {
                var result = new ResourceDiffResult
                {
                    DiffType = DiffType.Identical,
                    LeftIdentifier = leftId,
                    RightIdentifier = rightId,
                    ResourceType = resourceType.ToString().ToLowerInvariant()
                };
                return new DiffResult<object>
                {
                    DiffType = result.DiffType,
                    LeftIdentifier = result.LeftIdentifier,
                    RightIdentifier = result.RightIdentifier,
                    ErrorMessage = result.ErrorMessage,
                    Details = result.Details
                };
            }

            // Get the appropriate comparator
            if (!_comparators.TryGetValue(resourceType, out var comparator))
            {
                comparator = _comparators[DiffResourceType.Bytes];
            }

            // For format-specific comparisons, we need to parse the data first
            if (resourceType == DiffResourceType.Gff || resourceType == DiffResourceType.TwoDa || resourceType == DiffResourceType.Tlk)
            {
                try
                {
                    object leftParsed = null;
                    object rightParsed = null;

                    if (resourceType == DiffResourceType.Gff)
                    {
                        leftParsed = new GFFBinaryReader(leftData).Load();
                        rightParsed = new GFFBinaryReader(rightData).Load();
                    }
                    else if (resourceType == DiffResourceType.TwoDa)
                    {
                        leftParsed = new TwoDABinaryReader(leftData).Load();
                        rightParsed = new TwoDABinaryReader(rightData).Load();
                    }
                    else if (resourceType == DiffResourceType.Tlk)
                    {
                        leftParsed = new TLKBinaryReader(leftData).Load();
                        rightParsed = new TLKBinaryReader(rightData).Load();
                    }

                    if (leftParsed != null && rightParsed != null)
                    {
                        return comparator.Compare(leftParsed, rightParsed, leftId, rightId);
                    }
                }
                catch (Exception e)
                {
                    return new DiffResult<object>
                    {
                        DiffType = DiffType.Error,
                        LeftIdentifier = leftId,
                        RightIdentifier = rightId,
                        ErrorMessage = $"Failed to parse {resourceType}: {e.Message}"
                    };
                }
            }

            // Fallback to bytes comparison
            return comparator.Compare(leftData, rightData, leftId, rightId);
        }
    }
}

