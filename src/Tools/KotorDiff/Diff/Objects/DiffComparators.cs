// Matching PyKotor implementation at vendor/PyKotor/Libraries/PyKotor/src/pykotor/tslpatcher/diff/objects.py:198-391
// Original: class DiffComparator(ABC, Generic[T]): ... class BytesDiffComparator, GFFDiffComparator, TwoDADiffComparator, TLKDiffComparator, LIPDiffComparator
using System;
using System.Collections.Generic;
using BioWare.NET.Resource.Formats.GFF;
using BioWare.NET.Resource.Formats.LIP;
using BioWare.NET.Resource.Formats.TLK;
using BioWare.NET.Resource.Formats.TwoDA;
using BioWare.NET.Common;
using JetBrains.Annotations;

namespace KotorDiff.Diff.Objects
{
    /// <summary>
    /// Abstract base class for diff comparators.
    /// 1:1 port of DiffComparator from vendor/PyKotor/Libraries/PyKotor/src/pykotor/tslpatcher/diff/objects.py:198-203
    /// </summary>
    public abstract class DiffComparator<T>
    {
        /// <summary>
        /// Compare two objects and return a structured diff result.
        /// </summary>
        public abstract DiffResult<T> Compare(T left, T right, string leftId, string rightId);
    }

    /// <summary>
    /// Comparator for raw bytes data.
    /// 1:1 port of BytesDiffComparator from vendor/PyKotor/Libraries/PyKotor/src/pykotor/tslpatcher/diff/objects.py:206-225
    /// </summary>
    public class BytesDiffComparator : DiffComparator<object>
    {
        public override DiffResult<object> Compare(object left, object right, string leftId, string rightId)
        {
            byte[] leftBytes = left as byte[];
            byte[] rightBytes = right as byte[];

            if (leftBytes == null && rightBytes == null)
            {
                var result = new ResourceDiffResult
                {
                    DiffType = DiffType.Identical,
                    LeftIdentifier = leftId,
                    RightIdentifier = rightId,
                    LeftValue = leftBytes,
                    RightValue = rightBytes
                };
                return ConvertToObjectResult(result);
            }

            if (leftBytes != null && rightBytes != null && leftBytes.Length == rightBytes.Length)
            {
                bool isEqual = true;
                for (int i = 0; i < leftBytes.Length; i++)
                {
                    if (leftBytes[i] != rightBytes[i])
                    {
                        isEqual = false;
                        break;
                    }
                }

                if (isEqual)
                {
                    var result = new ResourceDiffResult
                    {
                        DiffType = DiffType.Identical,
                        LeftIdentifier = leftId,
                        RightIdentifier = rightId,
                        LeftValue = leftBytes,
                        RightValue = rightBytes
                    };
                    return ConvertToObjectResult(result);
                }
            }

            var modifiedResult = new ResourceDiffResult
            {
                DiffType = DiffType.Modified,
                LeftIdentifier = leftId,
                RightIdentifier = rightId,
                LeftValue = leftBytes,
                RightValue = rightBytes
            };
            return ConvertToObjectResult(modifiedResult);
        }

        private static DiffResult<object> ConvertToObjectResult(ResourceDiffResult result)
        {
            return new DiffResult<object>
            {
                DiffType = result.DiffType,
                LeftIdentifier = result.LeftIdentifier,
                RightIdentifier = result.RightIdentifier,
                LeftValue = result.LeftValue,
                RightValue = result.RightValue,
                ErrorMessage = result.ErrorMessage,
                Details = result.Details
            };
        }
    }

