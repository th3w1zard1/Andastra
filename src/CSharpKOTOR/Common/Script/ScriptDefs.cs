using System.Collections.Generic;
using CSharpKOTOR.Common;
using CSharpKOTOR.Common.Script;

namespace CSharpKOTOR.Common.Script
{

    /// <summary>
    /// NWScript constant and function definitions for KOTOR and TSL.
    /// Generated from k1_nwscript.nss and tsl_nwscript.nss using GenerateScriptDefs tool.
    /// Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/common/scriptdefs.py
    /// </summary>
    public static class ScriptDefs
    {
        // Built-in object constants (not defined in NSS files but used as defaults)
        public const int OBJECT_SELF = 0;
        public const int OBJECT_INVALID = 1;

        /// <summary>
        /// KOTOR (Knights of the Old Republic) script constants.
        /// </summary>
        public static readonly List<ScriptConstant> KOTOR_CONSTANTS = new List<ScriptConstant>()
        {
        };

        /// <summary>
        /// TSL (The Sith Lords) script constants.
        /// </summary>
        public static readonly List<ScriptConstant> TSL_CONSTANTS = new List<ScriptConstant>()
        {
        };

        /// <summary>
        /// KOTOR (Knights of the Old Republic) script functions.
        /// </summary>
        public static readonly List<ScriptFunction> KOTOR_FUNCTIONS = new List<ScriptFunction>()
        {
        };

        /// <summary>
        /// TSL (The Sith Lords) script functions.
        /// </summary>
        public static readonly List<ScriptFunction> TSL_FUNCTIONS = new List<ScriptFunction>()
        {
        };
    }
}
