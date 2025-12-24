using System;
using System.IO;
using System.Linq;
using Andastra.Parsing.Common;
using Andastra.Parsing.Formats.WAV;
using Andastra.Parsing.Resource;
using Andastra.Parsing.Tests.Common;
using FluentAssertions;
using Xunit;

namespace Andastra.Parsing.Tests.Formats
{
    /// <summary>
    /// Comprehensive tests for WAV binary I/O operations.
    /// Tests validate the WAV format structure as defined in WAV.ksy Kaitai Struct definition.
    /// </summary>
    public class WAVFormatTests
    {
        private static readonly string BinaryTestFile = TestFileHelper.GetPath("test.wav");
        private static readonly string DoesNotExistFile = "./thisfiledoesnotexist";
        private static readonly string CorruptBinaryTestFile = TestFileHelper.GetPath("test_corrupted.wav");

        [Fact(Timeout = 120000)]
        public void TestBinaryIO()
        {
            if (!File.Exists(BinaryTestFile))
            {
                CreateTestWavFile(BinaryTestFile);
            }

            // Test reading WAV file
            WAV wav = new WAVBinaryReader(BinaryTestFile).Load();
            ValidateIO(wav);

            // Test writing and reading back
            byte[] data = new WAVBinaryWriter(wav).Write();
            wav = new WAVBinaryReader(data).Load();
            ValidateIO(wav);
        }

        [Fact(Timeout = 120000)]
        public void TestWavTypeDetection()
        {
            if (!File.Exists(BinaryTestFile))
            {
                CreateTestWavFile(BinaryTestFile);
            }

            WAV wav = new WAVBinaryReader(BinaryTestFile).Load();

            // Validate WAV type
            wav.WavType.Should().BeOneOf(new[] { WAVType.VO, WAVType.SFX }, "WAV type should be VO or SFX");
        }

        [Fact(Timeout = 120000)]
        public void TestWavAudioFormat()
        {
            if (!File.Exists(BinaryTestFile))
            {
                CreateTestWavFile(BinaryTestFile);
            }

            WAV wav = new WAVBinaryReader(BinaryTestFile).Load();

            // Validate audio format
            wav.AudioFormat.Should().BeOneOf(new[] { AudioFormat.Wave, AudioFormat.MP3 }, "Audio format should be Wave or MP3");
            wav.Encoding.Should().BeGreaterThanOrEqualTo(0, "Encoding should be non-negative");
            wav.Channels.Should().BeGreaterThan(0, "Channels should be positive");
            wav.SampleRate.Should().BeGreaterThan(0, "Sample rate should be positive");
            wav.BitsPerSample.Should().BeGreaterThan(0, "Bits per sample should be positive");
        }

        [Fact(Timeout = 120000)]
        public void TestWavRIFFHeader()
        {
            if (!File.Exists(BinaryTestFile))
            {
                CreateTestWavFile(BinaryTestFile);
            }

            // Read raw header bytes
            byte[] header = new byte[12];
            using (var fs = File.OpenRead(BinaryTestFile))
            {
                // Skip SFX/VO header if present
                byte[] firstByte = new byte[1];
                fs.Read(firstByte, 0, 1);
                fs.Seek(0, SeekOrigin.Begin);

                int offset = 0;
                if (firstByte[0] == 0xFF)
                {
                    offset = 470; // SFX header
                }
                else if (firstByte[0] == 0x52)
                {
                    // Check if VO header
                    byte[] checkBytes = new byte[4];
                    fs.Read(checkBytes, 0, 4);
                    if (checkBytes[0] == 0x52 && checkBytes[1] == 0x49 && checkBytes[2] == 0x46 && checkBytes[3] == 0x46)
                    {
                        // Check if "RIFF" appears again at offset 20
                        if (fs.Length >= 24)
                        {
                            fs.Seek(20, SeekOrigin.Begin);
                            fs.Read(checkBytes, 0, 4);
                            if (checkBytes[0] == 0x52 && checkBytes[1] == 0x49 && checkBytes[2] == 0x46 && checkBytes[3] == 0x46)
                            {
                                offset = 20; // VO header
                            }
                        }
                    }
                }

                fs.Seek(offset, SeekOrigin.Begin);
                fs.Read(header, 0, 12);
            }

            // Validate RIFF header matches WAV.ksy
            string riffId = System.Text.Encoding.ASCII.GetString(header, 0, 4);
            riffId.Should().Be("RIFF", "RIFF ID should match WAV.ksy definition");

            string waveId = System.Text.Encoding.ASCII.GetString(header, 8, 4);
            waveId.Should().Be("WAVE", "WAVE ID should match WAV.ksy definition");
        }

