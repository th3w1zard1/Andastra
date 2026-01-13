using System;
using System.IO;
using BioWare.NET.Common;
using BioWare.NET.Resource;

namespace BioWare.NET.Resource.Formats.LYT
{
    /// <summary>
    /// Automatic LYT (Layout) file loading and saving utilities.
    /// Provides convenient methods to read and write LYT files from various sources.
    /// </summary>
    /// <remarks>
    /// WHAT IS LYTAUTO?
    /// 
    /// LYTAuto is a helper class that makes it easy to load and save LYT files. Instead of
    /// manually creating an LYTAsciiReader or LYTAsciiWriter, you can use these simple methods
    /// to read from files, byte arrays, or streams, and write to files, byte arrays, or streams.
    /// 
    /// HOW TO USE IT:
    /// 
    /// READING A LYT FILE:
    /// 
    /// From a file path:
    /// LYT lyt = LYTAuto.ReadLyt("path/to/file.lyt");
    /// 
    /// From a byte array:
    /// byte[] data = File.ReadAllBytes("file.lyt");
    /// LYT lyt = LYTAuto.ReadLyt(data);
    /// 
    /// From a stream:
    /// using (FileStream stream = File.OpenRead("file.lyt"))
    /// {
    ///     LYT lyt = LYTAuto.ReadLyt(stream);
    /// }
    /// 
    /// WRITING A LYT FILE:
    /// 
    /// To a file path:
    /// LYTAuto.WriteLyt(lyt, "path/to/output.lyt");
    /// 
    /// To a byte array:
    /// byte[] data = LYTAuto.BytesLyt(lyt);
    /// 
    /// To a stream:
    /// using (FileStream stream = File.Create("output.lyt"))
    /// {
    ///     LYTAuto.WriteLyt(lyt, stream);
    /// }
    /// 
    /// WHAT HAPPENS WHEN YOU READ A LYT?
    /// 
    /// When you call ReadLyt, it:
    /// 1. Creates an LYTAsciiReader with your source (file, bytes, or stream)
    /// 2. Calls reader.Load() which:
    ///    - Reads the ASCII text format line by line
    ///    - Parses room definitions (model name and position)
    ///    - Parses track definitions (swoop track boosters)
    ///    - Parses obstacle definitions (swoop track obstacles)
    ///    - Parses door hook definitions (door placement points)
    ///    - Creates a LYT object with all this data
    /// 3. Returns the LYT object
    /// 
    /// WHAT HAPPENS WHEN YOU WRITE A LYT?
    /// 
    /// When you call WriteLyt or BytesLyt, it:
    /// 1. Creates an LYTAsciiWriter with your LYT object and target (file, stream, or byte array)
    /// 2. Calls writer.Write() which:
    ///    - Writes the ASCII text format line by line
    ///    - Writes room definitions (model name and position)
    ///    - Writes track definitions (swoop track boosters)
    ///    - Writes obstacle definitions (swoop track obstacles)
    ///    - Writes door hook definitions (door placement points)
    /// 3. Returns the written data (for BytesLyt) or writes to file/stream (for WriteLyt)
    /// 
    /// LYT FILE FORMAT:
    /// 
    /// LYT files are stored in ASCII text format (not binary). Each line contains one piece of
    /// information. The format is:
    /// 
    /// - Room lines: "roommodel modelname x y z"
    /// - Track lines: "trackmodel modelname x y z"
    /// - Obstacle lines: "obstaclemodel modelname x y z"
    /// - Door hook lines: "doorhook roomname doorname x y z qx qy qz qw"
    /// 
    /// WHERE:
    /// - modelname: The name of the MDL file (without .mdl extension)
    /// - x, y, z: The position coordinates
    /// - qx, qy, qz, qw: The quaternion orientation (for door hooks)
    /// 
    /// OFFSET AND SIZE PARAMETERS:
    /// 
    /// The offset parameter lets you read a LYT from a specific position in the source.
    /// This is useful if the LYT data is embedded in a larger file.
    /// 
    /// The size parameter lets you limit how much data is read. If null, it reads until
    /// the end of the source. This is useful if you know the exact size of the LYT data.
    /// 
    /// ORIGINAL IMPLEMENTATION:
    /// 
    /// [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): The original engine loads LYT files directly from disk or
    /// from ERF/BIF archives. The file format is ASCII text, making it easy to read and
    /// edit manually.
    /// 
    /// Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/resource/formats/lyt/lyt_auto.py
    /// Original: read_lyt, write_lyt, bytes_lyt functions
    /// </remarks>
    public static class LYTAuto
    {
        /// <summary>
        /// Reads a LYT layout file from various sources (file path, byte array, or stream).
        /// </summary>
        /// <param name="source">The source containing LYT data (string filepath, byte array, or Stream)</param>
        /// <param name="offset">Byte offset to start reading from (default: 0)</param>
        /// <param name="size">Number of bytes to read (null = read until end)</param>
        /// <returns>A LYT object containing the parsed layout data</returns>
        /// <remarks>
        /// Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/resource/formats/lyt/lyt_auto.py:13-39
        /// Original: def read_lyt(source: SOURCE_TYPES, offset: int = 0, size: int | None = None) -> LYT
        /// </remarks>
        public static BioWare.NET.Resource.Formats.LYT.LYT ReadLyt(object source, int offset = 0, int? size = null)
        {
            int sizeValue = size ?? 0;
            if (source is string filepath)
            {
                return new LYTAsciiReader(filepath, offset, sizeValue).Load();
            }
            if (source is byte[] bytes)
            {
                return new LYTAsciiReader(bytes, offset, sizeValue).Load();
            }
            if (source is Stream stream)
            {
                return new LYTAsciiReader(stream, offset, sizeValue).Load();
            }
            throw new ArgumentException("Source must be string, byte[], or Stream for LYT");
        }

