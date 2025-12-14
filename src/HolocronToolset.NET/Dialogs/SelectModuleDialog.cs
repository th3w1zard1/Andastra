using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using CSharpKOTOR.Common;
using HolocronToolset.NET.Data;

namespace HolocronToolset.NET.Dialogs
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
            var moduleNames = _installation.ModuleNames();
            var listedModules = new HashSet<string>();

            // Build module list
            foreach (var kvp in moduleNames)
            {
                string moduleFile = kvp.Key;
                string moduleName = kvp.Value;
                string moduleRoot = Path.GetFileNameWithoutExtension(moduleFile);
                string casefoldModuleFileName = moduleRoot.ToLowerInvariant();

                if (listedModules.Contains(casefoldModuleFileName))
                {
                    continue;
                }
                listedModules.Add(casefoldModuleFileName);

                // Add to list with display text and data
                string displayText = $"{moduleName}  [{casefoldModuleFileName}]";
                _moduleList.Items.Add(new { Text = displayText, Data = casefoldModuleFileName });
            }
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/dialogs/select_module.py:100-111
        // Original: def browse(self):
        private void Browse()
        {
            // TODO: Implement file dialog when available
            // For now, just close
            System.Console.WriteLine("Browse not yet implemented");
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/dialogs/select_module.py:113-127
        // Original: def confirm(self):
        private void Confirm()
        {
            if (_moduleList?.SelectedItem != null)
            {
                // TODO: Extract module data from selected item
                // For now, just close
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
        private void OnFilterEdited()
        {
            string filterText = _filterEdit?.Text?.ToLowerInvariant() ?? "";
            if (_moduleList == null)
            {
                return;
            }

            // Filter modules based on text
            // TODO: Implement proper filtering when ListBox item access is available
        }

        public string SelectedModule => _selectedModule;
    }
}
