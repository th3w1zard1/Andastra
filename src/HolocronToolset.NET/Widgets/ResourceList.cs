using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using CSharpKOTOR.Resources;
using HolocronToolset.NET.Data;

namespace HolocronToolset.NET.Widgets
{
    // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/widgets/main_widgets.py:77
    // Original: class ResourceList(MainWindowList):
    public class ResourceList : UserControl
    {
        private ResourceModel _modulesModel;
        private HTInstallation _installation;

        public ResourceList()
        {
            InitializeComponent();
            SetupSignals();
            _modulesModel = new ResourceModel();
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
        }

        private void SetupSignals()
        {
            // Signals will be connected when UI controls are available
        }

        public void SetInstallation(HTInstallation installation)
        {
            _installation = installation;
        }

        public void SetResources(List<FileResource> resources, string customCategory = null, bool clearExisting = true)
        {
            if (clearExisting)
            {
                _modulesModel.Clear();
            }
            _modulesModel.AddResourcesBatch(resources, customCategory);
        }

        public void SetSections(List<string> sections)
        {
            // Set sections when UI controls are available
        }

        public List<FileResource> SelectedResources()
        {
            // Get selected resources when UI controls are available
            return new List<FileResource>();
        }

        public void SetResourceSelection(FileResource resource)
        {
            // Select resource when UI controls are available
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
