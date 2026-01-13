using System;
using System.Numerics;
using System.Collections.Generic;
using BioWare.NET.Common;
using BioWare.NET.Resource;
using BioWare.NET.Resource.Formats.GFF.Generics;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using HolocronToolset.Data;
using KotorColor = BioWare.NET.Common.ParsingColor;
using Window = Avalonia.Controls.Window;
using PTH = BioWare.NET.Resource.Formats.GFF.Generics.PTH;
using PathSelection = HolocronToolset.Editors.PathSelection;
using MsBox.Avalonia;
using MsBox.Avalonia.Enums;
using BioWare.NET.Resource.Formats.LYT;
using BioWare.NET.Resource.Formats.BWM;

namespace HolocronToolset.Editors
{
    // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/pth.py
    public class PTHRenderArea : Control
    {
        private PTH _pth;
        private Vector2 _mousePosition;
        private bool _isMouseDown;
        private Vector2 _lastMousePosition;

        public RenderCamera Camera { get; private set; }
        public PathSelection PathSelection { get; private set; }

        // Signal events for proper Avalonia event handling
        public event EventHandler<MouseEventArgs> SigMousePressed;
        public event EventHandler<MouseEventArgs> SigMouseMoved;
        public event EventHandler<MouseWheelEventArgs> SigMouseScrolled;
        public event EventHandler<MouseEventArgs> SigMouseReleased;
        public event EventHandler<KeyEventArgs> SigKeyPressed;

        public PTHRenderArea()
        {
            _pth = new PTH();
            _mousePosition = Vector2.Zero;
            _isMouseDown = false;
            _lastMousePosition = Vector2.Zero;
            Camera = new RenderCamera();
            PathSelection = new PathSelection();

            // Set up Avalonia control properties
            Background = Brushes.Black;
            Focusable = true;

            // Set up event handlers
            PointerPressed += OnPointerPressed;
            PointerMoved += OnPointerMoved;
            PointerWheelChanged += OnPointerWheelChanged;
            PointerReleased += OnPointerReleased;
            KeyDown += OnKeyDown;
        }

        // Convert screen coordinates to world coordinates
        private Vector2 ScreenToWorld(Point screenPoint)
        {
            var centerX = Bounds.Width / 2.0;
            var centerY = Bounds.Height / 2.0;

            // Apply camera transformations in reverse
            var worldX = (screenPoint.X - centerX) / Camera.Zoom + Camera.Position.X;
            var worldY = (screenPoint.Y - centerY) / Camera.Zoom + Camera.Position.Y;

            return new Vector2((float)worldX, (float)worldY);
        }

        // Convert world coordinates to screen coordinates
        private Point WorldToScreen(Vector2 worldPoint)
        {
            var centerX = Bounds.Width / 2.0;
            var centerY = Bounds.Height / 2.0;

            // Apply camera transformations
            var screenX = (worldPoint.X - Camera.Position.X) * Camera.Zoom + centerX;
            var screenY = (worldPoint.Y - Camera.Position.Y) * Camera.Zoom + centerY;

            return new Point(screenX, screenY);
        }

        // Event handlers
        private void OnPointerPressed(object sender, PointerPressedEventArgs e)
        {
            var point = e.GetCurrentPoint(this);
            var worldPos = ScreenToWorld(point.Position);
            _mousePosition = worldPos;
            _isMouseDown = true;
            _lastMousePosition = worldPos;

            // Handle selection
            var nodesUnderMouse = PathNodesUnderMouse();
            if (nodesUnderMouse.Count > 0)
            {
                // Select the first node under mouse
                PathSelection.Select(new[] { nodesUnderMouse[0] });
            }
            else
            {
                // Clear selection if clicking empty space
                PathSelection.Clear();
            }

            InvalidateVisual();

            // Raise signal event
            SigMousePressed?.Invoke(this, new MouseEventArgs(point.Properties, point.Timestamp));
        }

        private void OnPointerMoved(object sender, PointerEventArgs e)
        {
            var point = e.GetCurrentPoint(this);
            var worldPos = ScreenToWorld(point.Position);
            _mousePosition = worldPos;

            // Handle dragging
            if (_isMouseDown)
            {
                var delta = worldPos - _lastMousePosition;
                _lastMousePosition = worldPos;

                // If we have selected nodes, move them
                var selected = PathSelection.All();
                if (selected.Count > 0)
                {
                    MoveSelected(delta.X, delta.Y);
                }
                else
                {
                    // Pan camera
                    Camera.NudgePosition(-delta.X, -delta.Y);
                }

                InvalidateVisual();
            }

            // Raise signal event
            SigMouseMoved?.Invoke(this, new MouseEventArgs(point.Properties, point.Timestamp));
        }

