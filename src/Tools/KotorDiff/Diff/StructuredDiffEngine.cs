// Matching PyKotor implementation at vendor/PyKotor/Libraries/PyKotor/src/pykotor/tslpatcher/diff/structured.py:38-607
// Original: class StructuredDiffEngine: ...
using System;
using System.Collections.Generic;
using System.Linq;
using BioWare.NET.Resource.Formats.GFF;
using BioWare.NET.Resource.Formats.TLK;
using BioWare.NET.Resource.Formats.TwoDA;
using KotorDiff.Diff.Objects;
using JetBrains.Annotations;

namespace KotorDiff.Diff
{
    /// <summary>
    /// Engine for generating structured, detailed diff results.
    /// 1:1 port of StructuredDiffEngine from vendor/PyKotor/Libraries/PyKotor/src/pykotor/tslpatcher/diff/structured.py:38-607
    /// </summary>
    public class StructuredDiffEngine
    {
        /// <summary>
        /// Compare two 2DA files and return structured diff.
        /// Matching PyKotor implementation at vendor/PyKotor/Libraries/PyKotor/src/pykotor/tslpatcher/diff/structured.py:41-87
        /// </summary>
        public TwoDADiffResult Compare2DA(
            byte[] leftData,
            byte[] rightData,
            string leftId,
            string rightId)
        {
            try
            {
                var leftReader = new BioWare.NET.Resource.Formats.TwoDA.TwoDABinaryReader(leftData);
                var rightReader = new BioWare.NET.Resource.Formats.TwoDA.TwoDABinaryReader(rightData);
                var left2da = leftReader.Load();
                var right2da = rightReader.Load();

                // Check for differences
                var headerDiffs = Compare2DAHeaders(left2da, right2da);
                var columnDiffs = Compare2DAColumns(left2da, right2da);
                var rowDiffs = Compare2DARows(left2da, right2da);

                // Determine overall diff type
                DiffType diffType;
                if (headerDiffs.Count == 0 && columnDiffs.Count == 0 && rowDiffs.Count == 0)
                {
                    diffType = DiffType.Identical;
                }
                else
                {
                    diffType = DiffType.Modified;
                }

                return new TwoDADiffResult
                {
                    DiffType = diffType,
                    LeftIdentifier = leftId,
                    RightIdentifier = rightId,
                    LeftValue = leftData,
                    RightValue = rightData,
                    HeaderDiffs = headerDiffs,
                    RowDiffs = rowDiffs,
                    ColumnDiffs = columnDiffs
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

        /// <summary>
        /// Compare 2DA headers.
        /// Matching PyKotor implementation at vendor/PyKotor/Libraries/PyKotor/src/pykotor/tslpatcher/diff/structured.py:89-127
        /// </summary>
        private static List<HeaderDiff> Compare2DAHeaders(TwoDA left2da, TwoDA right2da)
        {
            var headerDiffs = new List<HeaderDiff>();
            var leftHeaders = left2da.GetHeaders();
            var rightHeaders = right2da.GetHeaders();

            int maxLen = Math.Max(leftHeaders.Count, rightHeaders.Count);

            for (int idx = 0; idx < maxLen; idx++)
            {
                string leftHeader = idx < leftHeaders.Count ? leftHeaders[idx] : null;
                string rightHeader = idx < rightHeaders.Count ? rightHeaders[idx] : null;

                if (leftHeader != rightHeader)
                {
                    DiffType diffType;
                    if (leftHeader == null)
                    {
                        diffType = DiffType.Added;
                    }
                    else if (rightHeader == null)
                    {
                        diffType = DiffType.Removed;
                    }
                    else
                    {
                        diffType = DiffType.Modified;
                    }

                    headerDiffs.Add(new HeaderDiff
                    {
                        ColumnIndex = idx,
                        DiffType = diffType,
                        LeftHeader = leftHeader,
                        RightHeader = rightHeader
                    });
                }
            }

            return headerDiffs;
        }

        /// <summary>
        /// Compare 2DA columns.
        /// Matching PyKotor implementation at vendor/PyKotor/Libraries/PyKotor/src/pykotor/tslpatcher/diff/structured.py:129-168
        /// </summary>
            private static List<ColumnDiff> Compare2DAColumns(TwoDA left2da, TwoDA right2da)
        {
            var columnDiffs = new List<ColumnDiff>();
            var leftHeaders = new HashSet<string>(left2da.GetHeaders());
            var rightHeaders = new HashSet<string>(right2da.GetHeaders());

            // Added columns
            var addedColumns = rightHeaders.Except(leftHeaders).ToList();
            foreach (var colName in addedColumns)
            {
                int colIndex = right2da.GetHeaders().IndexOf(colName);
                columnDiffs.Add(new ColumnDiff
                {
                    ColumnIndex = colIndex,
                    ColumnName = colName,
                    DiffType = DiffType.Added
                });
            }

            // Removed columns
            var removedColumns = leftHeaders.Except(rightHeaders).ToList();
            foreach (var colName in removedColumns)
            {
                int colIndex = left2da.GetHeaders().IndexOf(colName);
                columnDiffs.Add(new ColumnDiff
                {
                    ColumnIndex = colIndex,
                    ColumnName = colName,
                    DiffType = DiffType.Removed
                });
            }

            return columnDiffs;
        }

        /// <summary>
        /// Compare 2DA rows.
        /// Matching PyKotor implementation at vendor/PyKotor/Libraries/PyKotor/src/pykotor/tslpatcher/diff/structured.py:170-253
        /// </summary>
        private static List<RowDiff> Compare2DARows(TwoDA left2da, TwoDA right2da)
        {
            var rowDiffs = new List<RowDiff>();
            int leftHeight = left2da.GetHeight();
            int rightHeight = right2da.GetHeight();

            var commonHeaders = left2da.GetHeaders().Where(h => right2da.GetHeaders().Contains(h)).ToList();

            // Check existing rows
            for (int rowIdx = 0; rowIdx < Math.Min(leftHeight, rightHeight); rowIdx++)
            {
                var cellDiffs = new List<CellDiff>();

                for (int colIdx = 0; colIdx < commonHeaders.Count; colIdx++)
                {
                    string header = commonHeaders[colIdx];
                    string leftValue = left2da.GetCellString(rowIdx, header);
                    string rightValue = right2da.GetCellString(rowIdx, header);

                    if (leftValue != rightValue)
                    {
                        cellDiffs.Add(new CellDiff
                        {
                            RowIndex = rowIdx,
                            ColumnIndex = colIdx,
                            ColumnName = header,
                            DiffType = DiffType.Modified,
                            LeftValue = leftValue,
                            RightValue = rightValue
                        });
                    }
                }

                if (cellDiffs.Count > 0)
                {
                    rowDiffs.Add(new RowDiff
                    {
                        RowIndex = rowIdx,
                        DiffType = DiffType.Modified,
                        CellDiffs = cellDiffs
                    });
                }
            }

            // Added rows
            if (rightHeight > leftHeight)
            {
                for (int rowIdx = leftHeight; rowIdx < rightHeight; rowIdx++)
                {
                    var cellDiffs = new List<CellDiff>();
                    var rightHeaders = right2da.GetHeaders();

                    for (int colIdx = 0; colIdx < rightHeaders.Count; colIdx++)
                    {
                        string header = rightHeaders[colIdx];
                        string cellValue = right2da.GetCellString(rowIdx, header);
                        cellDiffs.Add(new CellDiff
                        {
                            RowIndex = rowIdx,
                            ColumnIndex = colIdx,
                            ColumnName = header,
                            DiffType = DiffType.Added,
                            LeftValue = null,
                            RightValue = cellValue
                        });
                    }

                    rowDiffs.Add(new RowDiff
                    {
                        RowIndex = rowIdx,
                        DiffType = DiffType.Added,
                        CellDiffs = cellDiffs
                    });
                }
            }

            // Removed rows
            if (leftHeight > rightHeight)
            {
                for (int rowIdx = rightHeight; rowIdx < leftHeight; rowIdx++)
                {
                    rowDiffs.Add(new RowDiff
                    {
                        RowIndex = rowIdx,
                        DiffType = DiffType.Removed,
                        CellDiffs = new List<CellDiff>()
                    });
                }
            }

            return rowDiffs;
        }

        /// <summary>
        /// Compare two GFF files and return structured diff.
        /// Matching PyKotor implementation at vendor/PyKotor/Libraries/PyKotor/src/pykotor/tslpatcher/diff/structured.py:255-301
        /// </summary>
        public GFFDiffResult CompareGff(
            byte[] leftData,
            byte[] rightData,
            string leftId,
            string rightId)
        {
            try
            {
                var leftReader = new BioWare.NET.Resource.Formats.GFF.GFFBinaryReader(leftData);
                var rightReader = new BioWare.NET.Resource.Formats.GFF.GFFBinaryReader(rightData);
                var leftGff = leftReader.Load();
                var rightGff = rightReader.Load();

                var (fieldDiffs, structDiffs) = CompareGffStructs(leftGff.Root, rightGff.Root, "");

                // Determine overall diff type
                DiffType diffType;
                if (fieldDiffs.Count == 0 && structDiffs.Count == 0)
                {
                    diffType = DiffType.Identical;
                }
                else
                {
                    diffType = DiffType.Modified;
                }

                return new GFFDiffResult
                {
                    DiffType = diffType,
                    LeftIdentifier = leftId,
                    RightIdentifier = rightId,
                    LeftValue = leftData,
                    RightValue = rightData,
                    FieldDiffs = fieldDiffs,
                    StructDiffs = structDiffs
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

        /// <summary>
        /// Recursively compare GFF structs.
        /// Matching PyKotor implementation at vendor/PyKotor/Libraries/PyKotor/src/pykotor/tslpatcher/diff/structured.py:303-399
        /// </summary>
        private (List<FieldDiff> fieldDiffs, List<StructDiff> structDiffs) CompareGffStructs(
            GFFStruct leftStruct,
            GFFStruct rightStruct,
            string path)
        {
            var fieldDiffs = new List<FieldDiff>();
            var structDiffs = new List<StructDiff>();

            // Get all field labels from structs
            var leftFields = new HashSet<string>();
            var rightFields = new HashSet<string>();

            foreach (var (label, fieldType, value) in leftStruct)
            {
                leftFields.Add(label);
            }

            foreach (var (label, fieldType, value) in rightStruct)
            {
                rightFields.Add(label);
            }

            // Common fields - check for modifications
            var commonFields = leftFields.Intersect(rightFields).ToList();
            foreach (var fieldLabel in commonFields)
            {
                string fieldPath = string.IsNullOrEmpty(path) ? fieldLabel : $"{path}/{fieldLabel}";
                var fieldDiff = CompareGffField(leftStruct, rightStruct, fieldLabel, fieldPath);

                if (fieldDiff != null)
                {
                    // TODO:  Handle nested struct diffs (simplified for now)
                    fieldDiffs.Add(fieldDiff);
                }
            }

            // Added fields
            var addedFields = rightFields.Except(leftFields).ToList();
            foreach (var fieldLabel in addedFields)
            {
                string fieldPath = string.IsNullOrEmpty(path) ? fieldLabel : $"{path}/{fieldLabel}";
                try
                {
                    if (!rightStruct.Exists(fieldLabel))
                    {
                        continue;
                    }

                    var fieldType = rightStruct.GetFieldType(fieldLabel);
                    if (!fieldType.HasValue)
                    {
                        continue;
                    }

                    fieldDiffs.Add(new FieldDiff
                    {
                        FieldPath = fieldPath,
                        DiffType = DiffType.Added,
                        LeftValue = null,
                        RightValue = GetGffFieldValue(rightStruct, fieldLabel, fieldType.Value),
                        FieldType = fieldType.Value.ToString()
                    });
                }
                catch (Exception)
                {
                    continue;
                }
            }

            // Removed fields
            var removedFields = leftFields.Except(rightFields).ToList();
            foreach (var fieldLabel in removedFields)
            {
                string fieldPath = string.IsNullOrEmpty(path) ? fieldLabel : $"{path}/{fieldLabel}";
                try
                {
                    if (!leftStruct.Exists(fieldLabel))
                    {
                        continue;
                    }

                    var fieldType = leftStruct.GetFieldType(fieldLabel);
                    if (!fieldType.HasValue)
                    {
                        continue;
                    }

                    fieldDiffs.Add(new FieldDiff
                    {
                        FieldPath = fieldPath,
                        DiffType = DiffType.Removed,
                        LeftValue = GetGffFieldValue(leftStruct, fieldLabel, fieldType.Value),
                        RightValue = null,
                        FieldType = fieldType.Value.ToString()
                    });
                }
                catch (Exception)
                {
                    continue;
                }
            }

            return (fieldDiffs, structDiffs);
        }

        /// <summary>
        /// Compare a specific GFF field.
        /// Matching PyKotor implementation at vendor/PyKotor/Libraries/PyKotor/src/pykotor/tslpatcher/diff/structured.py:401-470
        /// </summary>
        [CanBeNull]
        private FieldDiff CompareGffField(
            GFFStruct leftStruct,
            GFFStruct rightStruct,
            string fieldLabel,
            string fieldPath)
        {
            try
            {
                if (!leftStruct.Exists(fieldLabel) || !rightStruct.Exists(fieldLabel))
                {
                    return null;
                }

                var leftFieldType = leftStruct.GetFieldType(fieldLabel);
                var rightFieldType = rightStruct.GetFieldType(fieldLabel);

                if (!leftFieldType.HasValue || !rightFieldType.HasValue)
                {
                    return null;
                }

                var leftType = leftFieldType.Value;
                var rightType = rightFieldType.Value;

                if (leftType != rightType)
                {
                    // Type changed
                    return new FieldDiff
                    {
                        FieldPath = fieldPath,
                        DiffType = DiffType.Modified,
                        LeftValue = leftType.ToString(),
                        RightValue = rightType.ToString(),
                        FieldType = $"TYPE_CHANGE: {leftType} -> {rightType}"
                    };
                }

                // Get field values first
                var leftValue = GetGffFieldValue(leftStruct, fieldLabel, leftType);
                var rightValue = GetGffFieldValue(rightStruct, fieldLabel, rightType);

                // Handle nested structures
                if (leftType == GFFFieldType.Struct)
                {
                    // Compare nested structs recursively using existing CompareGffStructs method
                    var leftStructValue = leftValue as GFFStruct;
                    var rightStructValue = rightValue as GFFStruct;
                    if (leftStructValue != null && rightStructValue != null)
                    {
                        var (fieldDiffs, structDiffs) = CompareGffStructs(leftStructValue, rightStructValue, "");
                        // Return null if identical, otherwise return a diff indicating nested differences
                        if (fieldDiffs.Count == 0 && structDiffs.Count == 0)
                        {
                            return null;
                        }
                        return new FieldDiff
                        {
                            FieldPath = fieldPath,
                            DiffType = DiffType.Modified,
                            LeftValue = leftStructValue,
                            RightValue = rightStructValue,
                            FieldType = leftType.ToString()
                        };
                    }
                    if (leftStructValue != rightStructValue)
                    {
                        return new FieldDiff
                        {
                            FieldPath = fieldPath,
                            DiffType = DiffType.Modified,
                            LeftValue = leftStructValue,
                            RightValue = rightStructValue,
                            FieldType = leftType.ToString()
                        };
                    }
                    return null;
                }

                if (!GffValuesEqual(leftValue, rightValue))
                {
                    return new FieldDiff
                    {
                        FieldPath = fieldPath,
                        DiffType = DiffType.Modified,
                        LeftValue = leftValue,
                        RightValue = rightValue,
                        FieldType = leftType.ToString()
                    };
                }

                return null;
            }
            catch (Exception)
            {
                return null;
            }
        }

        /// <summary>
        /// Get GFF field value.
        /// Matching PyKotor implementation at vendor/PyKotor/Libraries/PyKotor/src/pykotor/tslpatcher/diff/structured.py:472-501
        /// </summary>
        [CanBeNull]
        private static object GetGffFieldValue(GFFStruct structObj, string fieldLabel, GFFFieldType fieldType)
        {
            try
            {
                switch (fieldType)
                {
                    case GFFFieldType.UInt8: return structObj.GetUInt8(fieldLabel);
                    case GFFFieldType.Int8: return structObj.GetInt8(fieldLabel);
                    case GFFFieldType.UInt16: return structObj.GetUInt16(fieldLabel);
                    case GFFFieldType.Int16: return structObj.GetInt16(fieldLabel);
                    case GFFFieldType.UInt32: return structObj.GetUInt32(fieldLabel);
                    case GFFFieldType.Int32: return structObj.GetInt32(fieldLabel);
                    case GFFFieldType.UInt64: return structObj.GetUInt64(fieldLabel);
                    case GFFFieldType.Int64: return structObj.GetInt64(fieldLabel);
                    case GFFFieldType.Single: return structObj.GetSingle(fieldLabel);
                    case GFFFieldType.Double: return structObj.GetDouble(fieldLabel);
                    case GFFFieldType.String: return structObj.GetString(fieldLabel);
                    case GFFFieldType.ResRef: return structObj.GetResRef(fieldLabel);
                    case GFFFieldType.LocalizedString: return structObj.GetLocString(fieldLabel);
                    case GFFFieldType.Vector3: return structObj.GetVector3(fieldLabel);
                    case GFFFieldType.Vector4: return structObj.GetVector4(fieldLabel);
                    case GFFFieldType.Binary: return structObj.GetBinary(fieldLabel);
                    default: return null;
                }
            }
            catch (Exception)
            {
                return null;
            }
        }

        /// <summary>
        /// Check if two GFF values are equal.
        /// Matching PyKotor implementation at vendor/PyKotor/Libraries/PyKotor/src/pykotor/tslpatcher/diff/structured.py:503-507
        /// </summary>
        private static bool GffValuesEqual(object left, object right)
        {
            if (left is float leftFloat && right is float rightFloat)
            {
                return Math.Abs(leftFloat - rightFloat) < 1e-6;
            }
            return Equals(left, right);
        }

        /// <summary>
        /// Compare two TLK files and return structured diff.
        /// Matching PyKotor implementation at vendor/PyKotor/Libraries/PyKotor/src/pykotor/tslpatcher/diff/structured.py:509-606
        /// </summary>
        public static TLKDiffResult CompareTlk(
            byte[] leftData,
            byte[] rightData,
            string leftId,
            string rightId)
        {
            try
            {
                var leftReader = new BioWare.NET.Resource.Formats.TLK.TLKBinaryReader(leftData);
                var rightReader = new BioWare.NET.Resource.Formats.TLK.TLKBinaryReader(rightData);
                var leftTlk = leftReader.Load();
                var rightTlk = rightReader.Load();

                var entryDiffs = new List<TLKEntryDiff>();

                int leftSize = leftTlk.Count;
                int rightSize = rightTlk.Count;

                // Compare existing entries
                for (int idx = 0; idx < Math.Min(leftSize, rightSize); idx++)
                {
                    var leftEntry = leftTlk.Get(idx);
                    var rightEntry = rightTlk.Get(idx);

                    if (leftEntry == null || rightEntry == null)
                    {
                        continue;
                    }

                    if (leftEntry.Text != rightEntry.Text || leftEntry.Voiceover.ToString() != rightEntry.Voiceover.ToString())
                    {
                        entryDiffs.Add(new TLKEntryDiff
                        {
                            EntryId = idx,
                            DiffType = DiffType.Modified,
                            LeftText = leftEntry.Text,
                            RightText = rightEntry.Text,
                            LeftVoiceover = leftEntry.Voiceover.ToString(),
                            RightVoiceover = rightEntry.Voiceover.ToString()
                        });
                    }
                }

                // Added entries
                if (rightSize > leftSize)
                {
                    for (int idx = leftSize; idx < rightSize; idx++)
                    {
                        var rightEntry = rightTlk.Get(idx);
                        if (rightEntry == null)
                        {
                            continue;
                        }
                        entryDiffs.Add(new TLKEntryDiff
                        {
                            EntryId = idx,
                            DiffType = DiffType.Added,
                            LeftText = null,
                            RightText = rightEntry.Text,
                            LeftVoiceover = null,
                            RightVoiceover = rightEntry.Voiceover.ToString()
                        });
                    }
                }

                // Removed entries
                if (leftSize > rightSize)
                {
                    for (int idx = rightSize; idx < leftSize; idx++)
                    {
                        var leftEntry = leftTlk.Get(idx);
                        if (leftEntry == null)
                        {
                            continue;
                        }
                        entryDiffs.Add(new TLKEntryDiff
                        {
                            EntryId = idx,
                            DiffType = DiffType.Removed,
                            LeftText = leftEntry.Text,
                            RightText = null,
                            LeftVoiceover = leftEntry.Voiceover.ToString(),
                            RightVoiceover = null
                        });
                    }
                }

                // Determine overall diff type
                DiffType diffType = entryDiffs.Count == 0 ? DiffType.Identical : DiffType.Modified;

                return new TLKDiffResult
                {
                    DiffType = diffType,
                    LeftIdentifier = leftId,
                    RightIdentifier = rightId,
                    LeftValue = leftData,
                    RightValue = rightData,
                    EntryDiffs = entryDiffs
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
}