        [Fact(Timeout = 120000)]
        public void TestWavFormatChunk()
        {
            if (!File.Exists(BinaryTestFile))
            {
                CreateTestWavFile(BinaryTestFile);
            }

            WAV wav = new WAVBinaryReader(BinaryTestFile).Load();

            // Validate format chunk structure matches WAV.ksy
            // audio_format, channels, sample_rate, bytes_per_sec, block_align, bits_per_sample
            wav.Encoding.Should().BeOneOf(new[] {
                (int)WaveEncoding.PCM,
                (int)WaveEncoding.IMA_ADPCM,
                (int)WaveEncoding.MP3
            }, "Audio format should match WAV.ksy valid values");

            wav.Channels.Should().BeInRange(1, 2, "Channels should be 1 (mono) or 2 (stereo) as per WAV.ksy");
            wav.SampleRate.Should().BePositive("Sample rate should be positive");
            wav.BytesPerSec.Should().BePositive("Bytes per second should be positive");
            wav.BlockAlign.Should().BePositive("Block align should be positive");
            wav.BitsPerSample.Should().BeOneOf(new[] { 8, 16 }, "Bits per sample should be 8 or 16 as per WAV.ksy");
        }

        [Fact(Timeout = 120000)]
        public void TestWavDataChunk()
        {
            if (!File.Exists(BinaryTestFile))
            {
                CreateTestWavFile(BinaryTestFile);
            }

            WAV wav = new WAVBinaryReader(BinaryTestFile).Load();

            // Validate data chunk
            wav.Data.Should().NotBeNull("Data chunk should not be null");
            wav.Data.Length.Should().BeGreaterThanOrEqualTo(0, "Data length should be non-negative");
        }

        [Fact(Timeout = 120000)]
        public void TestWavEmptyFile()
        {
            // Test WAV with minimal valid structure
            var wav = new WAV(
                wavType: WAVType.VO,
                audioFormat: AudioFormat.Wave,
                encoding: (int)WaveEncoding.PCM,
                channels: 1,
                sampleRate: 44100,
                bitsPerSample: 16,
                data: new byte[0]
            );

            wav.Channels.Should().Be(1, "Empty WAV should have valid channels");
            wav.SampleRate.Should().Be(44100, "Empty WAV should have valid sample rate");
        }

        [Fact(Timeout = 120000)]
        public void TestWavMultipleFormats()
        {
            // Test different audio formats
            var formats = new[]
            {
                (int)WaveEncoding.PCM,
                (int)WaveEncoding.IMA_ADPCM
            };

            foreach (var encoding in formats)
            {
                var wav = new WAV(
                    wavType: WAVType.VO,
                    audioFormat: AudioFormat.Wave,
                    encoding: encoding,
                    channels: 2,
                    sampleRate: 22050,
                    bitsPerSample: 16,
                    data: new byte[100]
                );

                wav.Encoding.Should().Be(encoding);
                wav.Channels.Should().Be(2);
            }
        }

        [Fact(Timeout = 120000)]
        public void TestReadRaises()
        {
            // Test reading from directory
            Action act1 = () => new WAVBinaryReader(".").Load();
            if (System.Runtime.InteropServices.RuntimeInformation.IsOSPlatform(System.Runtime.InteropServices.OSPlatform.Windows))
            {
                act1.Should().Throw<UnauthorizedAccessException>();
            }
            else
            {
                act1.Should().Throw<IOException>();
            }

            // Test reading non-existent file
            Action act2 = () => new WAVBinaryReader(DoesNotExistFile).Load();
            act2.Should().Throw<FileNotFoundException>();
        }

        [Fact(Timeout = 120000)]
        public void TestWavInvalidSignature()
        {
            // Create file with invalid signature
            string tempFile = Path.GetTempFileName();
            try
            {
                using (var fs = File.Create(tempFile))
                {
                    byte[] invalid = System.Text.Encoding.ASCII.GetBytes("INVALID");
                    fs.Write(invalid, 0, invalid.Length);
                }

                Action act = () => new WAVBinaryReader(tempFile).Load();
                act.Should().Throw<ArgumentException>().WithMessage("*RIFF*");
            }
            finally
            {
                if (File.Exists(tempFile))
                {
                    File.Delete(tempFile);
                }
            }
        }

        [Fact(Timeout = 120000)]
        public void TestWavSFXHeader()
        {
            // Test SFX header detection (0xFF 0xF3 0x60 0xC4)
            string tempFile = Path.GetTempFileName();
            try
            {
                using (var fs = File.Create(tempFile))
                {
                    // Write SFX header
                    fs.WriteByte(0xFF);
                    fs.WriteByte(0xF3);
                    fs.WriteByte(0x60);
                    fs.WriteByte(0xC4);
                    // Write padding
                    for (int i = 0; i < 466; i++)
                    {
                        fs.WriteByte(0x00);
                    }
                    // Write minimal RIFF/WAVE structure
                    WriteMinimalRiffWave(fs);
                }

                // Should detect as SFX type
                WAV wav = new WAVBinaryReader(tempFile).Load();
                // Note: The actual detection happens in WAVObfuscation, but we can verify the file was parsed
                wav.Should().NotBeNull();
            }
            finally
            {
                if (File.Exists(tempFile))
                {
                    File.Delete(tempFile);
                }
            }
        }

