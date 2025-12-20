using System;
using System.Collections.Generic;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using MsBox.Avalonia;
using MsBox.Avalonia.Enums;

namespace HolocronToolset.Dialogs
{
    // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/dialogs/github_selector.py:76
    // Original: class GitHubFileSelector(QDialog):
    public partial class GitHubSelectorDialog : Window
    {
        private string _owner;
        private string _repo;
        private string _selectedPath;
        private TextBox _filterEdit;
        private TreeView _repoTreeWidget;
        private ComboBox _forkComboBox;
        private Button _searchButton;
        private Button _refreshButton;
        private Button _cloneButton;
        private Button _okButton;
        private Button _cancelButton;

        // Public parameterless constructor for XAML
        public GitHubSelectorDialog() : this(null, null, null)
        {
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/dialogs/github_selector.py:77-183
        // Original: def __init__(self, *args, selected_files=None, parent=None):
        public GitHubSelectorDialog(string owner, string repo, List<string> selectedFiles = null, Window parent = null)
        {
            InitializeComponent();
            _owner = owner ?? "";
            _repo = repo ?? "";
            _selectedPath = null;
            SetupUI();
            InitializeRepoData();
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
            Title = "Select a GitHub Repository File";
            MinWidth = 600;
            MinHeight = 400;

            var mainPanel = new StackPanel { Margin = new Avalonia.Thickness(10), Spacing = 10 };

            var label = new TextBlock { Text = "Please select the correct script path or enter manually:" };
            mainPanel.Children.Add(label);

            var forkLabel = new TextBlock { Text = "Select Fork:" };
            _forkComboBox = new ComboBox { MinWidth = 300 };
            mainPanel.Children.Add(forkLabel);
            mainPanel.Children.Add(_forkComboBox);

            var filterPanel = new StackPanel { Orientation = Avalonia.Layout.Orientation.Horizontal, Spacing = 5 };
            _filterEdit = new TextBox { Watermark = "Type to filter paths...", MinWidth = 200 };
            _searchButton = new Button { Content = "Search" };
            _refreshButton = new Button { Content = "Refresh" };
            filterPanel.Children.Add(_filterEdit);
            filterPanel.Children.Add(_searchButton);
            filterPanel.Children.Add(_refreshButton);
            mainPanel.Children.Add(filterPanel);

            _repoTreeWidget = new TreeView();
            mainPanel.Children.Add(_repoTreeWidget);

            var buttonPanel = new StackPanel { Orientation = Avalonia.Layout.Orientation.Horizontal, Spacing = 5, HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Right };
            _okButton = new Button { Content = "OK" };
            _okButton.Click += (s, e) => Accept();
            _cancelButton = new Button { Content = "Cancel" };
            _cancelButton.Click += (s, e) => Close();
            buttonPanel.Children.Add(_okButton);
            buttonPanel.Children.Add(_cancelButton);
            mainPanel.Children.Add(buttonPanel);

            _cloneButton = new Button { Content = "Clone Repository" };
            mainPanel.Children.Add(_cloneButton);

            Content = mainPanel;
        }

        private void SetupUI()
        {
            // Find controls from XAML
            _filterEdit = this.FindControl<TextBox>("filterEdit");
            _repoTreeWidget = this.FindControl<TreeView>("repoTreeWidget");
            _forkComboBox = this.FindControl<ComboBox>("forkComboBox");
            _searchButton = this.FindControl<Button>("searchButton");
            _refreshButton = this.FindControl<Button>("refreshButton");
            _cloneButton = this.FindControl<Button>("cloneButton");
            _okButton = this.FindControl<Button>("okButton");
            _cancelButton = this.FindControl<Button>("cancelButton");

            if (_searchButton != null)
            {
                _searchButton.Click += (s, e) => SearchFiles();
            }
            if (_refreshButton != null)
            {
                _refreshButton.Click += (s, e) => RefreshData();
            }
            if (_cloneButton != null)
            {
                _cloneButton.Click += (s, e) => CloneRepository();
            }
            if (_okButton != null)
            {
                _okButton.Click += (s, e) => Accept();
            }
            if (_cancelButton != null)
            {
                _cancelButton.Click += (s, e) => Close();
            }
            if (_filterEdit != null)
            {
                _filterEdit.TextChanged += (s, e) => OnFilterEditChanged();
            }
            if (_forkComboBox != null)
            {
                _forkComboBox.SelectionChanged += (s, e) => OnForkChanged();
            }
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/dialogs/github_selector.py:185-218
        // Original: def initialize_repo_data(self) -> CompleteRepoData | None:
        private void InitializeRepoData()
        {
            // TODO: Implement GitHub API integration when available
            System.Console.WriteLine($"Initializing repo data for {_owner}/{_repo}");
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/dialogs/github_selector.py:285-286
        // Original: def search_files(self):
        private void SearchFiles()
        {
            OnFilterEditChanged();
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/dialogs/github_selector.py:288-307
        // Original: def on_filter_edit_changed(self):
        private void OnFilterEditChanged()
        {
            // TODO: Implement file filtering when repo data is available
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/dialogs/github_selector.py:406-424
        // Original: def get_selected_path(self) -> str | None:
        private string GetSelectedPath()
        {
            // TODO: Get selected path from tree widget when available
            return _selectedPath;
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/dialogs/github_selector.py:418-424
        // Original: def accept(self) -> None:
        private void Accept()
        {
            _selectedPath = GetSelectedPath();
            if (string.IsNullOrEmpty(_selectedPath))
            {
                // Show warning message
                var msgBox = MsBox.Avalonia.MessageBoxManager.GetMessageBoxStandard(
                    "Warning",
                    "You must select a file.",
                    MsBox.Avalonia.Enums.ButtonEnum.Ok,
                    MsBox.Avalonia.Enums.Icon.Warning);
                msgBox.ShowAsync();
                return;
            }
            System.Console.WriteLine($"User selected '{_selectedPath}'");
            Close(true);
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/dialogs/github_selector.py:426-431
        // Original: def on_fork_changed(self, index: int) -> None:
        private void OnForkChanged()
        {
            // TODO: Reload repo data for selected fork when available
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/dialogs/github_selector.py:520-537
        // Original: def clone_repository(self) -> None:
        private void CloneRepository()
        {
            // TODO: Implement git clone when available
            System.Console.WriteLine("Clone repository not yet implemented");
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/dialogs/github_selector.py:630-633
        // Original: def refresh_data(self) -> None:
        private void RefreshData()
        {
            InitializeRepoData();
        }

        public string SelectedPath => _selectedPath;
    }
}
