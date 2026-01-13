using System;

namespace BioWare.NET.Common.Script
{

    // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/common/script.py:19-49
    // Original: class ScriptConstant:
    /// <summary>
    /// Represents a NWScript constant definition.
    /// 
    /// References:
    ///     vendor/xoreos-tools/src/nwscript/ (NWScript definitions)
    ///     vendor/KotOR.js/src/nwscript/NWScriptDefK1.ts (K1 constants)
    ///     vendor/KotOR.js/src/nwscript/NWScriptDefK2.ts (K2 constants)
    /// </summary>
    public class ScriptConstant
    {
        public DataType DataType { get; set; }
        public string Name { get; set; }
        public object Value { get; set; }

        public ScriptConstant(DataType dataType, string name, object value)
        {
            DataType = dataType;
            Name = name ?? throw new ArgumentNullException(nameof(name));
            Value = value ?? throw new ArgumentNullException(nameof(value));

            if (dataType == DataType.Int && !(value is int))
            {
                throw new ArgumentException("Script constant value argument does not match given datatype INT.");
            }
            if (dataType == DataType.Float && !(value is float || value is double))
            {
                throw new ArgumentException("Script constant value argument does not match given datatype FLOAT.");
            }
            if (dataType == DataType.String && !(value is string))
            {
                throw new ArgumentException("Script constant value argument does not match given datatype STRING.");
            }
        }

        public override string ToString()
        {
            return $"{DataType.ToScriptString()} {Name} = {Value};";
        }
    }
}

