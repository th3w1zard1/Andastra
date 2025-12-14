using System;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using CSharpKOTOR.Common;
using CSharpKOTOR.Formats.GFF;
using CSharpKOTOR.Resource.Generics;
using CSharpKOTOR.Resources;
using HolocronToolset.NET.Data;
using GFFAuto = CSharpKOTOR.Formats.GFF.GFFAuto;

namespace HolocronToolset.NET.Editors
{
    // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/are.py:35
    // Original: class AREEditor(Editor):
    public class AREEditor : Editor
    {
        private ARE _are;

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/are.py:36-74
        // Original: def __init__(self, parent, installation):
        public AREEditor(Window parent = null, HTInstallation installation = null)
            : base(parent, "ARE Editor", "none",
                new[] { ResourceType.ARE },
                new[] { ResourceType.ARE },
                installation)
        {
            InitializeComponent();
            SetupUI();
            MinWidth = 400;
            MinHeight = 600;
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
            // Setup UI elements - will be implemented when UI controls are created
            var panel = new StackPanel();
            Content = panel;
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/are.py:134-149
        // Original: def load(self, filepath, resref, restype, data):
        public override void Load(string filepath, string resref, ResourceType restype, byte[] data)
        {
            base.Load(filepath, resref, restype, data);

            if (data == null || data.Length == 0)
            {
                throw new ArgumentException("The ARE file data is empty or invalid.");
            }

            // ARE is a GFF-based format
            var gff = GFF.FromBytes(data);
            // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/are.py:146
            // Original: are: ARE = read_are(data)
            _are = AREHelpers.ConstructAre(gff);
            LoadARE(_are);
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/are.py:151-248
        // Original: def _loadARE(self, are: ARE):
        private void LoadARE(ARE are)
        {
            // Load ARE data into UI
            // This will be implemented when UI controls are created
            // For now, just store the ARE object
            _are = are;
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/are.py:250-300
        // Original: def build(self) -> tuple[bytes, bytes]:
        public override Tuple<byte[], byte[]> Build()
        {
            // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/are.py:250-277
            // Original: def build(self) -> tuple[bytes, bytes]:
            Game game = _installation?.Game ?? Game.K1;
            var gff = AREHelpers.DismantleAre(_are, game);
            byte[] data = GFFAuto.BytesGff(gff, ResourceType.ARE);
            return Tuple.Create(data, new byte[0]);
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/are.py:302-310
        // Original: def new(self):
        public override void New()
        {
            base.New();
            _are = new ARE();
            // Clear UI
        }

        public override void SaveAs()
        {
            Save();
        }
    }
}
