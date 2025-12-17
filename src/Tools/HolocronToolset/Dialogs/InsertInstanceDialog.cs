using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Andastra.Parsing.Common;
using Andastra.Parsing.Resource;
using HolocronToolset.Data;
using FileResource = Andastra.Parsing.Extract.FileResource;
using Module = Andastra.Parsing.Common.Module;

namespace HolocronToolset.Dialogs
{
    // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/dialogs/insert_instance.py:38
    // Original: class InsertInstanceDialog(QDialog):
    public partial class InsertInstanceDialog : Window
    {
        private HTInstallation _installation;
        private Module _module;
        private ResourceType _restype;
        private string _resname;
        private byte[] _data;
        private string _filepath;
        private GlobalSettings _globalSettings;

        // Public parameterless constructor for XAML
        public InsertInstanceDialog() : this(null, null, null, ResourceType.UTC)
        {
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/dialogs/insert_instance.py:39-77
        // Original: def __init__(self, parent, installation, module, restype):
        public InsertInstanceDialog(Window parent, HTInstallation installation, Module module, ResourceType restype)
        {
            InitializeComponent();
            _installation = installation;
            _module = module;
            _restype = restype;
            _resname = "";
            _data = new byte[0];
            _filepath = null;
            _globalSettings = new GlobalSettings();
            SetupUI();
            SetupLocationSelect();
            SetupResourceList();
            MinHeight = 500;
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
            Title = "Insert Instance";
            Width = 800;
            Height = 600;

            var panel = new StackPanel();
            var titleLabel = new TextBlock
            {
                Text = "Insert Instance",
                FontSize = 18,
                HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center
            };

            // Create UI controls programmatically for test scenarios
            _reuseResourceRadio = new RadioButton { Content = "Reuse Resource" };
            _copyResourceRadio = new RadioButton { Content = "Copy Resource" };
            _createResourceRadio = new RadioButton { Content = "Create Resource" };
            _resrefEdit = new TextBox { Watermark = "ResRef" };
            _locationSelect = new ComboBox();
            _resourceFilter = new TextBox { Watermark = "Filter" };
            _resourceList = new ListBox();
            _okButton = new Button { Content = "OK" };
            _cancelButton = new Button { Content = "Cancel" };

            // Connect events
            _reuseResourceRadio.IsCheckedChanged += (s, e) => OnResourceRadioToggled();
            _copyResourceRadio.IsCheckedChanged += (s, e) => OnResourceRadioToggled();
            _createResourceRadio.IsCheckedChanged += (s, e) => OnResourceRadioToggled();
            _resrefEdit.TextChanged += (s, e) => OnResrefEdited(_resrefEdit.Text);
            _resourceFilter.TextChanged += (s, e) => OnResourceFilterChanged();
            _resourceList.SelectionChanged += (s, e) => OnResourceSelected();
            _okButton.Click += (s, e) => Accept();
            _cancelButton.Click += (s, e) => Close();

            panel.Children.Add(titleLabel);
            panel.Children.Add(_reuseResourceRadio);
            panel.Children.Add(_copyResourceRadio);
            panel.Children.Add(_createResourceRadio);
            panel.Children.Add(_resrefEdit);
            panel.Children.Add(_locationSelect);
            panel.Children.Add(_resourceFilter);
            panel.Children.Add(_resourceList);
            panel.Children.Add(_okButton);
            panel.Children.Add(_cancelButton);
            Content = panel;
        }

        private RadioButton _reuseResourceRadio;
        private RadioButton _copyResourceRadio;
        private RadioButton _createResourceRadio;
        private TextBox _resrefEdit;
        private ComboBox _locationSelect;
        private TextBox _resourceFilter;
        private ListBox _resourceList;
        private Button _okButton;
        private Button _cancelButton;

        private void SetupUI()
        {
            // Use try-catch to handle cases where XAML controls might not be available (e.g., in tests)
            try
            {
                // Find controls from XAML
                _reuseResourceRadio = this.FindControl<RadioButton>("reuseResourceRadio");
                _copyResourceRadio = this.FindControl<RadioButton>("copyResourceRadio");
                _createResourceRadio = this.FindControl<RadioButton>("createResourceRadio");
                _resrefEdit = this.FindControl<TextBox>("resrefEdit");
                _locationSelect = this.FindControl<ComboBox>("locationSelect");
                _resourceFilter = this.FindControl<TextBox>("resourceFilter");
                _resourceList = this.FindControl<ListBox>("resourceList");
                _okButton = this.FindControl<Button>("okButton");
                _cancelButton = this.FindControl<Button>("cancelButton");
            }
            catch
            {
                // XAML controls not available - create programmatic UI for tests
                SetupProgrammaticUI();
                return; // SetupProgrammaticUI already connects events
            }

            if (_reuseResourceRadio != null)
            {
                _reuseResourceRadio.IsCheckedChanged += (s, e) => OnResourceRadioToggled();
            }
            if (_copyResourceRadio != null)
            {
                _copyResourceRadio.IsCheckedChanged += (s, e) => OnResourceRadioToggled();
            }
            if (_createResourceRadio != null)
            {
                _createResourceRadio.IsCheckedChanged += (s, e) => OnResourceRadioToggled();
            }
            if (_resrefEdit != null)
            {
                _resrefEdit.TextChanged += (s, e) => OnResrefEdited(_resrefEdit.Text);
            }
            if (_resourceFilter != null)
            {
                _resourceFilter.TextChanged += (s, e) => OnResourceFilterChanged();
            }
            if (_resourceList != null)
            {
                _resourceList.SelectionChanged += (s, e) => OnResourceSelected();
            }
            if (_okButton != null)
            {
                _okButton.Click += (s, e) => Accept();
            }
            if (_cancelButton != null)
            {
                _cancelButton.Click += (s, e) => Close();
            }
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/dialogs/insert_instance.py:87-93
        // Original: def _setup_location_select(self):
        private void SetupLocationSelect()
        {
            if (_locationSelect == null || _installation == null || _module == null)
            {
                return;
            }

            _locationSelect.Items.Clear();
            _locationSelect.Items.Add(_installation.OverridePath());

            // Add module capsules
            // TODO: Implement when Module.capsules() is available
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/dialogs/insert_instance.py:95-112
        // Original: def _setup_resource_list(self):
        private void SetupResourceList()
        {
            if (_resourceList == null || _installation == null)
            {
                return;
            }

            // Add core resources
            var coreResources = _installation.CoreResources();
            foreach (var resource in coreResources)
            {
                if (resource.ResType == _restype)
                {
                    _resourceList.Items.Add(resource);
                }
            }

            // Add module resources
            // TODO: Implement when Module resources access is available
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/dialogs/insert_instance.py:117-181
        // Original: def accept(self):
        private void Accept()
        {
            bool newResource = true;

            if (_resourceList?.SelectedItem == null)
            {
                // TODO: Show MessageBox when available
                System.Console.WriteLine("You must choose an instance.");
                return;
            }

            var resource = _resourceList.SelectedItem as FileResource;
            if (resource == null)
            {
                return;
            }

            if (_reuseResourceRadio?.IsChecked == true)
            {
                newResource = false;
                _resname = resource.ResName;
                _filepath = resource.FilePath;
                _data = resource.GetData();
            }
            else if (_copyResourceRadio?.IsChecked == true)
            {
                _resname = _resrefEdit?.Text ?? "";
                _filepath = _locationSelect?.SelectedItem?.ToString() ?? "";
                _data = resource.GetData();
            }
            else if (_createResourceRadio?.IsChecked == true)
            {
                _resname = _resrefEdit?.Text ?? "";
                _filepath = _locationSelect?.SelectedItem?.ToString() ?? "";
                // Create new resource data based on type
                _data = CreateNewResourceData(_restype);
            }

            // Save resource if new
            if (newResource && !string.IsNullOrEmpty(_filepath))
            {
                // TODO: Implement resource saving when ERF/RIM writing is available
            }

            // Add to module
            if (_module != null && !string.IsNullOrEmpty(_resname) && !string.IsNullOrEmpty(_filepath))
            {
                // TODO: Implement when Module.add_locations() is available
            }

            Close();
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/dialogs/insert_instance.py:148-165
        // Original: Create new resource data based on type
        private byte[] CreateNewResourceData(ResourceType restype)
        {
            // TODO: Implement resource creation when resource builders are available
            // For now, return empty data
            return new byte[0];
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/dialogs/insert_instance.py:183-201
        // Original: def on_resource_radio_toggled(self):
        private void OnResourceRadioToggled()
        {
            if (_resourceList != null)
            {
                _resourceList.IsEnabled = _createResourceRadio?.IsChecked != true;
            }
            if (_resourceFilter != null)
            {
                _resourceFilter.IsEnabled = _createResourceRadio?.IsChecked != true;
            }
            if (_resrefEdit != null)
            {
                _resrefEdit.IsEnabled = _reuseResourceRadio?.IsChecked != true;
            }

            if (_reuseResourceRadio?.IsChecked == true)
            {
                if (_okButton != null)
                {
                    _okButton.IsEnabled = true;
                }
            }
            else if (_copyResourceRadio?.IsChecked == true || _createResourceRadio?.IsChecked == true)
            {
                if (_okButton != null && _resrefEdit != null)
                {
                    _okButton.IsEnabled = IsValidResref(_resrefEdit.Text);
                }
            }
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/dialogs/insert_instance.py:203-262
        // Original: def on_resource_selected(self):
        private void OnResourceSelected()
        {
            if (_resourceList?.SelectedItem is FileResource resource)
            {
                // Update dynamic text label
                // TODO: Implement when UI controls are available

                // Update preview
                // TODO: Implement when preview renderer is available
            }
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/dialogs/insert_instance.py:291-297
        // Original: def on_resref_edited(self, text: str):
        private void OnResrefEdited(string text)
        {
            if (_okButton != null)
            {
                _okButton.IsEnabled = IsValidResref(text);
            }
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/dialogs/insert_instance.py:299-305
        // Original: def on_resource_filter_changed(self):
        private void OnResourceFilterChanged()
        {
            string filterText = _resourceFilter?.Text?.ToLowerInvariant() ?? "";

            if (_resourceList != null)
            {
                foreach (var item in _resourceList.Items)
                {
                    if (item is FileResource resource)
                    {
                        // Filter logic would be implemented here
                        // For now, just show all items
                    }
                }
            }
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/dialogs/insert_instance.py:308-312
        // Original: def is_valid_resref(self, text: str) -> bool:
        private bool IsValidResref(string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                return false;
            }

            // Check if resource already exists in module
            if (_module != null)
            {
                // TODO: Check when Module.resource() is available
            }

            // Validate ResRef format
            return ResRef.IsValid(text);
        }

        public string ResName => _resname;
        public byte[] Data => _data;
        public string Filepath => _filepath;
    }
}
