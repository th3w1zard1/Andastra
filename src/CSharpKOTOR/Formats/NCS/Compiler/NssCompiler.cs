using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using CSharpKOTOR.Common;
using CSharpKOTOR.Common.Script;
using CSharpKOTOR.Formats.NCS;
using CSharpKOTOR.Formats.NCS.Compiler.NSS;
using CSharpKOTOR.Formats.NCS.Optimizers;
using JetBrains.Annotations;

namespace CSharpKOTOR.Formats.NCS.Compiler
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

        public NssCompiler(Game game, [CanBeNull] List<string> libraryLookup = null, bool debug = false)
        {
            _game = game;
            _libraryLookup = libraryLookup;
            _debug = debug;
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

            // Embedded bytecode block takes priority (lossless roundtrip)
            Match bytecodeMatch = Constants.BytecodeBlockPattern.Match(source);
            if (bytecodeMatch.Success)
            {
                string encoded = string.Concat(bytecodeMatch.Groups[1].Value.Split());
                try
                {
                    byte[] data = Convert.FromBase64String(encoded);
                    using (var reader = new NCSBinaryReader(data))
                    {
                        return reader.Load();
                    }
                }
                catch (Exception ex)
                {
                    // Fall through to normal compilation
                    Console.WriteLine($"Warning: Failed to decode embedded NCS bytecode: {ex.Message}");
                }
            }

            List<ScriptFunction> functions = _game.IsK1() ? ScriptDefs.KOTOR_FUNCTIONS : ScriptDefs.TSL_FUNCTIONS;
            List<ScriptConstant> constants = _game.IsK1() ? ScriptDefs.KOTOR_CONSTANTS : ScriptDefs.TSL_CONSTANTS;
            var lib = library ?? new Dictionary<string, byte[]>();

            var parser = new NssParser(functions, constants, lib, _libraryLookup);
            CodeRoot root = parser.Parse(source);

            var ncs = new NCS();
            root.Compile(ncs);

            return ncs;
        }
    }

    // NssParser is now in NSS/NssParser.cs

    internal static class Constants
    {
        public static readonly Regex BytecodeBlockPattern = new Regex(
            @"/\*__NCS_BYTECODE__\s*([\s\S]*?)\s*__END_NCS_BYTECODE__\*/",
            RegexOptions.Multiline);
    }
}
