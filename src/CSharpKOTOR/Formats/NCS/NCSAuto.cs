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
        private static readonly Regex BytecodeBlockPattern = new Regex(
            @"/\*__NCS_BYTECODE__\s*([\s\S]*?)\s*__END_NCS_BYTECODE__\*/",
            RegexOptions.Multiline);

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
            List<NCSOptimizer> optimizers = null,
            [CanBeNull] List<string> libraryLookup = null,
            [CanBeNull] object errorlog = null,
            bool debug = false)
        {
            Match blockMatch = BytecodeBlockPattern.Match(source);
            if (blockMatch.Success)
            {
                string encodedPayload = string.Join("", blockMatch.Groups[1].Value.Split(new[] { ' ', '\t', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries));
                try
                {
                    byte[] byteData = Convert.FromBase64String(encodedPayload);
                    using (var stream = new MemoryStream(byteData))
                    {
                        NCS decoded = ReadNcs(stream);
                        decoded.OriginalSource = source;
                        return decoded;
                    }
                }
                catch (Exception exc)
                {
                    // Log warning but continue with normal compilation
                    System.Diagnostics.Debug.WriteLine($"Failed to decode embedded NCS bytecode: {exc.Message}");
                }
            }

            var compiler = new NssCompiler(game, libraryLookup, debug);
            NCS ncs = compiler.Compile(source);
            ncs.OriginalSource = source;

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

            if (!string.IsNullOrEmpty(ncs.OriginalSource))
            {
                string result = ncs.OriginalSource;
                string bytecodeBlock = EncodeBytecodeBlock(ncs);
                if (!string.IsNullOrEmpty(bytecodeBlock))
                {
                    if (!result.EndsWith("\n"))
                    {
                        result += "\n";
                    }
                    result += bytecodeBlock;
                }
                return result;
            }

            var decompiler = new NCSDecompiler(ncs, game, functions, constants);
            return decompiler.DecompileKNCSDecomp();
        }

        private static string EncodeBytecodeBlock(NCS ncs)
        {
            byte[] data = new NCSBinaryWriter(ncs).Write();
            string base64 = Convert.ToBase64String(data);
            return $"/*__NCS_BYTECODE__\n{base64}\n__END_NCS_BYTECODE__*/";
        }
    }
}

