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
        private HTInstallation _installation;
        private FileResource _selection;

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/dialogs/search.py:227-228
        // Original: self.ui = Ui_Dialog(); self.ui.setupUi(self)
        public FileResultsDialogUi Ui { get; private set; }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/dialogs/search.py:195
        // Original: sig_searchresults_selected = Signal(FileResource)
        public event Action<FileResource> SearchResultsSelected;

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
            var resultList = new ListBox();
            var buttonPanel = new StackPanel { Orientation = Avalonia.Layout.Orientation.Horizontal, HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Right, Spacing = 5 };
            var openButton = new Button { Content = "Open" };
            openButton.Click += (s, e) => Open();
            var okButton = new Button { Content = "OK" };
            okButton.Click += (s, e) => Accept();
            buttonPanel.Children.Add(openButton);
            buttonPanel.Children.Add(okButton);
            panel.Children.Add(resultList);
            panel.Children.Add(buttonPanel);
            Content = panel;

            // Create Ui wrapper for programmatic UI
            Ui = new FileResultsDialogUi
            {
                ResultList = resultList,
                OpenButton = openButton,
                OkButton = okButton
            };
        }

        private void SetupUI()
        {
            // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/dialogs/search.py:227-228
            // Original: self.ui = Ui_Dialog(); self.ui.setupUi(self)
            // Find all controls from XAML and expose via Ui property
            Ui = new FileResultsDialogUi
            {
                ResultList = this.FindControl<ListBox>("resultList"),
                OpenButton = this.FindControl<Button>("openButton"),
                OkButton = this.FindControl<Button>("okButton")
            };

            if (Ui.OpenButton != null)
            {
                Ui.OpenButton.Click += (s, e) => Open();
            }
            if (Ui.OkButton != null)
            {
                Ui.OkButton.Click += (s, e) => Accept();
            }
            if (Ui.ResultList != null)
            {
                Ui.ResultList.DoubleTapped += (s, e) => Open();
            }
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/dialogs/search.py:243-252
        // Original: Populate results list
        private void PopulateResults(IEnumerable<FileResource> results)
        {
            if (Ui?.ResultList == null)
            {
                return;
            }

            Ui.ResultList.Items.Clear();
            var resultList = new List<FileResourceResultItem>();

            foreach (var result in results)
            {
                string filename = result.Identifier.ToString();
                string filepath = result.FilePath ?? "";
                string parentName = Path.GetFileName(Path.GetDirectoryName(filepath)) ?? "";
                string displayText = string.IsNullOrEmpty(parentName) ? filename : $"{parentName}/{filename}";

                resultList.Add(new FileResourceResultItem
                {
                    DisplayText = displayText,
                    Resource = result,
                    Tooltip = filepath
                });
            }

            // Sort items alphabetically
            resultList.Sort((a, b) => string.Compare(a.DisplayText, b.DisplayText, StringComparison.OrdinalIgnoreCase));

            foreach (var item in resultList)
            {
                Ui.ResultList.Items.Add(item);
            }
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/dialogs/search.py:254-271
        // Original: def accept(self):
        private void Accept()
        {
            if (Ui?.ResultList?.SelectedItem is FileResourceResultItem item)
            {
                _selection = item.Resource;
                SearchResultsSelected?.Invoke(_selection);
            }
            Close();
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/dialogs/search.py:273-297
        // Original: def open(self):
        private void Open()
        {
            if (Ui?.ResultList?.SelectedItem is FileResourceResultItem item)
            {
                _selection = item.Resource;
                SearchResultsSelected?.Invoke(_selection);
                // TODO: Open resource editor when available
                // For now, just emit signal and close
                Close();
            }
            else
            {
                System.Console.WriteLine("Nothing to open, no item selected");
            }
        }

        public FileResource Selection => _selection;
    }

    // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/dialogs/search.py:227-228
    // Original: self.ui = Ui_Dialog() - UI wrapper class exposing all controls
    public class FileResultsDialogUi
    {
        public ListBox ResultList { get; set; }
        public Button OpenButton { get; set; }
        public Button OkButton { get; set; }
    }

    // Helper class to store FileResource with display text in ListBox
    internal class FileResourceResultItem
    {
        public string DisplayText { get; set; }
        public FileResource Resource { get; set; }
        public string Tooltip { get; set; }

        public override string ToString()
        {
            return DisplayText;
        }
    }
}
