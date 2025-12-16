using System;
using System.IO;
using AuroraEngine.Common.Resources;

namespace AuroraEngine.Common.Formats.GFF
{

    /// <summary>
    /// Auto-detection and convenience functions for GFF files.
    /// 1:1 port of Python gff_auto.py from pykotor/resource/formats/gff/gff_auto.py
    /// </summary>
    public static class GFFAuto
    {
        /// <summary>
        /// Reads the GFF data from the source location with the specified format (GFF or GFF_XML).
        /// 1:1 port of Python read_gff function.
        /// </summary>
        public static GFF ReadGff(string source, ResourceType fileFormat)
        {
            if (fileFormat.IsGff())
            {
                byte[] data = File.ReadAllBytes(source);
                var reader = new GFFBinaryReader(data);
                return reader.Load();
            }
            else
            {
                throw new ArgumentException("Unsupported format specified; use GFF, GFF_XML, or GFF_JSON.");
            }
        }


        /// <summary>
        /// Writes the GFF data to the target location with the specified format (GFF or GFF_XML).
        /// 1:1 port of Python write_gff function.
        /// </summary>
        public static void WriteGff(GFF gff, string target, ResourceType fileFormat)
        {
            if (fileFormat.IsGff())
            {
                // Set content type from filename if not already set
                if (gff.Content == GFFContent.GFF && !string.IsNullOrEmpty(target))
                {
                    gff.Content = GFFContentExtensions.FromResName(target);
                }

                var writer = new GFFBinaryWriter(gff);
                byte[] data = writer.Write();
                File.WriteAllBytes(target, data);
            }
            else
            {
                throw new ArgumentException("Unsupported format specified; use GFF, GFF_XML, or GFF_JSON.");
            }
        }

        /// <summary>
        /// Returns the GFF data as a byte array.
        /// 1:1 port of Python bytes_gff function.
        /// </summary>
        public static byte[] BytesGff(GFF gff, ResourceType fileFormat)
        {
            var writer = new GFFBinaryWriter(gff);
            return writer.Write();
        }
    }
}

