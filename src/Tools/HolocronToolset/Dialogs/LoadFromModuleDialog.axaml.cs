using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using BioWare.NET.Resource;
using BioWare.NET.Resource.Formats.Capsule;
using FileResource = BioWare.NET.Extract.FileResource;

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
        // Overload to accept Capsule directly (matching PyKotor interface)
        public LoadFromModuleDialog(Capsule capsule, IList<ResourceType> supported)
        {
            InitializeComponent();
            _supported = supported?.ToList() ?? new List<ResourceType>();
            _selectedResource = null;

            // Convert Capsule resources (CapsuleResource) to FileResource objects
            // Matching PyKotor: capsule is iterable and yields FileResource-like objects
            _resources = new List<FileResource>();
            if (capsule != null)
            {
                foreach (var capsuleResource in capsule)
                {
                    // Create FileResource from CapsuleResource
                    // FileResource constructor: (resname, restype, size, offset, filepath)
                    var fileResource = new FileResource(
                        capsuleResource.ResName,
                        capsuleResource.ResType,
                        capsuleResource.Size,
                        capsuleResource.Offset,
                        capsuleResource.FilePath
                    );
                    _resources.Add(fileResource);
                }
            }

            SetupUI();
            BuildResourceList();
        }

        // Legacy constructor for backward compatibility
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
            var okButton = new Button { Content = "OK", IsEnabled = false };
            okButton.Click += (sender, e) => { if (_selectedResource != null) Close(true); };

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
                _okButton.Click += (s, e) =>
                {
                    if (_selectedResource != null)
                    {
                        Close(true); // Close with result = true (matching PyKotor's exec() returning True)
                    }
                };
            }
            if (_cancelButton != null)
            {
                _cancelButton.Click += (s, e) =>
                {
                    _selectedResource = null;
                    Close(false); // Close with result = false (matching PyKotor's exec() returning False)
                };
            }
            if (_resourceList != null)
            {
                _resourceList.SelectionChanged += (s, e) =>
                {
                    if (_resourceList.SelectedItem is FileResource resource)
                    {
                        _selectedResource = resource;
                    }
                    else
                    {
                        _selectedResource = null;
                    }

                    // Enable/disable OK button based on selection (matching typical dialog behavior)
                    if (_okButton != null)
                    {
                        _okButton.IsEnabled = _selectedResource != null;
                    }
                };
            }

            // Initially disable OK button if no resource is selected
            if (_okButton != null)
            {
                _okButton.IsEnabled = false;
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
