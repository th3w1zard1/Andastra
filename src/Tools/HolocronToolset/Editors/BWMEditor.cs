using System;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using BioWare.NET.Resource.Formats.BWM;
using BioWare.NET.Resource;
using HolocronToolset.Data;

namespace HolocronToolset.Editors
{
    // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/bwm.py:38
    // Original: class BWMEditor(Editor):
    public class BWMEditor : Editor
    {
        private BWM _bwm;

        public BWMEditor(Window parent = null, HTInstallation installation = null)
            : base(parent, "Walkmesh Painter", "walkmesh",
                new[] { ResourceType.WOK, ResourceType.DWK, ResourceType.PWK },
                new[] { ResourceType.WOK, ResourceType.DWK, ResourceType.PWK },
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
            _bwm = BWMAuto.ReadBwm(data);
            LoadBWM(_bwm);
        }

        private void LoadBWM(BWM bwm)
        {
            // Load BWM data into UI renderer
        }

        public override Tuple<byte[], byte[]> Build()
        {
            ResourceType bwmType = _restype ?? ResourceType.WOK;
            byte[] data = BWMAuto.BytesBwm(_bwm, bwmType);
            return Tuple.Create(data, new byte[0]);
        }

        public override void New()
        {
            base.New();
            _bwm = new BWM();
        }

        public override void SaveAs()
        {
            Save();
        }
    }
}
