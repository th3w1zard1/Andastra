// Matching PyKotor implementation at vendor/PyKotor/Libraries/PyKotor/src/pykotor/tslpatcher/diff/objects.py:44-64
// Original: @dataclass class DiffResult(Generic[T]): ...
using System;
using System.Collections.Generic;
using JetBrains.Annotations;

namespace KotorDiff.NET.Diff.Objects
{
    /// <summary>
    /// Base result of a diff operation.
    /// 1:1 port of DiffResult from vendor/PyKotor/Libraries/PyKotor/src/pykotor/tslpatcher/diff/objects.py:44-64
    /// </summary>
    public class DiffResult<T>
    {
        public DiffType DiffType { get; set; }
        public string LeftIdentifier { get; set; }
        public string RightIdentifier { get; set; }
        [CanBeNull] public T LeftValue { get; set; }
        [CanBeNull] public T RightValue { get; set; }
        [CanBeNull] public string ErrorMessage { get; set; }
        [CanBeNull] public Dictionary<string, object> Details { get; set; }

        public DiffResult()
        {
            Details = new Dictionary<string, object>();
        }

        /// <summary>
        /// Check if the items are different.
        /// </summary>
        public bool IsDifferent => DiffType != DiffType.Identical;

        /// <summary>
        /// Check if there was an error during comparison.
        /// </summary>
        public bool HasError => DiffType == DiffType.Error;
    }
}

