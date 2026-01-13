using System;
using System.IO;
using BioWare.NET.Common;
using BioWare.NET.Resource;

namespace BioWare.NET.Resource.Formats.WAV
{
    // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/resource/formats/wav/wav_auto.py
    // Original: read_wav, write_wav, bytes_wav, get_playable_bytes, detect_audio_type functions
    public static class WAVAuto
    {
        // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/resource/formats/wav/wav_auto.py:40-69
        // Original: def read_wav(source: SOURCE_TYPES, offset: int = 0, size: int | None = None) -> WAV
        public static WAV ReadWav(object source, int offset = 0, int? size = null)
        {
            int sizeValue = size ?? 0;
            if (source is string filepath)
            {
                return new WAVBinaryReader(filepath, offset, sizeValue).Load();
            }
            if (source is byte[] bytes)
            {
                return new WAVBinaryReader(bytes, offset, sizeValue).Load();
            }
            if (source is Stream stream)
            {
                return new WAVBinaryReader(stream, offset, sizeValue).Load();
            }
            throw new ArgumentException("Source must be string, byte[], or Stream for WAV");
        }

        // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/resource/formats/wav/wav_auto.py:72-97
        // Original: def write_wav(wav: WAV, target: TARGET_TYPES, file_format: ResourceType = ResourceType.WAV)
        public static void WriteWav(WAV wav, object target, ResourceType fileFormat = null)
        {
            ResourceType format = fileFormat ?? ResourceType.WAV;
            if (format == ResourceType.WAV)
            {
                if (target is string filepath)
                {
                    new WAVBinaryWriter(wav, filepath).Write();
                }
                else if (target is Stream stream)
                {
                    new WAVBinaryWriter(wav, stream).Write();
                }
                else
                {
                    throw new ArgumentException("Target must be string or Stream for WAV");
                }
            }
            else
            {
                // WAV_DEOB or other formats use standard writer (clean output)
                if (target is string filepath)
                {
                    new WAVStandardWriter(wav, filepath).Write();
                }
                else if (target is Stream stream)
                {
                    new WAVStandardWriter(wav, stream).Write();
                }
                else
                {
                    throw new ArgumentException("Target must be string or Stream for WAV");
                }
            }
        }

        // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/resource/formats/wav/wav_auto.py:100-118
        // Original: def bytes_wav(wav: WAV, file_format: ResourceType = ResourceType.WAV) -> bytes
        public static byte[] BytesWav(WAV wav, ResourceType fileFormat = null)
        {
            ResourceType format = fileFormat ?? ResourceType.WAV;
            using (var ms = new MemoryStream())
            {
                WriteWav(wav, ms, format);
                return ms.ToArray();
            }
        }

        // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/resource/formats/wav/wav_auto.py:121-139
        // Original: def get_playable_bytes(wav: WAV) -> bytes
        public static byte[] GetPlayableBytes(WAV wav)
        {
            // Use a non-WAV resource type to trigger standard writer (clean output)
            // In Python this uses ResourceType.WAV_DEOB, but we'll use a different approach
            using (var ms = new MemoryStream())
            {
                new WAVStandardWriter(wav, ms).Write();
                return ms.ToArray();
            }
        }

        // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/resource/formats/wav/wav_auto.py:142-158
        // Original: def detect_audio_type(wav: WAV) -> str
        public static string DetectAudioType(WAV wav)
        {
            if (wav.AudioFormat == AudioFormat.MP3)
            {
                return "mp3";
            }
            return "wav";
        }
    }
}

