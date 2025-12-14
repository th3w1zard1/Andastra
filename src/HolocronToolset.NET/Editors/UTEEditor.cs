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
    // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/ute.py:26
    // Original: class UTEEditor(Editor):
    public class UTEEditor : Editor
    {
        private UTE _ute;

        public UTEEditor(Window parent = null, HTInstallation installation = null)
            : base(parent, "Encounter Editor", "encounter",
                new[] { ResourceType.UTE, ResourceType.BTE },
                new[] { ResourceType.UTE, ResourceType.BTE },
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
            _ute = new UTE();
            LoadUTE(_ute);
        }

        private void LoadUTE(UTE ute)
        {
            // Load UTE data into UI
        }

        public override Tuple<byte[], byte[]> Build()
        {
            // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/ute.py
            // Original: def build(self) -> tuple[bytes, bytes]:
            // TODO: Implement UTEHelpers.DismantleUte when available
            // For now, create a minimal valid GFF structure
            var gff = new GFF(GFFContent.UTE);
            // Build basic structure - full implementation will populate from _ute
            byte[] data = GFFAuto.BytesGff(gff, ResourceType.UTE);
            return Tuple.Create(data, new byte[0]);
        }

        public override void New()
        {
            base.New();
            _ute = new UTE();
        }

        public override void SaveAs()
        {
            Save();
        }
    }
}
