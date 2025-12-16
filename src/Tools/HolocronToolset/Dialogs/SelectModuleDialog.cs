using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Avalonia.Platform.Storage;
using Andastra.Parsing;
using HolocronToolset.Data;

namespace HolocronToolset.Dialogs
{
    // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/dialogs/select_module.py:18
    // Original: class SelectModuleDialog(QDialog):
    public partial class SelectModuleDialog : Window
    {
        private HTInstallation _installation;
        private string _selectedModule;
        private TextBox _filterEdit;
        private ListBox _moduleList;
        private Button _openButton;
        private Button _cancelButton;
        private Button _browseButton;
        private List<ModuleListItem> _allModuleItems; // Store all items for filtering

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/dialogs/select_module.py:50-52
        // Original: self.ui = Ui_Dialog()
        // Expose UI widgets for testing
        public SelectModuleDialogUi Ui { get; private set; }

        // Public parameterless constructor for XAML
        public SelectModuleDialog() : this(null, null)
        {
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/dialogs/select_module.py:19-67
        // Original: def __init__(self, parent, installation):
        public SelectModuleDialog(Window parent, HTInstallation installation)
        {
            InitializeComponent();
            _installation = installation;
            _selectedModule = "";
            SetupUI();
            BuildModuleList();
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
            Title = "Select Module";
            Width = 500;
            Height = 400;

            var panel = new StackPanel();
            var titleLabel = new TextBlock
            {
                Text = "Select Module",
                FontSize = 18,
                HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center
            };
            var openButton = new Button { Content = "Open" };
            openButton.Click += (sender, e) => Confirm();
            var cancelButton = new Button { Content = "Cancel" };
            cancelButton.Click += (sender, e) => Close();

            panel.Children.Add(titleLabel);
            panel.Children.Add(openButton);
            panel.Children.Add(cancelButton);
            Content = panel;
        }

        private void SetupUI()
        {
            // Find controls from XAML
            _filterEdit = this.FindControl<TextBox>("filterEdit");
            _moduleList = this.FindControl<ListBox>("moduleList");
            _openButton = this.FindControl<Button>("openButton");
            _cancelButton = this.FindControl<Button>("cancelButton");
            _browseButton = this.FindControl<Button>("browseButton");

            // Create UI wrapper for testing
            Ui = new SelectModuleDialogUi
            {
                FilterEdit = _filterEdit,
                ModuleList = _moduleList,
                OpenButton = _openButton,
                CancelButton = _cancelButton,
                BrowseButton = _browseButton
            };

            if (_openButton != null)
            {
                _openButton.Click += (s, e) => Confirm();
            }
            if (_cancelButton != null)
            {
                _cancelButton.Click += (s, e) => Close();
            }
            if (_browseButton != null)
            {
                _browseButton.Click += (s, e) => Browse();
            }
            if (_moduleList != null)
            {
                _moduleList.SelectionChanged += (s, e) => OnRowChanged();
                _moduleList.DoubleTapped += (s, e) => Confirm();
            }
            if (_filterEdit != null)
            {
                _filterEdit.TextChanged += (s, e) => OnFilterEdited();
            }
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/dialogs/select_module.py:69-98
        // Original: def _build_module_list(self):
        private void BuildModuleList()
        {
            if (_installation == null || _moduleList == null)
            {
                return;
            }

            _moduleList.Items.Clear();
            _allModuleItems = new List<ModuleListItem>();
            var moduleNames = _installation.ModuleNames();
            var modulesList = _installation.ModulesList();
            var listedModules = new HashSet<string>();

            // Build module list - matching Python logic
            foreach (var module in modulesList)
            {
                // Matching Python: Module.filepath_to_root(module)
                string moduleRoot = Andastra.Parsing.Installation.Installation.GetModuleRoot(module);
                string casefoldModuleFileName = (moduleRoot + System.IO.Path.GetExtension(module)).ToLowerInvariant().Trim();
                
                if (listedModules.Contains(casefoldModuleFileName))
                {
                    continue;
                }
                listedModules.Add(casefoldModuleFileName);

                // Get module name from moduleNames dict (key is the full module filename)
                string moduleName = moduleNames.ContainsKey(module) ? moduleNames[module] : moduleRoot;
                
                // Add to list with display text and data
                string displayText = $"{moduleName}  [{casefoldModuleFileName}]";
                var listItem = new ModuleListItem { Text = displayText, Data = casefoldModuleFileName };
                _allModuleItems.Add(listItem);
                _moduleList.Items.Add(listItem);
            }
        }

        // Helper class for ListBox items
        private class ModuleListItem
        {
            public string Text { get; set; }
            public string Data { get; set; }

            public override string ToString()
            {
                return Text;
            }
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/dialogs/select_module.py:100-111
        // Original: def browse(self):
        //          filepath, _ = QFileDialog.getOpenFileName(
        //              self,
        //              "Select module to open",
        //              str(self._installation.module_path()),
        //              "Module File (*.mod *.rim *.erf)",
        //          )
        //          if not filepath or not filepath.strip():
        //              return
        //          self.module = Module.filepath_to_root(filepath)
        //          self.accept()
        private async void Browse()
        {
            if (_installation == null)
            {
                return;
            }

            // Get the top-level window for file dialog
            var topLevel = TopLevel.GetTopLevel(this);
            if (topLevel == null)
            {
                return;
            }

            // Create file picker options
            var fileType = new FilePickerFileType("Module Files")
            {
                Patterns = new[] { "*.mod", "*.rim", "*.erf" }
            };

            var options = new FilePickerOpenOptions
            {
                Title = "Select module to open",
                FileTypeFilter = new[] { fileType },
                AllowMultiple = false
            };

            // Set initial directory to module path if available
            try
            {
                string modulePath = _installation.ModulePath();
                if (!string.IsNullOrEmpty(modulePath) && Directory.Exists(modulePath))
                {
                    var storageFolder = await topLevel.StorageProvider.TryGetFolderFromPathAsync(modulePath);
                    if (storageFolder != null)
                    {
                        options.SuggestedStartLocation = storageFolder;
                    }
                }
            }
            catch
            {
                // Ignore errors setting initial directory
            }

            // Show file dialog
            var files = await topLevel.StorageProvider.OpenFilePickerAsync(options);
            if (files == null || files.Count == 0)
            {
                return;
            }

            string filepath = files[0].Path.LocalPath;
            if (string.IsNullOrWhiteSpace(filepath))
            {
                return;
            }

            // Matching Python: Module.filepath_to_root(filepath)
            string moduleRoot = Andastra.Parsing.Installation.Installation.GetModuleRoot(filepath);
            _selectedModule = moduleRoot;
            Confirm();
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/dialogs/select_module.py:113-127
        // Original: def confirm(self):
        private void Confirm()
        {
            if (_moduleList?.SelectedItem is ModuleListItem item)
            {
                _selectedModule = item.Data;
            }
            Close();
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/dialogs/select_module.py:129-130
        // Original: def on_row_changed(self):
        private void OnRowChanged()
        {
            if (_openButton != null)
            {
                _openButton.IsEnabled = _moduleList?.SelectedItem != null;
            }
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/dialogs/select_module.py:132-150
        // Original: def on_filter_edited(self):
        // Made public for testing purposes (Python version is also accessible)
        public void OnFilterEdited()
        {
            string filterText = _filterEdit?.Text?.ToLowerInvariant() ?? "";
            if (_moduleList == null || _allModuleItems == null)
            {
                return;
            }

            // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/dialogs/select_module.py:144-150
            // Original: text = self.ui.filterEdit.text()
            // Original: for row in range(self.ui.moduleList.count()):
            // Original:     item: QListWidgetItem | None = self.ui.moduleList.item(row)
            // Original:     if item is None: continue
            // Original:     item.setHidden(text.lower() not in item.text().lower())
            // In Avalonia, we filter by rebuilding the list from _allModuleItems
            _moduleList.Items.Clear();
            foreach (var item in _allModuleItems)
            {
                if (string.IsNullOrEmpty(filterText) || item.Text.ToLowerInvariant().Contains(filterText))
                {
                    _moduleList.Items.Add(item);
                }
            }
        }

        public string SelectedModule => _selectedModule;

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/dialogs/select_module.py:50-52
        // Original: self.ui = Ui_Dialog()
        // UI wrapper class for testing access
        public class SelectModuleDialogUi
        {
            public TextBox FilterEdit { get; set; }
            public ListBox ModuleList { get; set; }
            public Button OpenButton { get; set; }
            public Button CancelButton { get; set; }
            public Button BrowseButton { get; set; }
        }
    }
}
