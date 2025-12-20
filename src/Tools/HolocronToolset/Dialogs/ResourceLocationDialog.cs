using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Markup.Xaml;
using Andastra.Parsing.Extract;
using Andastra.Parsing.Resource;
using HolocronToolset.Data;

namespace HolocronToolset.Dialogs
{
    // Matching PyKotor implementation pattern for resource location dialogs
    // Shows detailed information about where a resource is located in the installation
    public partial class ResourceLocationDialog : Window
    {
        private TextBlock _resourceNameText;
        private TextBlock _resourceTypeText;
        private DataGrid _locationsTable;
        private Button _closeButton;
        private Button _openButton;
        private List<LocationResult> _locations;
        private string _resourceName;
        private ResourceType _resourceType;
        private HTInstallation _installation;

        // Public parameterless constructor for XAML
        public ResourceLocationDialog() : this(null, null, null, null, null)
        {
        }

        // Matching PyKotor implementation pattern
        // Original: Shows resource location details in a dialog
        public ResourceLocationDialog(
            Window parent,
            string resourceName,
            ResourceType resourceType,
            List<LocationResult> locations,
            HTInstallation installation = null)
        {
            InitializeComponent();
            Title = "Resource Location";
            Width = 800;
            Height = 500;
            _resourceName = resourceName ?? "";
            _resourceType = resourceType;
            _locations = locations ?? new List<LocationResult>();
            _installation = installation;
            SetupUI();
            PopulateData();
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
            var mainPanel = new StackPanel
            {
                Margin = new Avalonia.Thickness(15),
                Spacing = 15
            };

            // Resource information section
            var infoPanel = new StackPanel { Spacing = 5 };

            var nameLabel = new TextBlock { Text = "Resource Name:", FontWeight = Avalonia.Media.FontWeight.Bold };
            _resourceNameText = new TextBlock { Text = _resourceName ?? "" };
            infoPanel.Children.Add(nameLabel);
            infoPanel.Children.Add(_resourceNameText);

            var typeLabel = new TextBlock { Text = "Resource Type:", FontWeight = Avalonia.Media.FontWeight.Bold, Margin = new Avalonia.Thickness(0, 10, 0, 0) };
            _resourceTypeText = new TextBlock { Text = _resourceType?.Extension ?? "" };
            infoPanel.Children.Add(typeLabel);
            infoPanel.Children.Add(_resourceTypeText);

            mainPanel.Children.Add(infoPanel);

            // Locations table
            var tableLabel = new TextBlock
            {
                Text = "Locations:",
                FontWeight = Avalonia.Media.FontWeight.Bold,
                Margin = new Avalonia.Thickness(0, 10, 0, 5)
            };
            mainPanel.Children.Add(tableLabel);

            _locationsTable = new DataGrid
            {
                AutoGenerateColumns = false,
                CanUserReorderColumns = true,
                CanUserResizeColumns = true,
                CanUserSortColumns = true,
                GridLinesVisibility = DataGridGridLinesVisibility.All,
                SelectionMode = DataGridSelectionMode.Single,
                Height = 250
            };

            _locationsTable.Columns.Add(new DataGridTextColumn
            {
                Header = "File Path",
                Binding = new Avalonia.Data.Binding("FilePath"),
                Width = new DataGridLength(1, DataGridLengthUnitType.Star)
            });
            _locationsTable.Columns.Add(new DataGridTextColumn
            {
                Header = "Offset",
                Binding = new Avalonia.Data.Binding("Offset"),
                Width = new DataGridLength(100)
            });
            _locationsTable.Columns.Add(new DataGridTextColumn
            {
                Header = "Size (bytes)",
                Binding = new Avalonia.Data.Binding("Size"),
                Width = new DataGridLength(120)
            });

            mainPanel.Children.Add(_locationsTable);

            // Buttons
            var buttonPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Spacing = 10,
                HorizontalAlignment = HorizontalAlignment.Right,
                Margin = new Avalonia.Thickness(0, 10, 0, 0)
            };

            _openButton = new Button
            {
                Content = "Open in Editor",
                IsEnabled = _locations != null && _locations.Count > 0
            };
            _openButton.Click += (s, e) => OpenSelectedLocation();

            _closeButton = new Button { Content = "Close" };
            _closeButton.Click += (s, e) => Close();

            buttonPanel.Children.Add(_openButton);
            buttonPanel.Children.Add(_closeButton);
            mainPanel.Children.Add(buttonPanel);

