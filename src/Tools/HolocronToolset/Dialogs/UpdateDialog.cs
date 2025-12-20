using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
using HolocronToolset.Config;
using HolocronToolset.Utils;
using Markdig;
using Newtonsoft.Json.Linq;
using MsBox.Avalonia;
using MsBox.Avalonia.Enums;

namespace HolocronToolset.Dialogs
{
    // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/dialogs/update_dialog.py:83
    // Original: class UpdateDialog(QDialog):
    public partial class UpdateDialog : Window
    {
        private Dictionary<string, object> _remoteInfo;
        private List<object> _releases;
        private Dictionary<string, List<object>> _forksCache;
        private CheckBox _preReleaseCheckbox;
        private ComboBox _forkComboBox;
        private ComboBox _releaseComboBox;
        private TextBox _changelogEdit;
        private Button _fetchReleasesButton;
        private Button _installSelectedButton;
        private Button _updateLatestButton;

        // Public parameterless constructor for XAML
        public UpdateDialog() : this(null)
        {
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/dialogs/update_dialog.py:84-100
        // Original: def __init__(self, parent=None):
        public UpdateDialog(Window parent)
        {
            InitializeComponent();
            Title = "Update Application";
            Width = 800;
            Height = 600;
            _remoteInfo = new Dictionary<string, object>();
            _releases = new List<object>();
            _forksCache = new Dictionary<string, List<object>>();
            SetupUI();
            InitConfig();
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
            var mainPanel = new StackPanel { Margin = new Avalonia.Thickness(10), Spacing = 10 };

            _preReleaseCheckbox = new CheckBox { Content = "Include Pre-releases" };
            mainPanel.Children.Add(_preReleaseCheckbox);

            _fetchReleasesButton = new Button { Content = "Fetch Releases", Height = 50 };
            _fetchReleasesButton.Click += (s, e) => InitConfig();
            mainPanel.Children.Add(_fetchReleasesButton);

            _forkComboBox = new ComboBox { MinWidth = 300 };
            mainPanel.Children.Add(new TextBlock { Text = "Select Fork:" });
            mainPanel.Children.Add(_forkComboBox);

            _releaseComboBox = new ComboBox { MinWidth = 300 };
            mainPanel.Children.Add(new TextBlock { Text = "Select Release:" });
            mainPanel.Children.Add(_releaseComboBox);

            _installSelectedButton = new Button { Content = "Install Selected", Width = 150, Height = 30 };
            _installSelectedButton.Click += (s, e) => OnInstallSelected();
            mainPanel.Children.Add(_installSelectedButton);

            _changelogEdit = new TextBox { IsReadOnly = true, AcceptsReturn = true, MinHeight = 200 };
            mainPanel.Children.Add(_changelogEdit);

            _updateLatestButton = new Button { Content = "Update to Latest", Height = 50 };
            _updateLatestButton.Click += (s, e) => OnUpdateLatestClicked();
            mainPanel.Children.Add(_updateLatestButton);

            Content = mainPanel;
        }

        private void SetupUI()
        {
            // Find controls from XAML
            _preReleaseCheckbox = this.FindControl<CheckBox>("preReleaseCheckbox");
            _forkComboBox = this.FindControl<ComboBox>("forkComboBox");
            _releaseComboBox = this.FindControl<ComboBox>("releaseComboBox");
            _changelogEdit = this.FindControl<TextBox>("changelogEdit");
            _fetchReleasesButton = this.FindControl<Button>("fetchReleasesButton");
            _installSelectedButton = this.FindControl<Button>("installSelectedButton");
            _updateLatestButton = this.FindControl<Button>("updateLatestButton");

            if (_fetchReleasesButton != null)
            {
                _fetchReleasesButton.Click += (s, e) => InitConfig();
            }
            if (_installSelectedButton != null)
            {
                _installSelectedButton.Click += (s, e) => OnInstallSelected();
            }
            if (_updateLatestButton != null)
            {
                _updateLatestButton.Click += (s, e) => OnUpdateLatestClicked();
            }
            if (_preReleaseCheckbox != null)
            {
                _preReleaseCheckbox.IsCheckedChanged += (s, e) => OnPreReleaseChanged();
            }
            if (_forkComboBox != null)
            {
                _forkComboBox.SelectionChanged += (s, e) => OnForkChanged();
            }
            if (_releaseComboBox != null)
            {
                _releaseComboBox.SelectionChanged += (s, e) => OnReleaseChanged();
            }
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/dialogs/update_dialog.py:102-106
        // Original: def include_prerelease(self) -> bool:
        private bool IncludePrerelease()
        {
            return _preReleaseCheckbox?.IsChecked ?? false;
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/dialogs/update_dialog.py:105-106
        // Original: def set_prerelease(self, value):
        private void SetPrerelease(bool value)
        {
            if (_preReleaseCheckbox != null)
            {
                _preReleaseCheckbox.IsChecked = value;
            }
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/dialogs/update_dialog.py:180-185
        // Original: def init_config(self):
        private void InitConfig()
        {
            SetPrerelease(false);
            
            // Show loading dialog while fetching forks and releases
            var loaderDialog = new AsyncLoaderDialog(
                this,
                "Fetching Releases...",
                () =>
                {
                    // Fetch and cache all forks with releases (synchronous wrapper for async operation)
                    try
                    {
                        _forksCache = UpdateGitHub.FetchAndCacheForksAsync().Result;
                        
                        // Add the main repository to the cache
                        var mainRepoReleases = UpdateGitHub.FetchForkReleasesAsync("th3w1zard1/PyKotor", includeAll: true).Result;
                        _forksCache["th3w1zard1/PyKotor"] = mainRepoReleases;
                    }
                    catch (Exception ex)
                    {
                        System.Console.WriteLine($"Error fetching forks and releases: {ex}");
                        throw;
                    }
                    
                    return (object)null;
                },
                "Error Fetching Releases",
                startImmediately: true
            );
            
            loaderDialog.Show();
            
            // Wait for the async operation to complete, then update UI on UI thread
            Task.Run(async () =>
            {
                // Wait a bit for the dialog to show
                await Task.Delay(100);
                
                // Wait for the operation to complete (check if dialog is still open)
                while (loaderDialog.IsVisible)
                {
                    await Task.Delay(100);
                }
                
                // Update UI on UI thread
                Dispatcher.UIThread.Post(() =>
                {
                    PopulateForkComboBox();
                    if (_forkComboBox != null && _forkComboBox.SelectedIndex >= 0)
                    {
                        OnForkChanged();
                    }
                });
            });
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/dialogs/update_dialog.py:225-229
        // Original: def on_pre_release_changed(self, state: bool):
        private void OnPreReleaseChanged()
        {
            FilterReleasesBasedOnPrerelease();
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/dialogs/update_dialog.py:219-223
        // Original: def populate_fork_combo_box(self):
        private void PopulateForkComboBox()
        {
            if (_forkComboBox == null)
            {
                return;
            }
            
            _forkComboBox.Items.Clear();
            _forkComboBox.Items.Add("th3w1zard1/PyKotor");
            
            foreach (var fork in _forksCache.Keys)
            {
                if (fork != "th3w1zard1/PyKotor")
                {
                    _forkComboBox.Items.Add(fork);
                }
            }
            
            // Select the first item (main repo)
            if (_forkComboBox.Items.Count > 0)
            {
                _forkComboBox.SelectedIndex = 0;
            }
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/dialogs/update_dialog.py:231-249
        // Original: def filter_releases_based_on_prerelease(self):
        private void FilterReleasesBasedOnPrerelease()
        {
            if (_forkComboBox == null || _releaseComboBox == null)
            {
                return;
            }
            
            string selectedFork = _forkComboBox.SelectedItem?.ToString() ?? "";
            if (string.IsNullOrEmpty(selectedFork) || !_forksCache.ContainsKey(selectedFork))
            {
                _releases = new List<object>();
                _releaseComboBox.Items.Clear();
                _changelogEdit.Text = "";
                return;
            }
            
            // Get all releases for the selected fork
            var allReleases = _forksCache[selectedFork];
            bool includePrerelease = IncludePrerelease();
            
            // Filter releases based on criteria
            _releases = UpdateGitHub.FilterReleases(allReleases, includePrerelease);
            
            // If no releases found and prerelease is not included, try with prerelease enabled
            if (!includePrerelease && _releases.Count == 0)
            {
                System.Console.WriteLine("No releases found, attempt to try again with prereleases");
                SetPrerelease(true);
                return; // Will be called again with prerelease enabled
            }
            
            // Sort releases: newer versions first
            _releases = _releases.OrderByDescending(r =>
            {
                if (r is JObject release)
                {
                    string tagName = release["tag_name"]?.Value<string>() ?? "";
                    string version = ConfigVersion.ToolsetTagToVersion(tagName);
                    var isNewer = ConfigUpdate.IsRemoteVersionNewer("0.0.0", version);
                    return isNewer == true;
                }
                return false;
            }).ToList();
            
            // Update release combo box
            _releaseComboBox.Items.Clear();
            _changelogEdit.Text = "";
            
            foreach (var release in _releases)
            {
                if (release is JObject releaseObj)
                {
                    string tagName = releaseObj["tag_name"]?.Value<string>() ?? "";
                    _releaseComboBox.Items.Add(new ComboBoxItem { Content = tagName, Tag = release });
                }
            }
            
            // Select first release if available
            if (_releaseComboBox.Items.Count > 0)
            {
                _releaseComboBox.SelectedIndex = 0;
                OnReleaseChanged();
            }
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/dialogs/update_dialog.py:251-257
        // Original: def on_fork_changed(self, index: int):
        private void OnForkChanged()
        {
            FilterReleasesBasedOnPrerelease();
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/dialogs/update_dialog.py:259-262
        // Original: def get_selected_tag(self) -> str:
        private string GetSelectedTag()
        {
            if (_releaseComboBox?.SelectedItem is ComboBoxItem item && item.Tag is JObject release)
            {
                return release["tag_name"]?.Value<string>() ?? "";
            }
            return "";
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/dialogs/update_dialog.py:263-273
        // Original: def on_release_changed(self, index: int):
        private void OnReleaseChanged()
        {
            if (_releaseComboBox == null || _changelogEdit == null)
            {
                return;
            }
            
            int index = _releaseComboBox.SelectedIndex;
            if (index < 0 || index >= _releases.Count)
            {
                _changelogEdit.Text = "";
                return;
            }
            
            var selectedItem = _releaseComboBox.Items[index] as ComboBoxItem;
            if (selectedItem?.Tag is JObject release)
            {
                string body = release["body"]?.Value<string>() ?? "";
                
                // Convert markdown to HTML using Markdig
                var pipeline = new MarkdownPipelineBuilder().UseAdvancedExtensions().Build();
                string htmlBody = Markdown.ToHtml(body, pipeline);
                
                // Set the HTML content in the changelog edit
                // Note: TextBox doesn't support HTML, so we'll show the markdown text
                // In a full implementation, we'd use a WebView or RichTextBox for HTML rendering
                _changelogEdit.Text = body;
            }
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/dialogs/update_dialog.py:275-280
        // Original: def get_latest_release(self) -> GithubRelease | None:
        private object GetLatestRelease()
        {
            if (_releases != null && _releases.Count > 0)
            {
                return _releases[0];
            }
            
            // If no releases found, try enabling prerelease
            SetPrerelease(true);
            FilterReleasesBasedOnPrerelease();
            
            if (_releases != null && _releases.Count > 0)
            {
                return _releases[0];
            }
            
            return null;
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/dialogs/update_dialog.py:281-286
        // Original: def on_update_latest_clicked(self):
        private void OnUpdateLatestClicked()
        {
            object latestRelease = GetLatestRelease();
            if (latestRelease == null)
            {
                var msgBox = MessageBoxManager.GetMessageBoxStandard(
                    "No Release Found",
                    "No toolset releases found?",
                    ButtonEnum.Ok,
                    MsBox.Avalonia.Enums.Icon.Information);
                msgBox.ShowAsync();
                return;
            }
            
            StartUpdate(latestRelease);
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/dialogs/update_dialog.py:288-294
        // Original: def on_install_selected(self):
        private void OnInstallSelected()
        {
            if (_releaseComboBox == null)
            {
                return;
            }
            
            int index = _releaseComboBox.SelectedIndex;
            if (index < 0 || index >= _releases.Count)
            {
                var msgBox = MessageBoxManager.GetMessageBoxStandard(
                    "Select a release",
                    "No release selected, select one first.",
                    ButtonEnum.Ok,
                    MsBox.Avalonia.Enums.Icon.Information);
                msgBox.ShowAsync();
                return;
            }
            
            var selectedItem = _releaseComboBox.Items[index] as ComboBoxItem;
            if (selectedItem?.Tag is object release)
            {
                StartUpdate(release);
            }
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/dialogs/update_dialog.py:296-379
        // Original: def start_update(self, release: GithubRelease):
        private void StartUpdate(object release)
        {
            if (release == null)
            {
                return;
            }
            
            if (!(release is JObject releaseObj))
            {
                System.Console.WriteLine("Invalid release object");
                return;
            }
            
            // Get platform and architecture information
            string osName = System.Runtime.InteropServices.RuntimeInformation.IsOSPlatform(System.Runtime.InteropServices.OSPlatform.Windows) ? "windows" :
                           System.Runtime.InteropServices.RuntimeInformation.IsOSPlatform(System.Runtime.InteropServices.OSPlatform.OSX) ? "darwin" :
                           System.Runtime.InteropServices.RuntimeInformation.IsOSPlatform(System.Runtime.InteropServices.OSPlatform.Linux) ? "linux" : "unknown";
            
            string procArch = System.Runtime.InteropServices.RuntimeInformation.ProcessArchitecture == System.Runtime.InteropServices.Architecture.X64 ? "x64" :
                             System.Runtime.InteropServices.RuntimeInformation.ProcessArchitecture == System.Runtime.InteropServices.Architecture.X86 ? "x86" : "unknown";
            
            // Find matching asset
            string downloadUrl = null;
            var assets = releaseObj["assets"] as JArray;
            if (assets != null)
            {
                foreach (var asset in assets)
                {
                    if (asset is JObject assetObj)
                    {
                        string assetName = assetObj["name"]?.Value<string>()?.ToLower() ?? "";
                        string browserDownloadUrl = assetObj["browser_download_url"]?.Value<string>() ?? "";
                        
                        if (assetName.Contains(procArch) && assetName.Contains(osName))
                        {
                            downloadUrl = browserDownloadUrl;
                            break;
                        }
                    }
                }
            }
            
            if (string.IsNullOrEmpty(downloadUrl))
            {
                // No matching asset found
                var msgBox = MessageBoxManager.GetMessageBoxStandard(
                    "No Asset Found",
                    $"There are no binaries available for download for release '{releaseObj["tag_name"]?.Value<string>() ?? "unknown"}'.",
                    ButtonEnum.Ok,
                    MsBox.Avalonia.Enums.Icon.Warning);
                msgBox.ShowAsync();
                return;
            }
            
            // Start the update process
            Task.Run(async () =>
            {
                try
                {
                    await UpdateProcess.StartUpdateProcessAsync(release, downloadUrl);
                }
                catch (NotImplementedException)
                {
                    // Update process not yet fully implemented
                    Dispatcher.UIThread.Post(() =>
                    {
                        var msgBox = MessageBoxManager.GetMessageBoxStandard(
                            "Update Not Available",
                            "The update process is not yet fully implemented. Please download and install updates manually.",
                            ButtonEnum.Ok,
                            MsBox.Avalonia.Enums.Icon.Information);
                        msgBox.ShowAsync();
                    });
                }
                catch (Exception ex)
                {
                    Dispatcher.UIThread.Post(() =>
                    {
                        var msgBox = MessageBoxManager.GetMessageBoxStandard(
                            "Update Error",
                            $"An error occurred while starting the update: {ex.Message}",
                            ButtonEnum.Ok,
                            MsBox.Avalonia.Enums.Icon.Error);
                        msgBox.ShowAsync();
                    });
                }
            });
        }
    }
}
