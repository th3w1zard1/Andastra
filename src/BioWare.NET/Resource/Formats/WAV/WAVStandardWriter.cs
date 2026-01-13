using System;
using System.IO;
using BioWare.NET.Common;
using BioWare.NET.Resource.Formats.WAV;

namespace BioWare.NET.Resource.Formats.WAV
{
    // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/resource/formats/wav/io_wav_standard.py:30-90
    // Original: class WAVStandardWriter(ResourceWriter)
    public class WAVStandardWriter : IDisposable
    {
        private readonly WAV _wav;
        private readonly RawBinaryWriter _writer;

        public WAVStandardWriter(WAV wav, string filepath)
        {
            _wav = wav ?? throw new ArgumentNullException(nameof(wav));
            _writer = RawBinaryWriter.ToFile(filepath);
        }

        public WAVStandardWriter(WAV wav, Stream target)
        {
            _wav = wav ?? throw new ArgumentNullException(nameof(wav));
            _writer = RawBinaryWriter.ToStream(target);
        }

        public WAVStandardWriter(WAV wav)
        {
            _wav = wav ?? throw new ArgumentNullException(nameof(wav));
            _writer = RawBinaryWriter.ToByteArray();
        }

        // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/resource/formats/wav/io_wav_standard.py:51-90
        // Original: @autoclose def write(self, *, auto_close: bool = True) -> None
        public void Write(bool autoClose = true)
        {
            try
            {
                // For MP3 format, write raw MP3 bytes
                if (_wav.AudioFormat == AudioFormat.MP3)
                {
                    _writer.WriteBytes(_wav.Data);
                    return;
                }

                // Calculate sizes for RIFF header
                int dataSize = _wav.Data.Length;
                int fmtChunkSize = 16;
                // RIFF size = 4 (WAVE) + 8 (fmt header) + fmt_chunk_size + 8 (data header) + data_size
                uint fileSize = (uint)(4 + 8 + fmtChunkSize + 8 + dataSize);

                // Write RIFF header
                _writer.WriteBytes(new byte[] { 0x52, 0x49, 0x46, 0x46 }); // "RIFF"
                _writer.WriteUInt32(fileSize);
                _writer.WriteBytes(new byte[] { 0x57, 0x41, 0x56, 0x45 }); // "WAVE"

                // Write format chunk
                _writer.WriteBytes(new byte[] { 0x66, 0x6D, 0x74, 0x20 }); // "fmt "
                _writer.WriteUInt32((uint)fmtChunkSize);
                _writer.WriteUInt16((ushort)_wav.Encoding);
                _writer.WriteUInt16((ushort)_wav.Channels);
                _writer.WriteUInt32((uint)_wav.SampleRate);
                int bytesPerSec = _wav.BytesPerSec != 0 ? _wav.BytesPerSec : (_wav.SampleRate * _wav.BlockAlign);
                _writer.WriteUInt32((uint)bytesPerSec);
                _writer.WriteUInt16((ushort)_wav.BlockAlign);
                _writer.WriteUInt16((ushort)_wav.BitsPerSample);

                // Write data chunk
                _writer.WriteBytes(new byte[] { 0x64, 0x61, 0x74, 0x61 }); // "data"
                _writer.WriteUInt32((uint)dataSize);
                _writer.WriteBytes(_wav.Data);
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