            Content = mainPanel;
        }

        private void SetupUI()
        {
            // Find controls from XAML
            try
            {
                _resourceNameText = this.FindControl<TextBlock>("resourceNameText");
                _resourceTypeText = this.FindControl<TextBlock>("resourceTypeText");
                _locationsTable = this.FindControl<DataGrid>("locationsTable");
                _openButton = this.FindControl<Button>("openButton");
                _closeButton = this.FindControl<Button>("closeButton");
            }
            catch
            {
                // XAML not loaded or controls not found - will use programmatic UI
            }

            if (_openButton != null)
            {
                _openButton.Click += (s, e) => OpenSelectedLocation();
            }
            if (_closeButton != null)
            {
                _closeButton.Click += (s, e) => Close();
            }
            if (_locationsTable != null)
            {
                _locationsTable.SelectionChanged += (s, e) =>
                {
                    if (_openButton != null)
                    {
                        _openButton.IsEnabled = _locationsTable.SelectedItem != null;
                    }
                };
            }
        }

        private void PopulateData()
        {
            // Set resource name and type
            if (_resourceNameText != null)
            {
                _resourceNameText.Text = _resourceName ?? "";
            }
            if (_resourceTypeText != null)
            {
                _resourceTypeText.Text = _resourceType?.Extension ?? "";
            }

            // Populate locations table
            if (_locationsTable != null && _locations != null)
            {
                var locationItems = _locations.Select(loc => new
                {
                    FilePath = loc.FilePath ?? "",
                    Offset = loc.Offset,
                    Size = loc.Size,
                    LocationResult = loc // Store reference for opening
                }).ToList();

                _locationsTable.ItemsSource = locationItems;
            }
        }

        // Open the selected location in the appropriate editor
        private void OpenSelectedLocation()
        {
            if (_locationsTable?.SelectedItem == null)
            {
                return;
            }

            try
            {
                // Get the LocationResult from the selected item
                var selectedItem = _locationsTable.SelectedItem;
                var locationResultProperty = selectedItem.GetType().GetProperty("LocationResult");
                if (locationResultProperty == null)
                {
                    return;
                }

                var locationResult = locationResultProperty.GetValue(selectedItem) as LocationResult;
                if (locationResult == null)
                {
                    return;
                }

                // Get FileResource from LocationResult
                FileResource fileResource = locationResult.FileResource;
                if (fileResource == null)
                {
                    // Create FileResource from LocationResult if not set
                    if (!File.Exists(locationResult.FilePath))
                    {
                        var msgBox = MsBox.Avalonia.MessageBoxManager.GetMessageBoxStandard(
                            "Error",
                            $"Resource file not found at:\n{locationResult.FilePath}",
                            MsBox.Avalonia.Enums.ButtonEnum.Ok,
                            MsBox.Avalonia.Enums.Icon.Error);
                        msgBox.ShowAsync();
                        return;
                    }

                    var fileInfo = new FileInfo(locationResult.FilePath);
                    fileResource = new FileResource(
                        _resourceName ?? "",
                        _resourceType ?? ResourceType.INVALID,
                        locationResult.Size,
                        locationResult.Offset,
                        locationResult.FilePath);
                    locationResult.SetFileResource(fileResource);
                }

                // Open in editor using WindowUtils
                var parentWindow = this.Parent as Window ?? this;
                HolocronToolset.Editors.WindowUtils.OpenResourceEditor(fileResource, _installation, parentWindow);

                // Close the dialog after opening
                Close();
            }
            catch (Exception ex)
            {
                var msgBox = MsBox.Avalonia.MessageBoxManager.GetMessageBoxStandard(
                    "Error",
                    $"Error opening resource:\n{ex.Message}",
                    MsBox.Avalonia.Enums.ButtonEnum.Ok,
                    MsBox.Avalonia.Enums.Icon.Error);
                msgBox.ShowAsync();
            }
        }

        // Show the dialog and return true if user clicked OK/Close
        // Matching PyKotor implementation pattern - simplified synchronous ShowDialog
        // Note: In a full implementation, this would use ShowDialogAsync for proper modal behavior
        public new bool ShowDialog(Window parent = null)
        {
            if (parent != null)
            {
                // Set parent window for modal behavior
                // Note: This is a simplified implementation - in a full implementation, we'd use ShowDialogAsync
                Show();
            }
            else
            {
                Show();
            }
            return true;
        }
    }
}

