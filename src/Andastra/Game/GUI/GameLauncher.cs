using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Eto.Forms;
using Eto.Drawing;
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
    /// - Cross-platform using Eto.Forms (Windows/Mac/Linux)
    /// - Native look-and-feel on each platform
    /// - Game selection combobox (K1, K2, NWN, DA:O, DA2)
    /// - Editable installation path combobox with browse button
    /// - Start button to launch the game
    /// - Error dialog for launch failures
    /// </remarks>
    public class GameLauncher : Dialog
    {
        private DropDown _gameComboBox;
        private DropDown _graphicsBackendComboBox;
        private TextBox _pathTextBox;
        private Button _browseButton;
        private Button _settingsButton;
        private Button _startButton;
        private Label _gameLabel;
        private Label _graphicsBackendLabel;
        private Label _pathLabel;
        private GraphicsSettingsData _graphicsSettings;
        private List<GraphicsBackendItem> _graphicsBackendItems;
        private List<GameItem> _gameItems;

        /// <summary>
        /// Gets or sets the dialog result.
        /// </summary>
        public DialogResult Result { get; set; }

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
            ClientSize = new Size(600, 200);
            Resizable = false;
            WindowStyle = WindowStyle.Default;

            SelectedGame = GameType.K1; // Default
            SelectedGraphicsBackend = GraphicsBackendType.MonoGame; // Default
            _graphicsSettings = new GraphicsSettingsData();
            _gameItems = new List<GameItem>();

            InitializeComponent();
            PopulateGameComboBox();
            PopulateGraphicsBackendComboBox();
            _gameComboBox.SelectedIndex = 0;
            _graphicsBackendComboBox.SelectedIndex = 0;
            _gameComboBox.SelectedIndexChanged += GameComboBox_SelectedIndexChanged;
            _graphicsBackendComboBox.SelectedIndexChanged += GraphicsBackendComboBox_SelectedIndexChanged;
            UpdatePathComboBox();
        }

        private void InitializeComponent()
        {
            var layout = new TableLayout
            {
                Spacing = new Size(10, 10),
                Padding = new Padding(20)
            };

            // Game selection row
            _gameLabel = new Label
            {
                Text = "Game:",
                VerticalAlignment = VerticalAlignment.Center
            };

            _gameComboBox = new DropDown
            {
                Width = 400
            };

            layout.Rows.Add(new TableRow(
                new TableCell(_gameLabel, true),
                new TableCell(_gameComboBox, true)
            ));

            // Graphics backend selection row
            _graphicsBackendLabel = new Label
            {
                Text = "Graphics Backend:",
                VerticalAlignment = VerticalAlignment.Center
            };

            _graphicsBackendComboBox = new DropDown
            {
                Width = 400
            };

            var settingsButtonLayout = new TableLayout { Spacing = new Size(5, 0) };
            _settingsButton = new Button
            {
                Text = "Settings...",
                Width = 100
            };
            _settingsButton.Click += SettingsButton_Click;

            settingsButtonLayout.Rows.Add(new TableRow(
                new TableCell(_graphicsBackendComboBox, true),
                new TableCell(_settingsButton, false)
            ));

            layout.Rows.Add(new TableRow(
                new TableCell(_graphicsBackendLabel, true),
                new TableCell(settingsButtonLayout, true)
            ));

            // Path selection row
            _pathLabel = new Label
            {
                Text = "Installation Path:",
                VerticalAlignment = VerticalAlignment.Center
            };

            // Use a TextBox for editable path input
            _pathTextBox = new TextBox
            {
                Width = 400
            };

            _browseButton = new Button
            {
                Text = "Browse...",
                Width = 100
            };
            _browseButton.Click += BrowseButton_Click;

            layout.Rows.Add(new TableRow(
                new TableCell(_pathLabel, true),
                new TableCell(_pathTextBox, true),
                new TableCell(_browseButton, false)
            ));

            // Button row
            var buttonLayout = new TableLayout
            {
                Spacing = new Size(10, 0)
            };

            _startButton = new Button
            {
                Text = "Start",
                Width = 100
            };
            _startButton.Click += StartButton_Click;

            var cancelButton = new Button
            {
                Text = "Cancel",
                Width = 100
            };
            cancelButton.Click += (sender, e) => Close();

            buttonLayout.Rows.Add(new TableRow(
                new TableCell(null, true),
                new TableCell(_startButton, false),
                new TableCell(cancelButton, false)
            ));

            layout.Rows.Add(new TableRow(
                new TableCell(buttonLayout, true)
            ));

            Content = layout;

            // Set default button
            DefaultButton = _startButton;
        }

        private void PopulateGameComboBox()
        {
            _gameComboBox.Items.Clear();
            _gameItems.Clear();

            // Odyssey Engine
            var k1Item = new GameItem(BioWareGame.K1, "Knights of the Old Republic (KotOR 1)");
            var k2Item = new GameItem(BioWareGame.K2, "Knights of the Old Republic II: The Sith Lords (KotOR 2)");
            _gameItems.Add(k1Item);
            _gameItems.Add(k2Item);
            _gameComboBox.Items.Add(k1Item.ToString());
            _gameComboBox.Items.Add(k2Item.ToString());

            // Aurora Engine
            var nwnItem = new GameItem(BioWareGame.NWN, "Neverwinter Nights");
            var nwn2Item = new GameItem(BioWareGame.NWN2, "Neverwinter Nights 2");
            _gameItems.Add(nwnItem);
            _gameItems.Add(nwn2Item);
            _gameComboBox.Items.Add(nwnItem.ToString());
            _gameComboBox.Items.Add(nwn2Item.ToString());

            // Eclipse Engine - Dragon Age
            var daItem = new GameItem(BioWareGame.DA, "Dragon Age: Origins");
            var da2Item = new GameItem(BioWareGame.DA2, "Dragon Age II");
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

        private void GraphicsBackendComboBox_SelectedIndexChanged(object sender, EventArgs e)
        {
            int selectedIndex = _graphicsBackendComboBox.SelectedIndex;
            if (selectedIndex >= 0 && selectedIndex < _graphicsBackendItems.Count)
            {
                var backendItem = _graphicsBackendItems[selectedIndex];
                SelectedGraphicsBackend = backendItem.BackendType;
            }
        }

        private void SettingsButton_Click(object sender, EventArgs e)
        {
            try
            {
                using (var settingsDialog = new GraphicsSettingsDialog(SelectedGraphicsBackend, _graphicsSettings))
                {
                    settingsDialog.ShowModal(this);
                    if (settingsDialog.Result == DialogResult.Ok)
                    {
                        _graphicsSettings = settingsDialog.Settings;
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    this,
                    $"Error opening graphics settings:\n\n{ex.Message}\n\n{ex.StackTrace}",
                    "Settings Error",
                    MessageBoxType.Error);
            }
        }

        private void GameComboBox_SelectedIndexChanged(object sender, EventArgs e)
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
            if (SelectedGame == BioWareGame.K1 || SelectedGame == BioWareGame.K2)
            {
                KotorGame kotorGame = SelectedGame == BioWareGame.K1 ? KotorGame.K1 : KotorGame.K2;

                // Use PathTools to find paths
                Dictionary<BioWareGame, List<CaseAwarePath>> foundPaths = PathTools.FindKotorPathsFromDefault();
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
            else if (SelectedGame == BioWareGame.NWN || SelectedGame == BioWareGame.NWN2 ||
                     SelectedGame == BioWareGame.DA || SelectedGame == BioWareGame.DA2)
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

        private void BrowseButton_Click(object sender, EventArgs e)
        {
            var dialog = new SelectFolderDialog
            {
                Title = "Select game installation directory"
            };

            // Set initial directory if path is already set
            if (!string.IsNullOrEmpty(_pathTextBox.Text) && Directory.Exists(_pathTextBox.Text))
            {
                dialog.Directory = _pathTextBox.Text;
            }

            if (dialog.ShowDialog(this) == DialogResult.Ok)
            {
                string selectedPath = dialog.Directory;

                // Validate installation
                if (ValidateInstallation(selectedPath))
                {
                    _pathTextBox.Text = selectedPath;
                }
                else
                {
                    MessageBox.Show(
                        this,
                        $"The selected directory does not appear to be a valid {GetGameName(SelectedGame)} installation.\n\n" +
                        "Please select a directory containing the game files.",
                        "Invalid Installation",
                        MessageBoxType.Warning);
                }
            }
        }

        private void StartButton_Click(object sender, EventArgs e)
        {
            string path = _pathTextBox.Text.Trim();

            if (string.IsNullOrEmpty(path))
            {
                MessageBox.Show(
                    this,
                    "Please select or enter a game installation path.",
                    "Path Required",
                    MessageBoxType.Warning);
                return;
            }

            if (!Directory.Exists(path))
            {
                MessageBox.Show(
                    this,
                    "The specified path does not exist.",
                    "Invalid Path",
                    MessageBoxType.Error);
                return;
            }

            if (!ValidateInstallation(path))
            {
                MessageBox.Show(
                    this,
                    $"The selected directory does not appear to be a valid {GetGameName(SelectedGame)} installation.\n\n" +
                    "Please select a directory containing the game files.",
                    "Invalid Installation",
                    MessageBoxType.Warning);
                return;
            }

            SelectedPath = path;
            StartClicked = true;
            Result = DialogResult.Ok;
            Close();
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
                case BioWareGame.K1:
                    return File.Exists(Path.Combine(path, "chitin.key")) &&
                           File.Exists(Path.Combine(path, "swkotor.exe"));

                case BioWareGame.K2:
                    return File.Exists(Path.Combine(path, "chitin.key")) &&
                           File.Exists(Path.Combine(path, "swkotor2.exe"));

                case BioWareGame.NWN:
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

                case BioWareGame.NWN2:
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

                case BioWareGame.DA:
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

                case BioWareGame.DA2:
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
                case BioWareGame.K1: return "Knights of the Old Republic";
                case BioWareGame.K2: return "Knights of the Old Republic II";
                case BioWareGame.NWN: return "Neverwinter Nights";
                case BioWareGame.NWN2: return "Neverwinter Nights 2";
                case BioWareGame.DAO: return "Dragon Age: Origins";
                case BioWareGame.DA2: return "Dragon Age II";
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
