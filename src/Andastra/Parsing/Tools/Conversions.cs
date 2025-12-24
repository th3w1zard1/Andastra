using System;
using System.IO;
using Andastra.Parsing;
using Andastra.Parsing.Common;
using Andastra.Parsing.Formats.GFF;
using Andastra.Parsing.Formats.SSF;
using Andastra.Parsing.Formats.TLK;
using Andastra.Parsing.Formats.TwoDA;
using Andastra.Parsing.Resource;

namespace Andastra.Parsing.Tools
{
    // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tools/conversions.py
    // Original: Format conversion utility functions for KOTOR game resources
    public static class Conversions
    {
        // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tools/conversions.py:30-43
        // Original: def convert_gff_to_xml(input_path: Path, output_path: Path) -> None:
        public static void ConvertGffToXml(string inputPath, string outputPath)
        {
            byte[] data = File.ReadAllBytes(inputPath);
            GFFBinaryReader reader = new GFFBinaryReader(data);
            GFF gff = reader.Load();
            GFFAuto.WriteGff(gff, outputPath, ResourceType.GFF_XML);
        }

        // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tools/conversions.py:46-60
        // Original: def convert_xml_to_gff(input_path: Path, output_path: Path, *, gff_content_type: str | None = None) -> None:
        public static void ConvertXmlToGff(string inputPath, string outputPath, string gffContentType = null)
        {
            // Note: XML/JSON reading not yet implemented - placeholder
            throw new NotImplementedException("XML/JSON GFF reading not yet implemented");
        }

        // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tools/conversions.py:63-76
        // Original: def convert_tlk_to_xml(input_path: Path, output_path: Path) -> None:
        public static void ConvertTlkToXml(string inputPath, string outputPath)
        {
            byte[] data = File.ReadAllBytes(inputPath);
            TLKBinaryReader reader = new TLKBinaryReader(data);
            TLK tlk = reader.Load();
            TLKAuto.WriteTlk(tlk, outputPath, ResourceType.TLK_XML);
        }

        // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tools/conversions.py:79-93
        // Original: def convert_xml_to_tlk(input_path: Path, output_path: Path, *, language: Language | None = None) -> None:
        public static void ConvertXmlToTlk(string inputPath, string outputPath, Language? language = null)
        {
            // Note: XML/JSON reading not yet implemented - placeholder
            throw new NotImplementedException("XML/JSON TLK reading not yet implemented");
        }

        // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tools/conversions.py:96-105
        // Original: def convert_ssf_to_xml(input_path: Path, output_path: Path) -> None:
        public static void ConvertSsfToXml(string inputPath, string outputPath)
        {
            byte[] data = File.ReadAllBytes(inputPath);
            SSFBinaryReader reader = new SSFBinaryReader(data);
            SSF ssf = reader.Load();
            SSFAuto.WriteSsf(ssf, outputPath, ResourceType.SSF_XML);
        }

        // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tools/conversions.py:108-117
        // Original: def convert_xml_to_ssf(input_path: Path, output_path: Path) -> None:
        public static void ConvertXmlToSsf(string inputPath, string outputPath)
        {
            // Note: XML/JSON reading not yet implemented - placeholder
            throw new NotImplementedException("XML/JSON SSF reading not yet implemented");
        }

        // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tools/conversions.py:120-134
        // Original: def convert_2da_to_csv(input_path: Path, output_path: Path, *, delimiter: str = ",") -> None:
        public static void Convert2daToCsv(string inputPath, string outputPath, string delimiter = ",")
        {
            byte[] data = File.ReadAllBytes(inputPath);
            TwoDABinaryReader reader = new TwoDABinaryReader(data);
            TwoDA twoda = reader.Load();
            TwoDAAuto.Write2DA(twoda, outputPath, ResourceType.TwoDA_CSV);
        }

        // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tools/conversions.py:137-151
        // Original: def convert_csv_to_2da(input_path: Path, output_path: Path, *, delimiter: str = ",") -> None:
        public static void ConvertCsvTo2da(string inputPath, string outputPath, string delimiter = ",")
        {
            // Note: CSV reading not yet implemented - placeholder
            throw new NotImplementedException("CSV 2DA reading not yet implemented");
        }

        // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tools/conversions.py:154-163
        // Original: def convert_gff_to_json(input_path: Path, output_path: Path) -> None:
        public static void ConvertGffToJson(string inputPath, string outputPath)
        {
            byte[] data = File.ReadAllBytes(inputPath);
            GFFBinaryReader reader = new GFFBinaryReader(data);
            GFF gff = reader.Load();
            GFFAuto.WriteGff(gff, outputPath, ResourceType.GFF_JSON);
        }

        // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tools/conversions.py:166-176
        // Original: def convert_json_to_gff(input_path: Path, output_path: Path, *, gff_content_type: str | None = None) -> None:
        public static void ConvertJsonToGff(string inputPath, string outputPath, string gffContentType = null)
        {
            // Note: JSON reading not yet implemented - placeholder
            throw new NotImplementedException("JSON GFF reading not yet implemented");
        }

        // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tools/conversions.py:179-188
        // Original: def convert_tlk_to_json(input_path: Path, output_path: Path) -> None:
        public static void ConvertTlkToJson(string inputPath, string outputPath)
        {
            byte[] data = File.ReadAllBytes(inputPath);
            TLKBinaryReader reader = new TLKBinaryReader(data);
            TLK tlk = reader.Load();
            TLKAuto.WriteTlk(tlk, outputPath, ResourceType.TLK_JSON);
        }

        // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tools/conversions.py:191-200
        // Original: def convert_json_to_tlk(input_path: Path, output_path: Path) -> None:
        public static void ConvertJsonToTlk(string inputPath, string outputPath)
        {
            // Note: JSON reading not yet implemented - placeholder
            throw new NotImplementedException("JSON TLK reading not yet implemented");
        }
    }
}
