using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Andastra.Parsing.Common;
using Andastra.Parsing.Tools;
using Andastra.Runtime.Core;
using Andastra.Runtime.Game.Core;
using Andastra.Runtime.Graphics.Common.Enums;
using GameType = Andastra.Parsing.Common.BioWareGame;

namespace Andastra.Game.GUI
{
    /// <summary>
    /// Cross-platform game launcher dialog with game selection and path configuration.
    /// </summary>
    /// <remarks>
    /// Launcher UI:
    /// - Cross-platform using Avalonia (Windows/Mac/Linux)
    /// - Native look-and-feel on each platform
    /// - Game selection combobox (K1, K2, NWN, DA:O, DA2)
    /// - Editable installation path combobox with browse button
    /// - Start button to launch the game
    /// - Error dialog for launch failures
    /// </remarks>
    public class GameLauncher : Window
    {
        private ComboBox _gameComboBox;
        private ComboBox _graphicsBackendComboBox;
        private TextBox _pathTextBox;
        private Button _browseButton;
        private Button _settingsButton;
        private Button _startButton;
        private TextBlock _gameLabel;
        private TextBlock _graphicsBackendLabel;
        private TextBlock _pathLabel;
        private GraphicsSettingsData _graphicsSettings;
        private List<GraphicsBackendItem> _graphicsBackendItems;
        private List<GameItem> _gameItems;

        /// <summary>
        /// Gets the selected game.
        /// </summary>
        public GameType SelectedGame { get; private set; }

        /// <summary>
        /// Gets the selected graphics backend.
        /// </summary>
        public GraphicsBackendType SelectedGraphicsBackend { get; private set; }

        /// <summary>
        /// Gets the selected installation path.
        /// </summary>
        public string SelectedPath { get; private set; }

        /// <summary>
        /// Gets the graphics settings.
        /// </summary>
        public GraphicsSettingsData GraphicsSettings => _graphicsSettings;

        /// <summary>
        /// Gets whether the user clicked Start.
        /// </summary>
        public bool StartClicked { get; private set; }

        /// <summary>
        /// Creates a new game launcher dialog.
        /// </summary>
        public GameLauncher()
        {
            Title = "Andastra Game Launcher";
            Width = 600;
            Height = 250;
            CanResize = false;
            WindowStartupLocation = WindowStartupLocation.CenterScreen;

            SelectedGame = GameType.K1; // Default
            SelectedGraphicsBackend = GraphicsBackendType.MonoGame; // Default
            _graphicsSettings = new GraphicsSettingsData();
            _gameItems = new List<GameItem>();

            InitializeComponent();
            PopulateGameComboBox();
            PopulateGraphicsBackendComboBox();
            _gameComboBox.SelectedIndex = 0;
            _graphicsBackendComboBox.SelectedIndex = 0;
            _gameComboBox.SelectionChanged += GameComboBox_SelectionChanged;
            _graphicsBackendComboBox.SelectionChanged += GraphicsBackendComboBox_SelectionChanged;
            UpdatePathComboBox();
        }

