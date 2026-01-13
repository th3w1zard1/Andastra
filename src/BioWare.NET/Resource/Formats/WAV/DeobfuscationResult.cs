namespace BioWare.NET.Resource.Formats.WAV
{
    // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/resource/formats/wav/wav_obfuscation.py:38-42
    // Original: class DeobfuscationResult(IntEnum)
    public enum DeobfuscationResult
    {
        Standard = 0,       // Standard RIFF/WAVE, no header removed
        SFX_Header = 1,     // SFX 470-byte header removed, data is WAVE
        MP3_In_WAV = 2      // MP3-in-WAV 58-byte header removed, data is MP3
    }
}

