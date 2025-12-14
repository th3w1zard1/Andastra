using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using CSharpKOTOR.Resources;

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
    }
}
