using System;
using System.IO;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Markup.Xaml;
using BioWare.NET.Resource;
using HolocronToolset.Data;
using Avalonia.Controls.Primitives;

namespace HolocronToolset.Editors
{
    // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/wav.py:42
    // Original: class WAVEditor(Editor):
    public class WAVEditor : Editor
    {
        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/wav.py:74-76
        // Original: self._audio_data: bytes = b""
        // Original: self._temp_file: str | None = None
        // Original: self._detected_format: str = "Unknown"
        private byte[] _audioData = new byte[0];
        private string _tempFile = null;
        private string _detectedFormat = "Unknown";

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/wav.py
        // Original: self.ui = Ui_Editor() - UI wrapper class exposing all controls
        public WAVEditorUi Ui { get; private set; }

        // Public properties for testing (matching Python's public attributes)
        public byte[] AudioData => _audioData;
        public string DetectedFormat => _detectedFormat;
        public string TempFile => _tempFile;

        public WAVEditor(Window parent = null, HTInstallation installation = null)
            : base(parent, "Audio Editor", "audio",
                new[] { ResourceType.WAV, ResourceType.MP3 },
                new[] { ResourceType.WAV, ResourceType.MP3 },
                installation)
        {
            InitializeComponent();
            SetupUI();
            MinWidth = 350;
            MinHeight = 150;
            New();
        }

        private void InitializeComponent()
        {
            bool xamlLoaded = false;
            try
            {
                AvaloniaXamlLoader.Load(this);
                xamlLoaded = true;
            }
            catch
            {
                // XAML not available - will use programmatic UI
            }

            if (!xamlLoaded)
            {
                SetupUI();
            }
        }

        private void SetupUI()
        {
            var panel = new StackPanel { Orientation = Orientation.Vertical };

            // Create play/pause/stop buttons
            var buttonPanel = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 5 };
            var playButton = new Button { Content = "Play" };
            var pauseButton = new Button { Content = "Pause" };
            var stopButton = new Button { Content = "Stop" };
            buttonPanel.Children.Add(playButton);
            buttonPanel.Children.Add(pauseButton);
            buttonPanel.Children.Add(stopButton);
            panel.Children.Add(buttonPanel);

            // Time slider
            var timeSlider = new Slider { Minimum = 0, Maximum = 0, Value = 0 };
            panel.Children.Add(timeSlider);

            // Time labels
            var timeLabelPanel = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 10 };
            var currentTimeLabel = new TextBlock { Text = "00:00:00" };
            var totalTimeLabel = new TextBlock { Text = "00:00:00" };
            timeLabelPanel.Children.Add(currentTimeLabel);
            timeLabelPanel.Children.Add(totalTimeLabel);
            panel.Children.Add(timeLabelPanel);

            // Format label
            var formatLabel = new TextBlock { Text = "Format: -" };
            panel.Children.Add(formatLabel);

            Content = panel;

            // Find controls from XAML if available
            try
            {
                playButton = this.FindControl<Button>("playButton") ?? playButton;
                pauseButton = this.FindControl<Button>("pauseButton") ?? pauseButton;
                stopButton = this.FindControl<Button>("stopButton") ?? stopButton;
                timeSlider = this.FindControl<Slider>("timeSlider") ?? timeSlider;
                currentTimeLabel = this.FindControl<TextBlock>("currentTimeLabel") ?? currentTimeLabel;
                totalTimeLabel = this.FindControl<TextBlock>("totalTimeLabel") ?? totalTimeLabel;
                formatLabel = this.FindControl<TextBlock>("formatLabel") ?? formatLabel;
            }
            catch
            {
                // XAML not loaded - use programmatic UI
            }

