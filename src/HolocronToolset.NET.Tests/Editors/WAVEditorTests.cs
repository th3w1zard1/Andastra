using System;
using System.IO;
using System.Text;
using CSharpKOTOR.Resources;
using FluentAssertions;
using HolocronToolset.NET.Data;
using HolocronToolset.NET.Editors;
using HolocronToolset.NET.Tests.TestHelpers;
using Xunit;

namespace HolocronToolset.NET.Tests.Editors
{
    // Matching PyKotor implementation at Tools/HolocronToolset/tests/gui/editors/test_wav_editor.py
    // Original: Comprehensive tests for WAV/Audio Editor
    [Collection("Avalonia Test Collection")]
    public class WAVEditorTests : IClassFixture<AvaloniaTestFixture>
    {
        private readonly AvaloniaTestFixture _fixture;

        public WAVEditorTests(AvaloniaTestFixture fixture)
        {
            _fixture = fixture;
        }

        [Fact]
        public void TestWavEditorNewFileCreation()
        {
            // Matching PyKotor implementation at Tools/HolocronToolset/tests/gui/editors/test_wav_editor.py
            // Original: def test_wav_editor_new_file_creation(qtbot, installation):
            var editor = new WAVEditor(null, null);

            editor.New();

            // Verify editor is ready
            var (data, _) = editor.Build();
            data.Should().NotBeNull();
        }

        [Fact]
        public void TestWavEditorInitialization()
        {
            // Matching PyKotor implementation at Tools/HolocronToolset/tests/gui/editors/test_wav_editor.py
            // Original: def test_wav_editor_initialization(qtbot, installation):
            var editor = new WAVEditor(null, null);

            // Verify editor is initialized
            editor.Should().NotBeNull();
            var (data, _) = editor.Build();
            data.Should().NotBeNull();
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/tests/gui/editors/test_wav_editor.py:256-262
        // Original: def test_load_wav_file(self, wav_editor, sample_wav_data: bytes):
        [Fact]
        public void TestWavEditorLoadExistingFile()
        {
            // Get test files directory
            string testFilesDir = System.IO.Path.Combine(
                System.IO.Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location),
                "..", "..", "..", "..", "vendor", "PyKotor", "Tools", "HolocronToolset", "tests", "test_files");

            // Try to find a WAV file
            string wavFile = System.IO.Path.Combine(testFilesDir, "test.wav");
            if (!System.IO.File.Exists(wavFile))
            {
                // Try alternative location
                testFilesDir = System.IO.Path.Combine(
                    System.IO.Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location),
                    "..", "..", "..", "..", "..", "vendor", "PyKotor", "Tools", "HolocronToolset", "tests", "test_files");
                wavFile = System.IO.Path.Combine(testFilesDir, "test.wav");
            }

            // If no test file, create a minimal valid WAV file for testing
            byte[] sampleWavData = null;
            if (!System.IO.File.Exists(wavFile))
            {
                // Create minimal valid WAV file (1-second mono 8kHz 8-bit PCM)
                // Matching PyKotor implementation at Tools/HolocronToolset/tests/gui/editors/test_wav_editor.py:43-85
                // Original: @pytest.fixture def sample_wav_data() -> bytes:
                int sampleRate = 8000;
                int numChannels = 1;
                int bitsPerSample = 8;
                int durationSeconds = 1;
                int numSamples = sampleRate * durationSeconds;
                byte[] audioData = new byte[numSamples];
                for (int i = 0; i < numSamples; i++)
                {
                    audioData[i] = 128; // 128 is silence for 8-bit
                }

                int dataSize = audioData.Length;
                int fmtChunkSize = 16;
                int fileSize = 4 + (8 + fmtChunkSize) + (8 + dataSize);

                using (var ms = new System.IO.MemoryStream())
                {
                    // RIFF header
                    ms.Write(System.Text.Encoding.ASCII.GetBytes("RIFF"), 0, 4);
                    ms.Write(BitConverter.GetBytes(fileSize), 0, 4);
                    ms.Write(System.Text.Encoding.ASCII.GetBytes("WAVE"), 0, 4);

                    // fmt chunk
                    ms.Write(System.Text.Encoding.ASCII.GetBytes("fmt "), 0, 4);
                    ms.Write(BitConverter.GetBytes(fmtChunkSize), 0, 4);
                    ms.Write(BitConverter.GetBytes((ushort)1), 0, 2); // Audio format (PCM)
                    ms.Write(BitConverter.GetBytes((ushort)numChannels), 0, 2);
                    ms.Write(BitConverter.GetBytes(sampleRate), 0, 4);
                    ms.Write(BitConverter.GetBytes(sampleRate * numChannels * bitsPerSample / 8), 0, 4); // Byte rate
                    ms.Write(BitConverter.GetBytes((ushort)(numChannels * bitsPerSample / 8)), 0, 2); // Block align
                    ms.Write(BitConverter.GetBytes((ushort)bitsPerSample), 0, 2);

                    // data chunk
                    ms.Write(System.Text.Encoding.ASCII.GetBytes("data"), 0, 4);
                    ms.Write(BitConverter.GetBytes(dataSize), 0, 4);
                    ms.Write(audioData, 0, audioData.Length);

                    sampleWavData = ms.ToArray();
                }
            }
            else
            {
                sampleWavData = System.IO.File.ReadAllBytes(wavFile);
            }

