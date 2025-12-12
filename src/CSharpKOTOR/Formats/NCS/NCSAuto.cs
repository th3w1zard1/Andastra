using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using CSharpKOTOR.Common;
using CSharpKOTOR.Common.Script;
using CSharpKOTOR.Formats.NCS.Compiler;
using CSharpKOTOR.Formats.NCS.Optimizers;
using CSharpKOTOR.Resources;
using JetBrains.Annotations;

namespace CSharpKOTOR.Formats.NCS
{

    /// <summary>
    /// Auto-loading functions for NCS files.
    /// </summary>
    public static class NCSAuto
    {

        /// <summary>
        /// Returns an NCS instance from the source.
        ///
        /// Args:
        ///     source: The source of the data (file path, byte array, or stream).
        ///     offset: The byte offset of the file inside the data.
        ///     size: Number of bytes to allowed to read from the stream. If not specified, uses the whole stream.
        ///
        /// Raises:
        ///     InvalidDataException: If the file was corrupted or in an unsupported format.
        ///
        /// Returns:
        ///     An NCS instance.
        /// </summary>
        public static NCS ReadNcs(string filepath, int offset = 0, int? size = null)
        {
            using (var reader = new NCSBinaryReader(filepath, offset, size ?? 0))
            {
                return reader.Load();
            }
        }

        public static NCS ReadNcs(byte[] data, int offset = 0, int? size = null)
        {
            using (var reader = new NCSBinaryReader(data, offset, size ?? 0))
            {
                return reader.Load();
            }
        }

        public static NCS ReadNcs(Stream source, int offset = 0, int? size = null)
        {
            using (var reader = new NCSBinaryReader(source, offset, size ?? 0))
            {
                return reader.Load();
            }
        }

        /// <summary>
        /// Writes the NCS data to the target location with the specified format (NCS only).
        ///
        /// Args:
        ///     ncs: The NCS file being written.
        ///     target: The location to write the data to (file path or stream).
        ///     fileFormat: The file format (currently only NCS is supported).
        ///
        /// Raises:
        ///     ArgumentException: If an unsupported file format was given.
        /// </summary>
        public static void WriteNcs(NCS ncs, string filepath, [CanBeNull] ResourceType fileFormat = null)
        {
            fileFormat = fileFormat ?? ResourceType.NCS;
            if (fileFormat != ResourceType.NCS)
            {
                throw new ArgumentException("Unsupported format specified; use NCS.", nameof(fileFormat));
            }

            byte[] data = new NCSBinaryWriter(ncs).Write();
            File.WriteAllBytes(filepath, data);
        }

        public static void WriteNcs(NCS ncs, [CanBeNull] Stream target, ResourceType fileFormat = null)
        {
            fileFormat = fileFormat ?? ResourceType.NCS;
            if (fileFormat != ResourceType.NCS)
            {
                throw new ArgumentException("Unsupported format specified; use NCS.", nameof(fileFormat));
            }

            byte[] data = new NCSBinaryWriter(ncs).Write();
            target.Write(data, 0, data.Length);
        }

        /// <summary>
        /// Returns the NCS data in the specified format (NCS only) as a byte array.
        ///
        /// This is a convenience method that wraps the WriteNcs() method.
        ///
        /// Args:
        ///     ncs: The target NCS object.
        ///     fileFormat: The file format (currently only NCS is supported).
        ///
        /// Raises:
        ///     ArgumentException: If an unsupported file format was given.
        ///
        /// Returns:
        ///     The NCS data as a byte array.
        /// </summary>
        public static byte[] BytesNcs(NCS ncs, [CanBeNull] ResourceType fileFormat = null)
        {
            fileFormat = fileFormat ?? ResourceType.NCS;
            if (fileFormat != ResourceType.NCS)
            {
                throw new ArgumentException("Unsupported format specified; use NCS.", nameof(fileFormat));
            }

            return new NCSBinaryWriter(ncs).Write();
        }

        /// <summary>
        /// Compile NSS source code to NCS bytecode.
        ///
        /// Args:
        ///     source: The NSS source code string to compile
        ///     game: Target game (K1 or TSL) - determines which function/constant definitions to use
        ///     optimizers: Optional list of post-compilation optimizers to apply
        ///     libraryLookup: Paths to search for #include files
        ///     errorlog: Optional error logger for parser (not yet implemented in C#)
        ///     debug: Enable debug output from parser
        ///
        /// Returns:
        ///     NCS: Compiled NCS bytecode object
        ///
        /// Raises:
        ///     CompileError: If source code has syntax errors or semantic issues
        ///     EntryPointError: If script has no main() or StartingConditional() entry point
        ///
        /// Note:
        ///     RemoveNopOptimizer is always applied first unless explicitly included in optimizers list,
        ///     as NOP instructions are compilation artifacts that should be removed.
        /// </summary>
        public static NCS CompileNss(
            string source,
            Game game,
            Dictionary<string, byte[]> library = null,
            List<NCSOptimizer> optimizers = null,
            [CanBeNull] List<string> libraryLookup = null,
            [CanBeNull] object errorlog = null,
            bool debug = false)
        {
            var compiler = new NssCompiler(game, libraryLookup, debug);
            // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/resource/formats/ncs/ncs_auto.py:184
            // Original: library=KOTOR_LIBRARY if game.is_k1() else TSL_LIBRARY
            // If library is not provided, use default game library
            if (library == null)
            {
                library = game.IsK1() ? Common.Script.ScriptLib.KOTOR_LIBRARY : Common.Script.ScriptLib.TSL_LIBRARY;
            }
            NCS ncs = compiler.Compile(source, library);

            // Ensure NOP removal is always first optimization pass
            if (optimizers == null || !optimizers.Any(o => o is RemoveNopOptimizer))
            {
                optimizers = new List<NCSOptimizer> { new RemoveNopOptimizer() }
                    .Concat(optimizers ?? Enumerable.Empty<NCSOptimizer>())
                    .ToList();
            }

            // Apply all optimizers
            foreach (NCSOptimizer optimizer in optimizers)
            {
                optimizer.Reset();
            }
            ncs.Optimize(optimizers);

            if (System.Environment.GetEnvironmentVariable("NCS_INTERPRETER_DEBUG") == "true")
            {
                System.Console.WriteLine("=== NCS after optimize ===");
                for (int i = 0; i < ncs.Instructions.Count; i++)
                {
                    NCSInstruction inst = ncs.Instructions[i];
                    int jumpIdx = inst.Jump != null ? ncs.GetInstructionIndex(inst.Jump) : -1;
                    System.Console.WriteLine($"{i}: {inst.InsType} args=[{string.Join(",", inst.Args ?? new List<object>())}] jumpIdx={jumpIdx}");
                }
            }

            return ncs;
        }

        /// <summary>
        /// Decompile NCS bytecode to NSS source code.
        /// </summary>
        public static string DecompileNcs(
            [CanBeNull] NCS ncs,
            Game game,
            List<ScriptFunction> functions = null,
            [CanBeNull] List<ScriptConstant> constants = null)
        {
            if (ncs == null)
            {
                throw new ArgumentNullException(nameof(ncs));
            }

            var decompiler = new NCSDecompiler(ncs, game, functions, constants);
            return decompiler.DecompileNCSDecomp();
        }
    }
}

