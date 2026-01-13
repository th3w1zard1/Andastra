namespace BioWare.NET.Utility
{
    // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/common/stream.py:45-53
    // Original: class ArrayHead:
    /// <summary>
    /// Represents an offset/length pair for array data in binary streams.
    /// </summary>
    public struct ArrayHead
    {
        public int Offset { get; set; }
        public int Length { get; set; }

        public ArrayHead(int arrayOffset = 0, int arrayLength = 0)
        {
            Offset = arrayOffset;
            Length = arrayLength;
        }
    }
}

