using System;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using CSharpKOTOR.Formats.WAV;
using CSharpKOTOR.Resources;
using HolocronToolset.NET.Data;

namespace HolocronToolset.NET.Editors
{
    // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/wav.py:42
    // Original: class WAVEditor(Editor):
    public class WAVEditor : Editor
    {
        private WAV _wav;
        private Button _playButton;
        private Button _stopButton;

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/wav.py
        // Original: self.ui = Ui_Editor() - UI wrapper class exposing all controls
        public WAVEditorUi Ui { get; private set; }

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
            if (!TryLoadXaml())
            {
                SetupUI();
            }
        }

        private void SetupUI()
        {
            var panel = new StackPanel();
            
            // Create play/stop buttons
            var buttonPanel = new StackPanel { Orientation = Avalonia.Layout.Orientation.Horizontal, Spacing = 5 };
            _playButton = new Button { Content = "Play" };
            _stopButton = new Button { Content = "Stop" };
            buttonPanel.Children.Add(_playButton);
            buttonPanel.Children.Add(_stopButton);
            panel.Children.Add(buttonPanel);
            
            Content = panel;

            // Find controls from XAML if available
            try
            {
                _playButton = this.FindControl<Button>("playButton") ?? _playButton;
                _stopButton = this.FindControl<Button>("stopButton") ?? _stopButton;
            }
            catch
            {
                // XAML not loaded - use programmatic UI
            }

            // Create UI wrapper for testing
            Ui = new WAVEditorUi
            {
                PlayButton = _playButton,
                StopButton = _stopButton
            };
        }

        public override void Load(string filepath, string resref, ResourceType restype, byte[] data)
        {
            base.Load(filepath, resref, restype, data);
            _wav = WAVAuto.ReadWav(data);
            LoadWAV(_wav);
        }

        private void LoadWAV(WAV wav)
        {
            // Load WAV data into UI player
        }

        public override Tuple<byte[], byte[]> Build()
        {
            ResourceType wavType = _restype ?? ResourceType.WAV;
            byte[] data = WAVAuto.BytesWav(_wav, wavType);
            return Tuple.Create(data, new byte[0]);
        }

        public override void New()
        {
            base.New();
            _wav = new WAV();
        }

        public override void SaveAs()
        {
            Save();
        }
    }

    // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/wav.py
    // Original: self.ui = Ui_Editor() - UI wrapper class exposing all controls
    public class WAVEditorUi
    {
        public Button PlayButton { get; set; }
        public Button StopButton { get; set; }
    }
}
