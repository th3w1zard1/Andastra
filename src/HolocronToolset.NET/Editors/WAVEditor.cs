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
            AvaloniaXamlLoader.Load(this);
        }

        private void SetupUI()
        {
            var panel = new StackPanel();
            Content = panel;
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
}
