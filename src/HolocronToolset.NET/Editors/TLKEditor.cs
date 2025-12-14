using System;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using CSharpKOTOR.Formats.TLK;
using CSharpKOTOR.Resources;
using HolocronToolset.NET.Data;

namespace HolocronToolset.NET.Editors
{
    // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/tlk.py:56
    // Original: class TLKEditor(Editor):
    public class TLKEditor : Editor
    {
        private TLK _tlk;

        public TLKEditor(Window parent = null, HTInstallation installation = null)
            : base(parent, "TLK Editor", "none",
                new[] { ResourceType.TLK, ResourceType.TLK_XML, ResourceType.TLK_JSON },
                new[] { ResourceType.TLK, ResourceType.TLK_XML, ResourceType.TLK_JSON },
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
            var reader = new TLKBinaryReader(data);
            _tlk = reader.Load();
            LoadTLK(_tlk);
        }

        private void LoadTLK(TLK tlk)
        {
            // Load TLK data into UI table
        }

        public override Tuple<byte[], byte[]> Build()
        {
            ResourceType tlkType = _restype ?? ResourceType.TLK;
            byte[] data = TLKAuto.BytesTlk(_tlk, tlkType);
            return Tuple.Create(data, new byte[0]);
        }

        public override void New()
        {
            base.New();
            _tlk = new TLK();
        }

        public override void SaveAs()
        {
            Save();
        }
    }
}
