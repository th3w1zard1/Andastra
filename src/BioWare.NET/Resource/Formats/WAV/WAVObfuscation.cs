using System;

namespace BioWare.NET.Resource.Formats.WAV
{
    // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/resource/formats/wav/wav_obfuscation.py
    // Original: detect_audio_format, deobfuscate_audio, get_deobfuscation_result, obfuscate_audio functions
    public static class WAVObfuscation
    {
        // Magic numbers for detection
        // vendor/reone/src/libs/audio/format/wavreader.cpp:34
        private static readonly byte[] SFX_MAGIC_BYTES = { 0xFF, 0xF3, 0x60, 0xC4 };
        private static readonly byte[] RIFF_MAGIC = { 0x52, 0x49, 0x46, 0x46 }; // "RIFF"
        private const int MP3_IN_WAV_RIFF_SIZE = 50;
        private const int MP3_IN_WAV_HEADER_SIZE = 58;
        private const int SFX_HEADER_SIZE = 470; // 0x1DA bytes
        private const int VO_HEADER_SIZE = 20;

        // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/resource/formats/wav/wav_obfuscation.py:66-109
        // Original: def detect_audio_format(data: bytes) -> tuple[DeobfuscationResult, int]
        public static Tuple<DeobfuscationResult, int> DetectAudioFormat(byte[] data)
        {
            if (data == null || data.Length < 12)
            {
                return Tuple.Create(DeobfuscationResult.Standard, 0);
            }

            // Check first 4 bytes
            byte[] firstFour = new byte[4];
            Array.Copy(data, 0, firstFour, 0, 4);

            // Check for SFX header: 0xFF 0xF3 0x60 0xC4
            if (firstFour[0] == SFX_MAGIC_BYTES[0] && firstFour[1] == SFX_MAGIC_BYTES[1] &&
                firstFour[2] == SFX_MAGIC_BYTES[2] && firstFour[3] == SFX_MAGIC_BYTES[3])
            {
                return Tuple.Create(DeobfuscationResult.SFX_Header, SFX_HEADER_SIZE);
            }

            // Check for RIFF header
            if (firstFour[0] == RIFF_MAGIC[0] && firstFour[1] == RIFF_MAGIC[1] &&
                firstFour[2] == RIFF_MAGIC[2] && firstFour[3] == RIFF_MAGIC[3])
            {
                // Check for VO header: if "RIFF" appears again at offset 20, it's a 20-byte VO header
                if (data.Length >= VO_HEADER_SIZE + 4)
                {
                    bool isRiffAt20 = data[VO_HEADER_SIZE] == RIFF_MAGIC[0] &&
                                      data[VO_HEADER_SIZE + 1] == RIFF_MAGIC[1] &&
                                      data[VO_HEADER_SIZE + 2] == RIFF_MAGIC[2] &&
                                      data[VO_HEADER_SIZE + 3] == RIFF_MAGIC[3];
                    if (isRiffAt20)
                    {
                        return Tuple.Create(DeobfuscationResult.Standard, VO_HEADER_SIZE);
                    }
                }

                // Read the riffSize (bytes 4-8)
                if (data.Length >= 8)
                {
                    uint riffSize = BitConverter.ToUInt32(data, 4);

                    // if(riffSize == 50) → MP3 wrapped in WAV
                    if (riffSize == MP3_IN_WAV_RIFF_SIZE)
                    {
                        return Tuple.Create(DeobfuscationResult.MP3_In_WAV, MP3_IN_WAV_HEADER_SIZE);
                    }
                }

                // Standard RIFF/WAVE
                return Tuple.Create(DeobfuscationResult.Standard, 0);
            }

            // Unknown format, assume standard
            return Tuple.Create(DeobfuscationResult.Standard, 0);
        }

        // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/resource/formats/wav/wav_obfuscation.py:112-138
        // Original: def deobfuscate_audio(data: bytes) -> bytes
        public static byte[] DeobfuscateAudio(byte[] data)
        {
            var result = DetectAudioFormat(data);
            int skipSize = result.Item2;

            if (skipSize > 0 && data.Length > skipSize)
            {
                byte[] deobfuscated = new byte[data.Length - skipSize];
                Array.Copy(data, skipSize, deobfuscated, 0, deobfuscated.Length);
                return deobfuscated;
            }

            return data;
        }

        // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/resource/formats/wav/wav_obfuscation.py:141-157
        // Original: def get_deobfuscation_result(data: bytes) -> tuple[bytes, DeobfuscationResult]
        public static Tuple<byte[], DeobfuscationResult> GetDeobfuscationResult(byte[] data)
        {
            var result = DetectAudioFormat(data);
            DeobfuscationResult formatType = result.Item1;
            int skipSize = result.Item2;

            if (skipSize > 0 && data.Length > skipSize)
            {
                byte[] deobfuscated = new byte[data.Length - skipSize];
                Array.Copy(data, skipSize, deobfuscated, 0, deobfuscated.Length);
                return Tuple.Create(deobfuscated, formatType);
            }

            return Tuple.Create(data, formatType);
        }

        // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/resource/formats/wav/wav_obfuscation.py:160-202
        // Original: def obfuscate_audio(data: bytes, wav_type: str = "SFX") -> bytes
        public static byte[] ObfuscateAudio(byte[] data, string wavType = "SFX")
        {
            if (wavType == "SFX")
            {
                // Create 470-byte SFX header
                byte[] header = new byte[SFX_HEADER_SIZE];
                Array.Copy(SFX_MAGIC_BYTES, 0, header, 0, 4);
                // Fill remaining bytes with 0x00
                byte[] result = new byte[header.Length + data.Length];
                Array.Copy(header, 0, result, 0, header.Length);
                Array.Copy(data, 0, result, header.Length, data.Length);
                return result;
            }
            else if (wavType == "VO")
            {
                // Create 20-byte VO header
                byte[] header = new byte[VO_HEADER_SIZE];
                Array.Copy(RIFF_MAGIC, 0, header, 0, 4);
                // Fill remaining bytes with 0x00
                byte[] result = new byte[header.Length + data.Length];
                Array.Copy(header, 0, result, 0, header.Length);
                Array.Copy(data, 0, result, header.Length, data.Length);
                return result;
            }

            // Unknown type, return unchanged
            return data;
        }
    }
}

