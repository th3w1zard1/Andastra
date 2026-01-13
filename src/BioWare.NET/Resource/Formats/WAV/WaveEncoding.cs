namespace BioWare.NET.Resource.Formats.WAV
{
    // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/resource/formats/wav/wav_data.py:27-40
    // Original: class WaveEncoding(IntEnum)
    public enum WaveEncoding
    {
        PCM = 0x01,           // Linear PCM (uncompressed)
        MS_ADPCM = 0x02,      // Microsoft ADPCM
        ALAW = 0x06,          // A-Law companded
        MULAW = 0x07,         // μ-Law companded
        IMA_ADPCM = 0x11,     // IMA ADPCM (also known as DVI ADPCM)
        MP3 = 0x55            // MPEG Layer 3
    }
}

