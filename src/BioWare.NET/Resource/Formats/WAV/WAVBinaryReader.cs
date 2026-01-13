using System;
using System.IO;
using BioWare.NET.Resource.Formats.WAV;

namespace BioWare.NET.Resource.Formats.WAV
{
    // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/resource/formats/wav/io_wav.py:38-220
    // Original: class WAVBinaryReader(ResourceReader)
    public class WAVBinaryReader : IDisposable
    {
        private readonly BioWare.NET.Common.RawBinaryReader _reader;

        public WAVBinaryReader(byte[] data, int offset = 0, int size = 0)
        {
            int? sizeNullable = size > 0 ? (int?)size : null;
            _reader = BioWare.NET.Common.RawBinaryReader.FromBytes(data, offset, sizeNullable);
        }

        public WAVBinaryReader(string filepath, int offset = 0, int size = 0)
        {
            int? sizeNullable = size > 0 ? (int?)size : null;
            _reader = BioWare.NET.Common.RawBinaryReader.FromFile(filepath, offset, sizeNullable);
        }

        public WAVBinaryReader(Stream source, int offset = 0, int size = 0)
        {
            int? sizeNullable = size > 0 ? (int?)size : null;
            _reader = BioWare.NET.Common.RawBinaryReader.FromStream(source, offset, sizeNullable);
        }

        // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/resource/formats/wav/io_wav.py:71-110
        // Original: @autoclose def load(self, *, auto_close: bool = True) -> WAV
        public WAV Load(bool autoClose = true)
        {
            try
            {
                // Read all data
                _reader.Seek(0);
                byte[] rawData = _reader.ReadAll();

                // Deobfuscate and get format info
                var result = WAVObfuscation.GetDeobfuscationResult(rawData);
                byte[] deobfuscatedData = result.Item1;
                DeobfuscationResult formatType = result.Item2;

                // Determine WAV type based on deobfuscation result
                WAVType wavType = formatType == DeobfuscationResult.SFX_Header ? WAVType.SFX : WAVType.VO;

                // If MP3-in-WAV format detected, return MP3 data directly
                if (formatType == DeobfuscationResult.MP3_In_WAV)
                {
                    return new WAV(
                        wavType: wavType,
                        audioFormat: AudioFormat.MP3,
                        encoding: (int)WaveEncoding.MP3,
                        channels: 2,  // Default stereo for MP3
                        sampleRate: 44100,  // Default sample rate
                        bitsPerSample: 16,
                        data: deobfuscatedData
                    );
                }

                // Parse as RIFF/WAVE
                return ParseRiffWave(deobfuscatedData, wavType);
            }
            finally
            {
                if (autoClose)
                {
                    Dispose();
                }
            }
        }

        // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/resource/formats/wav/io_wav.py:112-220
        // Original: def _parse_riff_wave(self, data: bytes, wav_type: WAVType) -> WAV
        private WAV ParseRiffWave(byte[] data, WAVType wavType)
        {
            using (var ms = new MemoryStream(data))
            using (var reader = BioWare.NET.Common.RawBinaryReader.FromStream(ms))
            {
                // Read RIFF header
                byte[] riffTag = reader.ReadBytes(4);
                if (riffTag[0] != 0x52 || riffTag[1] != 0x49 || riffTag[2] != 0x46 || riffTag[3] != 0x46) // "RIFF"
                {
                    throw new ArgumentException($"Not a valid RIFF file, got: {System.Text.Encoding.ASCII.GetString(riffTag)}");
                }

                uint fileSize = reader.ReadUInt32();
                byte[] waveTag = reader.ReadBytes(4);
                if (waveTag[0] != 0x57 || waveTag[1] != 0x41 || waveTag[2] != 0x56 || waveTag[3] != 0x45) // "WAVE"
                {
                    throw new ArgumentException($"Not a valid WAVE file, got: {System.Text.Encoding.ASCII.GetString(waveTag)}");
                }

                // Initialize format values with defaults
                int encoding = (int)WaveEncoding.PCM;
                int channels = 1;
                int sampleRate = 44100;
                int bytesPerSec = 88200;
                int blockAlign = 2;
                int bitsPerSample = 16;
                byte[] audioData = new byte[0];
                bool foundDataChunk = false;

                // Parse chunks until we find 'data'
                while (reader.Remaining >= 8)
                {
                    byte[] chunkId = reader.ReadBytes(4);
                    uint chunkSize = reader.ReadUInt32();

                    if (chunkId[0] == 0x66 && chunkId[1] == 0x6D && chunkId[2] == 0x74 && chunkId[3] == 0x20) // "fmt "
                    {
                        // Parse format chunk
                        ushort encodingValue = reader.ReadUInt16();
                        encoding = encodingValue;

                        channels = reader.ReadUInt16();
                        sampleRate = (int)reader.ReadUInt32();
                        bytesPerSec = (int)reader.ReadUInt32();
                        blockAlign = reader.ReadUInt16();
                        bitsPerSample = reader.ReadUInt16();

                        // Skip any extra format bytes
                        if (chunkSize > 16)
                        {
                            reader.Skip((int)chunkSize - 16);
                        }
                    }
                    else if (chunkId[0] == 0x64 && chunkId[1] == 0x61 && chunkId[2] == 0x74 && chunkId[3] == 0x61) // "data"
                    {
                        // Read audio data
                        int actualSize = (int)Math.Min(chunkSize, reader.Remaining);
                        audioData = reader.ReadBytes(actualSize);
                        foundDataChunk = true;
                        break; // Found data, stop parsing
                    }
                    else if (chunkId[0] == 0x66 && chunkId[1] == 0x61 && chunkId[2] == 0x63 && chunkId[3] == 0x74) // "fact"
                    {
                        // Skip fact chunk
                        reader.Skip((int)chunkSize);
                    }
                    else
                    {
                        // Skip unknown chunks
                        if (chunkSize > reader.Remaining)
                        {
                            break; // Malformed chunk, stop
                        }
                        reader.Skip((int)chunkSize);
                        // RIFF chunks are word-aligned
                        if (chunkSize % 2 == 1 && reader.Remaining > 0)
                        {
                            reader.Skip(1);
                        }
                    }
                }

                if (!foundDataChunk)
                {
                    throw new ArgumentException("No audio data chunk found in WAV file");
                }

                // Create WAV object
                return new WAV(
                    wavType: wavType,
                    audioFormat: AudioFormat.Wave,
                    encoding: encoding,
                    channels: channels,
                    sampleRate: sampleRate,
                    bitsPerSample: bitsPerSample,
                    bytesPerSec: bytesPerSec,
                    blockAlign: blockAlign,
                    data: audioData
                );
            }
        }

        public void Dispose()
        {
            _reader?.Dispose();
        }
    }
}

