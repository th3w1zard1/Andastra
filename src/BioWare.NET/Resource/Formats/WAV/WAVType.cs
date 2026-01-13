namespace BioWare.NET.Resource.Formats.WAV
{
    // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/resource/formats/wav/wav_data.py:50-53
    // Original: class WAVType(IntEnum)
    public enum WAVType
    {
        VO = 1,      // Voice over WAV (streamvoice, streamwaves)
        SFX = 2      // Sound effects WAV (streammusic/sounds with 470-byte header)
    }
}

