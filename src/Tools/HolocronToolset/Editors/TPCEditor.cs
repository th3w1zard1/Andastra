using System;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using BioWare.NET.Resource.Formats.TPC;
using BioWare.NET.Resource;
using HolocronToolset.Data;

namespace HolocronToolset.Editors
{
    // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/tpc.py:48
    // Original: class TPCEditor(Editor):
    public partial class TPCEditor : Editor
    {
        private TPC _tpc;

        public TPCEditor(Window parent = null, HTInstallation installation = null)
            : base(parent, "Texture Viewer", "none",
                new[] { ResourceType.TPC, ResourceType.TGA, ResourceType.JPG, ResourceType.PNG, ResourceType.BMP },
                new[] { ResourceType.TPC, ResourceType.TGA, ResourceType.JPG, ResourceType.PNG, ResourceType.BMP },
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
            _tpc = TPCAuto.ReadTpc(data);
            LoadTPC(_tpc);
        }

        private void LoadTPC(TPC tpc)
        {
            // Load TPC data into UI
        }

        public override Tuple<byte[], byte[]> Build()
        {
            ResourceType tpcType = _restype ?? ResourceType.TPC;
            byte[] data = TPCAuto.BytesTpc(_tpc, tpcType);
            return Tuple.Create(data, new byte[0]);
        }

        public override void New()
        {
            base.New();
            _tpc = new TPC();
        }

        public override void SaveAs()
        {
            Save();
        }
    }
}
