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
    // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/utw.py:23
    // Original: class UTWEditor(Editor):
    public class UTWEditor : Editor
    {
        private UTW _utw;

        public UTWEditor(Window parent = null, HTInstallation installation = null)
            : base(parent, "Waypoint Editor", "waypoint",
                new[] { ResourceType.UTW },
                new[] { ResourceType.UTW },
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
            _utw = new UTW();
            LoadUTW(_utw);
        }

        private void LoadUTW(UTW utw)
        {
            // Load UTW data into UI
        }

        public override Tuple<byte[], byte[]> Build()
        {
            // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/utw.py
            // Original: def build(self) -> tuple[bytes, bytes]:
            // TODO: Implement UTWHelpers.DismantleUtw when available
            // For now, create a minimal valid GFF structure
            var gff = new GFF(GFFContent.UTW);
            // Build basic structure - full implementation will populate from _utw
            byte[] data = GFFAuto.BytesGff(gff, ResourceType.UTW);
            return Tuple.Create(data, new byte[0]);
        }

        public override void New()
        {
            base.New();
            _utw = new UTW();
        }

        public override void SaveAs()
        {
            Save();
        }
    }
}