    /// <summary>
    /// Comparator for GFF files using the existing compare mixin.
    /// 1:1 port of GFFDiffComparator from vendor/PyKotor/Libraries/PyKotor/src/pykotor/tslpatcher/diff/objects.py:228-266
    /// </summary>
    public class GFFDiffComparator : DiffComparator<object>
    {
        public override DiffResult<object> Compare(object left, object right, string leftId, string rightId)
        {
            try
            {
                if (!(left is GFF leftGff) || !(right is GFF rightGff))
                {
                    throw new ArgumentException("Both arguments must be GFF objects");
                }

                // Use the existing GffDiff.Compare method
                var compareResult = BioWare.NET.Diff.GffDiff.Compare(leftGff.Root, rightGff.Root);

                DiffType diffType = compareResult.Differences.Count == 0 ? DiffType.Identical : DiffType.Modified;

                var fieldDiffs = new List<FieldDiff>();
                var structDiffs = new List<StructDiff>();

                // Convert differences to FieldDiff and StructDiff
                foreach (var (path, oldValue, newValue) in compareResult.Differences)
                {
                    fieldDiffs.Add(new FieldDiff
                    {
                        FieldPath = path,
                        DiffType = DiffType.Modified,
                        LeftValue = oldValue,
                        RightValue = newValue
                    });
                }

                return new GFFDiffResult
                {
                    DiffType = diffType,
                    LeftIdentifier = leftId,
                    RightIdentifier = rightId,
                    LeftValue = left,
                    RightValue = right,
                    FieldDiffs = fieldDiffs.Count > 0 ? fieldDiffs : null,
                    StructDiffs = structDiffs.Count > 0 ? structDiffs : null
                };
            }
            catch (Exception e)
            {
                return new GFFDiffResult
                {
                    DiffType = DiffType.Error,
                    LeftIdentifier = leftId,
                    RightIdentifier = rightId,
                    ErrorMessage = e.Message
                };
            }
        }
    }

    /// <summary>
    /// Comparator for 2DA files using the existing compare mixin.
    /// 1:1 port of TwoDADiffComparator from vendor/PyKotor/Libraries/PyKotor/src/pykotor/tslpatcher/diff/objects.py:269-313
    /// </summary>
    public class TwoDADiffComparator : DiffComparator<object>
    {
        public override DiffResult<object> Compare(object left, object right, string leftId, string rightId)
        {
            try
            {
                if (!(left is TwoDA left2da) || !(right is TwoDA right2da))
                {
                    throw new ArgumentException("Both arguments must be 2DA objects");
                }

                // Use StructuredDiffEngine for 2DA comparison
                var structuredEngine = new StructuredDiffEngine();
                var result = structuredEngine.Compare2DA(
                    BioWare.NET.Resource.Formats.TwoDA.TwoDAAuto.Bytes2DA(left2da, BioWare.NET.Common.ResourceType.TwoDA),
                    BioWare.NET.Resource.Formats.TwoDA.TwoDAAuto.Bytes2DA(right2da, BioWare.NET.Common.ResourceType.TwoDA),
                    leftId,
                    rightId);

                return new TwoDADiffResult
                {
                    DiffType = result.DiffType,
                    LeftIdentifier = result.LeftIdentifier,
                    RightIdentifier = result.RightIdentifier,
                    LeftValue = left,
                    RightValue = right,
                    HeaderDiffs = result.HeaderDiffs,
                    RowDiffs = result.RowDiffs,
                    ColumnDiffs = result.ColumnDiffs
                };
            }
            catch (Exception e)
            {
                return new TwoDADiffResult
                {
                    DiffType = DiffType.Error,
                    LeftIdentifier = leftId,
                    RightIdentifier = rightId,
                    ErrorMessage = e.Message
                };
            }
        }
    }