        private void OnPointerWheelChanged(object sender, PointerWheelEventArgs e)
        {
            var zoomFactor = e.Delta.Y > 0 ? 1.1f : 0.9f;
            Camera.NudgeZoom(zoomFactor);
            InvalidateVisual();

            // Raise signal event
            SigMouseScrolled?.Invoke(this, e);
        }

        private void OnPointerReleased(object sender, PointerReleasedEventArgs e)
        {
            _isMouseDown = false;

            // Raise signal event
            var point = e.GetCurrentPoint(this);
            SigMouseReleased?.Invoke(this, new MouseEventArgs(point.Properties, point.Timestamp));
        }

        private void OnKeyDown(object sender, KeyEventArgs e)
        {
            // Raise signal event
            SigKeyPressed?.Invoke(this, e);
        }

        // Render the path nodes and connections
        public override void Render(DrawingContext context)
        {
            base.Render(context);

            if (_pth == null || _pth.Count == 0)
            {
                return;
            }

            // Draw connections first (behind nodes)
            DrawConnections(context);

            // Draw nodes
            DrawNodes(context);

            // Draw selection highlights
            DrawSelectionHighlights(context);
        }

        private void DrawConnections(DrawingContext context)
        {
            foreach (var connection in _pth.GetConnections())
            {
                var startPoint = _pth.GetPoint(connection.SourceIndex);
                var endPoint = _pth.GetPoint(connection.TargetIndex);

                var startScreen = WorldToScreen(startPoint);
                var endScreen = WorldToScreen(endPoint);

                // Create connection line
                var pen = new Pen(Brushes.Gray, 2.0);
                context.DrawLine(pen, startScreen, endScreen);
            }
        }

        private void DrawNodes(DrawingContext context)
        {
            const float nodeRadius = 8.0f;

            foreach (var point in _pth.GetPoints())
            {
                var screenPos = WorldToScreen(point);

                // Draw node circle
                var ellipseGeometry = new EllipseGeometry(new Rect(
                    screenPos.X - nodeRadius,
                    screenPos.Y - nodeRadius,
                    nodeRadius * 2,
                    nodeRadius * 2));

                // Use default color for nodes (can be extended to use material colors)
                var brush = Brushes.LightBlue;
                context.DrawGeometry(brush, new Pen(Brushes.DarkBlue, 1.0), ellipseGeometry);
            }
        }

        private void DrawSelectionHighlights(DrawingContext context)
        {
            var selectedNodes = PathSelection.All();
            if (selectedNodes.Count == 0)
            {
                return;
            }

            const float highlightRadius = 12.0f;

            foreach (var selectedPoint in selectedNodes)
            {
                var screenPos = WorldToScreen(selectedPoint);

                // Draw selection highlight
                var ellipseGeometry = new EllipseGeometry(new Rect(
                    screenPos.X - highlightRadius,
                    screenPos.Y - highlightRadius,
                    highlightRadius * 2,
                    highlightRadius * 2));

                var brush = Brushes.Yellow;
                var pen = new Pen(Brushes.Orange, 2.0);
                context.DrawGeometry(brush, pen, ellipseGeometry);
            }
        }

        // Move selected nodes by the specified delta
        private void MoveSelected(float deltaX, float deltaY)
        {
            var selected = PathSelection.All();
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
                    var updated = new Vector2(point.X + deltaX, point.Y + deltaY);
                    _pth.SetPoint(index.Value, updated);
                    selected[i] = updated;
                }
            }

            PathSelection.Select(selected);
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

        public List<Vector2> PathNodesUnderMouse(float tolerance = 0.5f)
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
        public TextBlock LeftLabel { get; private set; }
        public TextBlock CenterLabel { get; private set; }
        public TextBlock RightLabel { get; private set; }

        // Status output handler
        public PTHStatusOut StatusOut { get; private set; }

        // Control scheme - exposed for testing
        public PTHControlScheme Controls => _controls;

        // Material colors dictionary - exposed for testing
        public Dictionary<SurfaceMaterial, Avalonia.Media.Color> MaterialColors { get; private set; }

        // TODO:  Render area - stub for testing (will be fully implemented when UI is available)
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
            LeftLabel = new TextBlock { Text = "Left Status" };
            CenterLabel = new TextBlock
            {
                Text = "Center Status",
                HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center
            };
            RightLabel = new TextBlock { Text = "Right Status" };
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
            if (source < 0 || target < 0 || source >= _pth.Count || target >= _pth.Count)
            {
                return;
            }

            // Create bidirectional connections like other path editors
            _pth.Connect(source, target);
            _pth.Connect(target, source);
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/pth.py:349-353
        // Original: def removeEdge(self, source: int, target: int):
        public void RemoveEdge(int source, int target)
        {
            if (source < 0 || target < 0 || source >= _pth.Count || target >= _pth.Count)
            {
                return;
            }

            // Remove bidirectional connections like other path editors
            _pth.Disconnect(source, target);
            _pth.Disconnect(target, source);
        }

