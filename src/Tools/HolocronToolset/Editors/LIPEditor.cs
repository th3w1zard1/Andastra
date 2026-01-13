using System;
using System.Collections.Generic;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using BioWare.NET.Resource.Formats.LIP;
using BioWare.NET.Resource;
using HolocronToolset.Data;

namespace HolocronToolset.Editors
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
            // Ensure LIP length matches duration (matching Python behavior)
            _lip.Length = _duration;
            ResourceType lipType = _restype ?? ResourceType.LIP;
            byte[] data = LIPAuto.BytesLip(_lip, lipType);
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
            set
            {
                _duration = value;
                if (_lip != null)
                {
                    _lip.Length = value;
                }
            }
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/lip/lip_editor.py:312-326
        // Original: def add_keyframe(self):
        // Helper method for tests to add keyframes
        public void AddKeyframe(float time, LIPShape shape)
        {
            if (_lip == null)
            {
                _lip = new LIP();
                _lip.Length = _duration;
            }
            _lip.Add(time, shape);
            // Note: In Python, lip.length is set to duration when creating, not based on max keyframe time
            // The duration property is separate from the max keyframe time
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/lip/lip_editor.py:328-352
        // Original: def update_keyframe(self):
        // Helper method for tests to update keyframes
        public void UpdateKeyframe(int index, float time, LIPShape shape)
        {
            if (_lip == null || _lip.Frames.Count == 0)
            {
                return;
            }

            if (index >= 0 && index < _lip.Frames.Count)
            {
                // Remove old keyframe and add updated one
                _lip.Remove(index);
                _lip.Add(time, shape);
            }
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/lip/lip_editor.py:354-367
        // Original: def delete_keyframe(self):
        // Helper method for tests to delete keyframes
        public void DeleteKeyframe(int index)
        {
            if (_lip == null || _lip.Frames.Count == 0)
            {
                return;
            }

            if (index >= 0 && index < _lip.Frames.Count)
            {
                _lip.Remove(index);
            }
        }
    }
}
