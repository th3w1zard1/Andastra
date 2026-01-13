using System;
using BioWare.NET.Common;
using BioWare.NET.Resource.Formats.GFF;
using BioWare.NET.Resource;

namespace BioWare.NET.Tools
{
    // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tools/template.py
    // Original: Template utility functions
    public static class TemplateTools
    {
        // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tools/template.py:14-36
        // Original: def extract_name(data: bytes) -> LocalizedString:
        public static LocalizedString ExtractName(byte[] data)
        {
            var reader = new GFFBinaryReader(data);
            GFF gff = reader.Load();
            if (gff.Content == GFFContent.UTC)
            {
                return gff.Root.GetLocString("FirstName");
            }
            if (gff.Content == GFFContent.UTT || gff.Content == GFFContent.UTW)
            {
                return gff.Root.GetLocString("LocalizedName");
            }
            return gff.Root.GetLocString("LocName");
        }

        // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tools/template.py:39-43
        // Original: def extract_tag_from_gff(data: bytes) -> str:
        public static string ExtractTagFromGff(byte[] data)
        {
            var reader = new GFFBinaryReader(data);
            GFF gff = reader.Load();
            return gff.Root.GetString("Tag");
        }
    }
}
