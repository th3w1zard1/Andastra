using System;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using CSharpKOTOR.Formats.ERF;
using CSharpKOTOR.Resources;
using HolocronToolset.NET.Data;

namespace HolocronToolset.NET.Editors
{
    // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/erf.py:97
    // Original: class ERFEditor(Editor):
    public class ERFEditor : Editor
    {
        private ERF _erf;
        private bool _hasChanges;

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/erf.py:98-146
        // Original: def __init__(self, parent, installation):
        public ERFEditor(Window parent = null, HTInstallation installation = null)
            : base(parent, "ERF Editor", "none",
                new[] { ResourceType.RIM, ResourceType.ERF, ResourceType.MOD, ResourceType.SAV, ResourceType.BIF },
                new[] { ResourceType.RIM, ResourceType.ERF, ResourceType.MOD, ResourceType.SAV, ResourceType.BIF },
                installation)
        {
            InitializeComponent();
            Width = 400;
            Height = 250;
            _hasChanges = false;
            New();
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
            // Initialize UI controls - will be done via XAML or programmatically
            var panel = new StackPanel();
            Content = panel;
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/erf.py:148-200
        // Original: def load(self, filepath, resref, restype, data):
        public override void Load(string filepath, string resref, ResourceType restype, byte[] data)
        {
            base.Load(filepath, resref, restype, data);

            try
            {
                if (restype == ResourceType.RIM)
                {
                    // Handle RIM files - will be implemented when RIM reading is available
                    _erf = ERFAuto.ReadErf(data);
                }
                else
                {
                    _erf = ERFAuto.ReadErf(data);
                }
                LoadErf(_erf);
            }
            catch (Exception)
            {
                // Show error message - will be implemented with MessageBox.Avalonia
                New();
            }
        }

        private void LoadErf(ERF erf)
        {
            // Load ERF data into UI table
            // This will be implemented when UI controls are created
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/erf.py:400-450
        // Original: def build(self) -> tuple[bytes, bytes]:
        public override Tuple<byte[], byte[]> Build()
        {
            // Build ERF from UI table
            // This will be implemented when UI controls are created
            ResourceType erfType = _restype ?? ResourceType.ERF;
            byte[] data = ERFAuto.BytesErf(_erf, erfType);
            return Tuple.Create(data, new byte[0]);
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/erf.py:452-460
        // Original: def new(self):
        public override void New()
        {
            base.New();
            _erf = new ERF();
            _hasChanges = false;
            // Clear UI table
        }

        public override void SaveAs()
        {
            Save();
        }
    }
}
