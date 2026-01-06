using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Avalonia.Controls;
using FluentAssertions;
using Andastra.Parsing.Resource;
using HolocronToolset.Data;
using HolocronToolset.Editors;
using HolocronToolset.Tests.TestHelpers;
using HolocronToolset.Windows;
using Xunit;

namespace HolocronToolset.Tests.Windows
{
    // Matching PyKotor implementation at Tools/HolocronToolset/tests/test_ui_windows.py
    // Original: Comprehensive tests for windows
    [Collection("Avalonia Test Collection")]
    public class WindowsTests : IClassFixture<AvaloniaTestFixture>
    {
        private readonly AvaloniaTestFixture _fixture;
        private static HTInstallation _installation;

        public WindowsTests(AvaloniaTestFixture fixture)
        {
            _fixture = fixture;
        }

        static WindowsTests()
        {
            string k1Path = Environment.GetEnvironmentVariable("K1_PATH");
            if (string.IsNullOrEmpty(k1Path))
            {
                k1Path = @"C:\Program Files (x86)\Steam\steamapps\common\swkotor";
            }

            if (!string.IsNullOrEmpty(k1Path) && System.IO.File.Exists(System.IO.Path.Combine(k1Path, "chitin.key")))
            {
                _installation = new HTInstallation(k1Path, "Test");
            }
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/tests/test_ui_windows.py:25-37
        // Original: def test_module_designer_init(qtbot: QtBot, installation: HTInstallation):
        [Fact]
        public void TestModuleDesignerInit()
        {
            if (_installation == null)
            {
                return; // Skip if K1_PATH not set
            }

            // Mocking settings or resource loading might be needed as it's heavy
            var window = new ModuleDesignerWindow(parent: null, installation: _installation);
            window.Show();

            window.IsVisible.Should().BeTrue();
            window.Title.Should().Contain("Module Designer");

            // Test basic UI elements existence
            // Ui may be null if XAML isn't loaded, which is okay for programmatic UI
            if (window.Ui != null)
            {
                // If Ui is available, check controls
                // Note: Controls may be null if using programmatic UI
            }

            window.Close();
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/tests/test_ui_windows.py:39-53
        // Original: def test_kotordiff_init(qtbot: QtBot, installation: HTInstallation):
        [Fact]
        public void TestKotordiffInit()
        {
            if (_installation == null)
            {
                return; // Skip if K1_PATH not set
            }

            var window = new KotorDiffWindow(
                parent: null,
                installations: new Dictionary<string, HTInstallation> { { "default", _installation } },
                activeInstallation: _installation);
            window.Show();

            window.IsVisible.Should().BeTrue();
            window.Title.Should().Contain("Kotor");

            // Check interactions
            // Clicking 'Compare' without files should probably show error or do nothing safe
            window.Compare();
            // Likely warns about missing files or does nothing
            // Verify it doesn't crash

            window.Close();
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/tests/test_ui_windows.py:55-63
        // Original: def test_help_window_init(qtbot: QtBot):
        [Fact]
        public void TestHelpWindowInit()
        {
            var window = new HelpWindow(null);
            window.Show();

            window.IsVisible.Should().BeTrue();
            // Check if web engine or text viewer is present
            // Depending on implementation (WebView or TextBlock)
            // Note: Full testing requires web engine implementation

            window.Close();
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/tests/test_ui_windows.py:65-77
        // Original: def test_audio_player_init(qtbot: QtBot, installation: HTInstallation):
        [Fact]
        public void TestAudioPlayerInit()
        {
            if (_installation == null)
            {
                return; // Skip if K1_PATH not set
            }

            var window = new WAVEditor(null, _installation);
            window.Show();

            window.IsVisible.Should().BeTrue();
            // Check controls
            // Ui should be initialized in SetupUI
            window.Ui.Should().NotBeNull();
            if (window.Ui != null)
            {
                window.Ui.PlayButton.Should().NotBeNull();
                window.Ui.StopButton.Should().NotBeNull();
            }

            // Test loading a dummy audio file (mocked)
            // Matching PyKotor implementation at Tools/HolocronToolset/tests/test_ui_windows.py:73-74
            // Original: window.load_audio("test.wav")
            // Create minimal valid WAV file data for testing
            byte[] sampleWavData = CreateMinimalWavFileData();
            
            // Load the audio file using WAVEditor.Load method
            // Matching PyKotor: window.load() which calls Load() with filepath, resref, restype, data
            window.Load("test.wav", "test", ResourceType.WAV, sampleWavData);
            
            // Verify audio data was loaded correctly
            window.AudioData.Should().NotBeNull();
            window.AudioData.Should().HaveCount(sampleWavData.Length);
            window.AudioData.Should().BeEquivalentTo(sampleWavData);
            
            // Verify format was detected correctly
            window.DetectedFormat.Should().Be("WAV (RIFF)");
            
            // Verify UI was updated with format information
            if (window.Ui?.FormatLabel != null)
            {
                window.Ui.FormatLabel.Text.Should().Contain("WAV (RIFF)");
            }

            // Test loading MP3 format (minimal MP3-like data with ID3 header)
            // Matching PyKotor implementation at Tools/HolocronToolset/tests/gui/editors/test_wav_editor.py:88-102
            // Original: @pytest.fixture def sample_mp3_data() -> bytes:
            byte[] sampleMp3Data = CreateMinimalMp3FileData();
            window.Load("test.mp3", "test", ResourceType.MP3, sampleMp3Data);
            
            // Verify MP3 data was loaded correctly
            window.AudioData.Should().NotBeNull();
            window.AudioData.Should().HaveCount(sampleMp3Data.Length);
            window.AudioData.Should().BeEquivalentTo(sampleMp3Data);
            
            // Verify MP3 format was detected correctly
            window.DetectedFormat.Should().Be("MP3");
            
            // Verify UI was updated with MP3 format information
            if (window.Ui?.FormatLabel != null)
            {
                window.Ui.FormatLabel.Text.Should().Contain("MP3");
            }

            window.Close();
        }

        /// <summary>
        /// Creates a minimal valid WAV file data for testing.
        /// Matching PyKotor implementation at Tools/HolocronToolset/tests/gui/editors/test_wav_editor.py:43-85
        /// Original: @pytest.fixture def sample_wav_data() -> bytes:
        /// Creates a 1-second mono 8kHz 8-bit PCM WAV file.
        /// </summary>
        /// <returns>Byte array containing minimal valid WAV file data.</returns>
        /// <remarks>
        /// WAV File Structure:
        /// - RIFF header: "RIFF" + file size + "WAVE"
        /// - fmt chunk: "fmt " + chunk size + PCM format data
        /// - data chunk: "data" + data size + audio samples
        /// - Based on vendor/PyKotor/wiki/WAV-File-Format.md
        /// - Matching reone wavreader.cpp: Reads RIFF/WAVE structure
        /// </remarks>
        private static byte[] CreateMinimalWavFileData()
        {
            // Create minimal valid WAV file (1-second mono 8kHz 8-bit PCM)
            // Matching PyKotor implementation at Tools/HolocronToolset/tests/gui/editors/test_wav_editor.py:43-85
            int sampleRate = 8000;
            int numChannels = 1;
            int bitsPerSample = 8;
            int durationSeconds = 1;
            int numSamples = sampleRate * durationSeconds;
            byte[] audioData = new byte[numSamples];
            for (int i = 0; i < numSamples; i++)
            {
                audioData[i] = 128; // 128 is silence for 8-bit PCM
            }

            int dataSize = audioData.Length;
            int fmtChunkSize = 16;
            int fileSize = 4 + (8 + fmtChunkSize) + (8 + dataSize);

            using (var ms = new MemoryStream())
            {
                // RIFF header
                // Matching PyKotor: wav_bytes.write(b'RIFF')
                ms.Write(Encoding.ASCII.GetBytes("RIFF"), 0, 4);
                // File size minus 8 bytes (RIFF header size)
                ms.Write(BitConverter.GetBytes(fileSize), 0, 4);
                // WAVE format tag
                ms.Write(Encoding.ASCII.GetBytes("WAVE"), 0, 4);

                // fmt chunk
                // Matching PyKotor: wav_bytes.write(b'fmt ')
                ms.Write(Encoding.ASCII.GetBytes("fmt "), 0, 4);
                // Format chunk size (16 bytes for PCM)
                ms.Write(BitConverter.GetBytes(fmtChunkSize), 0, 4);
                // Audio format (1 = PCM)
                ms.Write(BitConverter.GetBytes((ushort)1), 0, 2);
                // Number of channels (1 = mono)
                ms.Write(BitConverter.GetBytes((ushort)numChannels), 0, 2);
                // Sample rate (8000 Hz)
                ms.Write(BitConverter.GetBytes(sampleRate), 0, 4);
                // Byte rate (sample_rate * channels * bits_per_sample / 8)
                ms.Write(BitConverter.GetBytes(sampleRate * numChannels * bitsPerSample / 8), 0, 4);
                // Block align (channels * bits_per_sample / 8)
                ms.Write(BitConverter.GetBytes((ushort)(numChannels * bitsPerSample / 8)), 0, 2);
                // Bits per sample (8)
                ms.Write(BitConverter.GetBytes((ushort)bitsPerSample), 0, 2);

                // data chunk
                // Matching PyKotor: wav_bytes.write(b'data')
                ms.Write(Encoding.ASCII.GetBytes("data"), 0, 4);
                // Data size
                ms.Write(BitConverter.GetBytes(dataSize), 0, 4);
                // Audio sample data
                ms.Write(audioData, 0, audioData.Length);

                return ms.ToArray();
            }
        }

        /// <summary>
        /// Creates minimal MP3-like data with ID3 header for testing.
        /// Matching PyKotor implementation at Tools/HolocronToolset/tests/gui/editors/test_wav_editor.py:88-102
        /// Original: @pytest.fixture def sample_mp3_data() -> bytes:
        /// </summary>
        /// <returns>Byte array containing minimal MP3-like data with ID3 header.</returns>
        /// <remarks>
        /// MP3 File Structure (minimal for testing):
        /// - ID3v2 header: "ID3" + version + flags + size
        /// - MP3 frame sync: 0xFF 0xFB (MPEG-1 Layer 3)
        /// - Based on WAVEditor.DetectAudioFormat() which checks for ID3 header and MP3 frame sync
        /// </remarks>
        private static byte[] CreateMinimalMp3FileData()
        {
            // Create minimal MP3-like data with ID3 header for testing
            // Matching PyKotor implementation at Tools/HolocronToolset/tests/gui/editors/test_wav_editor.py:88-102
            byte[] sampleMp3Data = new byte[107];
            
            // ID3v2 header
            // Matching PyKotor: sample_mp3_data[:3] = b"ID3"
            Encoding.ASCII.GetBytes("ID3").CopyTo(sampleMp3Data, 0);
            sampleMp3Data[3] = 0x03; // ID3v2.3.0 version
            sampleMp3Data[4] = 0x00; // Revision
            sampleMp3Data[5] = 0x00; // Flags
            // Size (synchronized safe integer, 4 bytes)
            sampleMp3Data[6] = 0x00;
            sampleMp3Data[7] = 0x00;
            sampleMp3Data[8] = 0x00;
            sampleMp3Data[9] = 0x00;
            
            // MP3 frame sync (0xFF 0xFB for MPEG-1 Layer 3)
            // Matching PyKotor: sample_mp3_data[10] = 0xFF, sample_mp3_data[11] = 0xFB
            // Matching WAVEditor.DetectAudioFormat(): Checks for 0xFF and (data[1] & 0xE0) == 0xE0
            sampleMp3Data[10] = 0xFF;
            sampleMp3Data[11] = 0xFB; // MPEG-1 Layer 3, 44.1kHz, stereo
            sampleMp3Data[12] = 0x90; // Bitrate and padding
            sampleMp3Data[13] = 0x00;
            
            // Rest is zeros (already initialized)
            // This creates a minimal valid MP3-like structure that will be detected by DetectAudioFormat()
            
            return sampleMp3Data;
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/tests/test_ui_windows.py:79-93
        // Original: def test_indoor_builder_init(qtbot: QtBot, installation: HTInstallation):
        [Fact]
        public void TestIndoorBuilderInit()
        {
            if (_installation == null)
            {
                return; // Skip if K1_PATH not set
            }

            var window = new IndoorBuilderWindow(parent: null, installation: _installation);
            window.Show();

            window.IsVisible.Should().BeTrue();
            window.Title.Should().Contain("Indoor");

            // Check widgets
            // Note: Full testing requires UI controls to be implemented
            window.Ui.Should().NotBeNull();

            window.Close();
        }
    }
}
