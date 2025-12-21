using System;
using System.Collections.Generic;
using System.IO;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using HolocronToolset.Data;
using HolocronToolset.Dialogs;
using HolocronToolset.Windows;
using Andastra.Parsing;
using Andastra.Parsing.Common;
using ModuleClass = Andastra.Parsing.Common.Module;
using GameModule = Andastra.Parsing.Common.Module;

namespace HolocronToolset.Windows
{
    // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/windows/module_designer.py
    // Original: class ModuleDesigner(QMainWindow):
    public class ModuleDesignerWindow : Window
    {
        private HTInstallation _installation;
        private string _modulePath;
        private string _moduleName;
        private GameModule _module;
        private UndoStack _undoStack;
        private List<object> _selectedInstances; // GITInstance equivalents

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/windows/module_designer.py
        // Original: self.ui = Ui_MainWindow() - UI wrapper class exposing all controls
        public ModuleDesignerWindowUi Ui { get; private set; }

        // Matching PyKotor implementation - self._module property
        public ModuleClass GetModule() => _module;

        // Matching PyKotor implementation - self.undo_stack property
        public UndoStack UndoStack => _undoStack;

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/windows/module_designer.py
        // Original: def __init__(self, parent, installation, module_path=None):
        public ModuleDesignerWindow(
            Window parent = null,
            HTInstallation installation = null,
            string modulePath = null)
        {
            InitializeComponent();
            _installation = installation;
            _modulePath = modulePath;
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
            Title = "Module Designer";
            Width = 1200;
            Height = 800;

            var panel = new StackPanel();
            var titleLabel = new TextBlock
            {
                Text = "Module Designer",
                FontSize = 18,
                HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center
            };
            panel.Children.Add(titleLabel);
            Content = panel;
        }

        private TreeView _moduleTree;
        private DataGrid _propertiesTable;

        private void SetupUI()
        {
            // Find controls from XAML
            try
            {
                _moduleTree = this.FindControl<TreeView>("moduleTree");
                _propertiesTable = this.FindControl<DataGrid>("propertiesTable");
            }
            catch
            {
                // XAML not loaded or controls not found - will use programmatic UI
                _moduleTree = null;
                _propertiesTable = null;
            }

            // Create UI wrapper for testing
            Ui = new ModuleDesignerWindowUi
            {
                ModuleTree = _moduleTree,
                PropertiesTable = _propertiesTable
            };

            // Initialize undo stack (matching Python: self.undo_stack: QUndoStack = QUndoStack(self))
            _undoStack = new UndoStack();
            _selectedInstances = new List<object>();

            // If module path provided, open it
            if (!string.IsNullOrEmpty(_modulePath))
            {
                // Defer opening to allow UI to initialize
                // Matching Python: QTimer().singleShot(33, lambda: self.open_module(mod_filepath))
                OpenModule(_modulePath);
            }
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/windows/module_designer.py:993-1005
        // Original: def open_module_with_dialog(self):
        public void OpenModuleWithDialog()
        {
            if (_installation == null)
            {
                return;
            }

            // Matching Python: dialog = SelectModuleDialog(self, self._installation)
            var dialog = new SelectModuleDialog(this, _installation);

            // Matching Python: if dialog.exec():
            if (dialog.ShowDialog(this))
            {
                // Matching Python: mod_filepath = self._installation.module_path().joinpath(dialog.module)
                string selectedModule = dialog.SelectedModule;
                if (string.IsNullOrEmpty(selectedModule))
                {
                    return;
                }

                // Construct full module filepath by combining module path with selected module filename
                string modulePath = _installation.ModulePath();
                string modFilepath = System.IO.Path.Combine(modulePath, selectedModule);

                // Matching Python: self.open_module(mod_filepath)
                OpenModule(modFilepath);
            }
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/windows/module_designer.py:1008-1095
        // Original: def open_module(self, mod_filepath: Path):
        public void OpenModule(string modFilepath)
        {
            if (_installation == null || string.IsNullOrEmpty(modFilepath))
            {
                return;
            }

            try
            {
                // Matching Python: mod_root: str = self._installation.get_module_root(mod_filepath)
                string modRoot = Andastra.Parsing.Installation.Installation.GetModuleRoot(modFilepath);

                // Matching Python: combined_module = Module(mod_root, self._installation, use_dot_mod=is_mod_file(mod_filepath))
                // Note: Module class needs to be implemented in Andastra.Parsing
                // TODO: STUB - For now, this is a placeholder matching the Python interface
                _modulePath = modFilepath;
                _moduleName = Path.GetFileNameWithoutExtension(modFilepath);

                // Matching Python: self._refresh_window_title()
                RefreshWindowTitle();

                // Matching Python: self.rebuild_resource_tree()
                RebuildResourceTree();

                // Matching Python: self.rebuild_instance_list()
                RebuildInstanceList();
            }
            catch (Exception ex)
            {
                // Error handling - in full implementation would show error dialog
                System.Console.WriteLine($"Failed to open module: {ex.Message}");
            }
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/windows/module_designer.py:1142-1144
        // Original: def unload_module(self):
        public void UnloadModule()
        {
            // Matching Python: self._module = None
            _module = null;
            _modulePath = null;
            _moduleName = null;

            // Matching Python: self.rebuild_resource_tree()
            RebuildResourceTree();

            // Matching Python: self.rebuild_instance_list()
            RebuildInstanceList();

            // Matching Python: self._refresh_window_title()
            RefreshWindowTitle();
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/windows/module_designer.py:1188-1197
        // Original: def save_git(self):
        public void SaveGit()
        {
            if (_module == null)
            {
                return;
            }

            // Matching Python: git = self.git()
            // Matching Python: if git is None: return
            // Matching Python: git.path = self._module.path() / "module.git"
            // Matching Python: write_git(git, git.path)
            // Note: GIT saving needs to be implemented in Andastra.Parsing
            // TODO: STUB - For now, this is a placeholder matching the Python interface
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/windows/module_designer.py:1199-1262
        // Original: def rebuild_resource_tree(self):
        public void RebuildResourceTree()
        {
            if (Ui.ModuleTree == null)
            {
                return;
            }

            // Matching Python implementation: Build tree of module resources
            // This will be fully implemented when Module class provides resource enumeration
            // TODO: STUB - For now, clear the tree
            Ui.ModuleTree.ItemsSource = null;
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/windows/module_designer.py:1348-1458
        // Original: def rebuild_instance_list(self):
        public void RebuildInstanceList()
        {
            // Matching Python implementation: Rebuild list of GIT instances
            // This will be fully implemented when GIT and instance classes are available
            // TODO: STUB - For now, this is a placeholder matching the Python interface
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/windows/module_designer.py:978-983
        // Original: def _refresh_window_title(self):
        private void RefreshWindowTitle()
        {
            if (string.IsNullOrEmpty(_moduleName))
            {
                Title = "Module Designer";
            }
            else
            {
                Title = $"Module Designer - {_moduleName}";
            }
        }

        // Matching PyKotor implementation - undo/redo handlers
        private void OnUndo()
        {
            if (_undoStack != null && _undoStack.CanUndo())
            {
                _undoStack.Undo();
            }
        }

        private void OnRedo()
        {
            if (_undoStack != null && _undoStack.CanRedo())
            {
                _undoStack.Redo();
            }
        }
    }

    // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/windows/module_designer.py
    // Original: self.ui = Ui_MainWindow() - UI wrapper class exposing all controls
    public class ModuleDesignerWindowUi
    {
        public TreeView ModuleTree { get; set; }
        public DataGrid PropertiesTable { get; set; }
    }
}
