// Matching PyKotor implementation at vendor/PyKotor/Tools/KotorDiff/src/kotordiff/gui.py:51-477
// Original: class KotorDiffApp(ThemedApp): ... GUI application with dark/orange themed interface
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Platform.Storage;
using Andastra.Parsing.Installation;
using Andastra.Parsing.Common;
using KotorDiff.App;
using KotorDiff.Cli;

namespace KotorDiff.Gui
{
    // Matching PyKotor implementation at vendor/PyKotor/Tools/KotorDiff/src/kotordiff/gui.py:51-477
    // Original: class KotorDiffApp(ThemedApp): ... KotorDiff GUI application with dark/orange themed interface
    public partial class KotorDiffApp : Window
    {
        // KotorDiff-specific state
        // Matching Python: self.path1: str = ""; self.path2: str = ""; etc.
        private string _path1 = "";
        private string _path2 = "";
        private string _tslpatchdataPath = "";
        private string _iniFilename = "changes.ini";
        private bool _compareHashes = true;
        private bool _taskRunning = false;

        // Matching Python: def __init__(self): ... super().__init__(title="KotorDiff - Holocron Toolset", ...)
        public KotorDiffApp()
        {
            InitializeComponent();
            SetupUI();
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
        }

        // Matching Python: def initialize_ui_controls(self): ... Initialize UI with dark-themed KotorDiff styling
        private void SetupUI()
        {
            // Populate installation paths in comboboxes
            var installationPaths = GetInstallationPaths();
            if (Path1ComboBox != null)
            {
                Path1ComboBox.Items = installationPaths;
            }
            if (Path2ComboBox != null)
            {
                Path2ComboBox.Items = installationPaths;
            }

            // Set up radio button handlers
            if (Path1RadioInstall != null)
            {
                Path1RadioInstall.Checked += (s, e) => UpdatePath1State();
            }
            if (Path1RadioCustom != null)
            {
                Path1RadioCustom.Checked += (s, e) => UpdatePath1State();
            }
            if (Path2RadioInstall != null)
            {
                Path2RadioInstall.Checked += (s, e) => UpdatePath2State();
            }
            if (Path2RadioCustom != null)
            {
                Path2RadioCustom.Checked += (s, e) => UpdatePath2State();
            }

            // Set default values
            if (IniFilenameTextBox != null)
            {
                IniFilenameTextBox.Text = "changes.ini";
            }
            if (LogLevelComboBox != null)
            {
                LogLevelComboBox.SelectedIndex = 1; // "info"
            }
        }