        /// <summary>
        /// Writes a LYT layout file to a file path or stream.
        /// </summary>
        /// <param name="lyt">The LYT object to write</param>
        /// <param name="target">The target (string filepath or Stream)</param>
        /// <param name="fileFormat">The file format (must be LYT)</param>
        /// <remarks>
        /// Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/resource/formats/lyt/lyt_auto.py:42-65
        /// Original: def write_lyt(lyt: LYT, target: TARGET_TYPES, file_format: ResourceType = ResourceType.LYT)
        /// </remarks>
        public static void WriteLyt(BioWare.NET.Resource.Formats.LYT.LYT lyt, object target, ResourceType fileFormat = null)
        {
            ResourceType format = fileFormat ?? ResourceType.LYT;
            if (format != ResourceType.LYT)
            {
                throw new ArgumentException("Unsupported format specified; use LYT.");
            }

            if (target is string filepath)
            {
                new LYTAsciiWriter(lyt, filepath).Write();
            }
            else if (target is Stream stream)
            {
                new LYTAsciiWriter(lyt, stream).Write();
            }
            else
            {
                throw new ArgumentException("Target must be string or Stream for LYT");
            }
        }

        /// <summary>
        /// Converts a LYT object to a byte array.
        /// </summary>
        /// <param name="lyt">The LYT object to convert</param>
        /// <param name="fileFormat">The file format (must be LYT)</param>
        /// <returns>A byte array containing the LYT file data in ASCII text format</returns>
        /// <remarks>
        /// Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/resource/formats/lyt/lyt_auto.py:68-91
        /// Original: def bytes_lyt(lyt: LYT, file_format: ResourceType = ResourceType.LYT) -> bytes
        /// </remarks>
        public static byte[] BytesLyt(BioWare.NET.Resource.Formats.LYT.LYT lyt, ResourceType fileFormat = null)
        {
            ResourceType format = fileFormat ?? ResourceType.LYT;
            using (var ms = new MemoryStream())
            {
                WriteLyt(lyt, ms, format);
                return ms.ToArray();
            }
        }
    }
}

