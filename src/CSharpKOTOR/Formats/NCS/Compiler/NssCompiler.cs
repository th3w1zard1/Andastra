using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using AuroraEngine.Common.Common;
using AuroraEngine.Common.Common.Script;
using AuroraEngine.Common.Formats.NCS;
using AuroraEngine.Common.Formats.NCS.Compiler.NSS;
using AuroraEngine.Common.Formats.NCS.Optimizers;
using JetBrains.Annotations;

namespace AuroraEngine.Common.Formats.NCS.Compiler
{

    /// <summary>
    /// NSS to NCS compiler.
    /// </summary>
    public class NssCompiler
    {
        private readonly Game _game;
        [CanBeNull]
        private readonly List<string> _libraryLookup;
        private readonly bool _debug;
        [CanBeNull]
        private readonly List<ScriptFunction> _functions;
        [CanBeNull]
        private readonly List<ScriptConstant> _constants;

        public NssCompiler(Game game, [CanBeNull] List<string> libraryLookup = null, bool debug = false,
            [CanBeNull] List<ScriptFunction> functions = null, [CanBeNull] List<ScriptConstant> constants = null)
        {
            _game = game;
            _libraryLookup = libraryLookup;
            _debug = debug;
            _functions = functions;
            _constants = constants;
        }

        /// <summary>
        /// Compile NSS source code to NCS bytecode.
        /// </summary>
        public NCS Compile(string source, Dictionary<string, byte[]> library = null)
        {
            if (string.IsNullOrWhiteSpace(source))
            {
                throw new ArgumentException("Source cannot be null or empty", nameof(source));
            }

            // Use provided functions/constants or fallback to ScriptDefs
            List<ScriptFunction> functions = _functions ?? (_game.IsK1() ? ScriptDefs.KOTOR_FUNCTIONS : ScriptDefs.TSL_FUNCTIONS);
            List<ScriptConstant> constants = _constants ?? (_game.IsK1() ? ScriptDefs.KOTOR_CONSTANTS : ScriptDefs.TSL_CONSTANTS);
            // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/resource/formats/ncs/ncs_auto.py:184
            // Original: library=KOTOR_LIBRARY if game.is_k1() else TSL_LIBRARY
            var lib = library ?? (_game.IsK1() ? ScriptLib.KOTOR_LIBRARY : ScriptLib.TSL_LIBRARY);

            var parser = new NssParser(functions, constants, lib, _libraryLookup);
            CodeRoot root = parser.Parse(source);

            var ncs = new NCS();
            root.Compile(ncs);

            return ncs;
        }
    }

    // NssParser is now in NSS/NssParser.cs
}
