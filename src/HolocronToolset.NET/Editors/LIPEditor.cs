using System;
using System.Collections.Generic;
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
        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/lip/lip_editor.py:131
        // Original: self.lip: Optional[LIP] = None; self.duration: float = 0.0
        private LIP _lip;
        private float _duration;

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/lip/lip_editor.py:44-132
        // Original: def __init__(self, parent: QWidget | None = None, installation: HTInstallation | None = None):
        public LIPEditor(Window parent = null, HTInstallation installation = null)
            : base(parent, "LIP Editor", "lip",
                new[] { ResourceType.LIP, ResourceType.LIP_XML, ResourceType.LIP_JSON },
                new[] { ResourceType.LIP, ResourceType.LIP_XML, ResourceType.LIP_JSON },
                installation)
        {
            _lip = null;
            _duration = 0.0f;
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

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/lip/lip_editor.py:481-497
        // Original: def load(self, filepath: os.PathLike | str, resref: str, restype: ResourceType, data: bytes):
        public override void Load(string filepath, string resref, ResourceType restype, byte[] data)
        {
            base.Load(filepath, resref, restype, data);

            _lip = LIPAuto.ReadLip(data);
            _duration = _lip != null ? _lip.Length : 0.0f;
            LoadLIP(_lip);
        }

        private void LoadLIP(LIP lip)
        {
            // Load LIP data into UI
            // This will be expanded when full UI is implemented
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/lip/lip_editor.py:499-504
        // Original: def build(self) -> tuple[bytes, bytes]:
        public override Tuple<byte[], byte[]> Build()
        {
            if (_lip == null)
            {
                _lip = new LIP();
            }
            ResourceType lipType = _restype ?? ResourceType.LIP;
            byte[] data;
            try
            {
                data = LIPAuto.BytesLip(_lip, lipType);
            }
            catch
            {
                // If LIP is empty or invalid, return empty data (matching Python behavior for empty LIPs)
                data = new byte[0];
            }
            return Tuple.Create(data, new byte[0]);
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/lip/lip_editor.py:506-510
        // Original: def new(self):
        public override void New()
        {
            base.New();
            _lip = new LIP();
            _duration = 0.0f;
        }

        public override void SaveAs()
        {
            Save();
        }

        // Properties for tests
        public LIP Lip => _lip;
        public float Duration
        {
            get => _duration;
            set => _duration = value;
        }
    }
}
