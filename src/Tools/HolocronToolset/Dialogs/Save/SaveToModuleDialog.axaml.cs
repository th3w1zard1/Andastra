using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using BioWare.NET.Resource;

namespace HolocronToolset.Dialogs.Save
{
    // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/dialogs/save/to_module.py:9
    // Original: class SaveToModuleDialog(QDialog):
    public partial class SaveToModuleDialog : Window
    {
        private TextBox _resrefEdit;
        private ComboBox _typeCombo;
        private Button _okButton;
        private Button _cancelButton;
        private List<ResourceType> _supportedTypes;

        // Public parameterless constructor for XAML
        public SaveToModuleDialog() : this("", ResourceType.UTC, new List<ResourceType> { ResourceType.UTC })
        {
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/dialogs/save/to_module.py:12-45
        // Original: def __init__(self, resname, restype, supported):
        public SaveToModuleDialog(string resname, ResourceType restype, List<ResourceType> supported)
        {
            InitializeComponent();
            _supportedTypes = supported ?? new List<ResourceType>();
            SetupUI();
            LoadData(resname, restype);
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
            Title = "Save to Module";
            Width = 300;
            Height = 150;

            var panel = new StackPanel { Margin = new Avalonia.Thickness(10), Spacing = 10 };
            var resrefLabel = new TextBlock { Text = "ResRef:" };
            _resrefEdit = new TextBox();
            var typeLabel = new TextBlock { Text = "Type:" };
            _typeCombo = new ComboBox();
            var okButton = new Button { Content = "OK" };
            okButton.Click += (s, e) => Close();
            var cancelButton = new Button { Content = "Cancel" };
            cancelButton.Click += (s, e) => Close();

            panel.Children.Add(resrefLabel);
            panel.Children.Add(_resrefEdit);
            panel.Children.Add(typeLabel);
            panel.Children.Add(_typeCombo);
            panel.Children.Add(okButton);
            panel.Children.Add(cancelButton);
            Content = panel;
        }

        private void SetupUI()
        {
            // Find controls from XAML
            _resrefEdit = this.FindControl<TextBox>("resrefEdit");
            _typeCombo = this.FindControl<ComboBox>("typeCombo");
            _okButton = this.FindControl<Button>("okButton");
            _cancelButton = this.FindControl<Button>("cancelButton");

            if (_okButton != null)
            {
                _okButton.Click += (s, e) => Close();
            }
            if (_cancelButton != null)
            {
                _cancelButton.Click += (s, e) => Close();
            }
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/dialogs/save/to_module.py:37-38
        // Original: Load data into UI
        private void LoadData(string resname, ResourceType restype)
        {
            if (_resrefEdit != null)
            {
                _resrefEdit.Text = resname ?? "";
            }

            if (_typeCombo != null && _supportedTypes != null)
            {
                _typeCombo.Items.Clear();
                foreach (var type in _supportedTypes)
                {
                    _typeCombo.Items.Add(type.Extension.ToUpperInvariant());
                }

                int index = _supportedTypes.IndexOf(restype);
                if (index >= 0)
                {
                    _typeCombo.SelectedIndex = index;
                }
            }
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/dialogs/save/to_module.py:41-42
        // Original: def resname(self) -> str:
        public string GetResname()
        {
            return _resrefEdit?.Text ?? "";
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/dialogs/save/to_module.py:44-45
        // Original: def restype(self) -> ResourceType:
        public ResourceType GetRestype()
        {
            if (_typeCombo?.SelectedIndex >= 0 && _typeCombo.SelectedIndex < _supportedTypes.Count)
            {
                return _supportedTypes[_typeCombo.SelectedIndex];
            }
            return ResourceType.UTC;
        }
    }
}
