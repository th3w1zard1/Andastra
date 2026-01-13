using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using BioWare.NET.Common;
using BioWare.NET.Common.Script;
using BioWare.NET.Resource.Formats.NCS.Compiler;
using BioWare.NET.Resource.Formats.NCS.Compiler.NSS;
using BioWare.NET.Resource.Formats.NCS.Decomp;
using BioWare.NET.Resource.Formats.NCS.Decomp.Utils;
using BioWare.NET.Resource.Formats.NCS.Optimizers;
using JetBrains.Annotations;

namespace BioWare.NET.Resource.Formats.NCS
{
    /// <summary>
    /// Auto-implementation of NCS read/write/compile/decompile operations.
    /// 1:1 port from pykotor.resource.formats.ncs.ncs_auto
    /// </summary>
    public static class NCSAuto
    {
        // Matching Python: BYTECODE_BLOCK_PATTERN = re.compile(r"/\*__NCS_BYTECODE__\s*([\s\S]*?)\s*__END_NCS_BYTECODE__\*/", re.MULTILINE)
        private static readonly Regex BytecodeBlockPattern = new Regex(
            @"/\*__NCS_BYTECODE__\s*([\s\S]*?)\s*__END_NCS_BYTECODE__\*/",
            RegexOptions.Multiline | RegexOptions.Compiled
        );

        /// <summary>
        /// Returns an NCS instance from the source.
        /// Matching Python: read_ncs(source, offset=0, size=None) -> NCS
        /// </summary>
        [CanBeNull]
        public static NCS ReadNcs(
            [CanBeNull] object source,
            int offset = 0,
            int? size = null)
        {
            if (source == null)
            {
                throw new ArgumentNullException(nameof(source));
            }

            // Handle different source types matching Python's SOURCE_TYPES
            if (source is string filepath)
            {
                using (var reader = new NCSBinaryReader(filepath, offset, size ?? 0))
                {
                    return reader.Load();
                }
            }
            else if (source is byte[] data)
            {
                using (var reader = new NCSBinaryReader(data, offset, size ?? 0))
                {
                    return reader.Load();
                }
            }
            else if (source is Stream stream)
            {
                using (var reader = new NCSBinaryReader(stream, offset, size ?? 0))
                {
                    return reader.Load();
                }
            }
            else
            {
                throw new ArgumentException($"Unsupported source type: {source.GetType()}", nameof(source));
            }
        }

        /// <summary>
        /// Writes the NCS data to the target location with the specified format (NCS only).
        /// Matching Python: write_ncs(ncs, target, file_format=ResourceType.NCS)
        /// </summary>
        public static void WriteNcs(
            [CanBeNull] NCS ncs,
            [CanBeNull] object target,
            [CanBeNull] ResourceType fileFormat = null)
        {
            if (ncs == null)
            {
                throw new ArgumentNullException(nameof(ncs));
            }
            if (target == null)
            {
                throw new ArgumentNullException(nameof(target));
            }

            fileFormat = fileFormat ?? ResourceType.NCS;

            if (fileFormat != ResourceType.NCS)
            {
                throw new ArgumentException("Unsupported format specified; use NCS.");
            }

            var writer = new NCSBinaryWriter(ncs);
            byte[] data = writer.Write();

            // Handle different target types matching Python's TARGET_TYPES
            if (target is string filepath)
            {
                File.WriteAllBytes(filepath, data);
            }
            else if (target is Stream stream)
            {
                stream.Write(data, 0, data.Length);
            }
            else if (target is List<byte> byteList)
            {
                byteList.Clear();
                byteList.AddRange(data);
            }
            else if (target is MemoryStream memoryStream)
            {
                memoryStream.SetLength(0);
                memoryStream.Write(data, 0, data.Length);
            }
            else
            {
                // Try to use reflection to set bytes if target supports it
                throw new ArgumentException($"Unsupported target type: {target.GetType()}", nameof(target));
            }
        }

        /// <summary>
        /// Returns the NCS data in the specified format (NCS only) as a byte array.
        /// This is a convenience method that wraps the WriteNcs() method.
        /// Matching Python: bytes_ncs(ncs, file_format=ResourceType.NCS) -> bytearray
        /// </summary>
        public static byte[] BytesNcs(
            [CanBeNull] NCS ncs,
            [CanBeNull] ResourceType fileFormat = null)
        {
            if (ncs == null)
            {
                throw new ArgumentNullException(nameof(ncs));
            }

            fileFormat = fileFormat ?? ResourceType.NCS;

            using (var ms = new MemoryStream())
            {
                WriteNcs(ncs, ms, fileFormat);
                return ms.ToArray();
            }
        }

