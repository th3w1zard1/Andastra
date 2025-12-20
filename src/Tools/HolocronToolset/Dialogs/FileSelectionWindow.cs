using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Markup.Xaml;
using Andastra.Parsing.Extract;
using Andastra.Parsing.Resource;
using HolocronToolset.Data;
using HolocronToolset.Utils;
using MsBox.Avalonia;
using MsBox.Avalonia.Enums;
using FileResource = Andastra.Parsing.Extract.FileResource;

namespace HolocronToolset.Dialogs
{
    // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/dialogs/load_from_location_result.py:1006
    // Original: class FileSelectionWindow(QMainWindow):
    /// <summary>
    /// Window for selecting a file from multiple search results.
    /// Displays resources in a table with detailed information and allows user to select and open one.
    /// </summary>
    public partial class FileSelectionWindow : Window
    {
        private DataGrid _resourceTable;
        private CheckBox _detailedCheckbox;
        private Button _openButton;
        private List<FileResource> _resources;
        private HTInstallation _installation;
        private List<string> _detailedStatAttributes;

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/dialogs/load_from_location_result.py:1007-1025
        // Original: def __init__(self, search_results: Sequence[FileResource | ResourceResult | LocationResult], ...):
        /// <summary>
        /// Initializes a new instance of FileSelectionWindow.
        /// </summary>
        /// <param name="searchResults">List of FileResource or LocationResult to display</param>
        /// <param name="installation">HTInstallation instance (optional)</param>
        /// <param name="parent">Parent window (optional)</param>
        public FileSelectionWindow(
            IEnumerable<object> searchResults,
            HTInstallation installation = null,
            Window parent = null)
        {
            InitializeComponent();
            _installation = installation;
            _detailedStatAttributes = new List<string>();
            
            // Convert search results to FileResource list
            _resources = UnifyResources(searchResults);
            
            SetupUI();
            InitTable();
            
            if (parent != null)
            {
                WindowStartupLocation = WindowStartupLocation.CenterOwner;
            }
            else
            {
                WindowStartupLocation = WindowStartupLocation.CenterScreen;
            }
        }

        // Public parameterless constructor for XAML
        public FileSelectionWindow() : this(new List<object>(), null, null)
        {
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

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/dialogs/load_from_location_result.py:671-681
        // Original: def _unify_resources(self, resources: Sequence[FileResource | ResourceResult | LocationResult]):
        /// <summary>
        /// Converts various resource types (FileResource, LocationResult) to a unified list of FileResource.
        /// </summary>
        private List<FileResource> UnifyResources(IEnumerable<object> resources)
        {
            var result = new List<FileResource>();
            if (resources == null)
            {
                return result;
            }

            foreach (var resource in resources)
            {
                if (resource is FileResource fileResource)
                {
                    if (!result.Contains(fileResource))
                    {
                        result.Add(fileResource);
                    }
                }
                else if (resource is LocationResult locationResult)
                {
                    // Convert LocationResult to FileResource
                    FileResource fr = locationResult.FileResource;
                    if (fr == null)
                    {
                        // Create FileResource from LocationResult
                        if (!File.Exists(locationResult.FilePath))
                        {
                            continue;
                        }

                        var fileInfo = new FileInfo(locationResult.FilePath);
                        // Try to extract resource name and type from path
                        string resName = Path.GetFileNameWithoutExtension(locationResult.FilePath);
                        ResourceType resType = ResourceType.INVALID;
                        
                        // Try to determine resource type from extension
                        string ext = Path.GetExtension(locationResult.FilePath)?.ToLowerInvariant();
                        if (!string.IsNullOrEmpty(ext))
                        {
                            ext = ext.TrimStart('.');
                            try
                            {
                                resType = ResourceType.FromExtension(ext);
                            }
                            catch
                            {
                                // If extension lookup fails, use INVALID
                                resType = ResourceType.INVALID;
                            }
                        }

                        fr = new FileResource(
                            resName ?? "",
                            resType,
                            locationResult.Size,
                            locationResult.Offset,
                            locationResult.FilePath);
                        locationResult.SetFileResource(fr);
                    }

                    if (!result.Contains(fr))
                    {
                        result.Add(fr);
                    }
                }
            }

            return result;
        }

        private void SetupProgrammaticUI()
        {
            Title = "File Selection";
            Width = 800;
            Height = 600;
            MinWidth = 600;
            MinHeight = 400;

            var mainPanel = new StackPanel
            {
                Margin = new Avalonia.Thickness(10),
                Spacing = 10
            };

            // Detailed checkbox
            _detailedCheckbox = new CheckBox
            {
                Content = "Show detailed",
                Margin = new Avalonia.Thickness(0, 0, 0, 5)
            };
            _detailedCheckbox.IsCheckedChanged += (s, e) => ToggleDetailedInfo();
            mainPanel.Children.Add(_detailedCheckbox);

            // Resource table
            _resourceTable = new DataGrid
            {
                AutoGenerateColumns = false,
                CanUserReorderColumns = true,
                CanUserResizeColumns = true,
                CanUserSortColumns = true,
                SelectionMode = DataGridSelectionMode.Single,
                GridLinesVisibility = DataGridGridLinesVisibility.All
            };
            _resourceTable.DoubleTapped += (s, e) => OnDoubleClick();
            mainPanel.Children.Add(_resourceTable);

            // Open button
            var buttonPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right,
                Spacing = 5
            };
            _openButton = new Button
            {
                Content = "Open",
                MinWidth = 100
            };
            _openButton.Click += (s, e) => OpenSelected();
            buttonPanel.Children.Add(_openButton);
            mainPanel.Children.Add(buttonPanel);

            Content = mainPanel;
        }

