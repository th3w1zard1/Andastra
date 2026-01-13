using System;

namespace BioWare.NET.Common.Script
{

    // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/common/script.py:120-129
    // Original: def size(self) -> int:
    /// <summary>
    /// Extension methods for DataType enum.
    /// 1:1 port from Python DataType methods in pykotor/common/script.py
    /// </summary>
    public static class DataTypeExtensions
    {
        /// <summary>
        /// Get the size in bytes for a data type.
        /// </summary>
        public static int Size(this DataType dataType)
        {
            if (dataType == DataType.Void)
            {
                return 0;
            }
            if (dataType == DataType.Vector)
            {
                return 12;
            }
            if (dataType == DataType.Struct)
            {
                throw new ArgumentException("Structs are variable size");
            }
            return 4;
        }

        /// <summary>
        /// Get the size in bytes for a data type (alias for Size).
        /// </summary>
        public static int GetSize(this DataType dataType)
        {
            return Size(dataType);
        }

        /// <summary>
        /// Convert DataType to its script string representation.
        /// </summary>
        public static string ToScriptString(this DataType dataType)
        {
            switch (dataType)
            {
                case DataType.Void:
                    return "void";
                case DataType.Int:
                    return "int";
                case DataType.Float:
                    return "float";
                case DataType.String:
                    return "string";
                case DataType.Object:
                    return "object";
                case DataType.Vector:
                    return "vector";
                case DataType.Location:
                    return "location";
                case DataType.Event:
                    return "event";
                case DataType.Effect:
                    return "effect";
                case DataType.ItemProperty:
                    return "itemproperty";
                case DataType.Talent:
                    return "talent";
                case DataType.Action:
                    return "action";
                case DataType.Struct:
                    return "struct";
                default:
                    return dataType.ToString().ToLowerInvariant();
            }
        }

    }
}
