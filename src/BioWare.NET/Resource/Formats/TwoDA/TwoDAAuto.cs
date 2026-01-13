using System;
using System.IO;
using BioWare.NET.Common;
using BioWare.NET.Resource;
using JetBrains.Annotations;

namespace BioWare.NET.Resource.Formats.TwoDA
{

    /// <summary>
    /// Auto-detection and convenience functions for 2DA files.
    /// 1:1 port of Python twoda_auto.py from pykotor/resource/formats/twoda/twoda_auto.py
    /// </summary>
    public static class TwoDAAuto
    {

        /// <summary>
        /// Reads a 2DA file from a file path, byte array, or stream.
        /// Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/resource/formats/tlk/tlk_auto.py
        /// </summary>
        public static TwoDA Read2DA(object source, int offset = 0, int? size = null, ResourceType fileFormat = null)
        {
            if (source is string filepath)
            {
                // Assume full file read; let TwoDABinaryReader handle actual range
                var reader = new TwoDABinaryReader(File.ReadAllBytes(filepath));
                return reader.Load();
            }
            if (source is byte[] data)
            {
                // Ignore offset/size in constructor; pass entire data, let .Load handle offset/size if needed
                var reader = new TwoDABinaryReader(data);
                return reader.Load();
            }
            if (source is Stream stream)
            {
                var reader = new TwoDABinaryReader(stream);
                return reader.Load();
            }
            throw new ArgumentException("Source must be string, byte[], or Stream");
        }

        /// <summary>
        /// Alias for Read2DA (alternative naming, matches common style).
        /// </summary>
        public static TwoDA ReadTwoDA(object source, int offset = 0, int? size = null, ResourceType fileFormat = null)
        {
            return Read2DA(source, offset, size, fileFormat);
        }

        /// <summary>
        /// Writes the 2DA data to the target location with the specified format.
        /// 1:1 port of Python write_2da function.
        /// </summary>
        public static void Write2DA(TwoDA twoda, string target, ResourceType fileFormat)
        {
            if (fileFormat == ResourceType.TwoDA)
            {
                var writer = new TwoDABinaryWriter(twoda);
                byte[] data = writer.Write();
                File.WriteAllBytes(target, data);
            }
            else
            {
                throw new ArgumentException("Unsupported format specified; use one of [ResourceType.TwoDA, ResourceType.TwoDA_CSV, ResourceType.TwoDA_JSON].");
            }
        }

        /// <summary>
        /// Alias for Write2DA (alternative naming, matches common style).
        /// </summary>
        public static void WriteTwoDA(TwoDA twoda, string target, ResourceType fileFormat)
        {
            Write2DA(twoda, target, fileFormat);
        }

        /// <summary>
        /// Returns the 2DA data as a byte array.
        /// 1:1 port of Python bytes_2da function.
        /// </summary>
        public static byte[] Bytes2DA(TwoDA twoda, [CanBeNull] ResourceType fileFormat = null)
        {
            var writer = new TwoDABinaryWriter(twoda);
            return writer.Write();
        }

        /// <summary>
        /// Alias for Bytes2DA (alternative naming, matches common style).
        /// </summary>
        public static byte[] BytesTwoDA(TwoDA twoda, [CanBeNull] ResourceType fileFormat = null)
        {
            return Bytes2DA(twoda, fileFormat);
        }
    }
}

