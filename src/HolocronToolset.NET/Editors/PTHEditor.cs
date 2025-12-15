using System;
using System.Numerics;
using System.Collections.Generic;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
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
        private PTHControlScheme _controls;
        
        // Status bar labels
        public Avalonia.Controls.TextBlock LeftLabel { get; private set; }
        public Avalonia.Controls.TextBlock CenterLabel { get; private set; }
        public Avalonia.Controls.TextBlock RightLabel { get; private set; }
        
        // Status output handler
        public PTHStatusOut StatusOut { get; private set; }
        
        // Control scheme - exposed for testing
        public PTHControlScheme Controls => _controls;
        
        // Material colors dictionary - exposed for testing
        public Dictionary<SurfaceMaterial, Avalonia.Media.Color> MaterialColors { get; private set; }

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
            _controls = new PTHControlScheme(this);

            // Initialize material colors
            InitializeMaterialColors();

            InitializeComponent();
            SetupStatusBar();
            StatusOut = new PTHStatusOut(this);
            SetupUI();
            New();
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/pth.py:143-169
        // Original: def intColorToQColor(num_color: int) -> QColor:
        private void InitializeMaterialColors()
        {
            // Helper to convert integer color to Avalonia Color
            Avalonia.Media.Color IntColorToAvaloniaColor(int numColor)
            {
                var kotorColor = CSharpKOTOR.Common.Color.FromRgbaInteger(numColor);
                return new Avalonia.Media.Color(
                    (byte)(kotorColor.A * 255),
                    (byte)(kotorColor.R * 255),
                    (byte)(kotorColor.G * 255),
                    (byte)(kotorColor.B * 255)
                );
            }

            MaterialColors = new Dictionary<SurfaceMaterial, Avalonia.Media.Color>
            {
                { SurfaceMaterial.Undefined, IntColorToAvaloniaColor(_settings.UndefinedMaterialColour) },
                { SurfaceMaterial.Obscuring, IntColorToAvaloniaColor(_settings.ObscuringMaterialColour) },
                { SurfaceMaterial.Dirt, IntColorToAvaloniaColor(_settings.DirtMaterialColour) },
                { SurfaceMaterial.Grass, IntColorToAvaloniaColor(_settings.GrassMaterialColour) },
                { SurfaceMaterial.Stone, IntColorToAvaloniaColor(_settings.StoneMaterialColour) },
                { SurfaceMaterial.Wood, IntColorToAvaloniaColor(_settings.WoodMaterialColour) },
                { SurfaceMaterial.Water, IntColorToAvaloniaColor(_settings.WaterMaterialColour) },
                { SurfaceMaterial.NonWalk, IntColorToAvaloniaColor(_settings.NonWalkMaterialColour) },
                { SurfaceMaterial.Transparent, IntColorToAvaloniaColor(_settings.TransparentMaterialColour) },
                { SurfaceMaterial.Carpet, IntColorToAvaloniaColor(_settings.CarpetMaterialColour) },
                { SurfaceMaterial.Metal, IntColorToAvaloniaColor(_settings.MetalMaterialColour) },
                { SurfaceMaterial.Puddles, IntColorToAvaloniaColor(_settings.PuddlesMaterialColour) },
                { SurfaceMaterial.Swamp, IntColorToAvaloniaColor(_settings.SwampMaterialColour) },
                { SurfaceMaterial.Mud, IntColorToAvaloniaColor(_settings.MudMaterialColour) },
                { SurfaceMaterial.Leaves, IntColorToAvaloniaColor(_settings.LeavesMaterialColour) },
                { SurfaceMaterial.Lava, IntColorToAvaloniaColor(_settings.LavaMaterialColour) },
                { SurfaceMaterial.BottomlessPit, IntColorToAvaloniaColor(_settings.BottomlessPitMaterialColour) },
                { SurfaceMaterial.DeepWater, IntColorToAvaloniaColor(_settings.DeepWaterMaterialColour) },
                { SurfaceMaterial.Door, IntColorToAvaloniaColor(_settings.DoorMaterialColour) },
                { SurfaceMaterial.NonWalkGrass, IntColorToAvaloniaColor(_settings.NonWalkGrassMaterialColour) },
                { SurfaceMaterial.Trigger, IntColorToAvaloniaColor(_settings.NonWalkGrassMaterialColour) }
            };
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

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/pth.py:179-207
        // Original: def setup_status_bar(self):
        private void SetupStatusBar()
        {
            // Create labels for the different parts of the status message
            LeftLabel = new Avalonia.Controls.TextBlock { Text = "Left Status" };
            CenterLabel = new Avalonia.Controls.TextBlock 
            { 
                Text = "Center Status",
                HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center
            };
            RightLabel = new Avalonia.Controls.TextBlock { Text = "Right Status" };
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/pth.py:209-212
        // Original: def update_status_bar(self, left: str = "", center: str = "", right: str = ""):
        public void UpdateStatusBar(string left = "", string center = "", string right = "")
        {
            if (LeftLabel != null)
            {
                LeftLabel.Text = left ?? "";
            }
            if (CenterLabel != null)
            {
                CenterLabel.Text = center ?? "";
            }
            if (RightLabel != null)
            {
                RightLabel.Text = right ?? "";
            }
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

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/pth.py:307-310
        // Original: def moveCameraToSelection(self):
        public void MoveCameraToSelection()
        {
            // Will be implemented when render area is available
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/pth.py:313-314
        // Original: def move_camera(self, x: float, y: float):
        public void MoveCamera(float x, float y)
        {
            // Will be implemented when render area is available
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/pth.py:317-318
        // Original: def zoom_camera(self, amount: float):
        public void ZoomCamera(float amount)
        {
            // Will be implemented when render area is available
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/pth.py:321-322
        // Original: def rotate_camera(self, angle: float):
        public void RotateCamera(float angle)
        {
            // Will be implemented when render area is available
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/pth.py:325-326
        // Original: def move_selected(self, x: float, y: float):
        public void MoveSelected(float x, float y)
        {
            // Will be implemented when render area is available
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/pth.py:373-374
        // Original: def select_node_under_mouse(self):
        public void SelectNodeUnderMouse()
        {
            // Will be implemented when render area is available
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

    // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/pth.py:42-76
    // Original: class CustomStdout:
    public class PTHStatusOut
    {
        private string _prevStatusOut = "";
        private string _prevStatusError = "";
        private Vector2 _mousePos = Vector2.Zero;
        private PTHEditor _editor;

        public PTHStatusOut(PTHEditor editor)
        {
            _editor = editor;
        }

        public void Write(string text)
        {
            UpdateStatusBar(stdout: text);
        }

        public void Flush()
        {
            // Required for compatibility
        }

        public void UpdateStatusBar(string stdout = "", string stderr = "")
        {
            // Update stderr if provided
            if (!string.IsNullOrEmpty(stderr))
            {
                _prevStatusError = stderr;
            }

            // If a message is provided, use it as the last stdout
            if (!string.IsNullOrEmpty(stdout))
            {
                _prevStatusOut = stdout;
            }

            // Construct the status text using last known values
            string leftStatus = _mousePos.ToString();
            string centerStatus = _prevStatusOut;
            string rightStatus = _prevStatusError;
            _editor.UpdateStatusBar(leftStatus, centerStatus, rightStatus);
        }
    }

    // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/pth.py:425
    // Original: class PTHControlScheme:
    public class PTHControlScheme
    {
        public PTHEditor Editor { get; private set; }

        // Control properties for test compatibility
        public object PanCamera { get; private set; }
        public object RotateCamera { get; private set; }
        public object ZoomCamera { get; private set; }
        public object MoveSelected { get; private set; }
        public object SelectUnderneath { get; private set; }
        public object DeleteSelected { get; private set; }

        public PTHControlScheme(PTHEditor editor)
        {
            Editor = editor;
            // Initialize control properties - will be fully implemented when render area is available
            PanCamera = new object();
            RotateCamera = new object();
            ZoomCamera = new object();
            MoveSelected = new object();
            SelectUnderneath = new object();
            DeleteSelected = new object();
        }
    }
}
