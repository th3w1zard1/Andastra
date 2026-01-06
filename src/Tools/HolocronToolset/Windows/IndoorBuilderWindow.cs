using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using HolocronToolset.Data;
using DuplicateRoomsCommand = HolocronToolset.Windows.DuplicateRoomsCommand;

namespace HolocronToolset.Windows
{
    // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/windows/indoor_builder.py
    // Original: class IndoorBuilder(QMainWindow):
    public class IndoorBuilderWindow : Window
    {
        private HTInstallation _installation;


        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/windows/indoor_builder.py
        // Original: def __init__(self, parent, installation):
        public IndoorBuilderWindow(Window parent = null, HTInstallation installation = null)
        {
            InitializeComponent();
            _installation = installation;

            // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/windows/indoor_builder.py:512-515
            // Original: if installation is not None:
            // Original:     self._module_kit_manager: ModuleKitManager = ModuleKitManager(installation)
            // Original: else:
            // Original:     self._module_kit_manager = None
            if (installation != null)
            {
                ModuleKitManager = new ModuleKitManager(installation);
            }
            else
            {
                ModuleKitManager = null;
            }

            SetupUI();

            // Disable ActionSettings when no installation is provided (matching Python test expectation)
            // Original: assert builder.ui.actionSettings.isEnabled() is False
            if (Ui != null)
            {
                Ui.ActionSettingsEnabled = (_installation != null);
            }
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
            Title = "Indoor Builder";
            Width = 1200;
            Height = 800;

            var panel = new StackPanel();
            var titleLabel = new TextBlock
            {
                Text = "Indoor Builder",
                FontSize = 18,
                HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center
            };
            panel.Children.Add(titleLabel);
            Content = panel;
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/windows/indoor_builder.py
        // Original: self.ui = Ui_MainWindow() - UI wrapper class exposing all controls
        public IndoorBuilderWindowUi Ui { get; private set; }

        // Matching PyKotor implementation - self._map property
        // Original: self._map = IndoorMap()
        public IndoorMap Map { get; private set; }

        // Matching PyKotor implementation - self._undo_stack property
        // Original: self._undo_stack: QUndoStack = QUndoStack(self)
        public UndoStack UndoStack { get; private set; }

        // Matching PyKotor implementation - self._clipboard property
        // Original: self._clipboard: list[RoomClipboardData] = []
        // Intentionally hides base Clipboard property (IClipboard? - system clipboard)
        // to provide domain-specific room clipboard (List<RoomClipboardData>)
        public new List<RoomClipboardData> Clipboard { get; private set; }

        // Matching PyKotor implementation - self._module_kit_manager property
        // Original: self._module_kit_manager: ModuleKitManager | None
        // Module kit management (lazy loading) - handles converting game modules to kit-like components
        public ModuleKitManager ModuleKitManager { get; private set; }

        private void SetupUI()
        {
            // Create UI wrapper for testing
            Ui = new IndoorBuilderWindowUi();

            // Initialize map (matching Python: self._map = IndoorMap())
            Map = new IndoorMap();

            // Initialize undo stack (matching Python: self._undo_stack: QUndoStack = QUndoStack(self))
            UndoStack = new UndoStack();

            // Initialize clipboard (matching Python: self._clipboard: list[RoomClipboardData] = [])
            Clipboard = new List<RoomClipboardData>();

            // Initialize MapRenderer (matching Python: self.ui.mapRenderer)
            Ui.MapRenderer = new IndoorMapRenderer();
            Ui.MapRenderer.SetMap(Map);
            // Matching Python: self.ui.mapRenderer.set_undo_stack(self._undo_stack)
            Ui.MapRenderer.SetUndoStack(UndoStack);
            // Matching Python: self.ui.mapRenderer.set_status_callback(self._refresh_status_bar)
            Ui.MapRenderer.SetStatusCallback(RefreshStatusBar);

            // Setup select all action (matching Python: self.ui.actionSelectAll.triggered.connect(self.select_all))
            Ui.ActionSelectAll = SelectAll;

            // Setup deselect all action (matching Python: self.ui.actionDeselectAll.triggered.connect(self.deselect_all))
            Ui.ActionDeselectAll = DeselectAll;

            // Setup delete selected action (matching Python: self.ui.actionDeleteSelected.triggered.connect(self.delete_selected))
            Ui.ActionDeleteSelected = DeleteSelected;

            // Setup duplicate action (matching Python: self.ui.actionDuplicate.triggered.connect(self.duplicate_selected))
            Ui.ActionDuplicate = DuplicateSelected;

            // Setup undo/redo actions (matching Python lines 690-703)
            // Matching Python: self.ui.actionUndo.triggered.connect(self._undo_stack.undo)
            Ui.ActionUndo = () => UndoStack.Undo();
            // Matching Python: self.ui.actionRedo.triggered.connect(self._undo_stack.redo)
            Ui.ActionRedo = () => UndoStack.Redo();

            // Matching Python: self._undo_stack.canUndoChanged.connect(self.ui.actionUndo.setEnabled)
            UndoStack.CanUndoChanged += (sender, canUndo) => Ui.ActionUndoEnabled = canUndo;
            // Matching Python: self._undo_stack.canRedoChanged.connect(self.ui.actionRedo.setEnabled)
            UndoStack.CanRedoChanged += (sender, canRedo) => Ui.ActionRedoEnabled = canRedo;

            // Matching Python lines 702-703: self.ui.actionUndo.setEnabled(False); self.ui.actionRedo.setEnabled(False)
            Ui.ActionUndoEnabled = false;
            Ui.ActionRedoEnabled = false;

            // Matching Python line 609: self.ui.actionZoomIn.triggered.connect(lambda: self.ui.mapRenderer.zoom_in_camera(ZOOM_STEP))
            Ui.ActionZoomIn = () => Ui.MapRenderer.ZoomInCamera(0.2f); // ZOOM_STEP = 0.2

            // Matching Python line 610: self.ui.actionZoomOut.triggered.connect(lambda: self.ui.mapRenderer.zoom_in_camera(-ZOOM_STEP))
            Ui.ActionZoomOut = () => Ui.MapRenderer.ZoomInCamera(-0.2f); // ZOOM_STEP = 0.2

            // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/windows/indoor_builder.py:632-638
            // Original: self.ui.gridSizeSpin.valueChanged.connect(self.ui.mapRenderer.set_grid_size)
            // Original: self.ui.rotSnapSpin.valueChanged.connect(self.ui.mapRenderer.set_rotation_snap)
            // Original: self.ui.snapToHooksCheck.toggled.connect(self.ui.mapRenderer.set_snap_to_hooks)
            // Setup spinbox bindings for grid size and rotation snap
            Ui.GridSizeSpinValueChanged = (value) => Ui.MapRenderer.SetGridSize((float)value);
            Ui.RotSnapSpinValueChanged = (value) => Ui.MapRenderer.SetRotationSnap((float)value);
            // Setup checkbox binding for snap to hooks
            Ui.SnapToHooksCheckToggled = (value) => Ui.MapRenderer.SetSnapToHooks(value);

            // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/windows/indoor_builder.py:1222-1247
            // Original: def _initialize_options_ui(self):
            InitializeOptionsUI();
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/windows/indoor_builder.py:1222-1247
        // Original: def _initialize_options_ui(self):
        //     """Initialize Options UI to match renderer's initial state."""
        private void InitializeOptionsUI()
        {
            // Matching Python line 1224: renderer = self.ui.mapRenderer
            var renderer = Ui.MapRenderer;

            // Matching Python lines 1226-1231: Block signals temporarily to avoid triggering updates during initialization
            // In Avalonia/C#, we use a flag to prevent event handlers from firing during initialization
            Ui.BlockSpinboxSignals = true;
            Ui.BlockCheckboxSignals = true;

            // Matching Python lines 1234-1239: Set UI to match renderer state
            // Matching Python line 1234: self.ui.snapToGridCheck.setChecked(renderer.snap_to_grid)
            // Note: snapToGridCheck will be implemented when needed
            // Matching Python line 1235: self.ui.snapToHooksCheck.setChecked(renderer.snap_to_hooks)
            Ui.SetSnapToHooksCheckChecked(renderer.SnapToHooks);
            // Matching Python line 1238: self.ui.gridSizeSpin.setValue(renderer.grid_size)
            Ui.GridSizeSpinValue = (decimal)renderer.GridSize;
            // Matching Python line 1239: self.ui.rotSnapSpin.setValue(int(renderer.rotation_snap))
            Ui.RotSnapSpinValue = (decimal)renderer.RotationSnap;

            // Matching Python lines 1242-1247: Unblock signals
            Ui.BlockSpinboxSignals = false;
            Ui.BlockCheckboxSignals = false;
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/windows/indoor_builder.py:1751-1755
        // Original: def select_all(self):
        private void SelectAll()
        {
            // Matching Python: self.ui.mapRenderer.select_all_rooms()
            // Original implementation:
            // def select_all(self):
            //     renderer = self.ui.mapRenderer
            //     renderer.clear_selected_rooms()
            //     for room in self._map.rooms:
            //         renderer.select_room(room, clear_existing=False)
            var renderer = Ui.MapRenderer;
            renderer.ClearSelectedRooms();
            foreach (var room in Map.Rooms)
            {
                renderer.SelectRoom(room, clearExisting: false);
            }
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/windows/indoor_builder.py:1756-1764
        // Original: def deselect_all(self):
        private void DeselectAll()
        {
            // Matching Python: self.ui.mapRenderer.clear_selected_rooms()
            // Original implementation:
            // def deselect_all(self):
            //     self.ui.mapRenderer.clear_selected_rooms()
            //     self.ui.mapRenderer.set_cursor_component(None)
            //     self.ui.componentList.clearSelection()
            //     self.ui.componentList.setCurrentItem(None)
            //     self.ui.moduleComponentList.clearSelection()
            //     self.ui.moduleComponentList.setCurrentItem(None)
            //     self._set_preview_image(None)
            //     self._refresh_status_bar()
            var renderer = Ui.MapRenderer;
            renderer.ClearSelectedRooms();
            // Matching Python line 1758: self.ui.mapRenderer.set_cursor_component(None)
            renderer.SetCursorComponent(null);
            // Note: Additional UI clearing (componentList, moduleComponentList, preview image, status bar)
            // These will be implemented when those UI components are available
            // Matching Python lines 1759-1764:
            // self.ui.componentList.clearSelection()
            // self.ui.componentList.setCurrentItem(None)
            // self.ui.moduleComponentList.clearSelection()
            // self.ui.moduleComponentList.setCurrentItem(None)
            // self._set_preview_image(None)
            // self._refresh_status_bar()
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/windows/indoor_builder.py:1628-1640
        // Original: def delete_selected(self):
        private void DeleteSelected()
        {
            // Matching Python implementation:
            // def delete_selected(self):
            //     selected = self.ui.mapRenderer.selected_rooms()
            //     if not selected:
            //         return
            //     cmd = DeleteRoomsCommand(self._map, selected)
            //     self._undo_stack.push(cmd)
            var renderer = Ui.MapRenderer;
            var selected = renderer.SelectedRooms();
            if (selected == null || selected.Count == 0)
            {
                return;
            }

            var cmd = new DeleteRoomsCommand(Map, selected);
            UndoStack.Push(cmd);
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/windows/indoor_builder.py:1650-1674
        // Original: def duplicate_selected(self):
        private void DuplicateSelected()
        {
            // Matching Python implementation:
            // def duplicate_selected(self):
            //     rooms: list[IndoorMapRoom] = self.ui.mapRenderer.selected_rooms()
            //     if not rooms:
            //         return
            //     duplicate_cmd = DuplicateRoomsCommand(
            //         self._map,
            //         rooms,
            //         Vector3(DUPLICATE_OFFSET_X, DUPLICATE_OFFSET_Y, DUPLICATE_OFFSET_Z),
            //         self._invalidate_rooms,
            //     )
            //     self._undo_stack.push(duplicate_cmd)
            //     # Select the duplicates
            //     self.ui.mapRenderer.clear_selected_rooms()
            //     for room in duplicate_cmd.duplicates:
            //         self.ui.mapRenderer.select_room(room, clear_existing=False)
            var renderer = Ui.MapRenderer;
            var selected = renderer.SelectedRooms();
            if (selected == null || selected.Count == 0)
            {
                return;
            }

            var duplicateCmd = new DuplicateRoomsCommand(Map, selected);
            UndoStack.Push(duplicateCmd);

            // Select the duplicates (matching Python lines 1669-1671)
            renderer.ClearSelectedRooms();
            foreach (var room in duplicateCmd.Duplicates)
            {
                renderer.SelectRoom(room, clearExisting: false);
            }
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/windows/indoor_builder.py:1770-1773
        // Original: def reset_view(self):
        public void ResetView()
        {
            // Matching Python: self.ui.mapRenderer.set_camera_position(DEFAULT_CAMERA_POSITION_X, DEFAULT_CAMERA_POSITION_Y)
            Ui.MapRenderer.SetCameraPosition(0.0f, 0.0f); // DEFAULT_CAMERA_POSITION_X/Y = 0.0
            // Matching Python: self.ui.mapRenderer.set_camera_rotation(DEFAULT_CAMERA_ROTATION)
            Ui.MapRenderer.SetCameraRotation(0.0f); // DEFAULT_CAMERA_ROTATION = 0.0
            // Matching Python: self.ui.mapRenderer.set_camera_zoom(DEFAULT_CAMERA_ZOOM)
            Ui.MapRenderer.SetCameraZoom(1.0f); // DEFAULT_CAMERA_ZOOM = 1.0
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/windows/indoor_builder.py:1775-1782
        // Original: def center_on_selection(self):
        public void CenterOnSelection()
        {
            // Matching Python line 1776: rooms = self.ui.mapRenderer.selected_rooms()
            var rooms = Ui.MapRenderer.SelectedRooms();
            // Matching Python line 1777: if not rooms: return
            if (rooms == null || rooms.Count == 0)
            {
                return;
            }

            // Matching Python lines 1780-1781: Calculate average position
            // cx = sum(r.position.x for r in rooms) / len(rooms)
            // cy = sum(r.position.y for r in rooms) / len(rooms)
            float cx = rooms.Sum(r => r.Position.X) / rooms.Count;
            float cy = rooms.Sum(r => r.Position.Y) / rooms.Count;

            // Matching Python line 1782: self.ui.mapRenderer.set_camera_position(cx, cy)
            Ui.MapRenderer.SetCameraPosition(cx, cy);
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/windows/indoor_builder.py:1249-1256
        // Original: def _refresh_status_bar(self, mouse_pos: QPoint | Vector2 | None = None, mouse_buttons: set[int | Qt.MouseButton] | None = None, keys: set[int | Qt.Key] | None = None):
        private void RefreshStatusBar(System.Numerics.Vector2? mousePos, System.Collections.Generic.HashSet<int> mouseButtons, System.Collections.Generic.HashSet<int> keys)
        {
            // Matching Python line 1002: self._update_status_bar(screen, buttons, keys)
            UpdateStatusBar(mousePos, mouseButtons, keys);
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/windows/indoor_builder.py:1004-1171
        // Original: def _update_status_bar(self, screen: QPoint | Vector2 | None = None, buttons: set[int | Qt.MouseButton] | None = None, keys: set[int | Qt.Key] | None = None):
        /// <summary>
        /// Rich status bar mirroring Module Designer style.
        /// Updates status bar with mouse position, hover room, selection, keys/buttons, and mode/status.
        /// </summary>
        private void UpdateStatusBar(System.Numerics.Vector2? mousePos, System.Collections.Generic.HashSet<int> mouseButtons, System.Collections.Generic.HashSet<int> keys)
        {
            var renderer = Ui.MapRenderer;
            if (renderer == null)
            {
                return;
            }

            // Matching Python lines 1013-1021: Resolve screen coords
            System.Numerics.Vector2 screenVec;
            if (mousePos.HasValue)
            {
                screenVec = mousePos.Value;
            }
            else
            {
                // If no mouse position provided, use (0, 0) as default
                // In a full implementation, this would get cursor position from the renderer
                screenVec = new System.Numerics.Vector2(0, 0);
            }

            // Matching Python lines 1023-1039: Resolve buttons/keys - ensure they are sets
            if (mouseButtons == null)
            {
                mouseButtons = new System.Collections.Generic.HashSet<int>();
                // In a full implementation, this would get mouse buttons from renderer.mouse_down()
            }
            if (keys == null)
            {
                keys = new System.Collections.Generic.HashSet<int>();
                // In a full implementation, this would get keys from renderer.keys_down()
            }

            // Matching Python line 1041: world: Vector3 = renderer.to_world_coords(screen_vec.x, screen_vec.y)
            // Note: to_world_coords method needs to be implemented in IndoorMapRenderer
            // For now, we'll use screen coordinates as a placeholder
            System.Numerics.Vector3 world = new System.Numerics.Vector3(screenVec.X, screenVec.Y, 0.0f);

            // Matching Python line 1042: hover_room: IndoorMapRoom | None = renderer.room_under_mouse()
            // Note: room_under_mouse method needs to be implemented in IndoorMapRenderer
            IndoorMapRoom hoverRoom = null; // Placeholder - will be implemented when room_under_mouse is available

            // Matching Python line 1043: sel_rooms = renderer.selected_rooms()
            var selRooms = renderer.SelectedRooms();

            // Matching Python line 1044: sel_hook = renderer.selected_hook()
            // Note: selected_hook method needs to be implemented in IndoorMapRenderer
            Tuple<IndoorMapRoom, int> selHook = null; // Placeholder - will be implemented when selected_hook is available

            // Matching Python lines 1046-1052: Mouse/hover
            string hoverText;
            if (hoverRoom != null && hoverRoom.Component != null)
            {
                hoverText = $"<b><span style=\"{EmojiStyle}\">🧩</span>&nbsp;Hover:</b> <span style='color:#0055B0'>{System.Security.SecurityElement.Escape(hoverRoom.Component.Name)}</span>";
            }
            else
            {
                hoverText = $"<b><span style=\"{EmojiStyle}\">🧩</span>&nbsp;Hover:</b> <span style='color:#a6a6a6'><i>None</i></span>";
            }
            Ui.StatusBarHoverText = hoverText;

            // Matching Python lines 1054-1058: Mouse coordinates
            string mouseText = $"<b><span style=\"{EmojiStyle}\">🖱</span>&nbsp;Coords:</b> " +
                               $"<span style='color:#0055B0'>{world.X:F2}</span>, " +
                               $"<span style='color:#228800'>{world.Y:F2}</span>";
            Ui.StatusBarMouseText = mouseText;

            // Matching Python lines 1060-1068: Selection
            string selText;
            if (selHook != null)
            {
                var hookRoom = selHook.Item1;
                var hookIdx = selHook.Item2;
                if (hookRoom != null && hookRoom.Component != null)
                {
                    selText = $"<b><span style=\"{EmojiStyle}\">🎯</span>&nbsp;Selected Hook:</b> <span style='color:#0055B0'>{System.Security.SecurityElement.Escape(hookRoom.Component.Name)}</span> (#{hookIdx})";
                }
                else
                {
                    selText = $"<b><span style=\"{EmojiStyle}\">🎯</span>&nbsp;Selected Hook:</b> <span style='color:#a6a6a6'><i>None</i></span>";
                }
            }
            else if (selRooms != null && selRooms.Count > 0)
            {
                selText = $"<b><span style=\"{EmojiStyle}\">🟦</span>&nbsp;Selected Rooms:</b> <span style='color:#0055B0'>{selRooms.Count}</span>";
            }
            else
            {
                selText = $"<b><span style=\"{EmojiStyle}\">🟦</span>&nbsp;Selected:</b> <span style='color:#a6a6a6'><i>None</i></span>";
            }
            Ui.StatusBarSelectionText = selText;

            // Matching Python lines 1073-1094: Keys/buttons (sorted with modifiers first)
            var keysSorted = SortWithModifiers(keys, GetKeyString, "QtKey");
            var buttonsSorted = SortWithModifiers(mouseButtons, GetButtonString, "QtMouse");

            // Matching Python lines 1135-1149: Format keys and buttons
            string keysText = FormatItems(keysSorted, GetKeyString, "#a13ac8");
            string buttonsText = FormatItems(buttonsSorted, GetButtonString, "#228800");
            string sep = (keysText.Length > 0 && buttonsText.Length > 0) ? " + " : "";
            string keysButtonsText = $"<b><span style=\"{EmojiStyle}\">⌨</span>&nbsp;Keys/<span style=\"{EmojiStyle}\">🖱</span>&nbsp;Buttons:</b> {keysText}{sep}{buttonsText}";
            Ui.StatusBarKeysText = keysButtonsText;

            // Matching Python lines 1154-1171: Mode/status line
            var modeParts = new List<string>();
            // Note: _painting_walkmesh and _current_material need to be implemented
            // if (_paintingWalkmesh)
            // {
            //     var material = _currentMaterial();
            //     string matText = material != null ? material.Name.Replace("_", " ").Title() : "Material";
            //     modeParts.Add($"<span style='color:#c46811'>Paint: {matText}</span>");
            // }
            // Note: _colorize_materials needs to be implemented
            // if (_colorizeMaterials)
            // {
            //     modeParts.Add("Colorized");
            // }
            if (renderer.SnapToGrid)
            {
                modeParts.Add("Grid Snap");
            }
            if (renderer.SnapToHooks)
            {
                modeParts.Add("Hook Snap");
            }
            string modeText = $"<b><span style=\"{EmojiStyle}\">ℹ</span>&nbsp;Status:</b> " +
                             (modeParts.Count > 0 ? string.Join(" | ", modeParts) : "<span style='color:#a6a6a6'><i>Idle</i></span>");
            Ui.StatusBarModeText = modeText;
        }

        // Matching PyKotor implementation - emoji style constant
        // Original: self._emoji_style = "font-size:12pt; font-family:'Segoe UI Emoji','Apple Color Emoji','Noto Color Emoji','EmojiOne','Twemoji Mozilla','Segoe UI Symbol',sans-serif; vertical-align:middle;"
        private const string EmojiStyle = "font-size:12pt; font-family:'Segoe UI Emoji','Apple Color Emoji','Noto Color Emoji','EmojiOne','Twemoji Mozilla','Segoe UI Symbol',sans-serif; vertical-align:middle;";

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/windows/indoor_builder.py:1074-1094
        // Original: def sort_with_modifiers(...)
        private List<int> SortWithModifiers(System.Collections.Generic.HashSet<int> items, Func<int, string> getStringFunc, string qtEnumType)
        {
            if (items == null || items.Count == 0)
            {
                return new List<int>();
            }

            var modifiers = new List<int>();
            var normal = new List<int>();

            if (qtEnumType == "QtKey")
            {
                // Matching Python line 1089: modifier_set = {Qt.Key.Key_Control, Qt.Key.Key_Shift, Qt.Key.Key_Alt, Qt.Key.Key_Meta}
                // Note: These are Qt key codes - in C# we'd use Avalonia Input.Key enum values
                // For now, we'll use placeholder values that match common key codes
                var modifierSet = new System.Collections.Generic.HashSet<int>
                {
                    17, // Control
                    16, // Shift
                    18, // Alt
                    91  // Meta/Windows
                };
                foreach (var item in items)
                {
                    if (modifierSet.Contains(item))
                    {
                        modifiers.Add(item);
                    }
                    else
                    {
                        normal.Add(item);
                    }
                }
            }
            else
            {
                normal.AddRange(items);
            }

            modifiers.Sort((a, b) => string.Compare(getStringFunc(a), getStringFunc(b), StringComparison.Ordinal));
            normal.Sort((a, b) => string.Compare(getStringFunc(a), getStringFunc(b), StringComparison.Ordinal));
            return modifiers.Concat(normal).ToList();
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/windows/indoor_builder.py:1096-1112
        // Original: def get_qt_key_string_local(key: int | Qt.Key | Qt.MouseButton) -> str:
        private string GetKeyString(int key)
        {
            // Matching Python: Remove "Key_" prefix if present
            // In a full implementation, this would use Avalonia Input.Key enum
            // For now, return a simple string representation
            return $"Key{key}";
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/windows/indoor_builder.py:1114-1130
        // Original: def get_qt_button_string_local(btn: int | Qt.MouseButton | Qt.Key) -> str:
        private string GetButtonString(int button)
        {
            // Matching Python: Remove "Button" suffix if present
            // In a full implementation, this would use Avalonia Input.MouseButton enum
            // For now, return a simple string representation
            return $"Btn{button}";
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/windows/indoor_builder.py:1135-1145
        // Original: def fmt(seq: list[int | Qt.Key | Qt.MouseButton], formatter: Callable[[int | Qt.Key | Qt.MouseButton], str], color: str) -> str:
        private string FormatItems(List<int> seq, Func<int, string> formatter, string color)
        {
            if (seq == null || seq.Count == 0)
            {
                return "";
            }
            var formattedItems = seq.Select(item => System.Security.SecurityElement.Escape(formatter(item))).ToList();
            var coloredItems = formattedItems.Select(item => $"<span style='color: {color}'>{item}</span>").ToList();
            return string.Join("&nbsp;+&nbsp;", coloredItems);
        }
    }

    // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/windows/indoor_builder.py
    // Original: self.ui = Ui_MainWindow() - UI wrapper class exposing all controls
    public class IndoorBuilderWindowUi
    {
        // Matching PyKotor implementation - actionSelectAll menu action
        // Original: self.ui.actionSelectAll.triggered.connect(self.select_all)
        public Action ActionSelectAll { get; set; }

        // Matching PyKotor implementation - actionDeselectAll menu action
        // Original: self.ui.actionDeselectAll.triggered.connect(self.deselect_all)
        public Action ActionDeselectAll { get; set; }

        // Matching PyKotor implementation - actionDeleteSelected menu action
        // Original: self.ui.actionDeleteSelected.triggered.connect(self.delete_selected)
        public Action ActionDeleteSelected { get; set; }

        // Matching PyKotor implementation - actionDuplicate menu action
        // Original: self.ui.actionDuplicate.triggered.connect(self.duplicate_selected)
        public Action ActionDuplicate { get; set; }

        // Matching PyKotor implementation - actionUndo menu action
        // Original: self.ui.actionUndo.triggered.connect(self._undo_stack.undo)
        public Action ActionUndo { get; set; }

        // Matching PyKotor implementation - actionSettings menu action
        // Original: self.ui.actionSettings - settings dialog action, disabled when no installation
        public Action ActionSettings { get; set; }

        // Matching PyKotor implementation - actionRedo menu action
        // Original: self.ui.actionRedo.triggered.connect(self._undo_stack.redo)
        public Action ActionRedo { get; set; }

        // Matching PyKotor implementation - actionUndo.isEnabled property
        // Original: self.ui.actionUndo.isEnabled()
        public bool ActionUndoEnabled { get; set; }

        // Matching PyKotor implementation - actionRedo.isEnabled property
        // Original: self.ui.actionRedo.isEnabled()
        public bool ActionRedoEnabled { get; set; }

        // Matching PyKotor implementation - actionSettings.isEnabled property
        // Original: self.ui.actionSettings.isEnabled()
        public bool ActionSettingsEnabled { get; set; }

        // Matching PyKotor implementation - mapRenderer widget
        // Original: self.ui.mapRenderer
        public IndoorMapRenderer MapRenderer { get; set; }

        // Matching PyKotor implementation - actionZoomIn menu action
        // Original: self.ui.actionZoomIn.triggered.connect(lambda: self.ui.mapRenderer.zoom_in_camera(ZOOM_STEP))
        public Action ActionZoomIn { get; set; }

        // Matching PyKotor implementation - actionZoomOut menu action
        // Original: self.ui.actionZoomOut.triggered.connect(lambda: self.ui.mapRenderer.zoom_in_camera(-ZOOM_STEP))
        public Action ActionZoomOut { get; set; }

        // Matching PyKotor implementation - gridSizeSpin widget
        // Original: self.ui.gridSizeSpin (QDoubleSpinBox)
        // Matching PyKotor UI file: Tools/HolocronToolset/src/ui/windows/indoor_builder.ui:355-368
        // Original: <widget class="QDoubleSpinBox" name="gridSizeSpin">
        // Original:   <property name="minimum"><double>0.5</double></property>
        // Original:   <property name="maximum"><double>10.0</double></property>
        // Original:   <property name="singleStep"><double>0.5</double></property>
        // Original:   <property name="value"><double>1.0</double></property>
        // Grid size spinbox minimum property (matching UI file: minimum = 0.5)
        private const decimal GridSizeSpinMinimum = 0.5m;
        public decimal GridSizeSpinMinimumValue
        {
            get { return GridSizeSpinMinimum; }
        }

        // Grid size spinbox maximum property (matching UI file: maximum = 10.0)
        private const decimal GridSizeSpinMaximum = 10.0m;
        public decimal GridSizeSpinMaximumValue
        {
            get { return GridSizeSpinMaximum; }
        }

        // Grid size spinbox value property (decimal for NumericUpDown compatibility)
        // Values are clamped to min/max range to match QDoubleSpinBox behavior
        private decimal _gridSizeSpinValue = 1.0m; // DEFAULT_GRID_SIZE = 1.0
        public decimal GridSizeSpinValue
        {
            get { return _gridSizeSpinValue; }
            set
            {
                // Clamp value to min/max range (matching QDoubleSpinBox behavior)
                decimal clampedValue = value;
                if (clampedValue < GridSizeSpinMinimum)
                {
                    clampedValue = GridSizeSpinMinimum;
                }
                else if (clampedValue > GridSizeSpinMaximum)
                {
                    clampedValue = GridSizeSpinMaximum;
                }

                if (_gridSizeSpinValue != clampedValue)
                {
                    _gridSizeSpinValue = clampedValue;
                    // Trigger value changed event if signals are not blocked
                    if (!BlockSpinboxSignals && GridSizeSpinValueChanged != null)
                    {
                        GridSizeSpinValueChanged((double)clampedValue);
                    }
                }
            }
        }

        // Matching PyKotor implementation - rotSnapSpin widget
        // Original: self.ui.rotSnapSpin (QSpinBox)
        // Rotation snap spinbox value property (decimal for NumericUpDown compatibility)
        private decimal _rotSnapSpinValue = 15m; // DEFAULT_ROTATION_SNAP = 15
        public decimal RotSnapSpinValue
        {
            get { return _rotSnapSpinValue; }
            set
            {
                if (_rotSnapSpinValue != value)
                {
                    _rotSnapSpinValue = value;
                    // Trigger value changed event if signals are not blocked
                    if (!BlockSpinboxSignals && RotSnapSpinValueChanged != null)
                    {
                        RotSnapSpinValueChanged((double)value);
                    }
                }
            }
        }

        // Matching PyKotor implementation - blockSignals functionality
        // Original: self.ui.gridSizeSpin.blockSignals(True/False)
        // Original: self.ui.rotSnapSpin.blockSignals(True/False)
        // Original: self.ui.snapToHooksCheck.blockSignals(True/False)
        // Flag to prevent value changed events from firing during initialization
        public bool BlockSpinboxSignals { get; set; } = false;
        public bool BlockCheckboxSignals { get; set; } = false;

        // Matching PyKotor implementation - valueChanged signal/event
        // Original: self.ui.gridSizeSpin.valueChanged.connect(self.ui.mapRenderer.set_grid_size)
        // Action to call when grid size spinbox value changes
        public Action<double> GridSizeSpinValueChanged { get; set; }

        // Matching PyKotor implementation - valueChanged signal/event
        // Original: self.ui.rotSnapSpin.valueChanged.connect(self.ui.mapRenderer.set_rotation_snap)
        // Action to call when rotation snap spinbox value changes
        public Action<double> RotSnapSpinValueChanged { get; set; }

        // Matching PyKotor implementation - snapToHooksCheck widget
        // Original: self.ui.snapToHooksCheck (QCheckBox)
        // Snap to hooks checkbox checked property
        private bool _snapToHooksCheckChecked = true; // Default matches renderer default (SnapToHooks = true)
        public bool SnapToHooksCheckChecked
        {
            get { return _snapToHooksCheckChecked; }
            set
            {
                if (_snapToHooksCheckChecked != value)
                {
                    _snapToHooksCheckChecked = value;
                    // Trigger toggled event if signals are not blocked
                    if (!BlockCheckboxSignals && SnapToHooksCheckToggled != null)
                    {
                        SnapToHooksCheckToggled(value);
                    }
                }
            }
        }

        // Matching PyKotor implementation - toggled signal/event
        // Original: self.ui.snapToHooksCheck.toggled.connect(self.ui.mapRenderer.set_snap_to_hooks)
        // Action to call when snap to hooks checkbox is toggled
        public Action<bool> SnapToHooksCheckToggled { get; set; }

        // Matching PyKotor implementation - setChecked method
        // Original: self.ui.snapToHooksCheck.setChecked(value)
        // Method to set snap to hooks checkbox checked state programmatically (for testing and initialization)
        public void SetSnapToHooksCheckChecked(bool value)
        {
            SnapToHooksCheckChecked = value;
        }

        // Matching PyKotor implementation - snapToHooksCheck widget accessor
        // Original: self.ui.snapToHooksCheck (QCheckBox)
        // Property to access checkbox for testing (matches Python API: builder.ui.snapToHooksCheck.setChecked())
        private SnapToHooksCheckboxWrapper _snapToHooksCheck;
        public SnapToHooksCheckboxWrapper SnapToHooksCheck
        {
            get
            {
                if (_snapToHooksCheck == null)
                {
                    _snapToHooksCheck = new SnapToHooksCheckboxWrapper(this);
                }
                return _snapToHooksCheck;
            }
        }

        // Matching PyKotor implementation - setValue method
        // Original: self.ui.gridSizeSpin.setValue(value)
        // Method to set grid size spinbox value programmatically (for testing and initialization)
        // Values are automatically clamped to min/max range (matching QDoubleSpinBox behavior)
        public void SetGridSizeSpinValue(double value)
        {
            GridSizeSpinValue = (decimal)value;
        }

        // Matching PyKotor implementation - gridSizeSpin widget accessor
        // Original: self.ui.gridSizeSpin (QDoubleSpinBox)
        // Property to access spinbox for testing (matches Python API: builder.ui.gridSizeSpin.value(), builder.ui.gridSizeSpin.minimum(), builder.ui.gridSizeSpin.maximum())
        private GridSizeSpinboxWrapper _gridSizeSpin;
        public GridSizeSpinboxWrapper GridSizeSpin
        {
            get
            {
                if (_gridSizeSpin == null)
                {
                    _gridSizeSpin = new GridSizeSpinboxWrapper(this);
                }
                return _gridSizeSpin;
            }
        }

        // Matching PyKotor implementation - setValue method
        // Original: self.ui.rotSnapSpin.setValue(value)
        // Method to set rotation snap spinbox value programmatically (for testing and initialization)
        public void SetRotSnapSpinValue(int value)
        {
            RotSnapSpinValue = (decimal)value;
        }

        // Matching PyKotor implementation - status bar label text properties
        // Original: self._mouse_label.setText(...), self._hover_label.setText(...), etc.
        // These properties store the status bar text that can be displayed when UI is available
        public string StatusBarMouseText { get; set; } = "";
        public string StatusBarHoverText { get; set; } = "";
        public string StatusBarSelectionText { get; set; } = "";
        public string StatusBarKeysText { get; set; } = "";
        public string StatusBarModeText { get; set; } = "";
    }

    // Matching PyKotor implementation - checkbox wrapper for API compatibility
    // Original: self.ui.snapToHooksCheck.setChecked(value)
    // Wrapper class to match Python API where checkbox has setChecked method
    public class SnapToHooksCheckboxWrapper
    {
        private readonly IndoorBuilderWindowUi _ui;

        public SnapToHooksCheckboxWrapper(IndoorBuilderWindowUi ui)
        {
            _ui = ui;
        }

        // Matching PyKotor implementation - setChecked method
        // Original: self.ui.snapToHooksCheck.setChecked(value)
        public void SetChecked(bool value)
        {
            _ui.SetSnapToHooksCheckChecked(value);
        }

        // Matching PyKotor implementation - isChecked method
        // Original: self.ui.snapToHooksCheck.isChecked()
        public bool IsChecked()
        {
            return _ui.SnapToHooksCheckChecked;
        }
    }

    // Matching PyKotor implementation - gridSizeSpin widget wrapper for API compatibility
    // Original: self.ui.gridSizeSpin (QDoubleSpinBox)
    // Wrapper class to match Python API where spinbox has setValue, value, minimum, and maximum methods
    public class GridSizeSpinboxWrapper
    {
        private readonly IndoorBuilderWindowUi _ui;

        public GridSizeSpinboxWrapper(IndoorBuilderWindowUi ui)
        {
            _ui = ui;
        }

        // Matching PyKotor implementation - setValue method
        // Original: self.ui.gridSizeSpin.setValue(value)
        public void SetValue(double value)
        {
            _ui.SetGridSizeSpinValue(value);
        }

        // Matching PyKotor implementation - value method
        // Original: self.ui.gridSizeSpin.value()
        public double Value()
        {
            return (double)_ui.GridSizeSpinValue;
        }

        // Matching PyKotor implementation - minimum method
        // Original: self.ui.gridSizeSpin.minimum()
        public double Minimum()
        {
            return (double)_ui.GridSizeSpinMinimumValue;
        }

        // Matching PyKotor implementation - maximum method
        // Original: self.ui.gridSizeSpin.maximum()
        public double Maximum()
        {
            return (double)_ui.GridSizeSpinMaximumValue;
        }
    }

}
