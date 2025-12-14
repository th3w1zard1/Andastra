using System;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using CSharpKOTOR.Formats.TwoDA;
using CSharpKOTOR.Resources;
using HolocronToolset.NET.Data;

namespace HolocronToolset.NET.Editors
{
    // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/twoda.py:31
    // Original: class TwoDAEditor(Editor):
    public class TwoDAEditor : Editor
    {
        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/twoda.py:32-64
        // Original: def __init__(self, parent, installation):
        public TwoDAEditor(Window parent = null, HTInstallation installation = null)
            : base(parent, "2DA Editor", "none",
                new[] { ResourceType.TwoDA, ResourceType.TwoDA_CSV, ResourceType.TwoDA_JSON },
                new[] { ResourceType.TwoDA, ResourceType.TwoDA_CSV, ResourceType.TwoDA_JSON },
                installation)
        {
            InitializeComponent();
            Width = 400;
            Height = 250;
            New();
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
            // Initialize UI controls - will be done via XAML or programmatically
            var panel = new StackPanel();
            Content = panel;
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/twoda.py:128-178
        // Original: def load(self, filepath, resref, restype, data):
        public override void Load(string filepath, string resref, ResourceType restype, byte[] data)
        {
            base.Load(filepath, resref, restype, data);

            try
            {
                var reader = new TwoDABinaryReader(data);
                TwoDA twoda = reader.Load();
                LoadMain(twoda);
            }
            catch (Exception)
            {
                // Show error message - will be implemented with MessageBox.Avalonia
                New();
            }
        }

        private void LoadMain(TwoDA twoda)
        {
            // Load TwoDA data into UI
            // This will be implemented when UI controls are created
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/twoda.py:203-218
        // Original: def build(self) -> tuple[bytes, bytes]:
        public override Tuple<byte[], byte[]> Build()
        {
            // Build TwoDA from UI
            // This will be implemented when UI controls are created
            var twoda = new TwoDA();
            byte[] data = TwoDAAuto.BytesTwoDA(twoda, _restype ?? ResourceType.TwoDA);
            return Tuple.Create(data, new byte[0]);
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/twoda.py:220-224
        // Original: def new(self):
        public override void New()
        {
            base.New();
            // Clear UI
        }

        public override void SaveAs()
        {
            Save();
        }
    }
}
