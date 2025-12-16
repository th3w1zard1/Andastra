using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Andastra.Parsing.Resources;
using FileResource = Andastra.Parsing.Resources.FileResource;

namespace HolocronToolset.Dialogs
{
    // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/dialogs/load_from_module.py:14
    // Original: class LoadFromModuleDialog(QDialog):
    public partial class LoadFromModuleDialog : Window
    {
        private List<FileResource> _resources;
        private List<ResourceType> _supported;
        private FileResource _selectedResource;

        // Public parameterless constructor for XAML
        public LoadFromModuleDialog() : this(null, null)
        {
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/dialogs/load_from_module.py:17-60
        // Original: def __init__(self, capsule, supported):
        public LoadFromModuleDialog(List<FileResource> resources, List<ResourceType> supported)
        {
            InitializeComponent();
            _resources = resources ?? new List<FileResource>();
            _supported = supported ?? new List<ResourceType>();
            _selectedResource = null;
            SetupUI();
            BuildResourceList();
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
            Title = "Load from Module";
            Width = 500;
            Height = 400;

            var panel = new StackPanel();
            var titleLabel = new TextBlock
            {
                Text = "Load from Module",
                FontSize = 18,
                HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center
            };
            var okButton = new Button { Content = "OK" };
            okButton.Click += (sender, e) => Close();

            panel.Children.Add(titleLabel);
            panel.Children.Add(okButton);
            Content = panel;
        }

        private ListBox _resourceList;
        private Button _okButton;
        private Button _cancelButton;

        private void SetupUI()
        {
            // Find controls from XAML
            _resourceList = this.FindControl<ListBox>("resourceList");
            _okButton = this.FindControl<Button>("okButton");
            _cancelButton = this.FindControl<Button>("cancelButton");

            if (_okButton != null)
            {
                _okButton.Click += (s, e) => { if (_selectedResource != null) Close(); };
            }
            if (_cancelButton != null)
            {
                _cancelButton.Click += (s, e) => { _selectedResource = null; Close(); };
            }
            if (_resourceList != null)
            {
                _resourceList.SelectionChanged += (s, e) =>
                {
                    if (_resourceList.SelectedItem is FileResource resource)
                    {
                        _selectedResource = resource;
                    }
                };
            }
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/dialogs/load_from_module.py:54-60
        // Original: Build resource list from capsule
        private void BuildResourceList()
        {
            // Filter resources by supported types
            var filteredResources = _resources.Where(r => _supported.Contains(r.ResType)).ToList();
            if (_resourceList != null)
            {
                _resourceList.Items.Clear();
                foreach (var resource in filteredResources)
                {
                    _resourceList.Items.Add(resource);
                }
            }
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/dialogs/load_from_module.py:62-84
        // Original: def resname(self) -> str | None:
        public string ResName()
        {
            return _selectedResource?.ResName;
        }

        public ResourceType ResType()
        {
            return _selectedResource?.ResType;
        }

        public byte[] Data()
        {
            return _selectedResource?.GetData();
        }
    }
}
