using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using Avalonia.Threading;
using MsBox.Avalonia;
using MsBox.Avalonia.Enums;
using Newtonsoft.Json;
using HolocronToolset.Utils;

namespace HolocronToolset.Dialogs
{
    // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/dialogs/github_selector.py:76
    // Original: class GitHubFileSelector(QDialog):
    public partial class GitHubSelectorDialog : Window
    {
        // Using GitHubApiModels.TreeInfoData and GitHubApiModels.CompleteRepoData instead of local classes

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

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/dialogs/github_selector.py:120
        // Original: self.repo_data: CompleteRepoData | None = None
        private Utils.CompleteRepoData _repoData;

        // Dictionary to map file paths to TreeViewItems for efficient lookup during filtering
        private Dictionary<string, TreeViewItem> _pathToItemMap;

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/dialogs/github_selector.py:121-122
        // Original: self.rate_limit_reset: int | None = None, self.rate_limit_remaining: int | None = None
        private int? _rateLimitReset;
        private int? _rateLimitRemaining;

        // HTTP client for GitHub API requests
        private static readonly HttpClient HttpClient = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(15)
        };

        // Rate limit timer (matching PyKotor's QTimer)
        private DispatcherTimer _rateLimitTimer;

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
            _repoData = null;
            _pathToItemMap = new Dictionary<string, TreeViewItem>();
            SetupUI();
            InitializeRepoData();

            // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/dialogs/github_selector.py:180-183
            // Original: if self.selected_files and self.repo_data is not None: self.filter_edit.setText(";".join(self.selected_files)); self.search_files()
            if (selectedFiles != null && selectedFiles.Count > 0 && _repoData != null && _filterEdit != null)
            {
                _filterEdit.Text = string.Join(";", selectedFiles);
                SearchFiles();
            }
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
            _cloneButton.Click += (s, e) => CloneRepository();
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
        private async void InitializeRepoData()
        {
            if (string.IsNullOrEmpty(_owner) || string.IsNullOrEmpty(_repo))
            {
                return;
            }

            try
            {
                Utils.CompleteRepoData repoData = await LoadRepoAsync(_owner, _repo);
                if (repoData != null)
                {
                    LoadRepoData(repoData);
                    StopRateLimitTimer();
                }
            }
            catch (HttpRequestException ex)
            {
                // Check for rate limiting (403 status)
                if (ex.Message.Contains("403") || ex.Message.Contains("rate limit"))
                {
                    if (_rateLimitTimer == null || !_rateLimitTimer.IsEnabled)
                    {
                        var msgBox = MessageBoxManager.GetMessageBoxStandard(
                            "Rate Limited",
                            "You have submitted too many requests to GitHub's API. Check the status bar at the bottom.",
                            ButtonEnum.Ok,
                            MsBox.Avalonia.Enums.Icon.Error);
                        msgBox.ShowAsync();
                        StartRateLimitTimer(null);
                    }
                    return;
                }

                // Try to load forks as fallback
                var errorMsgBox = MessageBoxManager.GetMessageBoxStandard(
                    "Repository Not Found",
                    $"The repository '{_owner}/{_repo}' had an unexpected error:\n\n{ex.Message}",
                    ButtonEnum.Ok,
                    MsBox.Avalonia.Enums.Icon.Error);
                await errorMsgBox.ShowAsync();

                // Try to fetch forks
                try
                {
                    string forksUrl = $"https://api.github.com/repos/{_owner}/{_repo}/forks";
                    string forksJson = await HttpClient.GetStringAsync(forksUrl);
                    List<Utils.ForkContentsData> forks = JsonConvert.DeserializeObject<List<Utils.ForkContentsData>>(forksJson);

                    if (forks != null && forks.Count > 0)
                    {
                        string firstFork = forks[0].FullName;
                        var infoMsgBox = MessageBoxManager.GetMessageBoxStandard(
                            "Using Fork",
                            $"The main repository is not available. Using the fork: {firstFork}",
                            ButtonEnum.Ok,
                            MsBox.Avalonia.Enums.Icon.Info);
                        await infoMsgBox.ShowAsync();

                        string[] forkParts = firstFork.Split('/');
                        if (forkParts.Length == 2)
                        {
                            Utils.CompleteRepoData forkRepoData = await LoadRepoAsync(forkParts[0], forkParts[1]);
                            if (forkRepoData != null)
                            {
                                if (_forkComboBox != null)
                                {
                                    _forkComboBox.Items.Add(firstFork);
                                }
                                LoadRepoData(forkRepoData, false);
                                return;
                            }
                        }
                    }

                    var noForksMsgBox = MessageBoxManager.GetMessageBoxStandard(
                        "No Forks Available",
                        "No forks are available to load.",
                        ButtonEnum.Ok,
                        MsBox.Avalonia.Enums.Icon.Error);
                    await noForksMsgBox.ShowAsync();
                }
                catch (Exception forkEx)
                {
                    var forkErrorMsgBox = MessageBoxManager.GetMessageBoxStandard(
                        "Forks Load Error",
                        $"Failed to load forks: {forkEx.Message}",
                        ButtonEnum.Ok,
                        MsBox.Avalonia.Enums.Icon.Error);
                    await forkErrorMsgBox.ShowAsync();
                }
            }
            catch (Exception ex)
            {
                var errorMsgBox = MessageBoxManager.GetMessageBoxStandard(
                    "Error",
                    $"Failed to initialize repository data: {ex.Message}",
                    ButtonEnum.Ok,
                    MsBox.Avalonia.Enums.Icon.Error);
                await errorMsgBox.ShowAsync();
            }
        }

