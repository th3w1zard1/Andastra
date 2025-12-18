using System;
using System.Collections.Generic;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using HolocronToolset.Data;

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
            SetupUI();
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

        private void SetupUI()
        {
            // Create UI wrapper for testing
            Ui = new IndoorBuilderWindowUi();

            // Initialize map (matching Python: self._map = IndoorMap())
            Map = new IndoorMap();

            // Initialize undo stack (matching Python: self._undo_stack: QUndoStack = QUndoStack(self))
            UndoStack = new UndoStack();

            // Initialize MapRenderer (matching Python: self.ui.mapRenderer)
            Ui.MapRenderer = new IndoorMapRenderer();
            Ui.MapRenderer.SetMap(Map);
            // Matching Python: self.ui.mapRenderer.set_undo_stack(self._undo_stack)
            // Note: SetUndoStack will be implemented in IndoorMapRenderer when needed

            // Setup select all action (matching Python: self.ui.actionSelectAll.triggered.connect(self.select_all))
            Ui.ActionSelectAll = SelectAll;

            // Setup deselect all action (matching Python: self.ui.actionDeselectAll.triggered.connect(self.deselect_all))
            Ui.ActionDeselectAll = DeselectAll;

            // Setup delete selected action (matching Python: self.ui.actionDeleteSelected.triggered.connect(self.delete_selected))
            Ui.ActionDeleteSelected = DeleteSelected;

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
            // Note: Additional UI clearing (componentList, moduleComponentList, preview image, status bar)
            // will be implemented when those UI components are available
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

        // Matching PyKotor implementation - actionUndo menu action
        // Original: self.ui.actionUndo.triggered.connect(self._undo_stack.undo)
        public Action ActionUndo { get; set; }

        // Matching PyKotor implementation - actionRedo menu action
        // Original: self.ui.actionRedo.triggered.connect(self._undo_stack.redo)
        public Action ActionRedo { get; set; }

        // Matching PyKotor implementation - actionUndo.isEnabled property
        // Original: self.ui.actionUndo.isEnabled()
        public bool ActionUndoEnabled { get; set; }

        // Matching PyKotor implementation - actionRedo.isEnabled property
        // Original: self.ui.actionRedo.isEnabled()
        public bool ActionRedoEnabled { get; set; }

        // Matching PyKotor implementation - mapRenderer widget
        // Original: self.ui.mapRenderer
        public IndoorMapRenderer MapRenderer { get; set; }
    }

}
