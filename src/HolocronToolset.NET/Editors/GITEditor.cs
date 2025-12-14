using System;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using CSharpKOTOR.Common;
using CSharpKOTOR.Resource.Generics;
using CSharpKOTOR.Resources;
using HolocronToolset.NET.Data;
using GFFAuto = CSharpKOTOR.Formats.GFF.GFFAuto;

namespace HolocronToolset.NET.Editors
{
    // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/git.py
    // Original: class GITEditor(Editor):
    public class GITEditor : Editor
    {
        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/git.py
        // Original: self.git: GIT = GIT()
        private GIT _git;

        public GITEditor(Window parent = null, HTInstallation installation = null)
            : base(parent, "GIT Editor", "git",
                new[] { ResourceType.GIT },
                new[] { ResourceType.GIT },
                installation)
        {
            _git = new GIT();
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

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/git.py
        // Original: def load(self, filepath: os.PathLike | str, resref: str, restype: ResourceType, data: bytes):
        public override void Load(string filepath, string resref, ResourceType restype, byte[] data)
        {
            base.Load(filepath, resref, restype, data);

            _git = ResourceAutoHelpers.ReadGit(data);
            LoadGIT(_git);
        }

        private void LoadGIT(GIT git)
        {
            // Load GIT data into UI
            // This will be expanded when full UI is implemented
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/git.py
        // Original: def build(self) -> tuple[bytes, bytes]:
        public override Tuple<byte[], byte[]> Build()
        {
            Game gameToUse = _installation?.Game ?? Game.K2;
            byte[] data = GITHelpers.BytesGit(_git, gameToUse, ResourceType.GIT);
            return Tuple.Create(data, new byte[0]);
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/git.py
        // Original: def new(self):
        public override void New()
        {
            base.New();
            _git = new GIT();
        }

        public override void SaveAs()
        {
            Save();
        }
    }
}
