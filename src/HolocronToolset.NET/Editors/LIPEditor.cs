using System;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using CSharpKOTOR.Formats.LIP;
using CSharpKOTOR.Resources;
using HolocronToolset.NET.Data;

namespace HolocronToolset.NET.Editors
{
    // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/lip/lip_editor.py:41
    // Original: class LIPEditor(Editor):
    public class LIPEditor : Editor
    {
        private LIP _lip;

        public LIPEditor(Window parent = null, HTInstallation installation = null)
            : base(parent, "LIP Editor", "lip",
                new[] { ResourceType.LIP, ResourceType.LIP_XML, ResourceType.LIP_JSON },
                new[] { ResourceType.LIP, ResourceType.LIP_XML, ResourceType.LIP_JSON },
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
            _lip = LIPAuto.ReadLip(data);
            LoadLIP(_lip);
        }

        private void LoadLIP(LIP lip)
        {
            // Load LIP data into UI
        }

        public override Tuple<byte[], byte[]> Build()
        {
            ResourceType lipType = _restype ?? ResourceType.LIP;
            byte[] data = LIPAuto.BytesLip(_lip, lipType);
            return Tuple.Create(data, new byte[0]);
        }

        public override void New()
        {
            base.New();
            _lip = new LIP();
        }

        public override void SaveAs()
        {
            Save();
        }
    }
}
