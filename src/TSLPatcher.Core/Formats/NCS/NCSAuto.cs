using System;
using System.Collections.Generic;
using System.IO;
using JetBrains.Annotations;
using TSLPatcher.Core.Common;
using TSLPatcher.Core.Common.Script;
using TSLPatcher.Core.Formats.NCS.Compiler;
using TSLPatcher.Core.Resources;

namespace TSLPatcher.Core.Formats.NCS
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
        /// </summary>
        public static NCS CompileNss(
            string source,
            [CanBeNull] Game game,
            List<INCSOptimizer> optimizers = null,
            [CanBeNull] List<string> libraryLookup = null)
        {
            var compiler = new NssCompiler(game, libraryLookup);
            NCS ncs = compiler.Compile(source);

            if (optimizers != null && optimizers.Count > 0)
            {
                ncs.Optimize(optimizers);
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
            return decompiler.Decompile();
        }
    }
}