        /// <summary>
        /// Updates the cached mouse position used for hit testing.
        /// </summary>
        /// <param name="x">Mouse X coordinate in world space.</param>
        /// <param name="y">Mouse Y coordinate in world space.</param>
        public void UpdateMousePosition(float x, float y)
        {
            var position = new Vector2(x, y);
            if (StatusOut != null)
            {
                StatusOut.SetMousePosition(position);
            }
            RenderArea.SetMousePosition(position);
            if (StatusOut != null)
            {
                StatusOut.UpdateStatusBar();
            }
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/pth.py:361-363
        // Original: def points_under_mouse(self) -> list[Vector2]:
        public List<Vector2> PointsUnderMouse(float tolerance = 0.5f)
        {
            return RenderArea.PathNodesUnderMouse(tolerance);
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
        // swkotor2.exe: PTH loading requires LYT file for context (room layout and walkmesh information)
        public override void Load(string filepath, string resref, ResourceType restype, byte[] data)
        {
            base.Load(filepath, resref, restype, data);

            // Search for corresponding LYT file (same resref, but .lyt extension)
            // Matching PyKotor: order = [SearchLocation.OVERRIDE, SearchLocation.CHITIN, SearchLocation.MODULES]
            if (_installation != null)
            {
                SearchLocation[] searchOrder = new[] { SearchLocation.OVERRIDE, SearchLocation.CHITIN, SearchLocation.MODULES };
                ResourceResult lytResult = _installation.Resource(resref, ResourceType.LYT, searchOrder);

                if (lytResult != null)
                {
                    // Load the LYT layout
                    try
                    {
                        LYT layout = LYTAuto.ReadLyt(lytResult.Data);
                        LoadLayout(layout);
                    }
                    catch (Exception ex)
                    {
                        System.Console.WriteLine($"Failed to load LYT layout: {ex}");
                        // Continue with PTH loading even if LYT fails
                    }
                }
                else
                {
                    // LYT file not found - show error message
                    // Matching PyKotor: BetterMessageBox with critical icon
                    string message = $"PTHEditor requires {resref}.lyt in order to load '{resref}.{restype}', but it could not be found.";
                    var errorBox = MessageBoxManager.GetMessageBoxStandard(
                        "Layout not found",
                        message,
                        ButtonEnum.Ok,
                        Icon.Error);
                    errorBox.ShowAsync();
                    // Continue with PTH loading anyway (user may still want to edit the path)
                }
            }

            // Load the PTH data
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

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/pth.py:284-286
        // Original: @status_bar_decorator def pth(self) -> PTH:
        public PTH Pth()
        {
            return _pth;
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/pth.py:288-304
        // Original: @status_bar_decorator def loadLayout(self, layout: LYT):
        // swkotor2.exe: LoadLayout loads walkmeshes for each room in the layout to provide visual context
        private void LoadLayout(LYT layout)
        {
            if (_installation == null || layout == null)
            {
                return;
            }

            List<BWM> walkmeshes = new List<BWM>();
            SearchLocation[] searchOrder = new[] { SearchLocation.OVERRIDE, SearchLocation.CHITIN, SearchLocation.MODULES };

            // For each room in the layout, try to find and load its walkmesh (WOK file)
            foreach (LYTRoom room in layout.Rooms)
            {
                if (room == null || room.Model == null)
                {
                    continue;
                }

                string modelResRef = room.Model.ToString();
                ResourceResult wokResult = _installation.Resource(modelResRef, ResourceType.WOK, searchOrder);

                if (wokResult != null)
                {
                    try
                    {
                        // Matching PyKotor: print("loadLayout", "BWM Found", f"{findBWM.resname}.{findBWM.restype}", file=self.status_out)
                        if (StatusOut != null)
                        {
                            StatusOut.Write($"loadLayout BWM Found {wokResult.ResName}.{wokResult.ResType}");
                        }

                        BWM walkmesh = BWMAuto.ReadBwm(wokResult.Data);
                        walkmeshes.Add(walkmesh);
                    }
                    catch (Exception ex)
                    {
                        System.Console.WriteLine($"Failed to load walkmesh for room {modelResRef}: {ex}");
                        // Continue with other rooms even if one fails
                    }
                }
            }

            // Set walkmeshes on render area (if supported)
            // TODO: STUB - PTHRenderArea.SetWalkmeshes() method needs to be implemented for full walkmesh rendering
            // For now, the walkmeshes are loaded but not displayed in the render area
            // This matches the Python implementation where set_walkmeshes is called on the render area
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
