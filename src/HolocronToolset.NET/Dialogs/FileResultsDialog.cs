using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using CSharpKOTOR.Resources;
using HolocronToolset.NET.Data;
using FileResource = CSharpKOTOR.Resources.FileResource;

namespace HolocronToolset.NET.Dialogs
{
    // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/dialogs/search.py:194
    // Original: class FileResults(QDialog):
    public partial class FileResultsDialog : Window
    {
        private ListBox _resultList;
        private Button _openButton;
        private Button _okButton;
        private HTInstallation _installation;
        private FileResource _selection;

        // Public parameterless constructor for XAML
        public FileResultsDialog() : this(null, null, null)
        {
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/dialogs/search.py:197-252
        // Original: def __init__(self, parent, results, installation):
        public FileResultsDialog(Window parent, IEnumerable<FileResource> results, HTInstallation installation)
        {
            InitializeComponent();
            _installation = installation;
            SetupUI();
            PopulateResults(results ?? new List<FileResource>());
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
            Title = "Search Results";
            Width = 303;
            Height = 401;

            var panel = new StackPanel { Margin = new Avalonia.Thickness(10), Spacing = 10 };
            _resultList = new ListBox();
            var buttonPanel = new StackPanel { Orientation = Avalonia.Layout.Orientation.Horizontal, HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Right, Spacing = 5 };
            _openButton = new Button { Content = "Open" };
            _openButton.Click += (s, e) => Open();
            _okButton = new Button { Content = "OK" };
            _okButton.Click += (s, e) => Accept();
            buttonPanel.Children.Add(_openButton);
            buttonPanel.Children.Add(_okButton);
            panel.Children.Add(_resultList);
            panel.Children.Add(buttonPanel);
            Content = panel;
        }

        private void SetupUI()
        {
            // Find controls from XAML
            _resultList = this.FindControl<ListBox>("resultList");
            _openButton = this.FindControl<Button>("openButton");
            _okButton = this.FindControl<Button>("okButton");

            if (_openButton != null)
            {
                _openButton.Click += (s, e) => Open();
            }
            if (_okButton != null)
            {
                _okButton.Click += (s, e) => Accept();
            }
            if (_resultList != null)
            {
                _resultList.DoubleTapped += (s, e) => Open();
            }
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/dialogs/search.py:243-250
        // Original: Populate results list
        private void PopulateResults(IEnumerable<FileResource> results)
        {
            if (_resultList == null)
            {
                return;
            }

            _resultList.Items.Clear();
            foreach (var result in results)
            {
                string filename = result.ResName + "." + result.ResType.Extension;
                string filepath = result.FilePath ?? "";
                string parentName = Path.GetFileName(Path.GetDirectoryName(filepath)) ?? "";
                string displayText = string.IsNullOrEmpty(parentName) ? filename : $"{parentName}/{filename}";
                _resultList.Items.Add(new { Text = displayText, Resource = result, Tooltip = filepath });
            }

            // Sort items
            // TODO: Implement sorting when ListBox item access is available
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/dialogs/search.py:254-271
        // Original: def accept(self):
        private void Accept()
        {
            if (_resultList?.SelectedItem != null)
            {
                // TODO: Extract resource from selected item
                // For now, just close
            }
            Close();
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/dialogs/search.py:273-297
        // Original: def open(self):
        private void Open()
        {
            if (_resultList?.SelectedItem == null)
            {
                System.Console.WriteLine("Nothing to open, no item selected");
                return;
            }

            // TODO: Extract resource and open editor when available
            // For now, just log
            System.Console.WriteLine("Open resource editor not yet implemented");
        }

        public FileResource Selection => _selection;
    }
}