    /// <summary>
    /// Comparator for TLK files using the existing compare mixin.
    /// 1:1 port of TLKDiffComparator from vendor/PyKotor/Libraries/PyKotor/src/pykotor/tslpatcher/diff/objects.py:316-349
    /// </summary>
    public class TLKDiffComparator : DiffComparator<object>
    {
        public override DiffResult<object> Compare(object left, object right, string leftId, string rightId)
        {
            try
            {
                if (!(left is TLK leftTlk) || !(right is TLK rightTlk))
                {
                    throw new ArgumentException("Both arguments must be TLK objects");
                }

                // Use the existing TlkDiff.Compare method
                var compareResult = BioWare.NET.Diff.TlkDiff.Compare(leftTlk, rightTlk);

                var entryDiffs = new List<TLKEntryDiff>();

                // Convert differences to TLKEntryDiff
                foreach (var kvp in compareResult.ChangedEntries)
                {
                    entryDiffs.Add(new TLKEntryDiff
                    {
                        EntryId = kvp.Key,
                        DiffType = DiffType.Modified,
                        LeftText = kvp.Value.Text,
                        RightText = kvp.Value.Text,
                        LeftVoiceover = kvp.Value.Sound,
                        RightVoiceover = kvp.Value.Sound
                    });
                }

                foreach (var kvp in compareResult.AddedEntries)
                {
                    entryDiffs.Add(new TLKEntryDiff
                    {
                        EntryId = kvp.Key,
                        DiffType = DiffType.Added,
                        RightText = kvp.Value.Text,
                        RightVoiceover = kvp.Value.Sound
                    });
                }

                DiffType diffType = entryDiffs.Count == 0 ? DiffType.Identical : DiffType.Modified;

                return new TLKDiffResult
                {
                    DiffType = diffType,
                    LeftIdentifier = leftId,
                    RightIdentifier = rightId,
                    LeftValue = left,
                    RightValue = right,
                    EntryDiffs = entryDiffs.Count > 0 ? entryDiffs : null
                };
            }
            catch (Exception e)
            {
                return new TLKDiffResult
                {
                    DiffType = DiffType.Error,
                    LeftIdentifier = leftId,
                    RightIdentifier = rightId,
                    ErrorMessage = e.Message
                };
            }
        }
    }

    /// <summary>
    /// Comparator for LIP files using the existing compare mixin.
    /// 1:1 port of LIPDiffComparator from vendor/PyKotor/Libraries/PyKotor/src/pykotor/tslpatcher/diff/objects.py:352-391
    /// </summary>
    public class LIPDiffComparator : DiffComparator<object>
    {
        public override DiffResult<object> Compare(object left, object right, string leftId, string rightId)
        {
            try
            {
                if (!(left is LIP leftLip) || !(right is LIP rightLip))
                {
                    throw new ArgumentException("Both arguments must be LIP objects");
                }

                var entryDiffs = new List<LIPEntryDiff>();

                // Compare LIP entries (keyframes)
                int maxCount = Math.Max(leftLip.Count, rightLip.Count);
                for (int i = 0; i < maxCount; i++)
                {
                    bool leftExists = i < leftLip.Count;
                    bool rightExists = i < rightLip.Count;

                    if (!leftExists && rightExists)
                    {
                        var rightKeyFrame = rightLip[i];
                        entryDiffs.Add(new LIPEntryDiff
                        {
                            EntryId = i,
                            DiffType = DiffType.Added,
                            RightTime = rightKeyFrame.Time,
                            RightShape = rightKeyFrame.Shape
                        });
                    }
                    else if (leftExists && !rightExists)
                    {
                        var leftKeyFrame = leftLip[i];
                        entryDiffs.Add(new LIPEntryDiff
                        {
                            EntryId = i,
                            DiffType = DiffType.Removed,
                            LeftTime = leftKeyFrame.Time,
                            LeftShape = leftKeyFrame.Shape
                        });
                    }
                    else if (leftExists && rightExists)
                    {
                        var leftKeyFrame = leftLip[i];
                        var rightKeyFrame = rightLip[i];

                        if (Math.Abs(leftKeyFrame.Time - rightKeyFrame.Time) > 0.0001f ||
                            leftKeyFrame.Shape != rightKeyFrame.Shape)
                        {
                            entryDiffs.Add(new LIPEntryDiff
                            {
                                EntryId = i,
                                DiffType = DiffType.Modified,
                                LeftTime = leftKeyFrame.Time,
                                RightTime = rightKeyFrame.Time,
                                LeftShape = leftKeyFrame.Shape,
                                RightShape = rightKeyFrame.Shape
                            });
                        }
                    }
                }

                DiffType diffType = entryDiffs.Count == 0 ? DiffType.Identical : DiffType.Modified;

                return new LIPDiffResult
                {
                    DiffType = diffType,
                    LeftIdentifier = leftId,
                    RightIdentifier = rightId,
                    LeftValue = left,
                    RightValue = right,
                    EntryDiffs = entryDiffs.Count > 0 ? entryDiffs : null
                };
            }
            catch (Exception e)
            {
                return new LIPDiffResult
                {
                    DiffType = DiffType.Error,
                    LeftIdentifier = leftId,
                    RightIdentifier = rightId,
                    ErrorMessage = e.Message
                };
            }
        }
    }
}

