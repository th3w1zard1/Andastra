using System;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using CSharpKOTOR.Formats.LYT;
using CSharpKOTOR.Resources;
using HolocronToolset.NET.Data;

namespace HolocronToolset.NET.Editors
{
    // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/lyt.py:29
    // Original: class LYTEditor(Editor):
    public class LYTEditor : Editor
    {
        private LYT _lyt;

        public LYTEditor(Window parent = null, HTInstallation installation = null)
            : base(parent, "LYT Editor", "lyt",
                new[] { ResourceType.LYT },
                new[] { ResourceType.LYT },
                installation)
        {
            InitializeComponent();
            SetupUI();
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
            _lyt = LYTAuto.ReadLyt(data);
            LoadLYT(_lyt);
        }

        private void LoadLYT(LYT lyt)
        {
            // Load LYT data into UI
        }

        public override Tuple<byte[], byte[]> Build()
        {
            byte[] data = LYTAuto.BytesLyt(_lyt);
            return Tuple.Create(data, new byte[0]);
        }

        public override void New()
        {
            base.New();
            _lyt = new LYT();
        }

        public override void SaveAs()
        {
            Save();
        }
    }
}