            // Create UI wrapper for testing
            Ui = new WAVEditorUi
            {
                PlayButton = playButton,
                PauseButton = pauseButton,
                StopButton = stopButton,
                TimeSlider = timeSlider,
                CurrentTimeLabel = currentTimeLabel,
                TotalTimeLabel = totalTimeLabel,
                FormatLabel = formatLabel
            };
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/wav.py:145-187
        // Original: @staticmethod def detect_audio_format(data: bytes | bytearray) -> str:
        public static string DetectAudioFormat(byte[] data)
        {
            if (data == null || data.Length < 4)
            {
                return ".wav";
            }

            // Check for MP3 signatures
            // ID3 header (ID3v2 tags at start of MP3)
            if (data.Length >= 3 && data[0] == (byte)'I' && data[1] == (byte)'D' && data[2] == (byte)'3')
            {
                return ".mp3";
            }
            // MP3 frame sync (0xFF 0xFB, 0xFF 0xFA, 0xFF 0xF3, 0xFF 0xF2)
            if (data.Length >= 2 && data[0] == 0xFF && (data[1] & 0xE0) == 0xE0)
            {
                return ".mp3";
            }
            // LAME header
            if (data.Length >= 4 && data[0] == (byte)'L' && data[1] == (byte)'A' && data[2] == (byte)'M' && data[3] == (byte)'E')
            {
                return ".mp3";
            }

            // Check for RIFF/WAVE
            if (data.Length >= 4 && data[0] == (byte)'R' && data[1] == (byte)'I' && data[2] == (byte)'F' && data[3] == (byte)'F')
            {
                return ".wav";
            }

            // Check for OGG
            if (data.Length >= 4 && data[0] == (byte)'O' && data[1] == (byte)'g' && data[2] == (byte)'g' && data[3] == (byte)'S')
            {
                return ".ogg";
            }

            // Check for FLAC
            if (data.Length >= 4 && data[0] == (byte)'f' && data[1] == (byte)'L' && data[2] == (byte)'a' && data[3] == (byte)'C')
            {
                return ".flac";
            }

            // Default to wav
            return ".wav";
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/wav.py:189-205
        // Original: @staticmethod def get_format_name(extension: str) -> str:
        public static string GetFormatName(string extension)
        {
            if (string.IsNullOrEmpty(extension))
            {
                return "Unknown";
            }

            string extLower = extension.ToLowerInvariant();
            switch (extLower)
            {
                case ".wav":
                    return "WAV (RIFF)";
                case ".mp3":
                    return "MP3";
                case ".ogg":
                    return "OGG Vorbis";
                case ".flac":
                    return "FLAC";
                default:
                    return "Unknown";
            }
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/wav.py:211-236
        // Original: def load(self, filepath: os.PathLike | str, resref: str, restype: ResourceType, data: bytes | bytearray) -> None:
        public override void Load(string filepath, string resref, ResourceType restype, byte[] data)
        {
            base.Load(filepath, resref, restype, data);

            // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/wav.py:228-230
            // Original: data_bytes = bytes(data) if isinstance(data, bytearray) else data
            // Original: self._audio_data = data_bytes
            // Original: self._detected_format = self.get_format_name(self.detect_audio_format(data_bytes))
            byte[] dataBytes = data ?? new byte[0];
            _audioData = dataBytes;
            _detectedFormat = GetFormatName(DetectAudioFormat(dataBytes));

            // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/wav.py:233
            // Original: self.ui.formatLabel.setText(f"Format: {self._detected_format}")
            if (Ui?.FormatLabel != null)
            {
                Ui.FormatLabel.Text = "Format: " + _detectedFormat;
            }
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/wav.py:238-244
        // Original: def build(self) -> tuple[bytes, bytes]:
        public override Tuple<byte[], byte[]> Build()
        {
            // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/wav.py:244
            // Original: return self._audio_data, b""
            return Tuple.Create(_audioData ?? new byte[0], new byte[0]);
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/wav.py:246-262
        // Original: def new(self) -> None:
        public override void New()
        {
            base.New();

            // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/wav.py:250-251
            // Original: self._audio_data = b""
            // Original: self._detected_format = "Unknown"
            _audioData = new byte[0];
            _detectedFormat = "Unknown";
            CleanupTempFile();

            // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/wav.py:255-259
            // Original: Reset UI
            if (Ui != null)
            {
                if (Ui.CurrentTimeLabel != null)
                {
                    Ui.CurrentTimeLabel.Text = "00:00:00";
                }
                if (Ui.TotalTimeLabel != null)
                {
                    Ui.TotalTimeLabel.Text = "00:00:00";
                }
                if (Ui.TimeSlider != null)
                {
                    Ui.TimeSlider.Value = 0;
                    Ui.TimeSlider.Maximum = 0;
                }
                if (Ui.FormatLabel != null)
                {
                    Ui.FormatLabel.Text = "Format: -";
                }
            }
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/wav.py:421-428
        // Original: def _cleanup_temp_file(self) -> None:
        // Public method for testing (matching Python's test access pattern)
        public void CleanupTempFile()
        {
            if (!string.IsNullOrEmpty(_tempFile) && File.Exists(_tempFile))
            {
                try
                {
                    File.Delete(_tempFile);
                }
                catch
                {
                    // Ignore errors when deleting temp file
                }
            }
            _tempFile = null;
        }

        public override void SaveAs()
        {
            Save();
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/wav.py:529-539
        // Original: def closeEvent(self, a0: QCloseEvent | None) -> None:
        protected override void OnClosing(WindowClosingEventArgs e)
        {
            // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/wav.py:535-536
            // Original: self.mediaPlayer.player.stop()
            // Original: self._cleanup_temp_file()
            // TODO:  Stop player if available (TODO: STUB - not implemented in C# yet, but cleanup temp file)
            CleanupTempFile();
            base.OnClosing(e);
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/wav.py:456-478
        // Original: def _on_duration_changed(self, duration: int) -> None:
        // Public method for testing (matching Python's test access pattern)
        public void OnDurationChanged(long duration)
        {
            // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/wav.py:462-464
            // Original: max_slider_value = 2147483647
            // Original: duration = max(0, min(duration, max_slider_value))
            long maxSliderValue = 2147483647;
            long clampedDuration = Math.Max(0, Math.Min(duration, maxSliderValue));

            // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/wav.py:466-473
            // Original: Format time only if duration is valid
            string totalTime;
            if (clampedDuration > 0)
            {
                try
                {
                    TimeSpan timeSpan = TimeSpan.FromMilliseconds(clampedDuration);
                    totalTime = string.Format("{0:D2}:{1:D2}:{2:D2}", 
                        (int)timeSpan.TotalHours, 
                        timeSpan.Minutes, 
                        timeSpan.Seconds);
                }
                catch
                {
                    totalTime = "00:00:00";
                }
            }
            else
            {
                totalTime = "00:00:00";
            }

            // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/wav.py:475-477
            // Original: self.ui.totalTimeLabel.setText(total_time)
            // Original: self.ui.timeSlider.setMinimum(0)
            // Original: self.ui.timeSlider.setMaximum(duration)
            if (Ui?.TotalTimeLabel != null)
            {
                Ui.TotalTimeLabel.Text = totalTime;
            }
            if (Ui?.TimeSlider != null)
            {
                Ui.TimeSlider.Minimum = 0;
                Ui.TimeSlider.Maximum = clampedDuration;
            }
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/wav.py:480-500
        // Original: def _on_position_changed(self, position: int) -> None:
        // Public method for testing (matching Python's test access pattern)
        public void OnPositionChanged(long position)
        {
            // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/wav.py:486-487
            // Original: position = max(0, min(position, 2147483647))
            long maxSliderValue = 2147483647;
            long clampedPosition = Math.Max(0, Math.Min(position, maxSliderValue));

            // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/wav.py:489-497
            // Original: Only format time if position is valid
            string currentTime;
            if (clampedPosition > 0)
            {
                try
                {
                    TimeSpan timeSpan = TimeSpan.FromMilliseconds(clampedPosition);
                    currentTime = string.Format("{0:D2}:{1:D2}:{2:D2}", 
                        (int)timeSpan.TotalHours, 
                        timeSpan.Minutes, 
                        timeSpan.Seconds);
                }
                catch
                {
                    currentTime = "00:00:00";
                }
            }
            else
            {
                currentTime = "00:00:00";
            }

            // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/wav.py:499
            // Original: self.ui.currentTimeLabel.setText(current_time)
            if (Ui?.CurrentTimeLabel != null)
            {
                Ui.CurrentTimeLabel.Text = currentTime;
            }

            // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/wav.py:501-504
            // Original: if position > self.ui.timeSlider.maximum():
            // Original:     clamped_position = max(0, min(position, 2147483647))
            // Original:     self._on_duration_changed(clamped_position)
            // Fix for inaccurate duration calculation (but clamp to avoid overflow)
            if (Ui?.TimeSlider != null && clampedPosition > Ui.TimeSlider.Maximum)
            {
                long clampedPositionForDuration = Math.Max(0, Math.Min(clampedPosition, maxSliderValue));
                OnDurationChanged(clampedPositionForDuration);
            }

            // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/wav.py:506-508
            // Original: if not self.ui.timeSlider.isSliderDown():
            // Original:     self.ui.timeSlider.setValue(position)
            // Update slider if not being dragged
            if (Ui?.TimeSlider != null)
            {
                Ui.TimeSlider.Value = clampedPosition;
            }
        }
    }

    // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/wav.py
    // Original: self.ui = Ui_Editor() - UI wrapper class exposing all controls
    public class WAVEditorUi
    {
        public Button PlayButton { get; set; }
        public Button PauseButton { get; set; }
        public Button StopButton { get; set; }
        public Slider TimeSlider { get; set; }
        public TextBlock CurrentTimeLabel { get; set; }
        public TextBlock TotalTimeLabel { get; set; }
        public TextBlock FormatLabel { get; set; }
    }
}
