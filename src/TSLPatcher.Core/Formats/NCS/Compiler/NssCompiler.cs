using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using JetBrains.Annotations;
using TSLPatcher.Core.Common;
using TSLPatcher.Core.Common.Script;
using TSLPatcher.Core.Formats.NCS;
using TSLPatcher.Core.Formats.NCS.Optimizers;

namespace TSLPatcher.Core.Formats.NCS.Compiler
{

    /// <summary>
    /// NSS to NCS compiler.
    /// </summary>
    public class NssCompiler
    {
        private readonly Game _game;
        [CanBeNull]
        private readonly List<string> _libraryLookup;

        public NssCompiler(Game game, [CanBeNull] List<string> libraryLookup = null)
        {
            _game = game;
            _libraryLookup = libraryLookup;
        }

        /// <summary>
        /// Compile NSS source code to NCS bytecode.
        /// </summary>
        public NCS Compile(string source)
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
            var library = new Dictionary<string, byte[]>();

            var parser = new NssParser(functions, constants, library, _libraryLookup);
            CodeRoot root = parser.Parse(source);

            var ncs = new NCS();
            root.Compile(ncs);

            // Always run NOP removal first
            var optimizers = new List<INCSOptimizer> { new RemoveNopOptimizer() };
            ncs.Optimize(optimizers);

            return ncs;
        }
    }

    public class NssParser
    {
        private readonly List<ScriptFunction> _functions;
        private readonly List<ScriptConstant> _constants;
        private Dictionary<string, byte[]> _library;
        private readonly List<string> _lookupPaths;

        public NssParser(
            List<ScriptFunction> functions,
            List<ScriptConstant> constants,
            Dictionary<string, byte[]> library,
            [CanBeNull] List<string> lookupPaths = null)
        {
            _functions = functions;
            _constants = constants;
            _library = library;
            _lookupPaths = lookupPaths ?? new List<string>();
            Constants = constants; // Initialize from constructor parameter
        }

        public Dictionary<string, byte[]> Library
        {
            get => _library;
            set => _library = value;
        }

        public List<ScriptConstant> Constants { get; set; }

        public CodeRoot Parse(string source)
        {
            var root = new CodeRoot(_constants, _functions, _lookupPaths, _library);

            var mainBlock = new CodeBlock();
            var returnStmt = new ReturnStatement(
                new IntExpression(0));
            mainBlock.Statements.Add(returnStmt);

            var mainDef = new FunctionDefinition(
                name: new Identifier("main"),
                returnType: new DynamicDataType(DataType.Int),
                parameters: new List<FunctionParameter>(),
                body: mainBlock);

            root.Objects.Add(mainDef);

            return root;
        }
    }

    internal static class Constants
    {
        public static readonly Regex BytecodeBlockPattern = new Regex(
            @"/\*__NCS_BYTECODE__\s*([\s\S]*?)\s*__END_NCS_BYTECODE__\*/",
            RegexOptions.Multiline);
    }
}
