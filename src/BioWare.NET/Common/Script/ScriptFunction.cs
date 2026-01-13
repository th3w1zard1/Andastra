using System;
using System.Collections.Generic;
using System.Linq;

namespace BioWare.NET.Common.Script
{

    // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/common/script.py:75-104
    // Original: class ScriptFunction:
    /// <summary>
    /// Represents a NWScript function signature.
    /// 
    /// References:
    ///     vendor/xoreos-tools/src/nwscript/ (NWScript function definitions)
    ///     vendor/KotOR.js/src/nwscript/NWScriptDefK1.ts (K1 functions)
    ///     vendor/KotOR.js/src/nwscript/NWScriptDefK2.ts (K2 functions)
    /// </summary>
    public class ScriptFunction
    {
        public DataType ReturnType { get; set; }
        public string Name { get; set; }
        public List<ScriptParam> Params { get; set; }
        public string Description { get; set; }
        public string Raw { get; set; }

        public ScriptFunction(
            DataType returnType,
            string name,
            List<ScriptParam> params_,
            string description,
            string raw)
        {
            ReturnType = returnType;
            Name = name ?? throw new ArgumentNullException(nameof(name));
            Params = params_ ?? new List<ScriptParam>();
            Description = description ?? string.Empty;
            Raw = raw ?? string.Empty;
        }

        public override string ToString()
        {
            string paramStr = string.Join(", ", Params.Select(p => p.ToString()));
            return $"{ReturnType.ToScriptString()} {Name}({paramStr})";
        }
    }
}

