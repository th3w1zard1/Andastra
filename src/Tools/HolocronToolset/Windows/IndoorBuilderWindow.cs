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

        private void SetupUI()
        {
            // Create UI wrapper for testing
            Ui = new IndoorBuilderWindowUi();

            // Initialize map (matching Python: self._map = IndoorMap())
            Map = new IndoorMap();

            // Initialize MapRenderer (matching Python: self.ui.mapRenderer)
            Ui.MapRenderer = new IndoorMapRenderer();
            Ui.MapRenderer.SetMap(Map);

            // Setup select all action (matching Python: self.ui.actionSelectAll.triggered.connect(self.select_all))
            Ui.ActionSelectAll = SelectAll;
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
    }

    // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/windows/indoor_builder.py
    // Original: self.ui = Ui_MainWindow() - UI wrapper class exposing all controls
    public class IndoorBuilderWindowUi
    {
        // Matching PyKotor implementation - actionSelectAll menu action
        // Original: self.ui.actionSelectAll.triggered.connect(self.select_all)
        public Action ActionSelectAll { get; set; }

        // Matching PyKotor implementation - mapRenderer widget
        // Original: self.ui.mapRenderer
        public IndoorMapRenderer MapRenderer { get; set; }
    }

}
