using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using AuroraEngine.Common.Resources;

namespace HolocronToolset.NET.Dialogs
{
    // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/dialogs/load_from_location_result.py
    // Original: class LoadFromLocationResultDialog(QMainWindow):
    public partial class LoadFromLocationResultDialog : Window
    {
        private DataGrid _tableWidget;
        private TextBox _searchEdit;
        private Button _openButton;
        private Button _extractButton;
        private List<FileResource> _resources;

        // Public parameterless constructor for XAML
        public LoadFromLocationResultDialog() : this(null, null)
        {
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/dialogs/load_from_location_result.py
        // Original: def __init__(self, parent, resources):
        public LoadFromLocationResultDialog(Window parent, List<FileResource> resources)
        {
            InitializeComponent();
            Title = "Load From Location Result";
            Width = 1000;
            Height = 700;
            _resources = resources ?? new List<FileResource>();
            SetupUI();
            PopulateResources();
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
            var mainPanel = new StackPanel { Margin = new Avalonia.Thickness(10), Spacing = 10 };

            var searchPanel = new StackPanel { Orientation = Avalonia.Layout.Orientation.Horizontal, Spacing = 5 };
            _searchEdit = new TextBox { Watermark = "Search...", MinWidth = 200 };
            _searchEdit.TextChanged += (s, e) => OnSearchChanged();
            searchPanel.Children.Add(_searchEdit);
            mainPanel.Children.Add(searchPanel);

            _tableWidget = new DataGrid
            {
                AutoGenerateColumns = false,
                CanUserReorderColumns = true,
                CanUserResizeColumns = true,
                CanUserSortColumns = true
            };
            _tableWidget.Columns.Add(new DataGridTextColumn { Header = "ResRef", Binding = new Avalonia.Data.Binding("ResRef") });
            _tableWidget.Columns.Add(new DataGridTextColumn { Header = "Type", Binding = new Avalonia.Data.Binding("Type") });
            _tableWidget.Columns.Add(new DataGridTextColumn { Header = "Path", Binding = new Avalonia.Data.Binding("Path") });
            mainPanel.Children.Add(_tableWidget);

            var buttonPanel = new StackPanel { Orientation = Avalonia.Layout.Orientation.Horizontal, Spacing = 5, HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Right };
            _openButton = new Button { Content = "Open" };
            _openButton.Click += (s, e) => OpenSelected();
            _extractButton = new Button { Content = "Extract" };
            _extractButton.Click += (s, e) => ExtractSelected();
            buttonPanel.Children.Add(_openButton);
            buttonPanel.Children.Add(_extractButton);
            mainPanel.Children.Add(buttonPanel);

            Content = mainPanel;
        }

        private void SetupUI()
        {
            // Find controls from XAML
            try
            {
                _tableWidget = this.FindControl<DataGrid>("tableWidget");
                _searchEdit = this.FindControl<TextBox>("searchEdit");
                _openButton = this.FindControl<Button>("openButton");
                _extractButton = this.FindControl<Button>("extractButton");
            }
            catch
            {
                // XAML not loaded or controls not found - will use programmatic UI
                // Controls are already set up in SetupProgrammaticUI
            }

            if (_searchEdit != null)
            {
                _searchEdit.TextChanged += (s, e) => OnSearchChanged();
            }
            if (_openButton != null)
            {
                _openButton.Click += (s, e) => OpenSelected();
            }
            if (_extractButton != null)
            {
                _extractButton.Click += (s, e) => ExtractSelected();
            }
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/dialogs/load_from_location_result.py
        // Original: def populate_resources(self):
        private void PopulateResources()
        {
            if (_tableWidget != null)
            {
                var items = _resources.Select(r => new
                {
                    ResRef = r.ResName,
                    Type = r.ResType.Extension,
                    Path = r.FilePath
                }).ToList();
                _tableWidget.ItemsSource = items;
            }
        }

        private void OnSearchChanged()
        {
            // TODO: Filter resources based on search text
        }

        private void OpenSelected()
        {
            // TODO: Open selected resource in editor
            System.Console.WriteLine("Open selected not yet implemented");
        }

        private void ExtractSelected()
        {
            // TODO: Extract selected resources
            System.Console.WriteLine("Extract selected not yet implemented");
        }

        public List<FileResource> SelectedResources()
        {
            // TODO: Get selected resources from table
            return new List<FileResource>();
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/dialogs/load_from_location_result.py:1089-1105
        // Original: def resize_to_content(self):
        // This method resizes the window to fit the table content, using screen geometry instead of QDesktopWidget
        // In Qt, it uses QApplication.primaryScreen() which works for both Qt5 and Qt6 (QDesktopWidget is deprecated in Qt6)
        // In Avalonia, we use Screen API which is always available
        public void ResizeToContent()
        {
            if (_tableWidget == null)
            {
                return;
            }

            // Calculate width based on table columns
            // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/dialogs/load_from_location_result.py:1094-1096
            // Original: width = vert_header.width() + 4  # 4 for the frame
            //          for i in range(self.resource_table.columnCount()):
            //              width += self.resource_table.columnWidth(i)
            double width = 50; // Estimate for vertical header and frame padding

            if (_tableWidget.Columns != null)
            {
                foreach (var column in _tableWidget.Columns)
                {
                    // Estimate column width (header + content)
                    // In Avalonia DataGrid, we estimate based on typical content
                    width += 150; // Default column width estimate
                }
            }

            // Get screen bounds to ensure window doesn't exceed screen size
            // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/dialogs/load_from_location_result.py:1097-1102
            // Original: primary_screen: QScreen | None = QApplication.primaryScreen()
            //          if primary_screen is None:
            //              raise ValueError("Primary screen is not set")
            //          width = min(width, primary_screen.availableGeometry().width())
            try
            {
                var screens = Avalonia.Application.Current?.ApplicationLifetime as Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime;
                if (screens?.MainWindow != null)
                {
                    var screen = screens.MainWindow.Screens.ScreenFromWindow(screens.MainWindow);
                    if (screen != null)
                    {
                        var availableWidth = screen.WorkingArea.Width;
                        width = System.Math.Min(width, availableWidth * 0.9); // Max 90% of screen width
                    }
                }
            }
            catch
            {
                // If screen API is not available, use a reasonable default
                width = System.Math.Min(width, 1920); // Default max width
            }

            // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/dialogs/load_from_location_result.py:1103-1104
            // Original: height = self.height()  # keep the current height
            //          self.resize(width, height)
            // Set window width, keep current height
            Width = System.Math.Max(width, MinWidth);
        }
    }
}
