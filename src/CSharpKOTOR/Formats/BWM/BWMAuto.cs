using System;
using System.IO;
using CSharpKOTOR.Resources;
using JetBrains.Annotations;

namespace CSharpKOTOR.Formats.BWM
{
    // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/resource/formats/bwm/bwm_auto.py
    // Original: read_bwm, write_bwm, bytes_bwm
    public static class BWMAuto
    {
        public static BWM ReadBwm(byte[] source, int offset = 0, int? size = null)
        {
            var reader = new BWMBinaryReader(source, offset, size ?? 0);
            return reader.Load();
        }

        public static BWM ReadBwm(string filepath, int offset = 0, int? size = null)
        {
            var reader = new BWMBinaryReader(filepath, offset, size ?? 0);
            return reader.Load();
        }

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

