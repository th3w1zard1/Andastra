using System;
using BioWare.NET.Common;
using BioWare.NET.Resource;

namespace BioWare.NET.Resource.Formats.WAV
{
    // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/resource/formats/wav/wav_data.py:56-100
    // Original: class WAV
    public class WAV
    {
        public static readonly ResourceType BinaryType = ResourceType.WAV;

        public WAVType WavType { get; set; }
        public AudioFormat AudioFormat { get; set; }
        public int Encoding { get; set; }  // Stored as int to allow unsupported values
        public int Channels { get; set; }
        public int SampleRate { get; set; }
        public int BitsPerSample { get; set; }
        public int BytesPerSec { get; set; }
        public int BlockAlign { get; set; }
        public byte[] Data { get; set; }

        public WAV(
            WAVType wavType = WAVType.VO,
            AudioFormat audioFormat = AudioFormat.Wave,
            int encoding = (int)WaveEncoding.PCM,
            int channels = 1,
            int sampleRate = 44100,
            int bitsPerSample = 16,
            int bytesPerSec = 0,
            int blockAlign = 0,
            byte[] data = null)
        {
            // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/resource/formats/wav/wav_data.py:79-100
            // Original: def __init__(self, ...)
            WavType = wavType;
            AudioFormat = audioFormat;
            Encoding = encoding;
            Channels = channels;
            SampleRate = sampleRate;
            BitsPerSample = bitsPerSample;
            BytesPerSec = bytesPerSec;
            BlockAlign = blockAlign != 0 ? blockAlign : (channels * bitsPerSample / 8);
            Data = data ?? new byte[0];
        }
    }
}

