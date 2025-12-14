using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using CSharpKOTOR.Resources;
using HolocronToolset.NET.Data;

namespace HolocronToolset.NET.Widgets
{
    // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/widgets/main_widgets.py:77
    // Original: class ResourceList(MainWindowList):
    public partial class ResourceList : UserControl
    {
        private ResourceModel _modulesModel;
        private HTInstallation _installation;
        private ComboBox _sectionCombo;
        private TextBox _searchEdit;
        private Button _reloadButton;
        private Button _refreshButton;
        private TreeView _resourceTree;

        public ResourceList()
        {
            InitializeComponent();
            _modulesModel = new ResourceModel();
            SetupSignals();
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
            _sectionCombo = this.FindControl<ComboBox>("sectionCombo");
            _searchEdit = this.FindControl<TextBox>("searchEdit");
            _reloadButton = this.FindControl<Button>("reloadButton");
            _refreshButton = this.FindControl<Button>("refreshButton");
            _resourceTree = this.FindControl<TreeView>("resourceTree");
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/widgets/main_widgets.py:122-129
        // Original: def setup_signals(self):
        private void SetupSignals()
        {
            if (_searchEdit != null)
            {
                _searchEdit.TextChanged += (sender, e) => OnFilterStringUpdated();
            }
            if (_sectionCombo != null)
            {
                _sectionCombo.SelectionChanged += (sender, e) => OnSectionChanged();
            }
            if (_reloadButton != null)
            {
                _reloadButton.Click += (sender, e) => OnReloadClicked();
            }
            if (_refreshButton != null)
            {
                _refreshButton.Click += (sender, e) => OnRefreshClicked();
            }
            if (_resourceTree != null)
            {
                _resourceTree.DoubleTapped += (sender, e) => OnResourceDoubleClicked();
            }
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/widgets/main_widgets.py:150-152
        // Original: def set_installation(self, installation: HTInstallation):
        public void SetInstallation(HTInstallation installation)
        {
            _installation = installation;
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/widgets/main_widgets.py:154-187
        // Original: def set_resources(self, resources: list[FileResource], custom_category: str | None = None, *, clear_existing: bool = True):
        public void SetResources(List<FileResource> resources, string customCategory = null, bool clearExisting = true)
        {
            if (clearExisting)
            {
                _modulesModel.Clear();
            }
            _modulesModel.AddResourcesBatch(resources, customCategory);
            // TODO: Update TreeView with model data when TreeView binding is implemented
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/widgets/main_widgets.py:189-195
        // Original: def set_sections(self, sections: list[QStandardItem]):
        public void SetSections(List<string> sections)
        {
            if (_sectionCombo != null)
            {
                _sectionCombo.Items.Clear();
                foreach (var section in sections ?? new List<string>())
                {
                    _sectionCombo.Items.Add(section);
                }
            }
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/widgets/main_widgets.py:226-227
        // Original: def selected_resources(self) -> list[FileResource]:
        public List<FileResource> SelectedResources()
        {
            var selected = new List<FileResource>();
            if (_resourceTree != null && _resourceTree.SelectedItem != null)
            {
                // TODO: Get selected resources from TreeView when binding is implemented
            }
            return selected;
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/widgets/main_widgets.py:197-224
        // Original: def set_resource_selection(self, resource: FileResource):
        public void SetResourceSelection(FileResource resource)
        {
            // TODO: Select resource in TreeView when binding is implemented
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/widgets/main_widgets.py:229-232
        // Original: def on_filter_string_updated(self):
        private void OnFilterStringUpdated()
        {
            // TODO: Update filter when TreeView filtering is implemented
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/widgets/main_widgets.py:234-238
        // Original: def on_section_changed(self):
        private void OnSectionChanged()
        {
            // TODO: Emit section changed signal when events are implemented
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/widgets/main_widgets.py:240-244
        // Original: def on_reload_clicked(self):
        private void OnReloadClicked()
        {
            // TODO: Emit reload signal when events are implemented
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/widgets/main_widgets.py:246-250
        // Original: def on_refresh_clicked(self):
        private void OnRefreshClicked()
        {
            _modulesModel.Clear();
            // TODO: Emit refresh signal when events are implemented
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/widgets/main_widgets.py:416-418
        // Original: def on_resource_double_clicked(self):
        private void OnResourceDoubleClicked()
        {
            // TODO: Emit open resource signal when events are implemented
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/widgets/main_widgets.py:131-133
        // Original: def hide_reload_button(self):
        public void HideReloadButton()
        {
            if (_reloadButton != null)
            {
                _reloadButton.IsVisible = false;
            }
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/widgets/main_widgets.py:135-138
        // Original: def hide_section(self):
        public void HideSection()
        {
            if (_sectionCombo != null)
            {
                _sectionCombo.IsVisible = false;
            }
            if (_refreshButton != null)
            {
                _refreshButton.IsVisible = false;
            }
        }
    }

    // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/widgets/main_widgets.py:478
    // Original: class ResourceModel(QStandardItemModel):
    public class ResourceModel
    {
        private readonly Dictionary<string, ResourceCategoryItem> _categoryItems = new Dictionary<string, ResourceCategoryItem>();

        public ResourceModel()
        {
        }

        public void Clear()
        {
            _categoryItems.Clear();
        }

        public void AddResource(FileResource resource, string customCategory = null)
        {
            string category = customCategory ?? resource.ResType.Category;
            if (!_categoryItems.ContainsKey(category))
            {
                _categoryItems[category] = new ResourceCategoryItem(category);
            }
            _categoryItems[category].AddResource(resource);
        }

        public void AddResourcesBatch(List<FileResource> resources, string customCategory = null)
        {
            var resourcesByCategory = new Dictionary<string, List<FileResource>>();
            foreach (var resource in resources)
            {
                string category = customCategory ?? resource.ResType.Category;
                if (!resourcesByCategory.ContainsKey(category))
                {
                    resourcesByCategory[category] = new List<FileResource>();
                }
                resourcesByCategory[category].Add(resource);
            }

            foreach (var kvp in resourcesByCategory)
            {
                if (!_categoryItems.ContainsKey(kvp.Key))
                {
                    _categoryItems[kvp.Key] = new ResourceCategoryItem(kvp.Key);
                }
                foreach (var resource in kvp.Value)
                {
                    _categoryItems[kvp.Key].AddResource(resource);
                }
            }
        }

        public void RemoveUnusedCategories()
        {
            var emptyCategories = _categoryItems.Where(kvp => kvp.Value.ResourceCount == 0).Select(kvp => kvp.Key).ToList();
            foreach (var category in emptyCategories)
            {
                _categoryItems.Remove(category);
            }
        }
    }

    // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/widgets/main_widgets.py:69
    // Original: class ResourceStandardItem(QStandardItem):
    public class ResourceStandardItem
    {
        public FileResource Resource { get; set; }
        public string Text { get; set; }

        public ResourceStandardItem(string text, FileResource resource)
        {
            Text = text;
            Resource = resource;
        }
    }

    public class ResourceCategoryItem
    {
        public string CategoryName { get; }
        private readonly List<ResourceStandardItem> _resources = new List<ResourceStandardItem>();

        public ResourceCategoryItem(string categoryName)
        {
            CategoryName = categoryName;
        }

        public void AddResource(FileResource resource)
        {
            _resources.Add(new ResourceStandardItem(resource.ResName, resource));
        }

        public int ResourceCount => _resources.Count;
    }
}
