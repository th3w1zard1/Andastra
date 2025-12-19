using System;
using System.Numerics;
using System.Collections.Generic;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using Andastra.Parsing.Common;
using Andastra.Parsing.Resource.Generics;
using Andastra.Parsing.Resource;
using HolocronToolset.Data;
using KotorColor = Andastra.Parsing.Common.Color;

namespace HolocronToolset.Editors
{
    // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/pth.py
    // Stub for renderArea UI component - will be fully implemented when UI is available
    public class PTHRenderArea
    {
        private PTH _pth;
        private Vector2 _mousePosition;

        public RenderCamera Camera { get; private set; }
        public PathSelection PathSelection { get; private set; }

        // Signal properties for test compatibility
        public object SigMousePressed { get; private set; }
        public object SigMouseMoved { get; private set; }
        public object SigMouseScrolled { get; private set; }
        public object SigMouseReleased { get; private set; }
        public object SigKeyPressed { get; private set; }

        public PTHRenderArea()
        {
            _pth = new PTH();
            _mousePosition = Vector2.Zero;
            Camera = new RenderCamera();
            PathSelection = new PathSelection();

            // Initialize signal properties - will be fully implemented when UI is available
            SigMousePressed = new object();
            SigMouseMoved = new object();
            SigMouseScrolled = new object();
            SigMouseReleased = new object();
            SigKeyPressed = new object();
        }

        public void SetPth(PTH pth)
        {
            _pth = pth ?? new PTH();
        }

        public void SetMousePosition(Vector2 position)
        {
            _mousePosition = position;
        }

        public void CenterCamera()
        {
            if (_pth.Count == 0)
            {
                Camera.SetPosition(Vector2.Zero);
                return;
            }

            float sumX = 0f;
            float sumY = 0f;
            foreach (var point in _pth.GetPoints())
            {
                sumX += point.X;
                sumY += point.Y;
            }

            Camera.SetPosition(new Vector2(sumX / _pth.Count, sumY / _pth.Count));
        }

        public List<Vector2> PathNodesUnderMouse(float tolerance = 0.1f)
        {
            var hits = new List<Vector2>();
            foreach (var point in _pth.GetPoints())
            {
                var dx = point.X - _mousePosition.X;
                var dy = point.Y - _mousePosition.Y;
                if ((dx * dx) + (dy * dy) <= tolerance * tolerance)
                {
                    hits.Add(point);
                }
            }

            return hits;
        }
    }

    public class RenderCamera
    {
        public Vector2 Position { get; private set; }
        public float Zoom { get; private set; }
        public float Rotation { get; private set; }

        public RenderCamera()
        {
            Position = Vector2.Zero;
            Zoom = 1.0f;
            Rotation = 0.0f;
        }

        public void SetPosition(Vector2 position)
        {
            Position = position;
        }

        public void NudgePosition(float x, float y)
        {
            Position = new Vector2(Position.X + x, Position.Y + y);
        }

        public void NudgeZoom(float amount)
        {
            Zoom = amount <= 0 ? Zoom : Zoom * amount;
        }

        public void NudgeRotation(float angle)
        {
            Rotation += angle;
        }
    }

    public class PathSelection
    {
        private readonly List<Vector2> _selected = new List<Vector2>();

        public void Select(IEnumerable<Vector2> points)
        {
            _selected.Clear();
            if (points == null)
            {
                return;
            }

            foreach (var point in points)
            {
                if (!_selected.Contains(point))
                {
                    _selected.Add(point);
                }
            }
        }

        public void Clear()
        {
            _selected.Clear();
        }

        public List<Vector2> All()
        {
            return new List<Vector2>(_selected);
        }

        public Vector2? Last()
        {
            if (_selected.Count == 0)
            {
                return null;
            }

            return _selected[_selected.Count - 1];
        }
    }

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
        
        // Render area - stub for testing (will be fully implemented when UI is available)
        public PTHRenderArea RenderArea { get; private set; }

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
            RenderArea = new PTHRenderArea();
            RenderArea.SetPth(_pth);
            SetupUI();
            AddHelpAction("GFF-PTH.md");
            New();
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/pth.py:143-169
        // Original: def intColorToQColor(num_color: int) -> QColor:
        private void InitializeMaterialColors()
        {
            // Helper to convert integer color to Avalonia Color
            Avalonia.Media.Color IntColorToAvaloniaColor(int numColor)
            {
                var kotorColor = KotorColor.FromRgbaInteger(numColor);
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
            RenderArea.PathSelection.Clear();
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
            return RenderArea.PathNodesUnderMouse();
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/pth.py:365-367
        // Original: def selected_nodes(self) -> list[Vector2]:
        public List<Vector2> SelectedNodes()
        {
            return RenderArea.PathSelection.All();
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/pth.py:307-310
        // Original: def moveCameraToSelection(self):
        public void MoveCameraToSelection()
        {
            var selection = RenderArea.PathSelection.Last();
            if (selection.HasValue)
            {
                RenderArea.Camera.SetPosition(selection.Value);
            }
            else
            {
                RenderArea.CenterCamera();
            }
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/pth.py:313-314
        // Original: def move_camera(self, x: float, y: float):
        public void MoveCamera(float x, float y)
        {
            RenderArea.Camera.NudgePosition(x, y);
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/pth.py:317-318
        // Original: def zoom_camera(self, amount: float):
        public void ZoomCamera(float amount)
        {
            RenderArea.Camera.NudgeZoom(amount);
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/pth.py:321-322
        // Original: def rotate_camera(self, angle: float):
        public void RotateCamera(float angle)
        {
            RenderArea.Camera.NudgeRotation(angle);
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/pth.py:325-326
        // Original: def move_selected(self, x: float, y: float):
        public void MoveSelected(float x, float y)
        {
            var selected = RenderArea.PathSelection.All();
            if (selected.Count == 0)
            {
                return;
            }

            for (int i = 0; i < selected.Count; i++)
            {
                var point = selected[i];
                var index = _pth.Find(point);
                if (index.HasValue)
                {
                    var updated = new Vector2(x, y);
                    _pth.SetPoint(index.Value, updated);
                    selected[i] = updated;
                }
            }

            RenderArea.PathSelection.Select(selected);
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/pth.py:373-374
        // Original: def select_node_under_mouse(self):
        public void SelectNodeUnderMouse()
        {
            var underMouse = PointsUnderMouse();
            if (underMouse.Count > 0)
            {
                RenderArea.PathSelection.Select(new[] { underMouse[0] });
            }
            else
            {
                RenderArea.PathSelection.Clear();
            }
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
            RenderArea.SetPth(_pth);
            RenderArea.PathSelection.Clear();
            RenderArea.CenterCamera();
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
            RenderArea.SetPth(_pth);
            RenderArea.PathSelection.Clear();
            RenderArea.CenterCamera();
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

        public Vector2 MousePosition
        {
            get { return _mousePos; }
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
            _editor.RenderArea.SetMousePosition(_mousePos);
        }

        public void SetMousePosition(Vector2 position)
        {
            _mousePos = position;
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
