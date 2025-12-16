using System;
using System.IO;
using System.Text;
using AuroraEngine.Common.Resources;
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

        // Matching PyKotor implementation at Tools/HolocronToolset/tests/gui/editors/test_wav_editor.py:119-122
        // Original: def test_detect_mp3_format_id3(self, wav_editor, sample_mp3_data: bytes):
        [Fact]
        public void TestWavEditorDetectMp3FormatId3()
        {
            // Create minimal MP3-like data with ID3 header for testing (matching Python fixture)
            // Matching PyKotor implementation at Tools/HolocronToolset/tests/gui/editors/test_wav_editor.py:89-92
            // Original: @pytest.fixture def sample_mp3_data() -> bytes:
            byte[] sampleMp3Data = new byte[107];
            System.Text.Encoding.ASCII.GetBytes("ID3").CopyTo(sampleMp3Data, 0);
            sampleMp3Data[3] = 0x03;
            sampleMp3Data[4] = 0x00;
            sampleMp3Data[5] = 0x00;
            sampleMp3Data[6] = 0x00;
            sampleMp3Data[7] = 0x00;
            sampleMp3Data[8] = 0x00;
            sampleMp3Data[9] = 0x00;
            sampleMp3Data[10] = 0xFF;
            sampleMp3Data[11] = 0xFB;
            sampleMp3Data[12] = 0x90;
            sampleMp3Data[13] = 0x00;
            // Rest is zeros (already initialized)

            // Matching PyKotor implementation at Tools/HolocronToolset/tests/gui/editors/test_wav_editor.py:121
            // Original: result = wav_editor.detect_audio_format(sample_mp3_data)
            string result = WAVEditor.DetectAudioFormat(sampleMp3Data);

            // Matching PyKotor implementation at Tools/HolocronToolset/tests/gui/editors/test_wav_editor.py:122
            // Original: assert result == ".mp3"
            result.Should().Be(".mp3");
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/tests/gui/editors/test_wav_editor.py:124-129
        // Original: def test_detect_mp3_format_frame_sync(self, wav_editor):
        [Fact]
        public void TestWavEditorDetectMp3FormatFrameSync()
        {
            // Matching PyKotor implementation at Tools/HolocronToolset/tests/gui/editors/test_wav_editor.py:126-127
            // Original: data = b'\xff\xfb\x90\x00' + b'\x00' * 100
            byte[] data = new byte[104];
            data[0] = 0xFF;
            data[1] = 0xFB;
            data[2] = 0x90;
            data[3] = 0x00;
            // Rest is zeros (already initialized)

            // Matching PyKotor implementation at Tools/HolocronToolset/tests/gui/editors/test_wav_editor.py:128
            // Original: result = wav_editor.detect_audio_format(data)
            string result = WAVEditor.DetectAudioFormat(data);

            // Matching PyKotor implementation at Tools/HolocronToolset/tests/gui/editors/test_wav_editor.py:129
            // Original: assert result == ".mp3"
            result.Should().Be(".mp3");
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/tests/gui/editors/test_wav_editor.py:131-136
        // Original: def test_detect_mp3_format_lame(self, wav_editor):
        [Fact]
        public void TestWavEditorDetectMp3FormatLame()
        {
            // Matching PyKotor implementation at Tools/HolocronToolset/tests/gui/editors/test_wav_editor.py:133-134
            // Original: data = b"LAME" + b'\x00' * 100
            byte[] data = new byte[104];
            System.Text.Encoding.ASCII.GetBytes("LAME").CopyTo(data, 0);
            // Rest is zeros (already initialized)

            // Matching PyKotor implementation at Tools/HolocronToolset/tests/gui/editors/test_wav_editor.py:135
            // Original: result = wav_editor.detect_audio_format(data)
            string result = WAVEditor.DetectAudioFormat(data);

            // Matching PyKotor implementation at Tools/HolocronToolset/tests/gui/editors/test_wav_editor.py:136
            // Original: assert result == ".mp3"
            result.Should().Be(".mp3");
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/tests/gui/editors/test_wav_editor.py:137-140
        // Original: def test_detect_ogg_format(self, wav_editor, sample_ogg_data: bytes):
        [Fact]
        public void TestWavEditorDetectOggFormat()
        {
            // Create minimal OGG-like data for testing (matching Python fixture)
            // Matching PyKotor implementation at Tools/HolocronToolset/tests/gui/editors/test_wav_editor.py:96-98
            // Original: @pytest.fixture def sample_ogg_data() -> bytes:
            byte[] sampleOggData = new byte[110];
            System.Text.Encoding.ASCII.GetBytes("OggS").CopyTo(sampleOggData, 0);
            sampleOggData[4] = 0x00;
            sampleOggData[5] = 0x02;
            sampleOggData[6] = 0x00;
            sampleOggData[7] = 0x00;
            sampleOggData[8] = 0x00;
            sampleOggData[9] = 0x00;
            sampleOggData[10] = 0x00;
            sampleOggData[11] = 0x00;
            sampleOggData[12] = 0x00;
            sampleOggData[13] = 0x00;
            // Rest is zeros (already initialized)

            // Matching PyKotor implementation at Tools/HolocronToolset/tests/gui/editors/test_wav_editor.py:139
            // Original: result = wav_editor.detect_audio_format(sample_ogg_data)
            string result = WAVEditor.DetectAudioFormat(sampleOggData);

            // Matching PyKotor implementation at Tools/HolocronToolset/tests/gui/editors/test_wav_editor.py:140
            // Original: assert result == ".ogg"
            result.Should().Be(".ogg");
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/tests/gui/editors/test_wav_editor.py:142-145
        // Original: def test_detect_flac_format(self, wav_editor, sample_flac_data: bytes):
        [Fact]
        public void TestWavEditorDetectFlacFormat()
        {
            // Create minimal FLAC-like data for testing (matching Python fixture)
            // Matching PyKotor implementation at Tools/HolocronToolset/tests/gui/editors/test_wav_editor.py:100-102
            // Original: @pytest.fixture def sample_flac_data() -> bytes:
            byte[] sampleFlacData = new byte[104];
            System.Text.Encoding.ASCII.GetBytes("fLaC").CopyTo(sampleFlacData, 0);
            // Rest is zeros (already initialized)

            // Matching PyKotor implementation at Tools/HolocronToolset/tests/gui/editors/test_wav_editor.py:144
            // Original: result = wav_editor.detect_audio_format(sample_flac_data)
            string result = WAVEditor.DetectAudioFormat(sampleFlacData);

            // Matching PyKotor implementation at Tools/HolocronToolset/tests/gui/editors/test_wav_editor.py:145
            // Original: assert result == ".flac"
            result.Should().Be(".flac");
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/tests/gui/editors/test_wav_editor.py:147-151
        // Original: def test_detect_unknown_format_defaults_to_wav(self, wav_editor):
        [Fact]
        public void TestWavEditorDetectUnknownFormatDefaultsToWav()
        {
            // Matching PyKotor implementation at Tools/HolocronToolset/tests/gui/editors/test_wav_editor.py:149-150
            // Original: data = b'UNKN' + b'\x00' * 100
            byte[] data = new byte[104];
            System.Text.Encoding.ASCII.GetBytes("UNKN").CopyTo(data, 0);
            // Rest is zeros (already initialized)

            // Matching PyKotor implementation at Tools/HolocronToolset/tests/gui/editors/test_wav_editor.py:150
            // Original: result = wav_editor.detect_audio_format(data)
            string result = WAVEditor.DetectAudioFormat(data);

            // Matching PyKotor implementation at Tools/HolocronToolset/tests/gui/editors/test_wav_editor.py:151
            // Original: assert result == ".wav"
            result.Should().Be(".wav");
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/tests/gui/editors/test_wav_editor.py:153-156
        // Original: def test_detect_empty_data_defaults_to_wav(self, wav_editor):
        [Fact]
        public void TestWavEditorDetectEmptyDataDefaultsToWav()
        {
            // Matching PyKotor implementation at Tools/HolocronToolset/tests/gui/editors/test_wav_editor.py:155
            // Original: result = wav_editor.detect_audio_format(b'')
            string result = WAVEditor.DetectAudioFormat(new byte[0]);

            // Matching PyKotor implementation at Tools/HolocronToolset/tests/gui/editors/test_wav_editor.py:156
            // Original: assert result == ".wav"
            result.Should().Be(".wav");
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/tests/gui/editors/test_wav_editor.py:158-161
        // Original: def test_detect_short_data_defaults_to_wav(self, wav_editor):
        [Fact]
        public void TestWavEditorDetectShortDataDefaultsToWav()
        {
            // Matching PyKotor implementation at Tools/HolocronToolset/tests/gui/editors/test_wav_editor.py:160
            // Original: result = wav_editor.detect_audio_format(b'AB')
            byte[] data = new byte[] { (byte)'A', (byte)'B' };
            string result = WAVEditor.DetectAudioFormat(data);

            // Matching PyKotor implementation at Tools/HolocronToolset/tests/gui/editors/test_wav_editor.py:161
            // Original: assert result == ".wav"
            result.Should().Be(".wav");
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/tests/gui/editors/test_wav_editor.py:163-166
        // Original: def test_get_format_name_wav(self, wav_editor):
        [Fact]
        public void TestWavEditorGetFormatNameWav()
        {
            // Matching PyKotor implementation at Tools/HolocronToolset/tests/gui/editors/test_wav_editor.py:165
            // Original: result = wav_editor.get_format_name(".wav")
            string result = WAVEditor.GetFormatName(".wav");

            // Matching PyKotor implementation at Tools/HolocronToolset/tests/gui/editors/test_wav_editor.py:166
            // Original: assert result == "WAV (RIFF)"
            result.Should().Be("WAV (RIFF)");
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/tests/gui/editors/test_wav_editor.py:168-171
        // Original: def test_get_format_name_mp3(self, wav_editor):
        [Fact]
        public void TestWavEditorGetFormatNameMp3()
        {
            // Matching PyKotor implementation at Tools/HolocronToolset/tests/gui/editors/test_wav_editor.py:170
            // Original: result = wav_editor.get_format_name(".mp3")
            string result = WAVEditor.GetFormatName(".mp3");

            // Matching PyKotor implementation at Tools/HolocronToolset/tests/gui/editors/test_wav_editor.py:171
            // Original: assert result == "MP3"
            result.Should().Be("MP3");
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/tests/gui/editors/test_wav_editor.py:173-176
        // Original: def test_get_format_name_ogg(self, wav_editor):
        [Fact]
        public void TestWavEditorGetFormatNameOgg()
        {
            // Matching PyKotor implementation at Tools/HolocronToolset/tests/gui/editors/test_wav_editor.py:175
            // Original: result = wav_editor.get_format_name(".ogg")
            string result = WAVEditor.GetFormatName(".ogg");

            // Matching PyKotor implementation at Tools/HolocronToolset/tests/gui/editors/test_wav_editor.py:176
            // Original: assert result == "OGG Vorbis"
            result.Should().Be("OGG Vorbis");
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/tests/gui/editors/test_wav_editor.py:178-181
        // Original: def test_get_format_name_flac(self, wav_editor):
        [Fact]
        public void TestWavEditorGetFormatNameFlac()
        {
            // Matching PyKotor implementation at Tools/HolocronToolset/tests/gui/editors/test_wav_editor.py:180
            // Original: result = wav_editor.get_format_name(".flac")
            string result = WAVEditor.GetFormatName(".flac");

            // Matching PyKotor implementation at Tools/HolocronToolset/tests/gui/editors/test_wav_editor.py:181
            // Original: assert result == "FLAC"
            result.Should().Be("FLAC");
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/tests/gui/editors/test_wav_editor.py:183-186
        // Original: def test_get_format_name_unknown(self, wav_editor):
        [Fact]
        public void TestWavEditorGetFormatNameUnknown()
        {
            // Matching PyKotor implementation at Tools/HolocronToolset/tests/gui/editors/test_wav_editor.py:185
            // Original: result = wav_editor.get_format_name(".xyz")
            string result = WAVEditor.GetFormatName(".xyz");

            // Matching PyKotor implementation at Tools/HolocronToolset/tests/gui/editors/test_wav_editor.py:186
            // Original: assert result == "Unknown"
            result.Should().Be("Unknown");
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/tests/gui/editors/test_wav_editor.py:200-208
        // Original: def test_editor_has_ui_elements(self, wav_editor):
        [Fact]
        public void TestWavEditorHasUiElements()
        {
            var editor = new WAVEditor(null, null);

            // Matching PyKotor implementation at Tools/HolocronToolset/tests/gui/editors/test_wav_editor.py:202-208
            // Original: assert hasattr(wav_editor.ui, 'playButton')
            editor.Ui.Should().NotBeNull("UI should be initialized");
            editor.Ui.PlayButton.Should().NotBeNull("PlayButton should exist");
            editor.Ui.PauseButton.Should().NotBeNull("PauseButton should exist");
            editor.Ui.StopButton.Should().NotBeNull("StopButton should exist");
            editor.Ui.TimeSlider.Should().NotBeNull("TimeSlider should exist");
            editor.Ui.CurrentTimeLabel.Should().NotBeNull("CurrentTimeLabel should exist");
            editor.Ui.TotalTimeLabel.Should().NotBeNull("TotalTimeLabel should exist");
            editor.Ui.FormatLabel.Should().NotBeNull("FormatLabel should exist");
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/tests/gui/editors/test_wav_editor.py:223-228
        // Original: def test_editor_initial_state(self, wav_editor):
        [Fact]
        public void TestWavEditorInitialState()
        {
            var editor = new WAVEditor(null, null);

            // Matching PyKotor implementation at Tools/HolocronToolset/tests/gui/editors/test_wav_editor.py:225-228
            // Original: assert wav_editor.ui.currentTimeLabel.text() == "00:00:00"
            editor.Ui.CurrentTimeLabel.Text.Should().Be("00:00:00", "CurrentTimeLabel should be initialized to 00:00:00");
            editor.Ui.TotalTimeLabel.Text.Should().Be("00:00:00", "TotalTimeLabel should be initialized to 00:00:00");
            editor.Ui.FormatLabel.Text.Should().Be("Format: -", "FormatLabel should be initialized to 'Format: -'");
            editor.Ui.TimeSlider.Value.Should().Be(0, "TimeSlider should be initialized to 0");
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/tests/gui/editors/test_wav_editor.py:230-233
        // Original: def test_editor_window_title(self, wav_editor):
        [Fact]
        public void TestWavEditorWindowTitle()
        {
            var editor = new WAVEditor(null, null);

            // Matching PyKotor implementation at Tools/HolocronToolset/tests/gui/editors/test_wav_editor.py:232-233
            // Original: title = wav_editor.windowTitle()
            // Original: assert "Audio Editor" in title or "Audio" in title
            string title = editor.Title ?? "";
            (title.Contains("Audio Editor") || title.Contains("Audio")).Should().BeTrue($"Window title should contain 'Audio Editor' or 'Audio', but was '{title}'");
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/tests/gui/editors/test_wav_editor.py:243-254
        // Original: def test_new_resets_state(self, wav_editor, sample_wav_data: bytes):
        [Fact]
        public void TestWavEditorNewResetsState()
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

            var editor = new WAVEditor(null, null);

            // Matching PyKotor implementation at Tools/HolocronToolset/tests/gui/editors/test_wav_editor.py:248-250
            // Original: wav_editor.load(Path("test.wav"), "test", ResourceType.WAV, sample_wav_data)
            editor.Load("test.wav", "test", ResourceType.WAV, sampleWavData);

            // Matching PyKotor implementation at Tools/HolocronToolset/tests/gui/editors/test_wav_editor.py:252
            // Original: wav_editor.new()
            editor.New();

            // Matching PyKotor implementation at Tools/HolocronToolset/tests/gui/editors/test_wav_editor.py:252-254
            // Original: assert wav_editor._audio_data == b""
            // Original: assert wav_editor._detected_format == "Unknown"
            // Original: assert wav_editor.ui.formatLabel.text() == "Format: -"
            editor.AudioData.Should().BeEmpty("AudioData should be reset to empty");
            editor.DetectedFormat.Should().Be("Unknown", "DetectedFormat should be reset to 'Unknown'");
            editor.Ui.FormatLabel.Text.Should().Be("Format: -", "FormatLabel should be reset to 'Format: -'");
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/tests/gui/editors/test_wav_editor.py:270-275
        // Original: def test_load_updates_format_label(self, wav_editor, sample_wav_data: bytes):
        [Fact]
        public void TestWavEditorLoadUpdatesFormatLabel()
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

            var editor = new WAVEditor(null, null);

            // Matching PyKotor implementation at Tools/HolocronToolset/tests/gui/editors/test_wav_editor.py:272
            // Original: wav_editor.load(Path("test.wav"), "test", ResourceType.WAV, sample_wav_data)
            editor.Load("test.wav", "test", ResourceType.WAV, sampleWavData);

            // Matching PyKotor implementation at Tools/HolocronToolset/tests/gui/editors/test_wav_editor.py:274-275
            // Original: format_text = wav_editor.ui.formatLabel.text()
            // Original: assert "WAV" in format_text or "RIFF" in format_text
            string formatText = editor.Ui.FormatLabel.Text;
            (formatText.Contains("WAV") || formatText.Contains("RIFF")).Should().BeTrue($"Format label should contain 'WAV' or 'RIFF', but was '{formatText}'");
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/tests/gui/editors/test_wav_editor.py:277-283
        // Original: def test_build_returns_audio_data(self, wav_editor, sample_wav_data: bytes):
        [Fact]
        public void TestWavEditorBuildReturnsAudioData()
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

            var editor = new WAVEditor(null, null);

            // Matching PyKotor implementation at Tools/HolocronToolset/tests/gui/editors/test_wav_editor.py:279
            // Original: wav_editor.load(Path("test.wav"), "test", ResourceType.WAV, sample_wav_data)
            editor.Load("test.wav", "test", ResourceType.WAV, sampleWavData);

            // Matching PyKotor implementation at Tools/HolocronToolset/tests/gui/editors/test_wav_editor.py:281-283
            // Original: data, extra = wav_editor.build()
            // Original: assert data == sample_wav_data
            // Original: assert extra == b""
            var (data, extra) = editor.Build();
            data.Should().Equal(sampleWavData, "Build() should return the loaded audio data");
            extra.Should().BeEmpty("Build() should return empty extra data");
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/tests/gui/editors/test_wav_editor.py:285-290
        // Original: def test_build_empty_after_new(self, wav_editor):
        [Fact]
        public void TestWavEditorBuildEmptyAfterNew()
        {
            var editor = new WAVEditor(null, null);

            // Matching PyKotor implementation at Tools/HolocronToolset/tests/gui/editors/test_wav_editor.py:287
            // Original: wav_editor.new()
            editor.New();

            // Matching PyKotor implementation at Tools/HolocronToolset/tests/gui/editors/test_wav_editor.py:289-290
            // Original: data, extra = wav_editor.build()
            // Original: assert data == b""
            var (data, extra) = editor.Build();
            data.Should().BeEmpty("Build() should return empty data after new()");
            extra.Should().BeEmpty("Build() should return empty extra data");
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/tests/gui/editors/test_wav_editor.py:263-268
        // Original: def test_load_mp3_file(self, wav_editor, sample_mp3_data: bytes):
        [Fact]
        public void TestWavEditorLoadMp3File()
        {
            // Create minimal MP3-like data with ID3 header for testing (matching Python fixture)
            byte[] sampleMp3Data = new byte[107];
            System.Text.Encoding.ASCII.GetBytes("ID3").CopyTo(sampleMp3Data, 0);
            sampleMp3Data[3] = 0x03;
            sampleMp3Data[4] = 0x00;
            sampleMp3Data[5] = 0x00;
            sampleMp3Data[6] = 0x00;
            sampleMp3Data[7] = 0x00;
            sampleMp3Data[8] = 0x00;
            sampleMp3Data[9] = 0x00;
            sampleMp3Data[10] = 0xFF;
            sampleMp3Data[11] = 0xFB;
            sampleMp3Data[12] = 0x90;
            sampleMp3Data[13] = 0x00;
            // Rest is zeros (already initialized)

            var editor = new WAVEditor(null, null);

            // Matching PyKotor implementation at Tools/HolocronToolset/tests/gui/editors/test_wav_editor.py:265
            // Original: wav_editor.load(Path("test.mp3"), "test", ResourceType.MP3, sample_mp3_data)
            editor.Load("test.mp3", "test", ResourceType.MP3, sampleMp3Data);

            // Matching PyKotor implementation at Tools/HolocronToolset/tests/gui/editors/test_wav_editor.py:267-268
            // Original: assert wav_editor._audio_data == sample_mp3_data
            // Original: assert "MP3" in wav_editor._detected_format
            editor.AudioData.Should().Equal(sampleMp3Data, "AudioData should match loaded MP3 data");
            editor.DetectedFormat.Should().Contain("MP3", "DetectedFormat should contain 'MP3'");
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/tests/gui/editors/test_wav_editor.py:293-300
        // Original: def test_load_with_bytearray(self, wav_editor, sample_wav_data: bytes):
        [Fact]
        public void TestWavEditorLoadWithByteArray()
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

            var editor = new WAVEditor(null, null);

            // Matching PyKotor implementation at Tools/HolocronToolset/tests/gui/editors/test_wav_editor.py:295-296
            // Original: bytearray_data = bytearray(sample_wav_data)
            // Original: wav_editor.load(Path("test.wav"), "test", ResourceType.WAV, bytearray_data)
            // In C#, byte[] is already used, so we just pass the array directly (C# Load method accepts byte[])
            editor.Load("test.wav", "test", ResourceType.WAV, sampleWavData);

            // Matching PyKotor implementation at Tools/HolocronToolset/tests/gui/editors/test_wav_editor.py:299-300
            // Original: assert isinstance(wav_editor._audio_data, bytes)
            // Original: assert wav_editor._audio_data == sample_wav_data
            // In C#, AudioData is already byte[], so we just verify it matches
            editor.AudioData.Should().Equal(sampleWavData, "AudioData should match loaded WAV data");
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/tests/gui/editors/test_wav_editor.py:326-328
        // Original: def test_time_slider_exists(self, wav_editor):
        [Fact]
        public void TestWavEditorTimeSliderExists()
        {
            var editor = new WAVEditor(null, null);

            // Matching PyKotor implementation at Tools/HolocronToolset/tests/gui/editors/test_wav_editor.py:328
            // Original: assert wav_editor.ui.timeSlider is not None
            editor.Ui.TimeSlider.Should().NotBeNull("TimeSlider should exist");
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/tests/gui/editors/test_wav_editor.py:330-334
        // Original: def test_time_slider_initial_range(self, wav_editor):
        [Fact]
        public void TestWavEditorTimeSliderInitialRange()
        {
            var editor = new WAVEditor(null, null);

            // Matching PyKotor implementation at Tools/HolocronToolset/tests/gui/editors/test_wav_editor.py:332-334
            // Original: wav_editor.new()
            // Original: assert wav_editor.ui.timeSlider.minimum() == 0
            // Original: assert wav_editor.ui.timeSlider.maximum() == 0
            editor.New();
            editor.Ui.TimeSlider.Minimum.Should().Be(0, "TimeSlider minimum should be 0");
            editor.Ui.TimeSlider.Maximum.Should().Be(0, "TimeSlider maximum should be 0 after new()");
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/tests/gui/editors/test_wav_editor.py:336-342
        // Original: def test_on_duration_changed_updates_label(self, wav_editor):
        [Fact]
        public void TestWavEditorOnDurationChangedUpdatesLabel()
        {
            var editor = new WAVEditor(null, null);

            // Matching PyKotor implementation at Tools/HolocronToolset/tests/gui/editors/test_wav_editor.py:339
            // Original: wav_editor._on_duration_changed(65000)
            editor.OnDurationChanged(65000); // 1 minute 5 seconds

            // Matching PyKotor implementation at Tools/HolocronToolset/tests/gui/editors/test_wav_editor.py:341-342
            // Original: assert wav_editor.ui.totalTimeLabel.text() == "00:01:05"
            // Original: assert wav_editor.ui.timeSlider.maximum() == 65000
            editor.Ui.TotalTimeLabel.Text.Should().Be("00:01:05", "TotalTimeLabel should show 00:01:05 for 65000ms");
            editor.Ui.TimeSlider.Maximum.Should().Be(65000, "TimeSlider maximum should be 65000");
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/tests/gui/editors/test_wav_editor.py:344-352
        // Original: def test_on_position_changed_updates_label(self, wav_editor):
        [Fact]
        public void TestWavEditorOnPositionChangedUpdatesLabel()
        {
            var editor = new WAVEditor(null, null);

            // Matching PyKotor implementation at Tools/HolocronToolset/tests/gui/editors/test_wav_editor.py:347
            // Original: wav_editor._on_duration_changed(120000)  # 2 minutes
            editor.OnDurationChanged(120000); // 2 minutes

            // Matching PyKotor implementation at Tools/HolocronToolset/tests/gui/editors/test_wav_editor.py:350
            // Original: wav_editor._on_position_changed(65000)
            editor.OnPositionChanged(65000); // 1 minute 5 seconds

            // Matching PyKotor implementation at Tools/HolocronToolset/tests/gui/editors/test_wav_editor.py:352
            // Original: assert wav_editor.ui.currentTimeLabel.text() == "00:01:05"
            editor.Ui.CurrentTimeLabel.Text.Should().Be("00:01:05", "CurrentTimeLabel should show 00:01:05 for 65000ms");
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/tests/gui/editors/test_wav_editor.py:354-359
        // Original: def test_on_position_changed_updates_slider(self, wav_editor):
        [Fact]
        public void TestWavEditorOnPositionChangedUpdatesSlider()
        {
            var editor = new WAVEditor(null, null);

            // Matching PyKotor implementation at Tools/HolocronToolset/tests/gui/editors/test_wav_editor.py:356-357
            // Original: wav_editor._on_duration_changed(120000)
            // Original: wav_editor._on_position_changed(30000)
            editor.OnDurationChanged(120000); // 2 minutes
            editor.OnPositionChanged(30000); // 30 seconds

            // Matching PyKotor implementation at Tools/HolocronToolset/tests/gui/editors/test_wav_editor.py:359
            // Original: assert wav_editor.ui.timeSlider.value() == 30000
            editor.Ui.TimeSlider.Value.Should().Be(30000, "TimeSlider value should be 30000");
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/tests/gui/editors/test_wav_editor.py:369-374
        // Original: def test_format_label_shows_wav_format(self, wav_editor, sample_wav_data: bytes):
        [Fact]
        public void TestWavEditorFormatLabelShowsWavFormat()
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

            var editor = new WAVEditor(null, null);

            // Matching PyKotor implementation at Tools/HolocronToolset/tests/gui/editors/test_wav_editor.py:371
            // Original: wav_editor.load(Path("test.wav"), "test", ResourceType.WAV, sample_wav_data)
            editor.Load("test.wav", "test", ResourceType.WAV, sampleWavData);

            // Matching PyKotor implementation at Tools/HolocronToolset/tests/gui/editors/test_wav_editor.py:373-374
            // Original: format_text = wav_editor.ui.formatLabel.text()
            // Original: assert "WAV" in format_text or "RIFF" in format_text
            string formatText = editor.Ui.FormatLabel.Text;
            (formatText.Contains("WAV") || formatText.Contains("RIFF")).Should().BeTrue($"Format label should contain 'WAV' or 'RIFF', but was '{formatText}'");
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/tests/gui/editors/test_wav_editor.py:376-381
        // Original: def test_format_label_shows_mp3_format(self, wav_editor, sample_mp3_data: bytes):
        [Fact]
        public void TestWavEditorFormatLabelShowsMp3Format()
        {
            // Create minimal MP3-like data with ID3 header for testing (matching Python fixture)
            byte[] sampleMp3Data = new byte[107];
            System.Text.Encoding.ASCII.GetBytes("ID3").CopyTo(sampleMp3Data, 0);
            sampleMp3Data[3] = 0x03;
            sampleMp3Data[4] = 0x00;
            sampleMp3Data[5] = 0x00;
            sampleMp3Data[6] = 0x00;
            sampleMp3Data[7] = 0x00;
            sampleMp3Data[8] = 0x00;
            sampleMp3Data[9] = 0x00;
            sampleMp3Data[10] = 0xFF;
            sampleMp3Data[11] = 0xFB;
            sampleMp3Data[12] = 0x90;
            sampleMp3Data[13] = 0x00;
            // Rest is zeros (already initialized)

            var editor = new WAVEditor(null, null);

            // Matching PyKotor implementation at Tools/HolocronToolset/tests/gui/editors/test_wav_editor.py:378
            // Original: wav_editor.load(Path("test.mp3"), "test", ResourceType.MP3, sample_mp3_data)
            editor.Load("test.mp3", "test", ResourceType.MP3, sampleMp3Data);

            // Matching PyKotor implementation at Tools/HolocronToolset/tests/gui/editors/test_wav_editor.py:380-381
            // Original: format_text = wav_editor.ui.formatLabel.text()
            // Original: assert "MP3" in format_text
            string formatText = editor.Ui.FormatLabel.Text;
            formatText.Should().Contain("MP3", $"Format label should contain 'MP3', but was '{formatText}'");
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/tests/gui/editors/test_wav_editor.py:383-386
        // Original: def test_format_label_shows_dash_for_empty(self, wav_editor):
        [Fact]
        public void TestWavEditorFormatLabelShowsDashForEmpty()
        {
            var editor = new WAVEditor(null, null);

            // Matching PyKotor implementation at Tools/HolocronToolset/tests/gui/editors/test_wav_editor.py:385-386
            // Original: wav_editor.new()
            // Original: assert wav_editor.ui.formatLabel.text() == "Format: -"
            editor.New();
            editor.Ui.FormatLabel.Text.Should().Be("Format: -", "FormatLabel should show 'Format: -' when empty");
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/tests/gui/editors/test_wav_editor.py:388-404
        // Original: def test_time_labels_reset_on_new(self, wav_editor, sample_wav_data: bytes):
        [Fact]
        public void TestWavEditorTimeLabelsResetOnNew()
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

            var editor = new WAVEditor(null, null);

            // Matching PyKotor implementation at Tools/HolocronToolset/tests/gui/editors/test_wav_editor.py:391-397
            // Original: wav_editor.load(Path("test.wav"), "test", ResourceType.WAV, sample_wav_data)
            editor.Load("test.wav", "test", ResourceType.WAV, sampleWavData);

            // Set some duration and position to verify they reset
            editor.OnDurationChanged(120000); // 2 minutes
            editor.OnPositionChanged(65000); // 1 minute 5 seconds

            // Matching PyKotor implementation at Tools/HolocronToolset/tests/gui/editors/test_wav_editor.py:399
            // Original: wav_editor.new()
            editor.New();

            // Matching PyKotor implementation at Tools/HolocronToolset/tests/gui/editors/test_wav_editor.py:401-403
            // Original: assert wav_editor.ui.currentTimeLabel.text() == "00:00:00"
            // Original: assert wav_editor.ui.totalTimeLabel.text() == "00:00:00"
            editor.Ui.CurrentTimeLabel.Text.Should().Be("00:00:00", "CurrentTimeLabel should reset to 00:00:00");
            editor.Ui.TotalTimeLabel.Text.Should().Be("00:00:00", "TotalTimeLabel should reset to 00:00:00");
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/tests/gui/editors/test_wav_editor.py:406-408
        // Original: def test_temp_file_initially_none(self, wav_editor):
        [Fact]
        public void TestWavEditorTempFileInitiallyNone()
        {
            var editor = new WAVEditor(null, null);

            // Matching PyKotor implementation at Tools/HolocronToolset/tests/gui/editors/test_wav_editor.py:408
            // Original: assert wav_editor._temp_file is None
            editor.TempFile.Should().BeNull("TempFile should be None initially");
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/tests/gui/editors/test_wav_editor.py:410-414
        // Original: def test_cleanup_temp_file_when_none(self, wav_editor):
        [Fact]
        public void TestWavEditorCleanupTempFileWhenNone()
        {
            var editor = new WAVEditor(null, null);

            // Matching PyKotor implementation at Tools/HolocronToolset/tests/gui/editors/test_wav_editor.py:412-414
            // Original: wav_editor._temp_file = None
            // Original: wav_editor._cleanup_temp_file()
            // Original: assert wav_editor._temp_file is None
            // In C#, we can't directly set private fields, but CleanupTempFile should handle null gracefully
            editor.CleanupTempFile();
            editor.TempFile.Should().BeNull("TempFile should remain None after cleanup when already None");
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/tests/gui/editors/test_wav_editor.py:416-429
        // Original: def test_cleanup_temp_file_removes_file(self, wav_editor):
        [Fact]
        public void TestWavEditorCleanupTempFileRemovesFile()
        {
            // Create a real temp file
            string tempPath = System.IO.Path.GetTempFileName();
            System.IO.File.WriteAllBytes(tempPath, new byte[] { (byte)'t', (byte)'e', (byte)'s', (byte)'t' });

            try
            {
                var editor = new WAVEditor(null, null);

                // Set temp file using reflection (matching Python's direct attribute access)
                var tempFileField = typeof(WAVEditor).GetField("_tempFile", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                tempFileField.SetValue(editor, tempPath);

                // Matching PyKotor implementation at Tools/HolocronToolset/tests/gui/editors/test_wav_editor.py:424
                // Original: assert Path(temp_path).exists()
                System.IO.File.Exists(tempPath).Should().BeTrue("Temp file should exist before cleanup");

                // Matching PyKotor implementation at Tools/HolocronToolset/tests/gui/editors/test_wav_editor.py:426
                // Original: wav_editor._cleanup_temp_file()
                editor.CleanupTempFile();

                // Matching PyKotor implementation at Tools/HolocronToolset/tests/gui/editors/test_wav_editor.py:428-429
                // Original: assert wav_editor._temp_file is None
                // Original: assert not Path(temp_path).exists()
                editor.TempFile.Should().BeNull("TempFile should be None after cleanup");
                System.IO.File.Exists(tempPath).Should().BeFalse("Temp file should be deleted after cleanup");
            }
            finally
            {
                // Ensure cleanup even if test fails
                if (System.IO.File.Exists(tempPath))
                {
                    try
                    {
                        System.IO.File.Delete(tempPath);
                    }
                    catch
                    {
                        // Ignore cleanup errors
                    }
                }
            }
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/tests/gui/editors/test_wav_editor.py:431-441
        // Original: def test_new_cleans_up_temp_file(self, wav_editor, sample_wav_data: bytes):
        [Fact]
        public void TestWavEditorNewCleansUpTempFile()
        {
            // Create a temp file
            string tempPath = System.IO.Path.GetTempFileName();
            System.IO.File.WriteAllBytes(tempPath, new byte[] { (byte)'t', (byte)'e', (byte)'s', (byte)'t' });

            try
            {
                var editor = new WAVEditor(null, null);

                // Set temp file using reflection (matching Python's direct attribute access)
                var tempFileField = typeof(WAVEditor).GetField("_tempFile", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                tempFileField.SetValue(editor, tempPath);

                // Matching PyKotor implementation at Tools/HolocronToolset/tests/gui/editors/test_wav_editor.py:439
                // Original: wav_editor.new()
                editor.New();

                // Matching PyKotor implementation at Tools/HolocronToolset/tests/gui/editors/test_wav_editor.py:441
                // Original: assert not Path(temp_path).exists()
                System.IO.File.Exists(tempPath).Should().BeFalse("Temp file should be deleted after new()");
            }
            finally
            {
                // Ensure cleanup even if test fails
                if (System.IO.File.Exists(tempPath))
                {
                    try
                    {
                        System.IO.File.Delete(tempPath);
                    }
                    catch
                    {
                        // Ignore cleanup errors
                    }
                }
            }
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/tests/gui/editors/test_wav_editor.py:451-457
        // Original: def test_wav_roundtrip_preserves_data(self, wav_editor, sample_wav_data: bytes):
        [Fact]
        public void TestWavEditorWavRoundtripPreservesData()
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

            var editor = new WAVEditor(null, null);

            // Matching PyKotor implementation at Tools/HolocronToolset/tests/gui/editors/test_wav_editor.py:453
            // Original: wav_editor.load(Path("test.wav"), "test", ResourceType.WAV, sample_wav_data)
            editor.Load("test.wav", "test", ResourceType.WAV, sampleWavData);

            // Matching PyKotor implementation at Tools/HolocronToolset/tests/gui/editors/test_wav_editor.py:455-457
            // Original: saved_data, _ = wav_editor.build()
            // Original: assert saved_data == sample_wav_data
            var (savedData, _) = editor.Build();
            savedData.Should().Equal(sampleWavData, "Roundtrip should preserve WAV data exactly");
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/tests/gui/editors/test_wav_editor.py:459-465
        // Original: def test_mp3_roundtrip_preserves_data(self, wav_editor, sample_mp3_data: bytes):
        [Fact]
        public void TestWavEditorMp3RoundtripPreservesData()
        {
            // Create minimal MP3-like data with ID3 header for testing (matching Python fixture)
            byte[] sampleMp3Data = new byte[107];
            System.Text.Encoding.ASCII.GetBytes("ID3").CopyTo(sampleMp3Data, 0);
            sampleMp3Data[3] = 0x03;
            sampleMp3Data[4] = 0x00;
            sampleMp3Data[5] = 0x00;
            sampleMp3Data[6] = 0x00;
            sampleMp3Data[7] = 0x00;
            sampleMp3Data[8] = 0x00;
            sampleMp3Data[9] = 0x00;
            sampleMp3Data[10] = 0xFF;
            sampleMp3Data[11] = 0xFB;
            sampleMp3Data[12] = 0x90;
            sampleMp3Data[13] = 0x00;
            // Rest is zeros (already initialized)

            var editor = new WAVEditor(null, null);

            // Matching PyKotor implementation at Tools/HolocronToolset/tests/gui/editors/test_wav_editor.py:461
            // Original: wav_editor.load(Path("test.mp3"), "test", ResourceType.MP3, sample_mp3_data)
            editor.Load("test.mp3", "test", ResourceType.MP3, sampleMp3Data);

            // Matching PyKotor implementation at Tools/HolocronToolset/tests/gui/editors/test_wav_editor.py:463-465
            // Original: saved_data, _ = wav_editor.build()
            // Original: assert saved_data == sample_mp3_data
            var (savedData, _) = editor.Build();
            savedData.Should().Equal(sampleMp3Data, "Roundtrip should preserve MP3 data exactly");
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/tests/gui/editors/test_wav_editor.py:467-480
        // Original: def test_multiple_load_save_cycles(self, wav_editor, sample_wav_data: bytes, qtbot):
        [Fact]
        public void TestWavEditorMultipleLoadSaveCycles()
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

            var editor = new WAVEditor(null, null);
            byte[] originalData = sampleWavData;

            // Matching PyKotor implementation at Tools/HolocronToolset/tests/gui/editors/test_wav_editor.py:472-476
            // Original: for i in range(3):
            // Original:     wav_editor.load(Path(f"test_{i}.wav"), f"test_{i}", ResourceType.WAV, original_data)
            // Original:     qtbot.wait(50)  # Small delay to let Qt process events
            // Original:     saved_data, _ = wav_editor.build()
            // Original:     assert saved_data == original_data, f"Cycle {i} failed"
            for (int i = 0; i < 3; i++)
            {
                editor.Load($"test_{i}.wav", $"test_{i}", ResourceType.WAV, originalData);
                var (savedData, _) = editor.Build();
                savedData.Should().Equal(originalData, $"Cycle {i} should preserve data");
            }
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/tests/gui/editors/test_wav_editor.py:482-494
        // Original: def test_load_different_formats_sequentially(self, wav_editor, sample_wav_data: bytes, sample_mp3_data: bytes):
        [Fact]
        public void TestWavEditorLoadDifferentFormatsSequentially()
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

            // Create minimal MP3-like data with ID3 header for testing (matching Python fixture)
            byte[] sampleMp3Data = new byte[107];
            System.Text.Encoding.ASCII.GetBytes("ID3").CopyTo(sampleMp3Data, 0);
            sampleMp3Data[3] = 0x03;
            sampleMp3Data[4] = 0x00;
            sampleMp3Data[5] = 0x00;
            sampleMp3Data[6] = 0x00;
            sampleMp3Data[7] = 0x00;
            sampleMp3Data[8] = 0x00;
            sampleMp3Data[9] = 0x00;
            sampleMp3Data[10] = 0xFF;
            sampleMp3Data[11] = 0xFB;
            sampleMp3Data[12] = 0x90;
            sampleMp3Data[13] = 0x00;
            // Rest is zeros (already initialized)

            var editor = new WAVEditor(null, null);

            // Matching PyKotor implementation at Tools/HolocronToolset/tests/gui/editors/test_wav_editor.py:487-489
            // Original: wav_editor.load(Path("test.wav"), "test", ResourceType.WAV, sample_wav_data)
            // Original: data, _ = wav_editor.build()
            // Original: assert data == sample_wav_data
            editor.Load("test.wav", "test", ResourceType.WAV, sampleWavData);
            var (data1, _) = editor.Build();
            data1.Should().Equal(sampleWavData, "WAV data should be preserved");

            // Matching PyKotor implementation at Tools/HolocronToolset/tests/gui/editors/test_wav_editor.py:492-494
            // Original: wav_editor.load(Path("test.mp3"), "test", ResourceType.MP3, sample_mp3_data)
            // Original: data, _ = wav_editor.build()
            // Original: assert data == sample_mp3_data
            editor.Load("test.mp3", "test", ResourceType.MP3, sampleMp3Data);
            var (data2, _) = editor.Build();
            data2.Should().Equal(sampleMp3Data, "MP3 data should be preserved");
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/tests/gui/editors/test_wav_editor.py:504-509
        // Original: def test_load_empty_data(self, wav_editor):
        [Fact]
        public void TestWavEditorLoadEmptyData()
        {
            var editor = new WAVEditor(null, null);

            // Matching PyKotor implementation at Tools/HolocronToolset/tests/gui/editors/test_wav_editor.py:506
            // Original: wav_editor.load(Path("empty.wav"), "empty", ResourceType.WAV, b"")
            editor.Load("empty.wav", "empty", ResourceType.WAV, new byte[0]);

            // Matching PyKotor implementation at Tools/HolocronToolset/tests/gui/editors/test_wav_editor.py:508-509
            // Original: data, _ = wav_editor.build()
            // Original: assert data == b""
            var (data, _) = editor.Build();
            data.Should().BeEmpty("Build() should return empty data for empty input");
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/tests/gui/editors/test_wav_editor.py:511-517
        // Original: def test_load_very_small_data(self, wav_editor):
        [Fact]
        public void TestWavEditorLoadVerySmallData()
        {
            var editor = new WAVEditor(null, null);

            // Matching PyKotor implementation at Tools/HolocronToolset/tests/gui/editors/test_wav_editor.py:513-514
            // Original: small_data = b"AB"
            // Original: wav_editor.load(Path("small.wav"), "small", ResourceType.WAV, small_data)
            byte[] smallData = new byte[] { (byte)'A', (byte)'B' };
            editor.Load("small.wav", "small", ResourceType.WAV, smallData);

            // Matching PyKotor implementation at Tools/HolocronToolset/tests/gui/editors/test_wav_editor.py:516-517
            // Original: data, _ = wav_editor.build()
            // Original: assert data == small_data
            var (data, _) = editor.Build();
            data.Should().Equal(smallData, "Build() should return the small data");
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/tests/gui/editors/test_wav_editor.py:519-527
        // Original: def test_load_corrupted_wav_header(self, wav_editor):
        [Fact]
        public void TestWavEditorLoadCorruptedWavHeader()
        {
            var editor = new WAVEditor(null, null);

            // Matching PyKotor implementation at Tools/HolocronToolset/tests/gui/editors/test_wav_editor.py:522
            // Original: corrupted = b"RIFF\x00\x00\x00\x00WAVE" + b"\x00" * 100
            byte[] corrupted = new byte[108];
            System.Text.Encoding.ASCII.GetBytes("RIFF").CopyTo(corrupted, 0);
            corrupted[4] = 0x00;
            corrupted[5] = 0x00;
            corrupted[6] = 0x00;
            corrupted[7] = 0x00;
            System.Text.Encoding.ASCII.GetBytes("WAVE").CopyTo(corrupted, 8);
            // Rest is zeros (already initialized)

            // Matching PyKotor implementation at Tools/HolocronToolset/tests/gui/editors/test_wav_editor.py:523
            // Original: wav_editor.load(Path("corrupt.wav"), "corrupt", ResourceType.WAV, corrupted)
            editor.Load("corrupt.wav", "corrupt", ResourceType.WAV, corrupted);

            // Matching PyKotor implementation at Tools/HolocronToolset/tests/gui/editors/test_wav_editor.py:526-527
            // Original: data, _ = wav_editor.build()
            // Original: assert data == corrupted
            var (data, _) = editor.Build();
            data.Should().Equal(corrupted, "Build() should return the corrupted data as-is");
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/tests/gui/editors/test_wav_editor.py:529-536
        // Original: def test_position_exceeds_duration(self, wav_editor):
        [Fact]
        public void TestWavEditorPositionExceedsDuration()
        {
            var editor = new WAVEditor(null, null);

            // Matching PyKotor implementation at Tools/HolocronToolset/tests/gui/editors/test_wav_editor.py:531
            // Original: wav_editor._on_duration_changed(60000)  # 1 minute
            editor.OnDurationChanged(60000); // 1 minute

            // Matching PyKotor implementation at Tools/HolocronToolset/tests/gui/editors/test_wav_editor.py:534
            // Original: wav_editor._on_position_changed(90000)
            // Position exceeds duration - should update duration (matching Python behavior)
            editor.OnPositionChanged(90000);

            // Matching PyKotor implementation at Tools/HolocronToolset/tests/gui/editors/test_wav_editor.py:536
            // Original: assert wav_editor.ui.timeSlider.maximum() >= 90000
            editor.Ui.TimeSlider.Maximum.Should().BeGreaterThanOrEqualTo(90000, "TimeSlider maximum should be updated when position exceeds duration");
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/tests/gui/editors/test_wav_editor.py:538-543
        // Original: def test_zero_duration(self, wav_editor):
        [Fact]
        public void TestWavEditorZeroDuration()
        {
            var editor = new WAVEditor(null, null);

            // Matching PyKotor implementation at Tools/HolocronToolset/tests/gui/editors/test_wav_editor.py:540
            // Original: wav_editor._on_duration_changed(0)
            editor.OnDurationChanged(0);

            // Matching PyKotor implementation at Tools/HolocronToolset/tests/gui/editors/test_wav_editor.py:542-543
            // Original: assert wav_editor.ui.totalTimeLabel.text() == "00:00:00"
            // Original: assert wav_editor.ui.timeSlider.maximum() == 0
            editor.Ui.TotalTimeLabel.Text.Should().Be("00:00:00", "TotalTimeLabel should show 00:00:00 for zero duration");
            editor.Ui.TimeSlider.Maximum.Should().Be(0, "TimeSlider maximum should be 0 for zero duration");
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/tests/gui/editors/test_wav_editor.py:545-552
        // Original: def test_very_long_duration(self, wav_editor):
        [Fact]
        public void TestWavEditorVeryLongDuration()
        {
            var editor = new WAVEditor(null, null);

            // Matching PyKotor implementation at Tools/HolocronToolset/tests/gui/editors/test_wav_editor.py:548
            // Original: wav_editor._on_duration_changed(12345000)  # 3 hours, 25 minutes, 45 seconds
            editor.OnDurationChanged(12345000); // 3 hours, 25 minutes, 45 seconds

            // Matching PyKotor implementation at Tools/HolocronToolset/tests/gui/editors/test_wav_editor.py:551-552
            // Original: total_time = wav_editor.ui.totalTimeLabel.text()
            // Original: assert "03:25:45" in total_time
            string totalTime = editor.Ui.TotalTimeLabel.Text;
            totalTime.Should().Contain("03:25:45", $"TotalTimeLabel should contain '03:25:45' for 12345000ms, but was '{totalTime}'");
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/tests/gui/editors/test_wav_editor.py:562-576
        // Original: def test_close_event_cleans_up(self, wav_editor, sample_wav_data: bytes):
        [Fact]
        public void TestWavEditorCloseEventCleansUp()
        {
            // Create a temp file
            string tempPath = System.IO.Path.GetTempFileName();
            System.IO.File.WriteAllBytes(tempPath, new byte[] { (byte)'t', (byte)'e', (byte)'s', (byte)'t' });

            try
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

                var editor = new WAVEditor(null, null);

                // Set temp file using reflection (matching Python's direct attribute access)
                var tempFileField = typeof(WAVEditor).GetField("_tempFile", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                tempFileField.SetValue(editor, tempPath);

                // Matching PyKotor implementation at Tools/HolocronToolset/tests/gui/editors/test_wav_editor.py:570
                // Original: wav_editor.load(Path("test.wav"), "test", ResourceType.WAV, sample_wav_data)
                editor.Load("test.wav", "test", ResourceType.WAV, sampleWavData);

                // Matching PyKotor implementation at Tools/HolocronToolset/tests/gui/editors/test_wav_editor.py:573
                // Original: wav_editor.close()
                editor.Close();

                // Matching PyKotor implementation at Tools/HolocronToolset/tests/gui/editors/test_wav_editor.py:576
                // Original: assert not Path(temp_path).exists() or wav_editor._temp_file is None
                bool tempFileExists = System.IO.File.Exists(tempPath);
                bool tempFileIsNull = editor.TempFile == null;
                (tempFileExists == false || tempFileIsNull).Should().BeTrue("Temp file should be cleaned up or TempFile should be None after close");
            }
            finally
            {
                // Ensure cleanup even if test fails
                if (System.IO.File.Exists(tempPath))
                {
                    try
                    {
                        System.IO.File.Delete(tempPath);
                    }
                    catch
                    {
                        // Ignore cleanup errors
                    }
                }
            }
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/tests/gui/editors/test_wav_editor.py:586-604
        // Original: def test_full_workflow_new_to_save(self, wav_editor, sample_wav_data: bytes, tmp_path: Path):
        [Fact]
        public void TestWavEditorFullWorkflowNewToSave()
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

            var editor = new WAVEditor(null, null);

            // Matching PyKotor implementation at Tools/HolocronToolset/tests/gui/editors/test_wav_editor.py:589
            // Original: wav_editor.new()
            editor.New();

            // Matching PyKotor implementation at Tools/HolocronToolset/tests/gui/editors/test_wav_editor.py:592
            // Original: wav_editor.load(tmp_path / "test.wav", "test", ResourceType.WAV, sample_wav_data)
            string testPath = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "test.wav");
            editor.Load(testPath, "test", ResourceType.WAV, sampleWavData);

            // Matching PyKotor implementation at Tools/HolocronToolset/tests/gui/editors/test_wav_editor.py:595
            // Original: assert wav_editor._audio_data == sample_wav_data
            editor.AudioData.Should().Equal(sampleWavData, "Audio data should match loaded data");

            // Matching PyKotor implementation at Tools/HolocronToolset/tests/gui/editors/test_wav_editor.py:598-599
            // Original: data, _ = wav_editor.build()
            // Original: assert data == sample_wav_data
            var (data, _) = editor.Build();
            data.Should().Equal(sampleWavData, "Build() should return the loaded data");

            // Matching PyKotor implementation at Tools/HolocronToolset/tests/gui/editors/test_wav_editor.py:602-604
            // Original: output_path.write_bytes(data)
            // Original: assert output_path.read_bytes() == sample_wav_data
            string outputPath = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "output.wav");
            try
            {
                System.IO.File.WriteAllBytes(outputPath, data);
                byte[] savedData = System.IO.File.ReadAllBytes(outputPath);
                savedData.Should().Equal(sampleWavData, "Saved file should match original data");
            }
            finally
            {
                if (System.IO.File.Exists(outputPath))
                {
                    try
                    {
                        System.IO.File.Delete(outputPath);
                    }
                    catch
                    {
                        // Ignore cleanup errors
                    }
                }
            }
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/tests/gui/editors/test_wav_editor.py:606-620
        // Original: def test_format_switching(self, wav_editor, sample_wav_data: bytes, sample_mp3_data: bytes):
        [Fact]
        public void TestWavEditorFormatSwitching()
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

            // Create minimal MP3-like data with ID3 header for testing (matching Python fixture)
            byte[] sampleMp3Data = new byte[107];
            System.Text.Encoding.ASCII.GetBytes("ID3").CopyTo(sampleMp3Data, 0);
            sampleMp3Data[3] = 0x03;
            sampleMp3Data[4] = 0x00;
            sampleMp3Data[5] = 0x00;
            sampleMp3Data[6] = 0x00;
            sampleMp3Data[7] = 0x00;
            sampleMp3Data[8] = 0x00;
            sampleMp3Data[9] = 0x00;
            sampleMp3Data[10] = 0xFF;
            sampleMp3Data[11] = 0xFB;
            sampleMp3Data[12] = 0x90;
            sampleMp3Data[13] = 0x00;
            // Rest is zeros (already initialized)

            var editor = new WAVEditor(null, null);

            // Matching PyKotor implementation at Tools/HolocronToolset/tests/gui/editors/test_wav_editor.py:611-612
            // Original: wav_editor.load(Path("test.wav"), "test", ResourceType.WAV, sample_wav_data)
            // Original: assert "WAV" in wav_editor._detected_format
            editor.Load("test.wav", "test", ResourceType.WAV, sampleWavData);
            editor.DetectedFormat.Should().Contain("WAV", "Detected format should contain 'WAV'");

            // Matching PyKotor implementation at Tools/HolocronToolset/tests/gui/editors/test_wav_editor.py:615-616
            // Original: wav_editor.load(Path("test.mp3"), "test", ResourceType.MP3, sample_mp3_data)
            // Original: assert "MP3" in wav_editor._detected_format
            editor.Load("test.mp3", "test", ResourceType.MP3, sampleMp3Data);
            editor.DetectedFormat.Should().Contain("MP3", "Detected format should contain 'MP3'");

            // Matching PyKotor implementation at Tools/HolocronToolset/tests/gui/editors/test_wav_editor.py:619-620
            // Original: wav_editor.new()
            // Original: assert wav_editor._detected_format == "Unknown"
            editor.New();
            editor.DetectedFormat.Should().Be("Unknown", "Detected format should be 'Unknown' after new()");
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/tests/gui/editors/test_wav_editor.py:630-633
        // Original: def test_inherits_from_editor(self, wav_editor):
        [Fact]
        public void TestWavEditorInheritsFromEditor()
        {
            var editor = new WAVEditor(null, null);

            // Matching PyKotor implementation at Tools/HolocronToolset/tests/gui/editors/test_wav_editor.py:632-633
            // Original: from toolset.gui.editor import Editor
            // Original: assert isinstance(wav_editor, Editor)
            editor.Should().BeAssignableTo<Editor>("WAVEditor should inherit from Editor");
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/tests/gui/editors/test_wav_editor.py:635-638
        // Original: def test_has_media_player_widget(self, wav_editor):
        [Fact]
        public void TestWavEditorHasMediaPlayerWidget()
        {
            var editor = new WAVEditor(null, null);

            // Matching PyKotor implementation at Tools/HolocronToolset/tests/gui/editors/test_wav_editor.py:638
            // Original: assert hasattr(wav_editor, 'mediaPlayer')
            // In C#, we check if the field or property exists via reflection
            // The Python test checks for the attribute, but in C# the implementation may differ
            // We verify the editor has playback functionality by checking for UI controls
            editor.Ui.Should().NotBeNull("WAVEditor should have UI");
            editor.Ui.PlayButton.Should().NotBeNull("WAVEditor should have PlayButton for playback");
            editor.Ui.PauseButton.Should().NotBeNull("WAVEditor should have PauseButton for playback");
            editor.Ui.StopButton.Should().NotBeNull("WAVEditor should have StopButton for playback");
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/tests/gui/editors/test_wav_editor.py:640-642
        // Original: def test_has_installation_attribute(self, wav_editor):
        [Fact]
        public void TestWavEditorHasInstallationAttribute()
        {
            var editor = new WAVEditor(null, null);

            // Matching PyKotor implementation at Tools/HolocronToolset/tests/gui/editors/test_wav_editor.py:642
            // Original: assert hasattr(wav_editor, '_installation')
            // In C#, we check if the field exists via reflection
            var installationField = typeof(WAVEditor).GetField("_installation", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            installationField.Should().NotBeNull("WAVEditor should have _installation field");
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/tests/gui/editors/test_wav_editor.py:644-655
        // Original: def test_supported_types_set_correctly(self, wav_editor):
        [Fact]
        public void TestWavEditorSupportedTypesSetCorrectly()
        {
            var editor = new WAVEditor(null, null);

            // Matching PyKotor implementation at Tools/HolocronToolset/tests/gui/editors/test_wav_editor.py:648-651
            // Original: read_supported = getattr(wav_editor, '_readSupported', None)
            // Original: if read_supported is not None:
            // Original:     assert ResourceType.WAV in read_supported
            // Original:     assert ResourceType.MP3 in read_supported
            // In C#, we check if the field exists via reflection
            var readSupportedField = typeof(WAVEditor).GetField("_readSupported", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (readSupportedField != null)
            {
                var readSupported = readSupportedField.GetValue(editor) as System.Collections.Generic.HashSet<ResourceType>;
                if (readSupported != null)
                {
                    readSupported.Should().Contain(ResourceType.WAV, "WAVEditor should support WAV resource type");
                    readSupported.Should().Contain(ResourceType.MP3, "WAVEditor should support MP3 resource type");
                }
            }
            else
            {
                // If attribute doesn't exist, verify by attempting to load (matching Python behavior)
                // This tests the functionality rather than internal structure
                editor.Load("test.wav", "test", ResourceType.WAV, new byte[] { (byte)'R', (byte)'I', (byte)'F', (byte)'F' });
                editor.Load("test.mp3", "test", ResourceType.MP3, new byte[] { (byte)'I', (byte)'D', (byte)'3' });
            }
        }
    }
}
