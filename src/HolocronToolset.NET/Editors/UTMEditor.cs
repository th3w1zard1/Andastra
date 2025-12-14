using System;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using CSharpKOTOR.Formats.GFF;
using CSharpKOTOR.Resource.Generics;
using CSharpKOTOR.Resources;
using HolocronToolset.NET.Data;
using GFFAuto = CSharpKOTOR.Formats.GFF.GFFAuto;

namespace HolocronToolset.NET.Editors
{
    // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/utm.py
    // Original: class UTMEditor(Editor):
    public class UTMEditor : Editor
    {
        private UTM _utm;

        public UTMEditor(Window parent = null, HTInstallation installation = null)
            : base(parent, "Merchant Editor", "merchant",
                new[] { ResourceType.UTM, ResourceType.BTM },
                new[] { ResourceType.UTM, ResourceType.BTM },
                installation)
        {
            InitializeComponent();
            SetupUI();
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
            Content = panel;
        }

        public override void Load(string filepath, string resref, ResourceType restype, byte[] data)
        {
            base.Load(filepath, resref, restype, data);
            var gff = GFF.FromBytes(data);
            _utm = new UTM();
            LoadUTM(_utm);
        }

        private void LoadUTM(UTM utm)
        {
            // Load UTM data into UI
        }

        public override Tuple<byte[], byte[]> Build()
        {
            // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/utm.py:138-170
            // Original: def build(self) -> tuple[bytes, bytes]:
            // TODO: Implement UTMHelpers.DismantleUtm when available
            // For now, create a minimal valid GFF structure
            var gff = new GFF(GFFContent.UTM);
            // Build basic structure - full implementation will populate from _utm
            byte[] data = GFFAuto.BytesGff(gff, ResourceType.UTM);
            return Tuple.Create(data, new byte[0]);
        }

        public override void New()
        {
            base.New();
            _utm = new UTM();
        }

        public override void SaveAs()
        {
            Save();
        }
    }
}