        private void InitializeComponent()
        {
            var mainGrid = new Grid
            {
                Margin = new Thickness(20),
                RowDefinitions = new RowDefinitions("Auto,10,Auto,10,Auto,10,Auto"),
                ColumnDefinitions = new ColumnDefinitions("Auto,10,*,10,Auto")
            };

            // Game selection row
            _gameLabel = new TextBlock
            {
                Text = "Game:",
                VerticalAlignment = VerticalAlignment.Center
            };
            Grid.SetRow(_gameLabel, 0);
            Grid.SetColumn(_gameLabel, 0);
            mainGrid.Children.Add(_gameLabel);

            _gameComboBox = new ComboBox
            {
                MinWidth = 400,
                VerticalAlignment = VerticalAlignment.Center
            };
            Grid.SetRow(_gameComboBox, 0);
            Grid.SetColumn(_gameComboBox, 2);
            Grid.SetColumnSpan(_gameComboBox, 3);
            mainGrid.Children.Add(_gameComboBox);

            // Graphics backend selection row
            _graphicsBackendLabel = new TextBlock
            {
                Text = "Graphics Backend:",
                VerticalAlignment = VerticalAlignment.Center
            };
            Grid.SetRow(_graphicsBackendLabel, 2);
            Grid.SetColumn(_graphicsBackendLabel, 0);
            mainGrid.Children.Add(_graphicsBackendLabel);

            _graphicsBackendComboBox = new ComboBox
            {
                MinWidth = 300,
                VerticalAlignment = VerticalAlignment.Center
            };
            Grid.SetRow(_graphicsBackendComboBox, 2);
            Grid.SetColumn(_graphicsBackendComboBox, 2);
            mainGrid.Children.Add(_graphicsBackendComboBox);

            _settingsButton = new Button
            {
                Content = "Settings...",
                Width = 100,
                VerticalAlignment = VerticalAlignment.Center
            };
            _settingsButton.Click += SettingsButton_Click;
            Grid.SetRow(_settingsButton, 2);
            Grid.SetColumn(_settingsButton, 4);
            mainGrid.Children.Add(_settingsButton);

            // Path selection row
            _pathLabel = new TextBlock
            {
                Text = "Installation Path:",
                VerticalAlignment = VerticalAlignment.Center
            };
            Grid.SetRow(_pathLabel, 4);
            Grid.SetColumn(_pathLabel, 0);
            mainGrid.Children.Add(_pathLabel);

            _pathTextBox = new TextBox
            {
                MinWidth = 300,
                VerticalAlignment = VerticalAlignment.Center
            };
            Grid.SetRow(_pathTextBox, 4);
            Grid.SetColumn(_pathTextBox, 2);
            mainGrid.Children.Add(_pathTextBox);

            _browseButton = new Button
            {
                Content = "Browse...",
                Width = 100,
                VerticalAlignment = VerticalAlignment.Center
            };
            _browseButton.Click += BrowseButton_Click;
            Grid.SetRow(_browseButton, 4);
            Grid.SetColumn(_browseButton, 4);
            mainGrid.Children.Add(_browseButton);

            // Button row
            var buttonPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right,
                Spacing = 10
            };

            _startButton = new Button
            {
                Content = "Start",
                Width = 100
            };
            _startButton.Click += StartButton_Click;
            buttonPanel.Children.Add(_startButton);

            var cancelButton = new Button
            {
                Content = "Cancel",
                Width = 100
            };
            cancelButton.Click += (sender, e) => Close();
            buttonPanel.Children.Add(cancelButton);

            Grid.SetRow(buttonPanel, 6);
            Grid.SetColumn(buttonPanel, 0);
            Grid.SetColumnSpan(buttonPanel, 5);
            mainGrid.Children.Add(buttonPanel);

            Content = mainGrid;
        }

        private void PopulateGameComboBox()
        {
            _gameComboBox.Items.Clear();
            _gameItems.Clear();

            // Odyssey Engine
            var k1Item = new GameItem(GameType.K1, "Knights of the Old Republic (KotOR 1)");
            var k2Item = new GameItem(GameType.K2, "Knights of the Old Republic II: The Sith Lords (KotOR 2)");
            _gameItems.Add(k1Item);
            _gameItems.Add(k2Item);
            _gameComboBox.Items.Add(k1Item.ToString());
            _gameComboBox.Items.Add(k2Item.ToString());

            // Aurora Engine
            var nwnItem = new GameItem(GameType.NWN, "Neverwinter Nights");
            var nwn2Item = new GameItem(GameType.NWN2, "Neverwinter Nights 2");
            _gameItems.Add(nwnItem);
            _gameItems.Add(nwn2Item);
            _gameComboBox.Items.Add(nwnItem.ToString());
            _gameComboBox.Items.Add(nwn2Item.ToString());

            // Eclipse Engine - Dragon Age
            var daItem = new GameItem(GameType.DA, "Dragon Age: Origins");
            var da2Item = new GameItem(GameType.DA2, "Dragon Age II");
            _gameItems.Add(daItem);
            _gameItems.Add(da2Item);
            _gameComboBox.Items.Add(daItem.ToString());
            _gameComboBox.Items.Add(da2Item.ToString());

        }

