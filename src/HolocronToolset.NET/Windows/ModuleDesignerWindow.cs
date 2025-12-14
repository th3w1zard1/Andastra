using System;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using HolocronToolset.NET.Data;

namespace HolocronToolset.NET.Windows
{
    // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/windows/module_designer.py
    // Original: class ModuleDesigner(QMainWindow):
    public class ModuleDesignerWindow : Window
    {
        private HTInstallation _installation;
        private string _modulePath;
        private string _moduleName;

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/windows/module_designer.py
        // Original: self.ui = Ui_MainWindow() - UI wrapper class exposing all controls
        public ModuleDesignerWindowUi Ui { get; private set; }

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