        /// <summary>
        /// Compile NSS source code to NCS bytecode.
        /// Matching Python: compile_nss(source, game, optimizers=None, library_lookup=None, *, errorlog=None, debug=False) -> NCS
        /// </summary>
        [CanBeNull]
        public static NCS CompileNss(
            string source,
            BioWareGame game,
            [CanBeNull] List<NCSOptimizer> optimizers = null,
            [CanBeNull] object libraryLookup = null,
            bool debug = false)
        {
            if (source == null)
            {
                throw new ArgumentNullException(nameof(source));
            }

            // Check for embedded bytecode block matching Python's BYTECODE_BLOCK_PATTERN
            Match blockMatch = BytecodeBlockPattern.Match(source);
            if (blockMatch.Success)
            {
                string encodedPayload = string.Join("", blockMatch.Groups[1].Value.Split(default(char[]), StringSplitOptions.RemoveEmptyEntries));
                try
                {
                    byte[] byteData = Convert.FromBase64String(encodedPayload);
                    using (var stream = new MemoryStream(byteData))
                    using (var reader = new NCSBinaryReader(stream))
                    {
                        return reader.Load();
                    }
                }
                catch (Exception exc)
                {
                    // Log warning but continue with normal compilation
                    System.Diagnostics.Debug.WriteLine($"Failed to decode embedded NCS bytecode: {exc.Message}");
                }
            }

            // Initialize lexer (creates parser tables if needed)
            // Matching Python: NssLexer()
            var lexer = new NssLexer();

            // Create parser with game-appropriate function and constant definitions
            // Matching Python: library_lookup handling
            List<string> lookupArg;
            if (libraryLookup is IEnumerable<string> lookupSeq)
            {
                lookupArg = lookupSeq.ToList();
            }
            else if (libraryLookup is string lookupStr)
            {
                lookupArg = new List<string> { lookupStr };
            }
            else
            {
                lookupArg = null;
            }

            // Get game-specific functions and constants
            List<ScriptFunction> functions = game.IsK1() ? ScriptDefs.KOTOR_FUNCTIONS : ScriptDefs.TSL_FUNCTIONS;
            List<ScriptConstant> constants = game.IsK1() ? ScriptDefs.KOTOR_CONSTANTS : ScriptDefs.TSL_CONSTANTS;
            Dictionary<string, byte[]> library = game.IsK1() ? ScriptLib.KOTOR_LIBRARY : ScriptLib.TSL_LIBRARY;

            // Matching Python: nss_parser = NssParser(...)
            var parser = new NssParser(functions, constants, library, lookupArg);
            CodeRoot block = parser.Parse(source);

            // Matching Python: ncs = NCS()
            var ncs = new NCS();

            // Matching Python: block.compile(ncs)
            block.Compile(ncs);

            // Ensure NOP removal is always first optimization pass
            // Matching Python: if not optimizers or not any(isinstance(optimizer, RemoveNopOptimizer) for optimizer in optimizers):
            if (optimizers == null || !optimizers.Any(opt => opt is RemoveNopOptimizer))
            {
                optimizers = optimizers ?? new List<NCSOptimizer>();
                optimizers = new List<NCSOptimizer> { new RemoveNopOptimizer() }.Concat(optimizers).ToList();
            }

            // Apply all optimizers
            // Matching Python: for optimizer in optimizers: optimizer.reset(); ncs.optimize(optimizers)
            foreach (NCSOptimizer optimizer in optimizers)
            {
                optimizer.Reset();
            }
            ncs.Optimize(optimizers);

            return ncs;
        }

        /// <summary>
        /// Decompile NCS bytecode to NSS source code.
        /// This function provides native NCS to NSS decompilation based on DeNCS implementation.
        /// Matching Python: decompile_ncs(ncs, game, functions=None, constants=None) -> str
        /// </summary>
        [CanBeNull]
        public static string DecompileNcs(
            [CanBeNull] NCS ncs,
            BioWareGame game,
            [CanBeNull] List<ScriptFunction> functions = null,
            [CanBeNull] List<ScriptConstant> constants = null)
        {
            if (ncs == null)
            {
                throw new ArgumentNullException(nameof(ncs));
            }

            // Use provided functions/constants or default to game-specific ones
            if (functions == null)
            {
                functions = game.IsK1() ? ScriptDefs.KOTOR_FUNCTIONS : ScriptDefs.TSL_FUNCTIONS;
            }
            if (constants == null)
            {
                constants = game.IsK1() ? ScriptDefs.KOTOR_CONSTANTS : ScriptDefs.TSL_CONSTANTS;
            }

            // Create FileDecompiler with game type
            // Matching Python: decompiler = NCSDecompiler(ncs, game, functions, constants)
            // Note: C# uses FileDecompiler.DecompileNcsObject instead of NCSDecompiler class
            NWScriptLocator.GameType gameType = game.IsK1() ? NWScriptLocator.GameType.K1 : NWScriptLocator.GameType.TSL;
            var decompiler = new FileDecompiler(null, gameType);

            // Decompile NCS object to FileScriptData
            // Matching Python: return decompiler.decompile_dencs()
            FileScriptData data = decompiler.DecompileNcsObject(ncs);
            if (data == null)
            {
                throw new InvalidOperationException("Decompilation failed - DecompileNcsObject returned null");
            }

            // Generate code and return it
            data.GenerateCode();
            string code = data.GetCode();

            // Ensure we always return a non-null string (even if empty)
            if (code == null)
            {
                code = "";
            }

            return code;
        }
    }
}
