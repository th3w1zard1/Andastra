using System;
using BioWare.NET.Common;

namespace BioWare.NET.Common.Script
{

    /// <summary>
    /// NWScript data types.
    /// 
    /// References:
    ///     vendor/xoreos-tools/src/nwscript/ (NWScript definitions)
    ///     vendor/xoreos-docs/specs/ (NWScript documentation)
    ///     vendor/KotOR.js/src/nwscript/NWScriptDefK1.ts (K1 script definitions)
    ///     vendor/KotOR.js/src/nwscript/NWScriptDefK2.ts (K2 script definitions)
    ///     vendor/HoloLSP/server/src/nwscript/ (Language server NWScript definitions)
    /// </summary>
    public enum DataType
    {
        Void,
        Int,
        Float,
        String,
        Object,
        Vector,
        Location,
        Event,
        Effect,
        ItemProperty,
        Talent,
        Action,
        Struct
    }
}
