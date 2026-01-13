using System;
using System.IO;
using BioWare.NET.Common;
using BioWare.NET.Resource.Formats.MDL;
using BioWare.NET.Resource.Formats.MDLData;
using BioWare.NET.Resource.Formats.TPC;
using BioWare.NET.Resource.Formats.WAV;
using BioWare.NET.Resource;

namespace BioWare.NET.Tools
{
    // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tools/resources.py
    // Original: Resource conversion utility functions for KOTOR game resources
    public static class ResourceConversions
    {
        // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tools/resources.py:28-51
        // Original: def convert_tpc_to_tga(input_path: Path, output_path: Path, *, txi_output_path: Path | None = None) -> None:
        public static void ConvertTpcToTga(string inputPath, string outputPath, string txiOutputPath = null)
        {
            TPC tpc = TPCAuto.ReadTpc(inputPath);
            TPCAuto.WriteTpc(tpc, outputPath, ResourceType.TGA);

            // Write TXI if available and requested
            if (!string.IsNullOrEmpty(txiOutputPath) && !string.IsNullOrEmpty(tpc.Txi))
            {
                File.WriteAllText(txiOutputPath, tpc.Txi, System.Text.Encoding.ASCII);
            }
        }

        // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tools/resources.py:54-80
        // Original: def convert_tga_to_tpc(input_path: Path, output_path: Path, *, txi_input_path: Path | None = None, target_format: TPCTextureFormat | None = None) -> None:
        public static void ConvertTgaToTpc(string inputPath, string outputPath, string txiInputPath = null, TPCTextureFormat targetFormat = TPCTextureFormat.Invalid)
        {
            TPC tpc = TPCAuto.ReadTpc(inputPath, txiSource: txiInputPath);

            // Convert format if specified
            if (targetFormat != TPCTextureFormat.Invalid)
            {
                tpc.Convert(targetFormat);
            }

            TPCAuto.WriteTpc(tpc, outputPath, ResourceType.TPC);
        }

        // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tools/resources.py:83-101
        // Original: def convert_wav_to_clean(input_path: Path, output_path: Path) -> None:
        public static void ConvertWavToClean(string inputPath, string outputPath)
        {
            WAV wav = WAVAuto.ReadWav(inputPath);
            // Write as clean WAV (standard format without obfuscation)
            WAVAuto.WriteWav(wav, outputPath, ResourceType.WAV);
        }

        // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tools/resources.py:104-133
        // Original: def convert_clean_to_wav(input_path: Path, output_path: Path, *, wav_type: str = "VO") -> None:
        public static void ConvertCleanToWav(string inputPath, string outputPath, string wavType = "VO")
        {
            WAV wav = WAVAuto.ReadWav(inputPath);

            // Set WAV type if converting to game format
            if (string.Equals(wavType, "SFX", StringComparison.OrdinalIgnoreCase))
            {
                wav.WavType = WAVType.SFX;
            }
            else
            {
                wav.WavType = WAVType.VO;
            }

            WAVAuto.WriteWav(wav, outputPath, ResourceType.WAV);
        }

        // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tools/resources.py:136-160
        // Original: def convert_mdl_to_ascii(input_path: Path, output_path: Path, *, mdx_path: Path | None = None) -> None:
        public static void ConvertMdlToAscii(string inputPath, string outputPath, string mdxPath = null)
        {
            string mdx = mdxPath ?? Path.ChangeExtension(inputPath, ".mdx");
            if (!File.Exists(mdx))
            {
                mdx = null;
            }

            var mdl = MDLAuto.ReadMdl(inputPath, sourceExt: mdx);
            MDLAuto.WriteMdl(mdl, outputPath, ResourceType.MDL_ASCII);
        }

        // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tools/resources.py:163-187
        // Original: def convert_ascii_to_mdl(input_path: Path, output_mdl_path: Path, *, output_mdx_path: Path | None = None) -> None:
        public static void ConvertAsciiToMdl(string inputPath, string outputMdlPath, string outputMdxPath = null)
        {
            MDL mdl = MDLAuto.ReadMdl(inputPath);

            if (string.IsNullOrEmpty(outputMdxPath))
            {
                outputMdxPath = Path.ChangeExtension(outputMdlPath, ".mdx");
            }

            MDLAuto.WriteMdl(mdl, outputMdlPath, ResourceType.MDL, targetExt: outputMdxPath);
        }

        // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tools/resources.py:190-213
        // Original: def convert_texture_format(input_path: Path, output_path: Path, *, target_format: TPCTextureFormat | None = None) -> None:
        public static void ConvertTextureFormat(string inputPath, string outputPath, TPCTextureFormat targetFormat = TPCTextureFormat.Invalid)
        {
            TPC tpc = TPCAuto.ReadTpc(inputPath);

            if (targetFormat != TPCTextureFormat.Invalid)
            {
                tpc.Convert(targetFormat);
            }

            TPCAuto.WriteTpc(tpc, outputPath, ResourceType.TPC);
        }
    }
}