        private void SetupUI()
        {
            // Find controls from XAML
            try
            {
                _resourceTable = this.FindControl<DataGrid>("resourceTable");
                _detailedCheckbox = this.FindControl<CheckBox>("detailedCheckbox");
                _openButton = this.FindControl<Button>("openButton");
            }
            catch
            {
                // XAML not loaded or controls not found - will use programmatic UI
            }

            if (_detailedCheckbox != null)
            {
                _detailedCheckbox.IsCheckedChanged += (s, e) => ToggleDetailedInfo();
            }

            if (_openButton != null)
            {
                _openButton.Click += (s, e) => OpenSelected();
            }

            if (_resourceTable != null)
            {
                _resourceTable.DoubleTapped += (s, e) => OnDoubleClick();
            }
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/dialogs/load_from_location_result.py:1073-1081
        // Original: def update_table_headers(self):
        private void UpdateTableHeaders()
        {
            if (_resourceTable == null)
            {
                return;
            }

            var headers = new List<string> { "File Name", "File Path", "Offset", "Size" };
            
            if (_detailedStatAttributes != null && _detailedStatAttributes.Count > 0)
            {
                foreach (var header in _detailedStatAttributes)
                {
                    if (!headers.Contains(header))
                    {
                        headers.Add(header);
                    }
                }
            }

            // Clear existing columns
            _resourceTable.Columns.Clear();

            // Add columns with proper property bindings
            foreach (var header in headers)
            {
                string propertyName = GetPropertyNameForHeader(header);
                var column = new DataGridTextColumn
                {
                    Header = header,
                    Binding = new Binding(propertyName)
                };
                _resourceTable.Columns.Add(column);
            }
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/dialogs/load_from_location_result.py:1116-1155
        // Original: def populate_table(self):
        private void PopulateTable()
        {
            if (_resourceTable == null || _resources == null)
            {
                return;
            }

            var tableItems = new List<ResourceTableItem>();

            foreach (var resource in _resources)
            {
                var item = new ResourceTableItem
                {
                    Resource = resource,
                    FileName = resource.Identifier.ToString(),
                    FilePath = resource.FilePath,
                    Offset = $"0x{resource.Offset:X}",
                    Size = HumanReadableSize(resource.Size)
                };

                if (_detailedStatAttributes != null && _detailedStatAttributes.Count > 0)
                {
                    try
                    {
                        var filePath = new FileInfo(resource.FilePath);
                        if (filePath.Exists)
                        {
                            var fileInfo = filePath;
                            // Add detailed stat information
                            // Note: Full stat implementation would require more detailed file system access
                            // For now, we'll add basic file information
                            item.SizeOnDisk = HumanReadableSize(fileInfo.Length);
                            item.LastModified = fileInfo.LastWriteTime.ToString("yyyy-MM-dd HH:mm:ss");
                            item.Created = fileInfo.CreationTime.ToString("yyyy-MM-dd HH:mm:ss");
                            
                            // Calculate size ratio
                            if (resource.Size > 0)
                            {
                                double ratio = (fileInfo.Length / (double)resource.Size) * 100.0;
                                item.SizeRatio = $"{ratio:F2}%";
                            }
                        }
                    }
                    catch
                    {
                        // Ignore errors when getting file stats
                    }
                }
                else
                {
                    // Show relative path when not in detailed mode
                    if (_installation != null && !string.IsNullOrEmpty(_installation.Path))
                    {
                        try
                        {
                            var installPath = new DirectoryInfo(_installation.Path);
                            var filePath = new FileInfo(resource.FilePath);
                            if (filePath.FullName.StartsWith(installPath.FullName, StringComparison.OrdinalIgnoreCase))
                            {
                                item.FilePath = Path.GetRelativePath(installPath.FullName, filePath.FullName);
                            }
                        }
                        catch
                        {
                            // Use full path if relative path calculation fails
                        }
                    }
                }

                tableItems.Add(item);
            }

            _resourceTable.ItemsSource = tableItems;
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/dialogs/load_from_location_result.py:1157-1166
        // Original: def human_readable_size(self, size: float, decimal_places: int = 2) -> str:
        private string HumanReadableSize(long size, int decimalPlaces = 2)
        {
            string[] units = { "B", "KB", "MB", "GB", "TB", "PB" };
            double sizeDouble = size;
            string unit = "B";

            foreach (var u in units)
            {
                if (sizeDouble < 1024.0 || u == "PB")
                {
                    unit = u;
                    break;
                }
                sizeDouble /= 1024.0;
            }

            return string.Format("{0:F" + decimalPlaces + "} {1}", sizeDouble, unit);
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/dialogs/load_from_location_result.py:1220-1234
        // Original: def toggle_detailed_info(self):
        private void ToggleDetailedInfo()
        {
            try
            {
                bool showDetailed = _detailedCheckbox?.IsChecked ?? false;
                
                if (showDetailed && _resources != null && _resources.Count > 0)
                {
                    // Get stat attributes from first resource
                    var firstResource = _resources[0];
                    var filePath = new FileInfo(firstResource.FilePath);
                    if (filePath.Exists)
                    {
                        _detailedStatAttributes = GetStatAttributes(filePath);
                        _detailedStatAttributes.Add("Size on Disk");
                        _detailedStatAttributes.Add("Size Ratio");
                    }
                    else
                    {
                        _detailedStatAttributes = new List<string>();
                    }
                }
                else
                {
                    _detailedStatAttributes = new List<string>();
                }

                InitTable();
            }
            catch (Exception ex)
            {
                System.Console.WriteLine($"Error toggling detailed info: {ex}");
                if (_detailedCheckbox != null)
                {
                    _detailedCheckbox.IsChecked = false;
                }
            }
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/dialogs/load_from_location_result.py:1247-1259
        // Original: def get_stat_attributes(self, path: Path) -> list[str]:
        private List<string> GetStatAttributes(FileInfo fileInfo)
        {
            var attributes = new List<string>();
            
            try
            {
                if (fileInfo.Exists)
                {
                    // Add basic file attributes that are available in C#
                    attributes.Add("Last Modified");
                    attributes.Add("Created");
                    // Note: Full stat implementation would require more detailed file system access
                    // The Python version uses os.stat() which provides more attributes
                }
            }
            catch
            {
                // Ignore errors
            }

            return attributes;
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/dialogs/load_from_location_result.py:1236-1241
        // Original: def _init_table(self):
        private void InitTable()
        {
            if (_resourceTable == null)
            {
                return;
            }

            _resourceTable.IsReadOnly = true;
            UpdateTableHeaders();
            PopulateTable();
            ResizeToContent();
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/dialogs/load_from_location_result.py:1089-1105
        // Original: def resize_to_content(self):
        private void ResizeToContent()
        {
            if (_resourceTable == null)
            {
                return;
            }

            // Calculate width based on columns
            double width = 100; // Base width for window chrome

            if (_resourceTable.Columns != null)
            {
                foreach (var column in _resourceTable.Columns)
                {
                    width += 150; // Estimated column width
                }
            }

            // Ensure window doesn't exceed screen size
            try
            {
                var screens = Avalonia.Application.Current?.ApplicationLifetime as Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime;
                if (screens?.MainWindow != null)
                {
                    var screen = screens.MainWindow.Screens.ScreenFromWindow(screens.MainWindow);
                    if (screen != null)
                    {
                        var availableWidth = screen.WorkingArea.Width;
                        width = Math.Min(width, availableWidth * 0.9);
                    }
                }
            }
            catch
            {
                width = Math.Min(width, 1920);
            }

            Width = Math.Max(width, MinWidth);
            CenterWindow();
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/dialogs/load_from_location_result.py:1063-1071
        // Original: def center_and_adjust_window(self):
        private void CenterWindow()
        {
            try
            {
                var screens = Avalonia.Application.Current?.ApplicationLifetime as Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime;
                if (screens?.MainWindow != null)
                {
                    var screen = screens.MainWindow.Screens.ScreenFromWindow(screens.MainWindow);
                    if (screen != null)
                    {
                        var screenWidth = screen.WorkingArea.Width;
                        var screenHeight = screen.WorkingArea.Height;
                        var windowWidth = Width;
                        var windowHeight = Height;

                        var x = Math.Max(0, (screenWidth - windowWidth) / 2);
                        var y = Math.Max(0, (screenHeight - windowHeight) / 2);

                        Position = new Avalonia.PixelPoint((int)x, (int)y);
                    }
                }
            }
            catch
            {
                // Ignore errors
            }
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/dialogs/load_from_location_result.py:1041-1042
        // Original: open_button.clicked.connect(lambda: self.resource_table.on_double_click(installation=self.installation))
        private void OpenSelected()
        {
            if (_resourceTable?.SelectedItem is ResourceTableItem item && item.Resource != null)
            {
                OpenResource(item.Resource);
            }
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/dialogs/load_from_location_result.py:1043
        // Original: self.resource_table.doubleClicked.connect(lambda: self.resource_table.on_double_click(installation=self.installation))
        private void OnDoubleClick()
        {
            OpenSelected();
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/dialogs/load_from_location_result.py:927-936
        // Original: def open_selected_resource(self, resources: set[FileResource], installation: HTInstallation | None = None, ...):
        private void OpenResource(FileResource resource)
        {
            if (resource == null)
            {
                return;
            }

            try
            {
                var parentWindow = this.Parent as Window ?? this;
                WindowUtils.OpenResourceEditor(resource, _installation, parentWindow);
                Close();
            }
            catch (Exception ex)
            {
                System.Console.WriteLine($"Error opening resource: {ex}");
                
                // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/utils/window.py:344-352
                // Original: QMessageBox(QMessageBox.Icon.Critical, tr("An unexpected error has occurred"), str(universal_simplify_exception(e)), ...).show()
                // Note: Using ex.Message for error details (similar to universal_simplify_exception in PyKotor)
                string errorMessage = ex.Message;
                if (string.IsNullOrEmpty(errorMessage))
                {
                    errorMessage = ex.ToString();
                }
                
                var errorBox = MessageBoxManager.GetMessageBoxStandard(
                    "Error",
                    $"Error opening resource:\n{errorMessage}",
                    ButtonEnum.Ok,
                    Icon.Error);
                errorBox.ShowAsync();
            }
        }

        // Property to access installation
        public HTInstallation Installation => _installation;

        // Helper method to map header names to property names
        private string GetPropertyNameForHeader(string header)
        {
            switch (header)
            {
                case "File Name":
                    return "FileName";
                case "File Path":
                    return "FilePath";
                case "Offset":
                    return "Offset";
                case "Size":
                    return "Size";
                case "Size on Disk":
                    return "SizeOnDisk";
                case "Size Ratio":
                    return "SizeRatio";
                case "Last Modified":
                    return "LastModified";
                case "Created":
                    return "Created";
                default:
                    return header.Replace(" ", "");
            }
        }
    }

    // Helper class for table items
    internal class ResourceTableItem
    {
        public FileResource Resource { get; set; }
        public string FileName { get; set; }
        public string FilePath { get; set; }
        public string Offset { get; set; }
        public string Size { get; set; }
        public string SizeOnDisk { get; set; }
        public string LastModified { get; set; }
        public string Created { get; set; }
        public string SizeRatio { get; set; }
    }
}

