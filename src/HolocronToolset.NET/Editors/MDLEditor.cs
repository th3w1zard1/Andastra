using System;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using CSharpKOTOR.Formats.MDL;
using CSharpKOTOR.Formats.MDLData;
using CSharpKOTOR.Resources;
using HolocronToolset.NET.Data;

namespace HolocronToolset.NET.Editors
{
    // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/mdl.py:25
    // Original: class MDLEditor(Editor):
    public class MDLEditor : Editor
    {
        private MDL _mdl;

        public MDLEditor(Window parent = null, HTInstallation installation = null)
            : base(parent, "Model Viewer", "none",
                new[] { ResourceType.MDL },
                new[] { ResourceType.MDL },
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
            // MDL requires both MDL and MDX data - simplified for now
            _mdl = MDLAuto.ReadMdl(data);
            LoadMDL(_mdl);
        }

        private void LoadMDL(MDL mdl)
        {
            // Load MDL data into UI renderer
        }

        public override Tuple<byte[], byte[]> Build()
        {
            // MDL requires both MDL and MDX data - simplified for now
            return Tuple.Create(new byte[0], new byte[0]);
        }

        public override void New()
        {
            base.New();
            _mdl = new MDL();
        }

        public override void SaveAs()
        {
            Save();
        }
    }
}
