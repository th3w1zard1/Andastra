using System;
using System.IO;
using Andastra.Parsing.Resource;
using JetBrains.Annotations;

namespace Andastra.Parsing.Formats.TwoDA
{

    /// <summary>
    /// Auto-detection and convenience functions for 2DA files.
    /// 1:1 port of Python twoda_auto.py from pykotor/resource/formats/twoda/twoda_auto.py
    /// </summary>
    public static class TwoDAAuto
    {
        /// <summary>
        /// Writes the 2DA data to the target location with the specified format.
        /// 1:1 port of Python write_2da function.
        /// </summary>
        public static void WriteTwoDA(TwoDA twoda, string target, ResourceType fileFormat)
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
        /// Alias for WriteTwoDA (alternative naming, matches common style).
        /// </summary>
        public static void Write2DA(TwoDA twoda, string target, ResourceType fileFormat)
        {
            WriteTwoDA(twoda, target, fileFormat);
        }

        /// <summary>
        /// Returns the 2DA data as a byte array.
        /// 1:1 port of Python bytes_2da function.
        /// </summary>
        public static byte[] BytesTwoDA(TwoDA twoda, [CanBeNull] ResourceType fileFormat = null)
        {
            var writer = new TwoDABinaryWriter(twoda);
            return writer.Write();
        }

        /// <summary>
        /// Alias for BytesTwoDA (alternative naming, matches common style).
        /// </summary>
        public static byte[] Bytes2DA(TwoDA twoda, [CanBeNull] ResourceType fileFormat = null)
        {
            return BytesTwoDA(twoda, fileFormat);
        }
    }
}

