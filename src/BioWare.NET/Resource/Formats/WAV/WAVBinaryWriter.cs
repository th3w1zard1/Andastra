using System;
using System.IO;
using BioWare.NET.Common;
using BioWare.NET.Resource.Formats.WAV;

namespace BioWare.NET.Resource.Formats.WAV
{
    // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/resource/formats/wav/io_wav.py:223-294
    // Original: class WAVBinaryWriter(ResourceWriter)
    public class WAVBinaryWriter : IDisposable
    {
        private readonly WAV _wav;
        private readonly RawBinaryWriter _writer;

        public WAVBinaryWriter(WAV wav, string filepath)
        {
            _wav = wav ?? throw new ArgumentNullException(nameof(wav));
            _writer = RawBinaryWriter.ToFile(filepath);
        }

        public WAVBinaryWriter(WAV wav, Stream target)
        {
            _wav = wav ?? throw new ArgumentNullException(nameof(wav));
            _writer = RawBinaryWriter.ToStream(target);
        }

        public WAVBinaryWriter(WAV wav)
        {
            _wav = wav ?? throw new ArgumentNullException(nameof(wav));
            _writer = RawBinaryWriter.ToByteArray();
        }

        // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/resource/formats/wav/io_wav.py:240-294
        // Original: @autoclose def write(self, *, auto_close: bool = True) -> None
        public byte[] Write()
        {
            using (var ms = new MemoryStream())
            {
                using (var writer = new WAVBinaryWriter(_wav, ms))
                {
                    writer.Write(false); // Don't auto-close
                }
                return ms.ToArray();
            }
        }

        public void Write(bool autoClose = true)
        {
            try
            {
                // For MP3 format, just obfuscate and write
                if (_wav.AudioFormat == AudioFormat.MP3)
                {
                    string wavTypeStr = _wav.WavType == WAVType.SFX ? "SFX" : "VO";
                    byte[] obfuscatedData = WAVObfuscation.ObfuscateAudio(_wav.Data, wavTypeStr);
                    _writer.WriteBytes(obfuscatedData);
                    return;
                }

                // Build clean WAV data
                using (var cleanBuffer = new MemoryStream())
                using (var cleanWriter = RawBinaryWriter.ToStream(cleanBuffer))
                {
                    // Calculate sizes
                    int dataSize = _wav.Data.Length;
                    int fmtChunkSize = 16;
                    // RIFF size = 4 (WAVE) + 8 (fmt header) + fmt_chunk_size + 8 (data header) + data_size
                    uint fileSize = (uint)(4 + 8 + fmtChunkSize + 8 + dataSize);

                    // Write RIFF header
                    cleanWriter.WriteBytes(new byte[] { 0x52, 0x49, 0x46, 0x46 }); // "RIFF"
                    cleanWriter.WriteUInt32(fileSize);
                    cleanWriter.WriteBytes(new byte[] { 0x57, 0x41, 0x56, 0x45 }); // "WAVE"

                    // Write format chunk
                    cleanWriter.WriteBytes(new byte[] { 0x66, 0x6D, 0x74, 0x20 }); // "fmt "
                    cleanWriter.WriteUInt32((uint)fmtChunkSize);
                    cleanWriter.WriteUInt16((ushort)_wav.Encoding);
                    cleanWriter.WriteUInt16((ushort)_wav.Channels);
                    cleanWriter.WriteUInt32((uint)_wav.SampleRate);
                    int bytesPerSec = _wav.BytesPerSec != 0 ? _wav.BytesPerSec : (_wav.SampleRate * _wav.BlockAlign);
                    cleanWriter.WriteUInt32((uint)bytesPerSec);
                    cleanWriter.WriteUInt16((ushort)_wav.BlockAlign);
                    cleanWriter.WriteUInt16((ushort)_wav.BitsPerSample);

                    // Write data chunk
                    cleanWriter.WriteBytes(new byte[] { 0x64, 0x61, 0x74, 0x61 }); // "data"
                    cleanWriter.WriteUInt32((uint)dataSize);
                    cleanWriter.WriteBytes(_wav.Data);

                    // Get clean data and obfuscate it
                    byte[] cleanData = cleanBuffer.ToArray();
                    string wavTypeStr = _wav.WavType == WAVType.SFX ? "SFX" : "VO";
                    byte[] obfuscatedData = WAVObfuscation.ObfuscateAudio(cleanData, wavTypeStr);

                    // Write obfuscated data
                    _writer.WriteBytes(obfuscatedData);
                }
            }
            finally
            {
                if (autoClose)
                {
                    Dispose();
                }
            }
        }

        public void Dispose()
        {
            _writer?.Dispose();
        }
    }
}
