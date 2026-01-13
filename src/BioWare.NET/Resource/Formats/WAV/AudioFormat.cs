namespace BioWare.NET.Resource.Formats.WAV
{
    // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/resource/formats/wav/wav_data.py:43-47
    // Original: class AudioFormat(IntEnum)
    public enum AudioFormat
    {
        Unknown = 0,    // Unknown format
        Wave = 1,       // Standard RIFF/WAVE format
        MP3 = 2         // MP3 data (possibly wrapped in WAV)
    }
}

