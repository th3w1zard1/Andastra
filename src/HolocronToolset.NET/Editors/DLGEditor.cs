using System;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using CSharpKOTOR.Common;
using CSharpKOTOR.Formats.GFF;
using CSharpKOTOR.Resource.Generics.DLG;
using CSharpKOTOR.Resources;
using HolocronToolset.NET.Data;
using GFFAuto = CSharpKOTOR.Formats.GFF.GFFAuto;

namespace HolocronToolset.NET.Editors
{
    // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/dlg/editor.py:88
    // Original: class DLGEditor(Editor):
    public class DLGEditor : Editor
    {
        private DLG _dlg;

        public DLGEditor(Window parent = null, HTInstallation installation = null)
            : base(parent, "Dialog Editor", "dialog",
                new[] { ResourceType.DLG },
                new[] { ResourceType.DLG },
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
            // DLG conversion will be implemented when DLG conversion methods are available
            _dlg = new DLG();
            LoadDLG(_dlg);
        }

        private void LoadDLG(DLG dlg)
        {
            // Load DLG data into UI tree
        }

        public override Tuple<byte[], byte[]> Build()
        {
            // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/dlg/editor.py
            // Original: def build(self) -> tuple[bytes, bytes]:
            // TODO: Implement DLGHelpers.DismantleDlg when available
            // For now, create a minimal valid GFF structure
            var gff = new GFF(GFFContent.DLG);
            // Build basic structure - full implementation will populate from _dlg
            byte[] data = GFFAuto.BytesGff(gff, ResourceType.DLG);
            return Tuple.Create(data, new byte[0]);
        }

        public override void New()
        {
            base.New();
            _dlg = new DLG();
        }

        public override void SaveAs()
        {
            Save();
        }
    }
}