        // Matching PyKotor implementation at Libraries/PyKotor/src/utility/updater/github.py:532-568
        // Original: @classmethod def load_repo(cls, owner: str, repo_name: str, *, timeout: int = 15) -> CompleteRepoData:
        private async Task<Utils.CompleteRepoData> LoadRepoAsync(string owner, string repoName, int timeoutSeconds = 15)
        {
            string baseUrl = $"https://api.github.com/repos/{owner}/{repoName}";

            var endpoints = new Dictionary<string, string>
            {
                { "repo_info", baseUrl },
                { "branches", $"{baseUrl}/branches" },
                { "contents", $"{baseUrl}/contents" },
                { "forks", $"{baseUrl}/forks" }
            };

            var repoData = new Dictionary<string, object>();

            foreach (var kvp in endpoints)
            {
                try
                {
                    System.Console.WriteLine($"Fetching {kvp.Key}...");
                    string responseJson = await HttpClient.GetStringAsync(kvp.Value);
                    repoData[kvp.Key] = JsonConvert.DeserializeObject(responseJson);
                }
                catch (HttpRequestException ex)
                {
                    // Update rate limit info from response headers if available
                    if (ex.Message.Contains("403"))
                    {
                        // Try to get rate limit info from exception
                        UpdateRateLimitInfoFromException(ex);
                    }
                    throw;
                }
            }

            // Fetch the tree using the correct default branch
            string defaultBranch = "main";
            if (repoData.ContainsKey("repo_info") && repoData["repo_info"] != null)
            {
                var repoInfoJson = JsonConvert.SerializeObject(repoData["repo_info"]);
                var repoInfo = JsonConvert.DeserializeObject<Utils.RepoIndexData>(repoInfoJson);
                if (repoInfo != null && !string.IsNullOrEmpty(repoInfo.DefaultBranch))
                {
                    defaultBranch = repoInfo.DefaultBranch;
                }
            }

            string treeUrl = $"{baseUrl}/git/trees/{defaultBranch}?recursive=1";
            System.Console.WriteLine($"Fetching tree from {treeUrl}...");

            try
            {
                string treeResponseJson = await HttpClient.GetStringAsync(treeUrl);
                var treeResponse = JsonConvert.DeserializeObject<Dictionary<string, object>>(treeResponseJson);
                if (treeResponse != null && treeResponse.ContainsKey("tree"))
                {
                    repoData["tree"] = treeResponse["tree"];
                }
            }
            catch (Exception ex)
            {
                System.Console.WriteLine($"Failed to fetch tree from {treeUrl}: {ex.Message}");
                repoData["tree"] = new List<object>();
            }

            // Build CompleteRepoData instance
            var completeRepoData = new Utils.CompleteRepoData
            {
                RepoInfo = repoData.ContainsKey("repo_info") && repoData["repo_info"] != null
                    ? JsonConvert.DeserializeObject<Utils.RepoIndexData>(JsonConvert.SerializeObject(repoData["repo_info"]))
                    : null,
                Branches = repoData.ContainsKey("branches") && repoData["branches"] != null
                    ? JsonConvert.DeserializeObject<List<Utils.BranchInfoData>>(JsonConvert.SerializeObject(repoData["branches"]))
                    : new List<Utils.BranchInfoData>(),
                Contents = repoData.ContainsKey("contents") && repoData["contents"] != null
                    ? JsonConvert.DeserializeObject<List<Utils.ContentInfoData>>(JsonConvert.SerializeObject(repoData["contents"]))
                    : new List<Utils.ContentInfoData>(),
                Forks = repoData.ContainsKey("forks") && repoData["forks"] != null
                    ? JsonConvert.DeserializeObject<List<Utils.ForkContentsData>>(JsonConvert.SerializeObject(repoData["forks"]))
                    : new List<Utils.ForkContentsData>(),
                Tree = repoData.ContainsKey("tree") && repoData["tree"] != null
                    ? JsonConvert.DeserializeObject<List<Utils.TreeInfoData>>(JsonConvert.SerializeObject(repoData["tree"]))
                    : new List<Utils.TreeInfoData>()
            };

            System.Console.WriteLine($"Completed loading of '{baseUrl}'");
            return completeRepoData;
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/dialogs/github_selector.py:220-234
        // Original: def _load_repo_data(self, data: CompleteRepoData, *, do_fork_combo_update: bool = True) -> None:
        private void LoadRepoData(Utils.CompleteRepoData data, bool doForkComboUpdate = true)
        {
            _repoData = data;
            if (doForkComboUpdate)
            {
                PopulateForkComboBox();
            }

            string selectedFork = null;
            if (_forkComboBox != null)
            {
                selectedFork = _forkComboBox.SelectedItem?.ToString() ?? _forkComboBox.Text;
            }

            if (string.IsNullOrEmpty(selectedFork) || selectedFork == $"{_owner}/{_repo} (main)")
            {
                LoadMainBranchFiles();
            }
            else
            {
                LoadFork(selectedFork);
            }
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/dialogs/github_selector.py:235-238
        // Original: def load_main_branch_files(self) -> None:
        private void LoadMainBranchFiles()
        {
            if (_repoData != null)
            {
                if (_repoTreeWidget != null)
                {
                    _repoTreeWidget.Items.Clear();
                }
                PopulateTreeWidget();
            }
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/dialogs/github_selector.py:240-269
        // Original: def populate_tree_widget(self, files: list[TreeInfoData] | None = None, parent_item: QTreeWidgetItem | None = None) -> None:
        private void PopulateTreeWidget(List<Utils.TreeInfoData> files = null)
        {
            if (files == null)
            {
                if (_repoData == null || _repoData.Tree == null)
                {
                    return;
                }
                files = _repoData.Tree;
            }

            if (_repoTreeWidget == null)
            {
                return;
            }

            // Dictionary to hold the tree items by their paths
            var pathToItem = new Dictionary<string, TreeViewItem>();

            // Create all tree items without parents first
            foreach (var item in files)
            {
                if (string.IsNullOrEmpty(item.Path))
                {
                    continue;
                }

                string itemPath = item.Path;
                string itemName = System.IO.Path.GetFileName(itemPath);
                if (string.IsNullOrEmpty(itemName))
                {
                    itemName = itemPath;
                }

                // Convert Utils.TreeInfoData to local TreeInfoData for Tag storage
                var localTreeInfo = new TreeInfoData
                {
                    Mode = item.Mode,
                    Type = item.Type,
                    Sha = item.Sha,
                    Size = item.Size,
                    Url = item.Url,
                    Path = item.Path
                };

                var treeItem = new TreeViewItem
                {
                    Header = itemName,
                    Tag = localTreeInfo
                };

                // Set tooltip to URL (matching PyKotor's setToolTip behavior)
                if (!string.IsNullOrEmpty(item.Url))
                {
                    ToolTip.SetTip(treeItem, item.Url);
                }

                pathToItem[itemPath] = treeItem;
                _pathToItemMap[itemPath] = treeItem;
            }

            // Add the tree items to their parents
            foreach (var kvp in pathToItem)
            {
                string itemPath = kvp.Key;
                TreeViewItem treeItem = kvp.Value;

                string parentPath = System.IO.Path.GetDirectoryName(itemPath)?.Replace('\\', '/');
                if (string.IsNullOrEmpty(parentPath))
                {
                    parentPath = "/";
                }

                if (pathToItem.ContainsKey(parentPath))
                {
                    TreeViewItem parentItem = pathToItem[parentPath];
                    parentItem.Items.Add(treeItem);
                }
                else
                {
                    // Top-level item
                    _repoTreeWidget.Items.Add(treeItem);
                }
            }
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/dialogs/github_selector.py:277-284
        // Original: def populate_fork_combobox(self) -> None:
        private void PopulateForkComboBox()
        {
            if (_forkComboBox == null)
            {
                return;
            }

            _forkComboBox.Items.Clear();
            _forkComboBox.Items.Add($"{_owner}/{_repo} (main)");

            if (_repoData == null || _repoData.Forks == null)
            {
                return;
            }

            foreach (var fork in _repoData.Forks)
            {
                if (!string.IsNullOrEmpty(fork.FullName))
                {
                    _forkComboBox.Items.Add(fork.FullName);
                }
            }
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/dialogs/github_selector.py:433-444
        // Original: def load_fork(self, fork_name: str) -> None:
        private async void LoadFork(string forkName)
        {
            if (_repoTreeWidget == null)
            {
                return;
            }

            _repoTreeWidget.Items.Clear();
            string fullName = forkName.Replace(" (main)", "");

            // Format the contents_url with the proper path and add the recursive parameter
            string treeUrl = $"https://api.github.com/repos/{fullName}/git/trees/master?recursive=1";

            try
            {
                Dictionary<string, object> contentsDict = await ApiGetAsync(treeUrl);
                if (contentsDict != null && contentsDict.ContainsKey("tree"))
                {
                    var treeArray = JsonConvert.SerializeObject(contentsDict["tree"]);
                    List<Utils.TreeInfoData> repoIndex = JsonConvert.DeserializeObject<List<Utils.TreeInfoData>>(treeArray);
                    PopulateTreeWidget(repoIndex);
                    SearchFiles();
                }
            }
            catch (Exception ex)
            {
                var errorMsgBox = MessageBoxManager.GetMessageBoxStandard(
                    "Error Loading Fork",
                    $"Failed to load fork '{forkName}': {ex.Message}",
                    ButtonEnum.Ok,
                    MsBox.Avalonia.Enums.Icon.Error);
                await errorMsgBox.ShowAsync();
            }
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/dialogs/github_selector.py:446-463
        // Original: def api_get(self, url: str) -> dict[str, Any]:
        private async Task<Dictionary<string, object>> ApiGetAsync(string url)
        {
            try
            {
                using (var response = await HttpClient.GetAsync(url))
                {
                    response.EnsureSuccessStatusCode();
                    UpdateRateLimitInfo(response.Headers);
                    string json = await response.Content.ReadAsStringAsync();
                    return JsonConvert.DeserializeObject<Dictionary<string, object>>(json);
                }
            }
            catch (HttpRequestException ex)
            {
                if (ex.Message.Contains("403"))
                {
                    // Check if rate limited
                    try
                    {
                        using (var response = await HttpClient.GetAsync(url))
                        {
                            if (response.StatusCode == System.Net.HttpStatusCode.Forbidden)
                            {
                                if (response.Headers.Contains("X-RateLimit-Reset"))
                                {
                                    StartRateLimitTimer(null);
                                    return new Dictionary<string, object>(); // Return empty dictionary when rate limit is exceeded
                                }
                            }
                        }
                    }
                    catch
                    {
                        // Ignore
                    }
                }
                throw;
            }
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/dialogs/github_selector.py:497-504
        // Original: def update_rate_limit_info(self, headers: dict[str, Any] | requests.structures.CaseInsensitiveDict[str]) -> None:
        private void UpdateRateLimitInfo(System.Net.Http.Headers.HttpResponseHeaders headers)
        {
            if (headers.Contains("X-RateLimit-Reset"))
            {
                var resetHeader = headers.GetValues("X-RateLimit-Reset").FirstOrDefault();
                if (int.TryParse(resetHeader, out int resetValue))
                {
                    _rateLimitReset = resetValue;
                }
            }

            if (headers.Contains("X-RateLimit-Remaining"))
            {
                var remainingHeader = headers.GetValues("X-RateLimit-Remaining").FirstOrDefault();
                if (int.TryParse(remainingHeader, out int remainingValue))
                {
                    _rateLimitRemaining = remainingValue;
                }
            }
        }

        private void UpdateRateLimitInfoFromException(HttpRequestException ex)
        {
            // Try to extract rate limit info from exception if possible
            // This is a fallback when we can't access headers directly
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/dialogs/github_selector.py:465-476
        // Original: def start_rate_limit_timer(self, e: requests.exceptions.HTTPError | None = None) -> None:
        private void StartRateLimitTimer(HttpRequestException e)
        {
            if (e != null)
            {
                // Try to extract rate limit info from exception
                // In a real implementation, we'd need to access response headers
                _rateLimitRemaining = 0;
            }

            UpdateRateLimitStatus();
            if (_rateLimitTimer == null)
            {
                _rateLimitTimer = new DispatcherTimer
                {
                    Interval = TimeSpan.FromSeconds(1)
                };
                _rateLimitTimer.Tick += (s, args) => UpdateRateLimitStatus();
            }
            _rateLimitTimer.Start();
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/dialogs/github_selector.py:478-480
        // Original: def stop_rate_limit_timer(self) -> None:
        private void StopRateLimitTimer()
        {
            if (_rateLimitTimer != null)
            {
                _rateLimitTimer.Stop();
            }
            // Clear status bar message if available
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/dialogs/github_selector.py:482-495
        // Original: def update_rate_limit_status(self) -> None:
        private void UpdateRateLimitStatus()
        {
            if (_rateLimitReset == null || _rateLimitRemaining == null)
            {
                return;
            }

            if (_rateLimitRemaining > 0)
            {
                // Status bar message: "Requests remaining: {remaining}"
                StopRateLimitTimer();
            }
            else
            {
                double remainingTime = Math.Max(_rateLimitReset.Value - DateTimeOffset.UtcNow.ToUnixTimeSeconds(), 0);
                // Status bar message: "Rate limit exceeded. Try again in {int(remaining_time)} seconds."
                if ((int)remainingTime % 15 == 0) // Refresh every 15 seconds
                {
                    RefreshData();
                }
                else if (remainingTime <= 0)
                {
                    RefreshData();
                    StopRateLimitTimer();
                }
            }
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
            if (_filterEdit == null || _repoTreeWidget == null)
            {
                return;
            }

            string filterText = _filterEdit.Text ?? "";

            if (!string.IsNullOrWhiteSpace(filterText))
            {
                // Split by semicolon to support multiple file names (matching PyKotor behavior)
                string[] fileNames = filterText.ToLowerInvariant().Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries);
                List<string> fileNamesList = fileNames.Select(f => f.Trim()).Where(f => !string.IsNullOrEmpty(f)).ToList();
                SearchAndHighlight(fileNamesList);
                ExpandAllItems();
            }
            else
            {
                // Unhide all items and collapse when filter is cleared
                UnhideAllItems();
                CollapseAllItems();
            }
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/dialogs/github_selector.py:406-424
        // Original: def get_selected_path(self) -> str | None:
        private string GetSelectedPath()
        {
            if (_repoTreeWidget == null)
            {
                return null;
            }

            TreeViewItem selectedItem = _repoTreeWidget.SelectedItem as TreeViewItem;
            if (selectedItem == null)
            {
                return null;
            }

            TreeInfoData itemInfo = selectedItem.Tag as TreeInfoData;
            if (itemInfo != null && itemInfo.Type == "blob")
            {
                return itemInfo.Path;
            }

            return null;
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
            if (_repoData != null)
            {
                LoadRepoData(_repoData, false);
            }
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/dialogs/github_selector.py:520-537
        // Original: def clone_repository(self) -> None:
        private void CloneRepository()
        {
            // Get selected fork from combo box
            string selectedFork = null;
            if (_forkComboBox != null)
            {
                selectedFork = _forkComboBox.SelectedItem?.ToString() ?? _forkComboBox.Text;
                // Remove " (main)" suffix if present (matching PyKotor behavior)
                if (!string.IsNullOrEmpty(selectedFork))
                {
                    selectedFork = selectedFork.Replace(" (main)", "");
                }
            }

            // Validate that a fork is selected
            if (string.IsNullOrWhiteSpace(selectedFork))
            {
                var warningMsgBox = MessageBoxManager.GetMessageBoxStandard(
                    "No Fork Selected",
                    "Please select a fork to clone.",
                    ButtonEnum.Ok,
                            MsBox.Avalonia.Enums.Icon.Warning);
                warningMsgBox.ShowAsync();
                return;
            }

            // Construct GitHub URL
            string url = $"https://github.com/{selectedFork}.git";

            try
            {
                // Check if git is available
                if (!IsGitAvailable())
                {
                    var errorMsgBox = MessageBoxManager.GetMessageBoxStandard(
                        "Git Not Found",
                        "Git is not installed or not available in PATH. Please install Git and ensure it is accessible from the command line.",
                        ButtonEnum.Ok,
                        MsBox.Avalonia.Enums.Icon.Error);
                    errorMsgBox.ShowAsync();
                    return;
                }

                // Prepare git clone command
                ProcessStartInfo startInfo = new ProcessStartInfo
                {
                    FileName = "git",
                    Arguments = $"clone {url}",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                };

                // Windows-specific: Set CREATE_NO_WINDOW flag (matching PyKotor behavior)
                if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                {
                    startInfo.CreateNoWindow = true;
                }

                // Execute git clone
                using (Process process = Process.Start(startInfo))
                {
                    if (process == null)
                    {
                        throw new InvalidOperationException("Failed to start git process");
                    }

                    // Read output and error streams
                    string output = process.StandardOutput.ReadToEnd();
                    string error = process.StandardError.ReadToEnd();
                    process.WaitForExit();

                    if (process.ExitCode == 0)
                    {
                        // Clone successful
                        var successMsgBox = MessageBoxManager.GetMessageBoxStandard(
                            "Clone Successful",
                            $"Repository {selectedFork} cloned successfully.",
                            ButtonEnum.Ok,
                            MsBox.Avalonia.Enums.Icon.Success);
                        successMsgBox.ShowAsync();
                    }
                    else
                    {
                        // Clone failed
                        string errorMessage = !string.IsNullOrEmpty(error) ? error : output;
                        if (string.IsNullOrEmpty(errorMessage))
                        {
                            errorMessage = $"Git clone failed with exit code {process.ExitCode}";
                        }
                        var errorMsgBox = MessageBoxManager.GetMessageBoxStandard(
                            "Clone Failed",
                            $"Failed to clone repository: {errorMessage}",
                            ButtonEnum.Ok,
                            MsBox.Avalonia.Enums.Icon.Error);
                        errorMsgBox.ShowAsync();
                    }
                }
            }
            catch (Exception ex)
            {
                // Handle exceptions during git clone execution
                var errorMsgBox = MessageBoxManager.GetMessageBoxStandard(
                    "Clone Failed",
                    $"Failed to clone repository: {ex.Message}",
                    ButtonEnum.Ok,
                    MsBox.Avalonia.Enums.Icon.Error);
                errorMsgBox.ShowAsync();
            }
        }

        /// <summary>
        /// Checks if git is available in the system PATH.
        /// </summary>
        /// <returns>True if git is available, false otherwise.</returns>
        private bool IsGitAvailable()
        {
            try
            {
                ProcessStartInfo startInfo = new ProcessStartInfo
                {
                    FileName = "git",
                    Arguments = "--version",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                };

                if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                {
                    startInfo.CreateNoWindow = true;
                }

                using (Process process = Process.Start(startInfo))
                {
                    if (process == null)
                    {
                        return false;
                    }

                    process.WaitForExit(5000); // 5 second timeout
                    return process.ExitCode == 0;
                }
            }
            catch
            {
                // If we can't start git, it's not available
                return false;
            }
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/dialogs/github_selector.py:630-633
        // Original: def refresh_data(self) -> None:
        private void RefreshData()
        {
            try
            {
                // InitializeRepoData is async, so we need to handle it differently
                InitializeRepoData();
            }
            catch (Exception ex)
            {
                var errorMsgBox = MessageBoxManager.GetMessageBoxStandard(
                    "Refresh Error",
                    $"Failed to refresh data: {ex.Message}",
                    ButtonEnum.Ok,
                    MsBox.Avalonia.Enums.Icon.Error);
                errorMsgBox.ShowAsync();
            }
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/dialogs/github_selector.py:309-316
        // Original: def search_and_highlight(self, partial_file_or_folder_names: list[str]) -> None:
        private void SearchAndHighlight(List<string> partialFileOrFolderNames)
        {
            if (_repoData == null || _repoData.Tree == null || _repoTreeWidget == null)
            {
                return;
            }

            // Find paths that match any of the partial names (case-insensitive, matching against the last part of the path)
            HashSet<string> pathsToHighlight = new HashSet<string>();
            foreach (Utils.TreeInfoData item in _repoData.Tree)
            {
                if (string.IsNullOrEmpty(item.Path))
                {
                    continue;
                }

                // Get the last part of the path (filename or folder name)
                string lastPart = Path.GetFileName(item.Path).ToLowerInvariant();

                // Check if any of the search terms match
                foreach (string searchTerm in partialFileOrFolderNames)
                {
                    if (lastPart.Contains(searchTerm))
                    {
                        pathsToHighlight.Add(item.Path);
                        break;
                    }
                }
            }

            ExpandAndHighlightPaths(pathsToHighlight);
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/dialogs/github_selector.py:318-386
        // Original: def expand_and_highlight_paths(self, paths: set[str]) -> None:
        private void ExpandAndHighlightPaths(HashSet<string> paths)
        {
            if (_repoTreeWidget == null)
            {
                return;
            }

            // Hide all items first
            HideAllItems();

            // Highlight each matching path
            foreach (string path in paths)
            {
                HighlightPath(path);
            }
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/dialogs/github_selector.py:335-359
        // Original: def highlight_path(path: str):
        private void HighlightPath(string path)
        {
            if (_repoTreeWidget == null || string.IsNullOrEmpty(path))
            {
                return;
            }

            // Split path into parts
            string[] parts = path.Split(new[] { '/', '\\' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 0)
            {
                return;
            }

            TreeViewItem currentItem = null;

            // Traverse the tree to find the item
            for (int i = 0; i < parts.Length; i++)
            {
                string part = parts[i];
                TreeViewItem nextItem = null;

                if (currentItem == null)
                {
                    // Top level - search in root items
                    if (_repoTreeWidget.ItemsSource == null)
                    {
                        return;
                    }

                    foreach (TreeViewItem topLevelItem in _repoTreeWidget.ItemsSource)
                    {
                        if (topLevelItem.Header?.ToString() == part)
                        {
                            nextItem = topLevelItem;
                            topLevelItem.Opacity = 1.0;
                            break;
                        }
                    }
                }
                else
                {
                    // Search in children
                    if (currentItem.ItemsSource == null)
                    {
                        return;
                    }

                    foreach (TreeViewItem childItem in currentItem.ItemsSource)
                    {
                        if (childItem.Header?.ToString() == part)
                        {
                            nextItem = childItem;
                            childItem.Opacity = 1.0;
                            break;
                        }
                    }
                }

                if (nextItem == null)
                {
                    // Path not found in tree
                    return;
                }

                currentItem = nextItem;
            }

            // Highlight the found item
            if (currentItem != null)
            {
                // Set background color to yellow for highlighting (matching PyKotor's QBrush(Qt.GlobalColor.yellow))
                currentItem.Background = new SolidColorBrush(Colors.Yellow);
                currentItem.IsExpanded = true;
                currentItem.Opacity = 1.0;

                // If it's a directory (tree), unhide all children
                TreeInfoData itemData = currentItem.Tag as TreeInfoData;
                if (itemData != null && itemData.Type == "tree")
                {
                    UnhideAllChildren(currentItem);
                }
            }
        }

        // Local TreeInfoData class for backward compatibility with existing code
        private class TreeInfoData
        {
            public string Mode { get; set; }
            public string Type { get; set; } // "blob" for files, "tree" for directories
            public string Sha { get; set; }
            public int? Size { get; set; }
            public string Url { get; set; }
            public string Path { get; set; }
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/dialogs/github_selector.py:361-367
        // Original: def unhide_all_children(item: QTreeWidgetItem):
        private void UnhideAllChildren(TreeViewItem item)
        {
            if (item == null || item.ItemsSource == null)
            {
                return;
            }

            foreach (TreeViewItem child in item.ItemsSource)
            {
                child.Opacity = 1.0;
                UnhideAllChildren(child);
            }
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/dialogs/github_selector.py:369-383
        // Original: def hide_all_items(): ... def hide_item(item: QTreeWidgetItem):
        private void HideAllItems()
        {
            if (_repoTreeWidget == null || _repoTreeWidget.ItemsSource == null)
            {
                return;
            }

            foreach (TreeViewItem topLevelItem in _repoTreeWidget.ItemsSource)
            {
                HideItem(topLevelItem);
            }
        }

        private void HideItem(TreeViewItem item)
        {
            if (item == null)
            {
                return;
            }

            // Hide item using opacity (matching PyKotor's setHidden(True) behavior)
            item.Opacity = 0.0;
            // Clear background color (matching PyKotor's QBrush(Qt.GlobalColor.transparent))
            item.Background = new SolidColorBrush(Colors.Transparent);

            if (item.ItemsSource != null)
            {
                foreach (TreeViewItem child in item.ItemsSource)
                {
                    HideItem(child);
                }
            }
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/dialogs/github_selector.py:296-307
        // Original: def unhide_item(item: QTreeWidgetItem):
        private void UnhideAllItems()
        {
            if (_repoTreeWidget == null || _repoTreeWidget.ItemsSource == null)
            {
                return;
            }

            foreach (TreeViewItem topLevelItem in _repoTreeWidget.ItemsSource)
            {
                UnhideItem(topLevelItem);
            }
        }

        private void UnhideItem(TreeViewItem item)
        {
            if (item == null)
            {
                return;
            }

            // Show item using opacity (matching PyKotor's setHidden(False) behavior)
            item.Opacity = 1.0;
            // Clear background color
            item.Background = new SolidColorBrush(Colors.Transparent);

            if (item.ItemsSource != null)
            {
                foreach (TreeViewItem child in item.ItemsSource)
                {
                    UnhideItem(child);
                }
            }
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/dialogs/github_selector.py:388-396
        // Original: def expand_all_items(self):
        private void ExpandAllItems()
        {
            if (_repoTreeWidget == null || _repoTreeWidget.ItemsSource == null)
            {
                return;
            }

            Stack<TreeViewItem> stack = new Stack<TreeViewItem>();

            // Add all top-level items to stack
            foreach (TreeViewItem item in _repoTreeWidget.ItemsSource)
            {
                stack.Push(item);
            }

            // Expand all items using iterative approach (matching PyKotor's stack-based implementation)
            while (stack.Count > 0)
            {
                TreeViewItem item = stack.Pop();
                item.IsExpanded = true;

                if (item.ItemsSource != null)
                {
                    foreach (TreeViewItem child in item.ItemsSource)
                    {
                        stack.Push(child);
                    }
                }
            }
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/dialogs/github_selector.py:398-404
        // Original: def collapse_all_items(self):
        private void CollapseAllItems()
        {
            if (_repoTreeWidget == null || _repoTreeWidget.ItemsSource == null)
            {
                return;
            }

            Stack<TreeViewItem> stack = new Stack<TreeViewItem>();

            // Add all top-level items to stack
            foreach (TreeViewItem item in _repoTreeWidget.ItemsSource)
            {
                stack.Push(item);
            }

            // Collapse all items using iterative approach (matching PyKotor's stack-based implementation)
            while (stack.Count > 0)
            {
                TreeViewItem item = stack.Pop();
                item.IsExpanded = false;

                if (item.ItemsSource != null)
                {
                    foreach (TreeViewItem child in item.ItemsSource)
                    {
                        stack.Push(child);
                    }
                }
            }
        }

        public string SelectedPath => _selectedPath;
    }
}
