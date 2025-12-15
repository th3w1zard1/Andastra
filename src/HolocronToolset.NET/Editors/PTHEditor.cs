using System;
using System.Numerics;
using System.Collections.Generic;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using CSharpKOTOR.Common;
using CSharpKOTOR.Resource.Generics;
using CSharpKOTOR.Resources;
using HolocronToolset.NET.Data;

namespace HolocronToolset.NET.Editors
{
    // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/pth.py:120
    // Original: class PTHEditor(Editor):
    public partial class PTHEditor : Editor
    {
        private PTH _pth;
        private GITSettings _settings;

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/pth.py:121-177
        // Original: def __init__(self, parent, installation):
        public PTHEditor(Window parent = null, HTInstallation installation = null)
            : base(parent, "PTH Editor", "pth",
                new[] { ResourceType.PTH },
                new[] { ResourceType.PTH },
                installation)
        {
            _pth = new PTH();
            _settings = new GITSettings();

            InitializeComponent();
            SetupUI();
            New();
        }

        private void InitializeComponent()
        {
            bool xamlLoaded = false;
            try
            {
                AvaloniaXamlLoader.Load(this);
                xamlLoaded = true;
            }
            catch
            {
                // XAML not available - will use programmatic UI
            }

            if (!xamlLoaded)
            {
                SetupProgrammaticUI();
            }
        }

        private void SetupProgrammaticUI()
        {
            var panel = new StackPanel();
            Content = panel;
        }

        private void SetupUI()
        {
            // UI setup - will be implemented when XAML is available
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/pth.py:340-342
        // Original: def addNode(self, x: float, y: float):
        public void AddNode(float x, float y)
        {
            _pth.Add(x, y);
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/pth.py:344-346
        // Original: def remove_node(self, index: int):
        public void RemoveNode(int index)
        {
            _pth.Remove(index);
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/pth.py:356-359
        // Original: def addEdge(self, source: int, target: int):
        public void AddEdge(int source, int target)
        {
            // Create bidirectional connections like other path editors
            _pth.Connect(source, target);
            _pth.Connect(target, source);
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/pth.py:349-353
        // Original: def removeEdge(self, source: int, target: int):
        public void RemoveEdge(int source, int target)
        {
            // Remove bidirectional connections like other path editors
            _pth.Disconnect(source, target);
            _pth.Disconnect(target, source);
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/pth.py:361-363
        // Original: def points_under_mouse(self) -> list[Vector2]:
        public List<Vector2> PointsUnderMouse()
        {
            // Will be implemented when render area is available
            return new List<Vector2>();
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/pth.py:365-367
        // Original: def selected_nodes(self) -> list[Vector2]:
        public List<Vector2> SelectedNodes()
        {
            // Will be implemented when render area is available
            return new List<Vector2>();
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/pth.py:249-269
        // Original: def load(self, filepath, resref, restype, data):
        public override void Load(string filepath, string resref, ResourceType restype, byte[] data)
        {
            base.Load(filepath, resref, restype, data);

            try
            {
                var pth = PTHAuto.ReadPth(data);
                LoadPTH(pth);
            }
            catch (Exception ex)
            {
                System.Console.WriteLine($"Failed to load PTH: {ex}");
                New();
            }
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/pth.py:271-275
        // Original: def _loadPTH(self, pth: PTH):
        private void LoadPTH(PTH pth)
        {
            _pth = pth;
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/pth.py:277-278
        // Original: def build(self) -> tuple[bytes, bytes]:
        public override Tuple<byte[], byte[]> Build()
        {
            byte[] data = PTHAuto.BytesPth(_pth);
            return Tuple.Create(data, new byte[0]);
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/pth.py:280-282
        // Original: def new(self):
        public override void New()
        {
            base.New();
            _pth = new PTH();
        }

        public override void SaveAs()
        {
            Save();
        }
    }
}