            // Get installation if available
            string k1Path = Environment.GetEnvironmentVariable("K1_PATH");
            if (string.IsNullOrEmpty(k1Path))
            {
                k1Path = @"C:\Program Files (x86)\Steam\steamapps\common\swkotor";
            }

            HTInstallation installation = null;
            if (System.IO.Directory.Exists(k1Path) && System.IO.File.Exists(System.IO.Path.Combine(k1Path, "chitin.key")))
            {
                installation = new HTInstallation(k1Path, "Test Installation", tsl: false);
            }

            var editor = new WAVEditor(null, installation);

            // Matching PyKotor implementation at Tools/HolocronToolset/tests/gui/editors/test_wav_editor.py:258
            // Original: wav_editor.load(Path("test.wav"), "test", ResourceType.WAV, sample_wav_data)
            editor.Load("test.wav", "test", ResourceType.WAV, sampleWavData);

            // Matching PyKotor implementation at Tools/HolocronToolset/tests/gui/editors/test_wav_editor.py:260-261
            // Original: assert wav_editor._audio_data == sample_wav_data
            // Original: assert "WAV" in wav_editor._detected_format
            editor.AudioData.Should().Equal(sampleWavData);
            editor.DetectedFormat.Should().Contain("WAV");
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/tests/gui/editors/test_wav_editor.py:114-117
        // Original: def test_detect_wav_format(self, wav_editor, sample_wav_data: bytes):
        [Fact]
        public void TestWavEditorDetectWavFormat()
        {
            // Create sample WAV data (matching Python fixture)
            int sampleRate = 8000;
            int numChannels = 1;
            int bitsPerSample = 8;
            int durationSeconds = 1;
            int numSamples = sampleRate * durationSeconds;
            byte[] audioData = new byte[numSamples];
            for (int i = 0; i < numSamples; i++)
            {
                audioData[i] = 128; // 128 is silence for 8-bit
            }

            int dataSize = audioData.Length;
            int fmtChunkSize = 16;
            int fileSize = 4 + (8 + fmtChunkSize) + (8 + dataSize);

            byte[] sampleWavData;
            using (var ms = new System.IO.MemoryStream())
            {
                // RIFF header
                ms.Write(System.Text.Encoding.ASCII.GetBytes("RIFF"), 0, 4);
                ms.Write(BitConverter.GetBytes(fileSize), 0, 4);
                ms.Write(System.Text.Encoding.ASCII.GetBytes("WAVE"), 0, 4);

                // fmt chunk
                ms.Write(System.Text.Encoding.ASCII.GetBytes("fmt "), 0, 4);
                ms.Write(BitConverter.GetBytes(fmtChunkSize), 0, 4);
                ms.Write(BitConverter.GetBytes((ushort)1), 0, 2); // Audio format (PCM)
                ms.Write(BitConverter.GetBytes((ushort)numChannels), 0, 2);
                ms.Write(BitConverter.GetBytes(sampleRate), 0, 4);
                ms.Write(BitConverter.GetBytes(sampleRate * numChannels * bitsPerSample / 8), 0, 4); // Byte rate
                ms.Write(BitConverter.GetBytes((ushort)(numChannels * bitsPerSample / 8)), 0, 2); // Block align
                ms.Write(BitConverter.GetBytes((ushort)bitsPerSample), 0, 2);

                // data chunk
                ms.Write(System.Text.Encoding.ASCII.GetBytes("data"), 0, 4);
                ms.Write(BitConverter.GetBytes(dataSize), 0, 4);
                ms.Write(audioData, 0, audioData.Length);

                sampleWavData = ms.ToArray();
            }

            // Matching PyKotor implementation at Tools/HolocronToolset/tests/gui/editors/test_wav_editor.py:116
            // Original: result = wav_editor.detect_audio_format(sample_wav_data)
            string result = WAVEditor.DetectAudioFormat(sampleWavData);

            // Matching PyKotor implementation at Tools/HolocronToolset/tests/gui/editors/test_wav_editor.py:117
            // Original: assert result == ".wav"
            result.Should().Be(".wav");
        }
    }
}
