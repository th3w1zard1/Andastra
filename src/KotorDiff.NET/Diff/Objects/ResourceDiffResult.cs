// Matching PyKotor implementation at vendor/PyKotor/Libraries/PyKotor/src/pykotor/tslpatcher/diff/objects.py:67-80
// Original: @dataclass class ResourceDiffResult(DiffResult[bytes]): ...
using JetBrains.Annotations;

namespace KotorDiff.NET.Diff.Objects
{
    /// <summary>
    /// Result of comparing two resources.
    /// 1:1 port of ResourceDiffResult from vendor/PyKotor/Libraries/PyKotor/src/pykotor/tslpatcher/diff/objects.py:67-80
    /// </summary>
    public class ResourceDiffResult : DiffResult<byte[]>
    {
        [CanBeNull] public string ResourceType { get; set; }
        public int? LeftSize { get; set; }
        public int? RightSize { get; set; }

        public ResourceDiffResult()
        {
            // Calculate sizes if not provided
            if (LeftSize == null && LeftValue != null)
            {
                LeftSize = LeftValue.Length;
            }
            if (RightSize == null && RightValue != null)
            {
                RightSize = RightValue.Length;
            }
        }
    }
}

