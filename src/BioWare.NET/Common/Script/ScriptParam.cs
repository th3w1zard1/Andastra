using System;
using JetBrains.Annotations;

namespace BioWare.NET.Common.Script
{

    // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/common/script.py:51-73
    // Original: class ScriptParam:
    /// <summary>
    /// Represents a parameter in a NWScript function signature.
    /// 
    /// References:
    ///     vendor/xoreos-tools/src/nwscript/ (NWScript parameter definitions)
    ///     vendor/KotOR.js/src/nwscript/NWScriptDefK1.ts (K1 function parameters)
    /// </summary>
    public class ScriptParam
    {
        public DataType DataType { get; set; }
        public string Name { get; set; }
        [CanBeNull]
        public object Default { get; set; }

        public ScriptParam(DataType dataType, string name, [CanBeNull] object defaultValue = null)
        {
            DataType = dataType;
            Name = name ?? throw new ArgumentNullException(nameof(name));
            Default = defaultValue;
        }

        public override string ToString()
        {
            if (Default != null)
            {
                return $"{DataType.ToScriptString()} {Name} = {Default}";
            }
            return $"{DataType.ToScriptString()} {Name}";
        }
    }
}