        [Fact(Timeout = 120000)]
        public void TestWavVOHeader()
        {
            // Test VO header (20-byte header with "RIFF")
            string tempFile = Path.GetTempFileName();
            try
            {
                using (var fs = File.Create(tempFile))
                {
                    // Write VO header
                    byte[] riff = System.Text.Encoding.ASCII.GetBytes("RIFF");
                    fs.Write(riff, 0, 4);
                    for (int i = 0; i < 16; i++)
                    {
                        fs.WriteByte(0x00);
                    }
                    // Write minimal RIFF/WAVE structure
                    WriteMinimalRiffWave(fs);
                }

                WAV wav = new WAVBinaryReader(tempFile).Load();
                wav.Should().NotBeNull();
            }
            finally
            {
                if (File.Exists(tempFile))
                {
                    File.Delete(tempFile);
                }
            }
        }

        [Fact(Timeout = 120000)]
        public void TestWavStandardFormat()
        {
            // Test standard RIFF/WAVE format (no header)
            string tempFile = Path.GetTempFileName();
            try
            {
                using (var fs = File.Create(tempFile))
                {
                    WriteMinimalRiffWave(fs);
                }

                WAV wav = new WAVBinaryReader(tempFile).Load();
                wav.Should().NotBeNull();
                wav.WavType.Should().Be(WAVType.VO, "Standard format should be detected as VO");
            }
            finally
            {
                if (File.Exists(tempFile))
                {
                    File.Delete(tempFile);
                }
            }
        }

        private static void ValidateIO(WAV wav)
        {
            // Basic validation
            wav.Should().NotBeNull();
            wav.WavType.Should().BeOneOf(new[] { WAVType.VO, WAVType.SFX });
            wav.Channels.Should().BeGreaterThan(0);
            wav.SampleRate.Should().BeGreaterThan(0);
            wav.BitsPerSample.Should().BeGreaterThan(0);
            wav.Data.Should().NotBeNull();
        }

        private static void CreateTestWavFile(string path)
        {
            var wav = new WAV(
                wavType: WAVType.VO,
                audioFormat: AudioFormat.Wave,
                encoding: (int)WaveEncoding.PCM,
                channels: 1,
                sampleRate: 44100,
                bitsPerSample: 16,
                bytesPerSec: 88200,
                blockAlign: 2,
                data: new byte[1000] // Minimal test data
            );

            byte[] data = new WAVBinaryWriter(wav).Write();
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllBytes(path, data);
        }

        private static void WriteMinimalRiffWave(Stream fs)
        {
            // Write RIFF header
            byte[] riff = System.Text.Encoding.ASCII.GetBytes("RIFF");
            fs.Write(riff, 0, 4);

            // Calculate size: WAVE (4) + fmt chunk (8 + 16) + data chunk (8 + data size)
            uint dataSize = 100; // Minimal data
            uint totalSize = 4 + 8 + 16 + 8 + dataSize;
            byte[] sizeBytes = BitConverter.GetBytes(totalSize);
            fs.Write(sizeBytes, 0, 4);

            byte[] wave = System.Text.Encoding.ASCII.GetBytes("WAVE");
            fs.Write(wave, 0, 4);

            // Write fmt chunk
            byte[] fmt = System.Text.Encoding.ASCII.GetBytes("fmt ");
            fs.Write(fmt, 0, 4);
            byte[] fmtSize = BitConverter.GetBytes(16u);
            fs.Write(fmtSize, 0, 4);

            // PCM format
            byte[] audioFormat = BitConverter.GetBytes((ushort)1);
            fs.Write(audioFormat, 0, 2);
            byte[] channels = BitConverter.GetBytes((ushort)1);
            fs.Write(channels, 0, 2);
            byte[] sampleRate = BitConverter.GetBytes(44100u);
            fs.Write(sampleRate, 0, 4);
            byte[] bytesPerSec = BitConverter.GetBytes(88200u);
            fs.Write(bytesPerSec, 0, 4);
            byte[] blockAlign = BitConverter.GetBytes((ushort)2);
            fs.Write(blockAlign, 0, 2);
            byte[] bitsPerSample = BitConverter.GetBytes((ushort)16);
            fs.Write(bitsPerSample, 0, 2);

            // Write data chunk
            byte[] data = System.Text.Encoding.ASCII.GetBytes("data");
            fs.Write(data, 0, 4);
            byte[] dataSizeBytes = BitConverter.GetBytes(dataSize);
            fs.Write(dataSizeBytes, 0, 4);
            byte[] audioData = new byte[dataSize];
            fs.Write(audioData, 0, (int)dataSize);
        }
    }
}