        private void PopulateGraphicsBackendComboBox()
        {
            _graphicsBackendComboBox.Items.Clear();
            _graphicsBackendItems = new List<GraphicsBackendItem>();
            var mgItem = new GraphicsBackendItem(GraphicsBackendType.MonoGame, "MonoGame");
            var strideItem = new GraphicsBackendItem(GraphicsBackendType.Stride, "Stride");
            _graphicsBackendItems.Add(mgItem);
            _graphicsBackendItems.Add(strideItem);
            _graphicsBackendComboBox.Items.Add(mgItem.ToString());
            _graphicsBackendComboBox.Items.Add(strideItem.ToString());
        }

        private void GraphicsBackendComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            int selectedIndex = _graphicsBackendComboBox.SelectedIndex;
            if (selectedIndex >= 0 && selectedIndex < _graphicsBackendItems.Count)
            {
                var backendItem = _graphicsBackendItems[selectedIndex];
                SelectedGraphicsBackend = backendItem.BackendType;
            }
        }

        private async void SettingsButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var settingsDialog = new GraphicsSettingsDialog(SelectedGraphicsBackend, _graphicsSettings);
                var result = await settingsDialog.ShowDialog<bool>(this);
                if (result)
                {
                    _graphicsSettings = settingsDialog.Settings;
                }
            }
            catch (Exception ex)
            {
                var errorWindow = new Window
                {
                    Title = "Settings Error",
                    Width = 500,
                    Height = 400,
                    WindowStartupLocation = WindowStartupLocation.CenterOwner
                };
                var errorTextBox = new TextBox
                {
                    Text = $"Error opening graphics settings:\n\n{ex.Message}\n\n{ex.StackTrace}",
                    IsReadOnly = true,
                    TextWrapping = TextWrapping.Wrap,
                    AcceptsReturn = true
                };
                errorWindow.Content = errorTextBox;
                await errorWindow.ShowDialog(this);
            }
        }

        private void GameComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            int selectedIndex = _gameComboBox.SelectedIndex;
            if (selectedIndex >= 0 && selectedIndex < _gameItems.Count)
            {
                SelectedGame = _gameItems[selectedIndex].Game;
                UpdatePathComboBox();
            }
        }

        private void UpdatePathComboBox()
        {
            _pathTextBox.Text = string.Empty;

            // Get paths for the selected game
            List<string> paths = new List<string>();

            // For K1 and K2, use PathTools
            if (SelectedGame == GameType.K1 || SelectedGame == GameType.K2)
            {
                KotorGame kotorGame = (SelectedGame == GameType.K1) ? KotorGame.K1 : KotorGame.K2;

                // Use PathTools to find paths
                Dictionary<GameType, List<CaseAwarePath>> foundPaths = PathTools.FindKotorPathsFromDefault();
                if (foundPaths.TryGetValue(SelectedGame, out List<CaseAwarePath> gamePaths))
                {
                    foreach (var path in gamePaths)
                    {
                        string resolvedPath = path.GetResolvedPath();
                        if (Directory.Exists(resolvedPath) && !paths.Contains(resolvedPath))
                        {
                            paths.Add(resolvedPath);
                        }
                    }
                }

                // Also try GamePathDetector for additional paths
                List<string> detectorPaths = GamePathDetector.FindKotorPathsFromDefault(kotorGame);
                foreach (string path in detectorPaths)
                {
                    if (!paths.Contains(path))
                    {
                        paths.Add(path);
                    }
                }
            }
            else if (SelectedGame == GameType.NWN || SelectedGame == GameType.NWN2 ||
                     SelectedGame == GameType.DA || SelectedGame == GameType.DA2)
            {
                // Use GamePathDetector to find paths for NWN, DA games
                List<string> detectorPaths = GamePathDetector.FindGamePathsFromDefault(SelectedGame);
                foreach (string path in detectorPaths)
                {
                    if (!paths.Contains(path))
                    {
                        paths.Add(path);
                    }
                }
            }

            // Set first path if available
            if (paths.Count > 0)
            {
                _pathTextBox.Text = paths[0];
            }
        }

        private async void BrowseButton_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new OpenFolderDialog
            {
                Title = "Select game installation directory"
            };

            // Set initial directory if path is already set
            if (!string.IsNullOrEmpty(_pathTextBox.Text) && Directory.Exists(_pathTextBox.Text))
            {
                dialog.Directory = _pathTextBox.Text;
            }

            var result = await dialog.ShowAsync(this);
            if (!string.IsNullOrEmpty(result))
            {
                string selectedPath = result;

                // Validate installation
                if (ValidateInstallation(selectedPath))
                {
                    _pathTextBox.Text = selectedPath;
                }
                else
                {
                    var errorWindow = new Window
                    {
                        Title = "Invalid Installation",
                        Width = 450,
                        Height = 150,
                        WindowStartupLocation = WindowStartupLocation.CenterOwner,
                        CanResize = false
                    };
                    var errorPanel = new StackPanel
                    {
                        Margin = new Thickness(20),
                        Spacing = 10
                    };
                    errorPanel.Children.Add(new TextBlock
                    {
                        Text = $"The selected directory does not appear to be a valid {GetGameName(SelectedGame)} installation.\n\nPlease select a directory containing the game files.",
                        TextWrapping = TextWrapping.Wrap
                    });
                    var okButton = new Button
                    {
                        Content = "OK",
                        Width = 100,
                        HorizontalAlignment = HorizontalAlignment.Center
                    };
                    okButton.Click += (s, ev) => errorWindow.Close();
                    errorPanel.Children.Add(okButton);
                    errorWindow.Content = errorPanel;
                    await errorWindow.ShowDialog(this);
                }
            }
        }

        private void StartButton_Click(object sender, RoutedEventArgs e)
        {
            string path = _pathTextBox.Text.Trim();

            if (string.IsNullOrEmpty(path))
            {
                ShowMessageBox("Path Required", "Please select or enter a game installation path.");
                return;
            }

            if (!Directory.Exists(path))
            {
                ShowMessageBox("Invalid Path", "The specified path does not exist.");
                return;
            }

            if (!ValidateInstallation(path))
            {
                ShowMessageBox("Invalid Installation", $"The selected directory does not appear to be a valid {GetGameName(SelectedGame)} installation.\n\nPlease select a directory containing the game files.");
                return;
            }

            SelectedPath = path;
            StartClicked = true;
            Close();
        }

        private async void ShowMessageBox(string title, string message)
        {
            var msgWindow = new Window
            {
                Title = title,
                Width = 400,
                Height = 150,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                CanResize = false
            };
            var panel = new StackPanel
            {
                Margin = new Thickness(20),
                Spacing = 10
            };
            panel.Children.Add(new TextBlock
            {
                Text = message,
                TextWrapping = TextWrapping.Wrap
            });
            var okButton = new Button
            {
                Content = "OK",
                Width = 100,
                HorizontalAlignment = HorizontalAlignment.Center
            };
            okButton.Click += (s, ev) => msgWindow.Close();
            panel.Children.Add(okButton);
            msgWindow.Content = panel;
            await msgWindow.ShowDialog(this);
        }

        private bool ValidateInstallation(string path)
        {
            if (string.IsNullOrEmpty(path) || !Directory.Exists(path))
            {
                return false;
            }

            // Validate based on game type
            switch (SelectedGame)
            {
                case GameType.K1:
                    return File.Exists(Path.Combine(path, "chitin.key")) &&
                           File.Exists(Path.Combine(path, "swkotor.exe"));

                case GameType.K2:
                    return File.Exists(Path.Combine(path, "chitin.key")) &&
                           File.Exists(Path.Combine(path, "swkotor2.exe"));

                case GameType.NWN:
                    {
                        // Validate Neverwinter Nights installation
                        // Based on xoreos/src/engines/nwn/nwn.cpp:213-268
                        // Required files:
                        // - chitin.key: Main KEY file containing resource mappings (mandatory)
                        // - nwmain.exe: Game executable (mandatory)
                        // - gui_32bit.erf: GUI texture archive (mandatory)
                        // - data directory: Contains game data files (mandatory)
                        // Optional but commonly present:
                        // - modules directory: User-created modules
                        // - hak directory: Hakpak files
                        // - override directory: Override files
                        // - nwm directory: Neverwinter Nights module files
                        // - texturepacks directory: Texture pack files
                        string nwnChitinKey = Path.Combine(path, "chitin.key");
                        string nwnExe = Path.Combine(path, "nwmain.exe");
                        string nwnExeUpper = Path.Combine(path, "NWMAIN.EXE");
                        string nwnGuiErf = Path.Combine(path, "gui_32bit.erf");
                        string nwnDataDir = Path.Combine(path, "data");

                        // All mandatory files/directories must exist
                        bool hasChitinKey = File.Exists(nwnChitinKey);
                        bool hasExe = File.Exists(nwnExe) || File.Exists(nwnExeUpper);
                        bool hasGuiErf = File.Exists(nwnGuiErf);
                        bool hasDataDir = Directory.Exists(nwnDataDir);

                        return hasChitinKey && hasExe && hasGuiErf && hasDataDir;
                    }

                case GameType.NWN2:
                    // Validate Neverwinter Nights 2 installation
                    // Based on xoreos/src/engines/nwn2/nwn2.cpp:214-300
                    // Required files:
                    // - nwn2main.exe: Game executable (mandatory)
                    // - data directory: Contains game data files (mandatory)
                    // - 2da.zip: 2DA table archive (mandatory)
                    // - actors.zip: Actor models archive (mandatory)
                    // - nwn2_models.zip: Model archive (mandatory)
                    // - scripts.zip: Script archive (mandatory)
                    // Optional but commonly present:
                    // - modules directory: User-created modules
                    // - hak directory: Hakpak files
                    // - override directory: Override files
                    // Note: NWN2 uses ZIP archives instead of KEY files like NWN
                    string nwn2Exe = Path.Combine(path, "nwn2main.exe");
                    string nwn2ExeUpper = Path.Combine(path, "NWN2MAIN.EXE");
                    string nwn2DataDir = Path.Combine(path, "data");
                    string nwn2TwoDaZip = Path.Combine(path, "2da.zip");
                    string nwn2ActorsZip = Path.Combine(path, "actors.zip");
                    string nwn2ModelsZip = Path.Combine(path, "nwn2_models.zip");
                    string nwn2ScriptsZip = Path.Combine(path, "scripts.zip");

                    // All mandatory files/directories must exist
                    bool hasNwn2Exe = File.Exists(nwn2Exe) || File.Exists(nwn2ExeUpper);
                    bool hasNwn2DataDir = Directory.Exists(nwn2DataDir);
                    bool hasNwn2TwoDaZip = File.Exists(nwn2TwoDaZip);
                    bool hasNwn2ActorsZip = File.Exists(nwn2ActorsZip);
                    bool hasNwn2ModelsZip = File.Exists(nwn2ModelsZip);
                    bool hasNwn2ScriptsZip = File.Exists(nwn2ScriptsZip);

                    return hasNwn2Exe && hasNwn2DataDir && hasNwn2TwoDaZip &&
                           hasNwn2ActorsZip && hasNwn2ModelsZip && hasNwn2ScriptsZip;

                case GameType.DA:
                    {
                        // Validate Dragon Age: Origins installation
                        // Based on xoreos/src/engines/dragonage/probes.cpp:69-75
                        // Required files:
                        // - daoriginslauncher.exe: Launcher executable (Windows retail, mandatory)
                        // - daorigins.exe: Main game executable (mandatory)
                        // - packages directory: Eclipse Engine package structure (mandatory)
                        // - data directory: Contains game data files (mandatory)
                        // - data/global.rim: Global resource archive (mandatory, DA:O specific)
                        // Optional but commonly present:
                        // - modules directory: Contains game modules
                        // - addins directory: DLC and addon content
                        string daLauncherExe = Path.Combine(path, "daoriginslauncher.exe");
                        string daLauncherExeUpper = Path.Combine(path, "DAORIGINSLAUNCHER.EXE");
                        string daOriginsExe = Path.Combine(path, "daorigins.exe");
                        string daOriginsExeUpper = Path.Combine(path, "DAORIGINS.EXE");
                        string daPackagesDir = Path.Combine(path, "packages");
                        string daDataDir = Path.Combine(path, "data");
                        string daGlobalRim = Path.Combine(daDataDir, "global.rim");

                        // Check for launcher (Windows retail) OR main executable
                        bool hasLauncher = File.Exists(daLauncherExe) || File.Exists(daLauncherExeUpper);
                        bool hasExe = File.Exists(daOriginsExe) || File.Exists(daOriginsExeUpper);
                        bool hasPackagesDir = Directory.Exists(daPackagesDir);
                        bool hasDataDir = Directory.Exists(daDataDir);
                        bool hasGlobalRim = File.Exists(daGlobalRim);

                        // All mandatory files/directories must exist
                        // Either launcher OR main executable is acceptable (different distribution methods)
                        return (hasLauncher || hasExe) && hasPackagesDir && hasDataDir && hasGlobalRim;
                    }

                case GameType.DA2:
                    // Validate Dragon Age II installation
                    // Based on xoreos/src/engines/dragonage2/probes.cpp:72-89
                    // Required files:
                    // - dragonage2launcher.exe: Launcher executable (Windows retail, mandatory)
                    // - dragonage2.exe: Main executable in bin_ship (Windows Origin, mandatory)
                    // - DragonAge2.exe: Main game executable (mandatory)
                    // - modules/campaign_base/campaign_base.cif: Campaign base module (mandatory, DA2 specific)
                    // Optional but commonly present:
                    // - packages directory: Eclipse Engine package structure
                    // - addins directory: DLC and addon content
                    string da2LauncherExe = Path.Combine(path, "dragonage2launcher.exe");
                    string da2LauncherExeUpper = Path.Combine(path, "DRAGONAGE2LAUNCHER.EXE");
                    string da2Exe = Path.Combine(path, "DragonAge2.exe");
                    string da2ExeUpper = Path.Combine(path, "DRAGONAGE2.EXE");
                    string da2ExeLower = Path.Combine(path, "dragonage2.exe");
                    string da2BinShipExe = Path.Combine(path, "bin_ship", "dragonage2.exe");
                    string da2BinShipExeUpper = Path.Combine(path, "bin_ship", "DRAGONAGE2.EXE");
                    string da2CampaignBaseCif = Path.Combine(path, "modules", "campaign_base", "campaign_base.cif");

                    // Check for launcher (Windows retail) OR main executable (various locations)
                    bool hasDa2Launcher = File.Exists(da2LauncherExe) || File.Exists(da2LauncherExeUpper);
                    bool hasDa2Exe = File.Exists(da2Exe) || File.Exists(da2ExeUpper) || File.Exists(da2ExeLower);
                    bool hasDa2BinShipExe = File.Exists(da2BinShipExe) || File.Exists(da2BinShipExeUpper);
                    bool hasCampaignBaseCif = File.Exists(da2CampaignBaseCif);

                    // All mandatory files must exist
                    // Either launcher OR main executable is acceptable (different distribution methods)
                    return (hasDa2Launcher || hasDa2Exe || hasDa2BinShipExe) && hasCampaignBaseCif;


                default:
                    // For unknown games, just check if directory exists
                    return Directory.Exists(path);
            }
        }

        private string GetGameName(GameType game)
        {
            switch (game)
            {
                case GameType.K1: return "Knights of the Old Republic";
                case GameType.K2: return "Knights of the Old Republic II";
                case GameType.NWN: return "Neverwinter Nights";
                case GameType.NWN2: return "Neverwinter Nights 2";
                case GameType.DAO: return "Dragon Age: Origins";
                case GameType.DA2: return "Dragon Age II";
                default: return "Game";
            }
        }

        /// <summary>
        /// Helper class for game combobox items.
        /// </summary>
        private class GameItem
        {
            public GameType Game { get; }
            public string DisplayName { get; }

            public GameItem(GameType game, string displayName)
            {
                Game = game;
                DisplayName = displayName;
            }

            public override string ToString()
            {
                return DisplayName;
            }
        }

        /// <summary>
        /// Helper class for graphics backend combobox items.
        /// </summary>
        private class GraphicsBackendItem
        {
            public GraphicsBackendType BackendType { get; }
            public string DisplayName { get; }

            public GraphicsBackendItem(GraphicsBackendType backendType, string displayName)
            {
                BackendType = backendType;
                DisplayName = displayName;
            }

            public override string ToString()
            {
                return DisplayName;
            }
        }
    }
}
