using System;
using System.IO;
using BioWare.NET.Common;
using BioWare.NET.Resource;
using JetBrains.Annotations;

namespace BioWare.NET.Resource.Formats.BWM
{
    /// <summary>
    /// Automatic BWM (BioWare Walkmesh) file loading and saving utilities.
    /// Provides convenient methods to read and write BWM files from various sources.
    /// </summary>
    /// <remarks>
    /// WHAT IS BWMAUTO?
    /// 
    /// BWMAuto is a helper class that makes it easy to load and save BWM files. Instead of
    /// manually creating a BWMBinaryReader or BWMBinaryWriter, you can use these simple methods
    /// to read from files, byte arrays, or streams, and write to files, byte arrays, or streams.
    /// 
    /// HOW TO USE IT:
    /// 
    /// READING A BWM FILE:
    /// 
    /// From a file path:
    /// BWM bwm = BWMAuto.ReadBwm("path/to/file.wok");
    /// 
    /// From a byte array:
    /// byte[] data = File.ReadAllBytes("file.wok");
    /// BWM bwm = BWMAuto.ReadBwm(data);
    /// 
    /// From a stream:
    /// using (FileStream stream = File.OpenRead("file.wok"))
    /// {
    ///     BWM bwm = BWMAuto.ReadBwm(stream);
    /// }
    /// 
    /// WRITING A BWM FILE:
    /// 
    /// To a file path:
    /// BWMAuto.WriteBwm(bwm, "path/to/output.wok");
    /// 
    /// To a byte array:
    /// byte[] data = BWMAuto.BytesBwm(bwm);
    /// 
    /// To a stream:
    /// using (FileStream stream = File.Create("output.wok"))
    /// {
    ///     BWMAuto.WriteBwm(bwm, stream);
    /// }
    /// 
    /// WHAT HAPPENS WHEN YOU READ A BWM?
    /// 
    /// When you call ReadBwm, it:
    /// 1. Creates a BWMBinaryReader with your source (file, bytes, or stream)
    /// 2. Calls reader.Load() which:
    ///    - Reads the file header (walkmesh type, vertex count, face count, etc.)
    ///    - Reads all vertices (x, y, z coordinates)
    ///    - Reads all faces (triangles with vertex indices and materials)
    ///    - Reads the AABB tree (if it's an AreaModel walkmesh)
    ///    - Reads transition information (if it's an AreaModel walkmesh)
    ///    - Creates a BWM object with all this data
    /// 3. Returns the BWM object
    /// 
    /// WHAT HAPPENS WHEN YOU WRITE A BWM?
    /// 
    /// When you call WriteBwm or BytesBwm, it:
    /// 1. Creates a BWMBinaryWriter with your BWM object and target (file, stream, or byte array)
    /// 2. Calls writer.Write() which:
    ///    - Writes the file header (walkmesh type, vertex count, face count, etc.)
    ///    - Writes all vertices (x, y, z coordinates)
    ///    - Writes all faces (triangles with vertex indices and materials)
    ///    - Writes the AABB tree (if it's an AreaModel walkmesh)
    ///    - Writes transition information (if it's an AreaModel walkmesh)
    /// 3. Returns the written data (for BytesBwm) or writes to file/stream (for WriteBwm)
    /// 
    /// FILE FORMAT SUPPORT:
    /// 
    /// Currently, only WOK (AreaModel) format is supported. PWK and DWK (PlaceableOrDoor)
    /// formats are not yet implemented, but the BWM class can represent them in memory.
    /// 
    /// OFFSET AND SIZE PARAMETERS:
    /// 
    /// The offset parameter lets you read a BWM from a specific position in the source.
    /// This is useful if the BWM data is embedded in a larger file (like an ERF archive).
    /// 
    /// The size parameter lets you limit how much data is read. If null, it reads until
    /// the end of the source. This is useful if you know the exact size of the BWM data.
    /// 
    /// ORIGINAL IMPLEMENTATION:
    /// 
    /// [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): The original engine loads BWM files directly from disk or
    /// from ERF/BIF archives. The file format is binary and includes header, vertices,
    /// faces, AABB tree, and transition data.
    /// 
    /// Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/resource/formats/bwm/bwm_auto.py
    /// Original: read_bwm, write_bwm, bytes_bwm
    /// </remarks>
    public static class BWMAuto
    {
        /// <summary>
        /// Reads a BWM walkmesh from a byte array.
        /// </summary>
        /// <param name="source">The byte array containing BWM data</param>
        /// <param name="offset">Byte offset to start reading from (default: 0)</param>
        /// <param name="size">Number of bytes to read (null = read until end)</param>
        /// <returns>A BWM object containing the parsed walkmesh data</returns>
        public static BWM ReadBwm(byte[] source, int offset = 0, int? size = null)
        {
            var reader = new BWMBinaryReader(source, offset, size ?? 0);
            return reader.Load();
        }

        /// <summary>
        /// Reads a BWM walkmesh from a file path.
        /// </summary>
        /// <param name="filepath">Path to the BWM file (.wok, .pwk, or .dwk)</param>
        /// <param name="offset">Byte offset to start reading from (default: 0)</param>
        /// <param name="size">Number of bytes to read (null = read entire file)</param>
        /// <returns>A BWM object containing the parsed walkmesh data</returns>
        public static BWM ReadBwm(string filepath, int offset = 0, int? size = null)
        {
            var reader = new BWMBinaryReader(filepath, offset, size ?? 0);
            return reader.Load();
        }

        /// <summary>
        /// Reads a BWM walkmesh from a stream.
        /// </summary>
        /// <param name="source">The stream containing BWM data</param>
        /// <param name="offset">Byte offset to start reading from (default: 0)</param>
        /// <param name="size">Number of bytes to read (null = read until end of stream)</param>
        /// <returns>A BWM object containing the parsed walkmesh data</returns>
        public static BWM ReadBwm(Stream source, int offset = 0, int? size = null)
        {
            var reader = new BWMBinaryReader(source, offset, size ?? 0);
            return reader.Load();
        }

        public static void WriteBwm(BWM wok, object target, [CanBeNull] ResourceType fileFormat = null)
        {
            var format = fileFormat ?? ResourceType.WOK;
            if (format == ResourceType.WOK)
            {
                using (var writer = CreateWriter(wok, target))
                {
                    writer.Write();
                }
            }
            else
            {
                throw new ArgumentException("Unsupported format specified; use WOK.");
            }
        }

        public static byte[] BytesBwm(BWM bwm, [CanBeNull] ResourceType fileFormat = null)
        {
            var format = fileFormat ?? ResourceType.WOK;
            if (format != ResourceType.WOK)
            {
                throw new ArgumentException("Unsupported format specified; use WOK.");
            }

            using (var writer = new BWMBinaryWriter(bwm))
            {
                writer.Write();
                return writer.Data();
            }
        }

        private static BWMBinaryWriter CreateWriter(BWM wok, object target)
        {
            if (target is string path)
            {
                return new BWMBinaryWriter(wok, path);
            }

            if (target is Stream stream)
            {
                return new BWMBinaryWriter(wok, stream);
            }

            if (target is byte[])
            {
                return new BWMBinaryWriter(wok);
            }

            throw new ArgumentException("Unsupported target type for WriteBwm");
        }
    }
}

