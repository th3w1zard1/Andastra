using System;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using CSharpKOTOR.Formats.GFF;
using CSharpKOTOR.Resources;
using HolocronToolset.NET.Data;

namespace HolocronToolset.NET.Editors
{
    // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/gff.py:47
    // Original: class GFFEditor(Editor):
    public class GFFEditor : Editor
    {
        private GFF _gff;

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/gff.py:48-81
        // Original: def __init__(self, parent, installation):
        public GFFEditor(Window parent = null, HTInstallation installation = null)
            : base(parent, "GFF Editor", "none",
                GetSupportedTypes(),
                GetSupportedTypes(),
                installation)
        {
            InitializeComponent();
            Width = 400;
            Height = 250;
            New();
        }

        private static ResourceType[] GetSupportedTypes()
        {
            // Get all GFF resource types
            return new[]
            {
                ResourceType.GFF,
                ResourceType.GFF_XML,
                ResourceType.ARE,
                ResourceType.IFO,
                ResourceType.UTC,
                ResourceType.UTD,
                ResourceType.UTE,
                ResourceType.UTI,
                ResourceType.UTM,
                ResourceType.UTP,
                ResourceType.UTS,
                ResourceType.UTT,
                ResourceType.UTW,
                ResourceType.DLG,
                ResourceType.GIT,
                ResourceType.JRL,
                ResourceType.PTH
            };
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
            // Initialize UI controls - will be done via XAML or programmatically
            var panel = new StackPanel();
            Content = panel;
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/gff.py:120-142
        // Original: def load(self, filepath, resref, restype, data):
        public override void Load(string filepath, string resref, ResourceType restype, byte[] data)
        {
            base.Load(filepath, resref, restype, data);
            _gff = GFF.FromBytes(data);
            LoadGff(_gff);
        }

        private void LoadGff(GFF gff)
        {
            // Load GFF data into UI tree
            // This will be implemented when UI controls are created
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/gff.py:187-205
        // Original: def build(self) -> tuple[bytes, bytes]:
        public override Tuple<byte[], byte[]> Build()
        {
            // Build GFF from UI tree
            // This will be implemented when UI controls are created
            ResourceType gffType = _restype ?? ResourceType.GFF;
            byte[] data = _gff.ToBytes();
            return Tuple.Create(data, new byte[0]);
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/gff.py:274-282
        // Original: def new(self):
        public override void New()
        {
            base.New();
            // Determine content type from resname if available
            GFFContent content = GFFContent.GFF;
            if (!string.IsNullOrEmpty(_resname))
            {
                // Try to determine content type from resname
                // This will be expanded when GFFContent detection is available
            }
            _gff = new GFF(content);
            // Clear UI tree
        }

        public override void SaveAs()
        {
            Save();
        }
    }
}