        // Matching Python: def _get_installation_paths(self) -> list[str]: ... Get list of KOTOR installation paths
        private List<string> GetInstallationPaths()
        {
            var paths = new List<string>();
            try
            {
                // Try to find KOTOR installations from default locations
                // Matching Python: find_kotor_paths_from_default().values()
                // Using common default locations for KOTOR installations
                var defaultLocations = new[]
                {
                    Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "Steam", "steamapps", "common", "swkotor"),
                    Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "Steam", "steamapps", "common", "swkotor2"),
                    Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Steam", "steamapps", "common", "swkotor"),
                    Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Steam", "steamapps", "common", "swkotor2"),
                    Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "LucasArts", "SWKotOR"),
                    Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "LucasArts", "SWKotOR"),
                };

                foreach (var location in defaultLocations)
                {
                    if (Directory.Exists(location) && File.Exists(Path.Combine(location, "chitin.key")))
                    {
                        if (!paths.Contains(location))
                        {
                            paths.Add(location);
                        }
                    }
                }

                // Also check environment variables
                string k1Path = Environment.GetEnvironmentVariable("K1_PATH");
                if (!string.IsNullOrEmpty(k1Path) && Directory.Exists(k1Path) && !paths.Contains(k1Path))
                {
                    paths.Add(k1Path);
                }

                string k2Path = Environment.GetEnvironmentVariable("K2_PATH");
                if (!string.IsNullOrEmpty(k2Path) && Directory.Exists(k2Path) && !paths.Contains(k2Path))
                {
                    paths.Add(k2Path);
                }
            }
            catch (Exception)
            {
                // Ignore errors when finding paths
            }

            return paths;
        }

        // Matching Python: def _update_path1_state(self): ... Update path1 combobox based on radio selection
        private void UpdatePath1State()
        {
            if (Path1ComboBox == null) return;

            if (Path1RadioInstall != null && Path1RadioInstall.IsChecked == true)
            {
                Path1ComboBox.Items = GetInstallationPaths();
            }
            else
            {
                // In custom mode, allow typing (Items can be empty or null)
                Path1ComboBox.Items = null;
            }
        }

        // Matching Python: def _update_path2_state(self): ... Update path2 combobox based on radio selection
        private void UpdatePath2State()
        {
            if (Path2ComboBox == null) return;

            if (Path2RadioInstall != null && Path2RadioInstall.IsChecked == true)
            {
                Path2ComboBox.Items = GetInstallationPaths();
            }
            else
            {
                Path2ComboBox.Items = null;
            }
        }

        // Matching Python: def _browse_path1(self): ... Browse for path 1
        private async void OnPath1BrowseClick(object sender, RoutedEventArgs e)
        {
            var dialog = new OpenFolderDialog
            {
                Title = "Select Path 1 (Mine/Modified)"
            };

            var result = await dialog.ShowAsync(this);
            if (!string.IsNullOrEmpty(result))
            {
                if (Path1ComboBox != null)
                {
                    Path1ComboBox.Text = result;
                }
                if (Path1RadioCustom != null)
                {
                    Path1RadioCustom.IsChecked = true;
                }
                UpdatePath1State();
            }
        }

        // Matching Python: def _browse_path2(self): ... Browse for path 2
        private async void OnPath2BrowseClick(object sender, RoutedEventArgs e)
        {
            var dialog = new OpenFolderDialog
            {
                Title = "Select Path 2 (Older/Vanilla)"
            };

            var result = await dialog.ShowAsync(this);
            if (!string.IsNullOrEmpty(result))
            {
                if (Path2ComboBox != null)
                {
                    Path2ComboBox.Text = result;
                }
                if (Path2RadioCustom != null)
                {
                    Path2RadioCustom.IsChecked = true;
                }
                UpdatePath2State();
            }
        }

        // Matching Python: def _browse_tslpatchdata(self): ... Browse for TSLPatchData output folder
        private async void OnTslPatchDataBrowseClick(object sender, RoutedEventArgs e)
        {
            var dialog = new OpenFolderDialog
            {
                Title = "Select TSLPatchData Output Folder"
            };

            var result = await dialog.ShowAsync(this);
            if (!string.IsNullOrEmpty(result) && TslPatchDataTextBox != null)
            {
                TslPatchDataTextBox.Text = result;
            }
        }

        // Matching Python: def validate_inputs(self) -> bool: ... Validate user inputs before running diff
        private bool ValidateInputs()
        {
            if (_taskRunning)
            {
                ShowMessage("Task Running", "Wait for the current task to finish.");
                return false;
            }

            string path1 = Path1ComboBox?.Text?.Trim() ?? "";
            string path2 = Path2ComboBox?.Text?.Trim() ?? "";

            if (string.IsNullOrEmpty(path1))
            {
                ShowMessage("Missing Path", "Please select or enter Path 1 (Mine/Modified).");
                return false;
            }

            if (string.IsNullOrEmpty(path2))
            {
                ShowMessage("Missing Path", "Please select or enter Path 2 (Older/Vanilla).");
                return false;
            }

            if (!Directory.Exists(path1))
            {
                ShowError("Invalid Path", $"Path 1 does not exist: {path1}");
                return false;
            }

            if (!Directory.Exists(path2))
            {
                ShowError("Invalid Path", $"Path 2 does not exist: {path2}");
                return false;
            }

            return true;
        }

        // Matching Python: def run_diff(self): ... Start the diff operation in a background thread
        private void OnRunDiffClick(object sender, RoutedEventArgs e)
        {
            if (!ValidateInputs())
            {
                return;
            }

            // Run diff in background thread
            Task.Run(() => RunDiffThread());
        }

        // Matching Python: def _run_diff_thread(self, should_cancel: Event): ... Execute the diff operation
        private void RunDiffThread()
        {
            SetState(true);
            ClearOutput();

            try
            {
                string path1Str = Path1ComboBox?.Text?.Trim() ?? "";
                string path2Str = Path2ComboBox?.Text?.Trim() ?? "";
                string tslpatchdataStr = TslPatchDataTextBox?.Text?.Trim() ?? "";

                // Don't use placeholder text as path
                if (tslpatchdataStr == "Path to tslpatchdata folder")
                {
                    tslpatchdataStr = "";
                }

                // Resolve paths to Installation or Path objects
                // Matching Python: resolved_paths: list[Path | Installation] = []
                var resolvedPaths = new List<object>();

                foreach (string pathStr in new[] { path1Str, path2Str })
                {
                    try
                    {
                        // Try to create an Installation object (for KOTOR installations)
                        // Matching Python: installation = Installation(path_obj)
                        var installation = new Installation(pathStr);
                        resolvedPaths.Add(installation);
                        LogToUI($"[INFO] Loaded Installation for: {pathStr}");
                    }
                    catch (Exception)
                    {
                        // Fall back to Path object (for folders/files)
                        // Matching Python: resolved_paths.append(path_obj)
                        resolvedPaths.Add(pathStr);
                        LogToUI($"[INFO] Using Path (not Installation) for: {pathStr}");
                    }
                }

                // Create configuration
                // Matching Python: config = KotorDiffConfig(...)
                string logLevel = "info";
                if (LogLevelComboBox != null && LogLevelComboBox.SelectedIndex >= 0)
                {
                    var selectedItem = LogLevelComboBox.SelectedItem;
                    if (selectedItem is ComboBoxItem item)
                    {
                        logLevel = item.Content?.ToString() ?? "info";
                    }
                    else if (selectedItem is string str)
                    {
                        logLevel = str;
                    }
                }

                var config = new KotorDiffConfig
                {
                    Paths = resolvedPaths,
                    TslPatchDataPath = !string.IsNullOrEmpty(tslpatchdataStr) ? new DirectoryInfo(tslpatchdataStr) : null,
                    IniFilename = IniFilenameTextBox?.Text?.Trim() ?? "changes.ini",
                    LogLevel = logLevel,
                    CompareHashes = CompareHashesCheckBox?.IsChecked ?? true,
                };

                LogToUI($"\n{'='.PadRight(60, '=')}");
                LogToUI("Starting KotorDiff comparison...");
                LogToUI($"Path 1: {path1Str}");
                LogToUI($"Path 2: {path2Str}");
                if (!string.IsNullOrEmpty(tslpatchdataStr))
                {
                    LogToUI($"TSLPatchData Output: {tslpatchdataStr}");
                }
                LogToUI($"{'='.PadRight(60, '=')}\n");

                // Run the diff
                // Matching Python: exit_code = run_application(config)
                int exitCode = AppRunner.RunApplication(config);

                LogToUI($"\n{'='.PadRight(60, '=')}");
                if (exitCode == 0)
                {
                    LogToUI("[SUCCESS] Diff completed successfully!");
                    ShowMessage("Diff Complete", "Comparison completed successfully!");
                }
                else
                {
                    LogToUI($"[WARNING] Diff completed with exit code: {exitCode}");
                }
                LogToUI($"{'='.PadRight(60, '=')}");
            }
            catch (Exception ex)
            {
                LogToUI($"[ERROR] Error during diff operation: {ex.GetType().Name}: {ex.Message}");
                LogToUI(ex.StackTrace ?? "");
            }
            finally
            {
                SetState(false);
            }
        }

        // Matching Python: def _log_to_ui(self, message: str, tag: str = "INFO"): ... Log a message to the UI text widget
        private void LogToUI(string message)
        {
            if (OutputTextBox == null) return;

            Avalonia.Threading.Dispatcher.UIThread.Post(() =>
            {
                if (OutputTextBox != null)
                {
                    OutputTextBox.Text += message + Environment.NewLine;
                    // Auto-scroll to bottom
                    OutputTextBox.CaretIndex = OutputTextBox.Text.Length;
                }
            });
        }

        // Matching Python: def set_state(self, *, state: bool): ... Set the task running state and update UI accordingly
        private void SetState(bool state)
        {
            Avalonia.Threading.Dispatcher.UIThread.Post(() =>
            {
                if (state)
                {
                    ResetProgressBar();
                    _taskRunning = true;
                    if (RunDiffButton != null) RunDiffButton.IsEnabled = false;
                    if (Path1BrowseButton != null) Path1BrowseButton.IsEnabled = false;
                    if (Path2BrowseButton != null) Path2BrowseButton.IsEnabled = false;
                    if (TslPatchDataBrowseButton != null) TslPatchDataBrowseButton.IsEnabled = false;
                }
                else
                {
                    _taskRunning = false;
                    if (RunDiffButton != null) RunDiffButton.IsEnabled = true;
                    if (Path1BrowseButton != null) Path1BrowseButton.IsEnabled = true;
                    if (Path2BrowseButton != null) Path2BrowseButton.IsEnabled = true;
                    if (TslPatchDataBrowseButton != null) TslPatchDataBrowseButton.IsEnabled = true;
                }
            });
        }

        private void ResetProgressBar()
        {
            Avalonia.Threading.Dispatcher.UIThread.Post(() =>
            {
                if (ProgressBar != null)
                {
                    ProgressBar.Value = 0;
                    ProgressBar.IsIndeterminate = true;
                }
            });
        }

        // Matching Python: def clear_main_text(self): ... Clear the output text
        private void ClearOutput()
        {
            Avalonia.Threading.Dispatcher.UIThread.Post(() =>
            {
                if (OutputTextBox != null)
                {
                    OutputTextBox.Text = "";
                }
            });
        }

        private void OnClearClick(object sender, RoutedEventArgs e)
        {
            ClearOutput();
        }

        private void OnCloseClick(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void ShowMessage(string title, string message)
        {
            Avalonia.Threading.Dispatcher.UIThread.Post(async () =>
            {
                var okButton = new Button
                {
                    Content = "OK",
                    Width = 100,
                    HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center,
                    Margin = new Avalonia.Thickness(0, 10, 0, 0)
                };

                var dialog = new Window
                {
                    Title = title,
                    Width = 400,
                    Height = 150,
                    WindowStartupLocation = WindowStartupLocation.CenterOwner,
                    Content = new StackPanel
                    {
                        Margin = new Avalonia.Thickness(20),
                        Children =
                        {
                            new TextBlock { Text = message, TextWrapping = Avalonia.Media.TextWrapping.Wrap },
                            okButton
                        }
                    }
                };

                okButton.Click += (s, e) => dialog.Close();
                await dialog.ShowDialog(this);
            });
        }

        private void ShowError(string title, string message)
        {
            Avalonia.Threading.Dispatcher.UIThread.Post(async () =>
            {
                var okButton = new Button
                {
                    Content = "OK",
                    Width = 100,
                    HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center,
                    Margin = new Avalonia.Thickness(0, 10, 0, 0)
                };

                var dialog = new Window
                {
                    Title = title,
                    Width = 500,
                    Height = 200,
                    WindowStartupLocation = WindowStartupLocation.CenterOwner,
                    Content = new StackPanel
                    {
                        Margin = new Avalonia.Thickness(20),
                        Children =
                        {
                            new TextBlock { Text = message, TextWrapping = Avalonia.Media.TextWrapping.Wrap },
                            okButton
                        }
                    }
                };

                okButton.Click += (s, e) => dialog.Close();
                await dialog.ShowDialog(this);
            });
        }
    }
}

