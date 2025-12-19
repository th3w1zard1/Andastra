using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using Andastra.Parsing.Common;
using Andastra.Parsing.Tools;
using Andastra.Runtime.Core;
using Andastra.Runtime.Game.Core;

namespace Andastra.Game.GUI
{
    /// <summary>
    /// Game launcher dialog with game selection and path configuration.
    /// </summary>
    /// <remarks>
    /// Launcher UI:
    /// - Standard WinForms dialog (not GL/drawn)
    /// - Game selection combobox (K1, K2, NWN, DA:O, DA2, ME, ME2, ME3)
    /// - Editable installation path combobox with browse button
    /// - Start button to launch the game
    /// - Error dialog for launch failures
    /// </remarks>
    public class GameLauncher : Form
    {
        private ComboBox _gameComboBox;
        private ComboBox _pathComboBox;
        private Button _browseButton;
        private Button _startButton;
        private Label _gameLabel;
        private Label _pathLabel;

        /// <summary>
        /// Gets the selected game.
        /// </summary>
        public Game SelectedGame { get; private set; }

        /// <summary>
        /// Gets the selected installation path.
        /// </summary>
        public string SelectedPath { get; private set; }

        /// <summary>
        /// Gets whether the user clicked Start.
        /// </summary>
        public bool StartClicked { get; private set; }

        /// <summary>
        /// Creates a new game launcher dialog.
        /// </summary>
        public GameLauncher()
        {
            InitializeComponent();
            PopulateGameComboBox();
            SelectedGame = Game.K1; // Default
            _gameComboBox.SelectedIndex = 0;
            _gameComboBox.SelectedIndexChanged += GameComboBox_SelectedIndexChanged;
            UpdatePathComboBox();
        }

        private void InitializeComponent()
        {
            this.Text = "Andastra Game Launcher";
            this.Size = new System.Drawing.Size(600, 250);
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.StartPosition = FormStartPosition.CenterScreen;
            this.ShowInTaskbar = true;

            // Game label
            _gameLabel = new Label
            {
                Text = "Game:",
                Location = new System.Drawing.Point(20, 20),
                Size = new System.Drawing.Size(100, 23),
                TextAlign = System.Drawing.ContentAlignment.MiddleLeft
            };
            this.Controls.Add(_gameLabel);

            // Game combobox
            _gameComboBox = new ComboBox
            {
                Location = new System.Drawing.Point(130, 17),
                Size = new System.Drawing.Size(400, 23),
                DropDownStyle = ComboBoxStyle.DropDownList
            };
            this.Controls.Add(_gameComboBox);

            // Path label
            _pathLabel = new Label
            {
                Text = "Installation Path:",
                Location = new System.Drawing.Point(20, 60),
                Size = new System.Drawing.Size(100, 23),
                TextAlign = System.Drawing.ContentAlignment.MiddleLeft
            };
            this.Controls.Add(_pathLabel);

            // Path combobox (editable)
            _pathComboBox = new ComboBox
            {
                Location = new System.Drawing.Point(130, 57),
                Size = new System.Drawing.Size(350, 23),
                DropDownStyle = ComboBoxStyle.DropDown,
                AutoCompleteMode = AutoCompleteMode.SuggestAppend,
                AutoCompleteSource = AutoCompleteSource.FileSystemDirectories
            };
            this.Controls.Add(_pathComboBox);

            // Browse button
            _browseButton = new Button
            {
                Text = "Browse...",
                Location = new System.Drawing.Point(490, 56),
                Size = new System.Drawing.Size(80, 25)
            };
            _browseButton.Click += BrowseButton_Click;
            this.Controls.Add(_browseButton);

            // Start button
            _startButton = new Button
            {
                Text = "Start",
                Location = new System.Drawing.Point(250, 120),
                Size = new System.Drawing.Size(100, 35),
                DialogResult = DialogResult.OK
            };
            _startButton.Click += StartButton_Click;
            this.Controls.Add(_startButton);
            this.AcceptButton = _startButton;
            this.CancelButton = _startButton; // Allow ESC to close

            // Cancel button (close)
            var cancelButton = new Button
            {
                Text = "Cancel",
                Location = new System.Drawing.Point(360, 120),
                Size = new System.Drawing.Size(100, 35),
                DialogResult = DialogResult.Cancel
            };
            this.Controls.Add(cancelButton);
        }

        private void PopulateGameComboBox()
        {
            _gameComboBox.Items.Clear();

            // Odyssey Engine
            _gameComboBox.Items.Add(new GameItem(Game.K1, "Knights of the Old Republic (KotOR 1)"));
            _gameComboBox.Items.Add(new GameItem(Game.K2, "Knights of the Old Republic II: The Sith Lords (KotOR 2)"));

            // Aurora Engine
            _gameComboBox.Items.Add(new GameItem(Game.NWN, "Neverwinter Nights"));
            _gameComboBox.Items.Add(new GameItem(Game.NWN2, "Neverwinter Nights 2"));

            // Eclipse Engine - Dragon Age
            _gameComboBox.Items.Add(new GameItem(Game.DA, "Dragon Age: Origins"));
            _gameComboBox.Items.Add(new GameItem(Game.DA2, "Dragon Age II"));

            // Eclipse Engine - Mass Effect
            _gameComboBox.Items.Add(new GameItem(Game.ME, "Mass Effect"));
            _gameComboBox.Items.Add(new GameItem(Game.ME2, "Mass Effect 2"));
            _gameComboBox.Items.Add(new GameItem(Game.ME3, "Mass Effect 3"));
        }

        private void GameComboBox_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (_gameComboBox.SelectedItem is GameItem gameItem)
            {
                SelectedGame = gameItem.Game;
                UpdatePathComboBox();
            }
        }

        private void UpdatePathComboBox()
        {
            _pathComboBox.Items.Clear();
            _pathComboBox.Text = string.Empty;

            // Get paths for the selected game
            List<string> paths = new List<string>();

            // For K1 and K2, use PathTools
            if (SelectedGame == Game.K1 || SelectedGame == Game.K2)
            {
                KotorGame kotorGame = SelectedGame == Game.K1 ? KotorGame.K1 : KotorGame.K2;
                
                // Use PathTools to find paths
                Dictionary<Game, List<CaseAwarePath>> foundPaths = PathTools.FindKotorPathsFromDefault();
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
            else
            {
                // For other games, we'll need to implement path detection later
                // For now, just allow manual entry
                // TODO: Implement path detection for NWN, DA, ME games
            }

            // Populate combobox with found paths
            foreach (string path in paths)
            {
                _pathComboBox.Items.Add(path);
            }

            // Select first path if available
            if (_pathComboBox.Items.Count > 0)
            {
                _pathComboBox.SelectedIndex = 0;
            }
        }

        private void BrowseButton_Click(object sender, EventArgs e)
        {
            using (var dialog = new FolderBrowserDialog())
            {
                dialog.Description = "Select game installation directory";
                dialog.ShowNewFolderButton = false;

                // Set initial directory if path is already set
                if (!string.IsNullOrEmpty(_pathComboBox.Text) && Directory.Exists(_pathComboBox.Text))
                {
                    dialog.SelectedPath = _pathComboBox.Text;
                }

                if (dialog.ShowDialog() == DialogResult.OK)
                {
                    string selectedPath = dialog.SelectedPath;

                    // Validate installation
                    if (ValidateInstallation(selectedPath))
                    {
                        _pathComboBox.Text = selectedPath;
                        
                        // Add to combobox if not already present
                        if (!_pathComboBox.Items.Cast<string>().Contains(selectedPath))
                        {
                            _pathComboBox.Items.Insert(0, selectedPath);
                        }
                    }
                    else
                    {
                        MessageBox.Show(
                            $"The selected directory does not appear to be a valid {GetGameName(SelectedGame)} installation.\n\n" +
                            "Please select a directory containing the game files.",
                            "Invalid Installation",
                            MessageBoxButtons.OK,
                            MessageBoxIcon.Warning);
                    }
                }
            }
        }

        private void StartButton_Click(object sender, EventArgs e)
        {
            string path = _pathComboBox.Text.Trim();

            if (string.IsNullOrEmpty(path))
            {
                MessageBox.Show(
                    "Please select or enter a game installation path.",
                    "Path Required",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
                return;
            }

            if (!Directory.Exists(path))
            {
                MessageBox.Show(
                    "The specified path does not exist.",
                    "Invalid Path",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
                return;
            }

            if (!ValidateInstallation(path))
            {
                MessageBox.Show(
                    $"The selected directory does not appear to be a valid {GetGameName(SelectedGame)} installation.\n\n" +
                    "Please select a directory containing the game files.",
                    "Invalid Installation",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
                return;
            }

            SelectedPath = path;
            StartClicked = true;
            this.DialogResult = DialogResult.OK;
            this.Close();
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
                case Game.K1:
                    return File.Exists(Path.Combine(path, "chitin.key")) &&
                           File.Exists(Path.Combine(path, "swkotor.exe"));

                case Game.K2:
                    return File.Exists(Path.Combine(path, "chitin.key")) &&
                           File.Exists(Path.Combine(path, "swkotor2.exe"));

                case Game.NWN:
                case Game.NWN2:
                    // TODO: Validate NWN installation
                    return Directory.Exists(Path.Combine(path, "data")) ||
                           Directory.Exists(Path.Combine(path, "override"));

                case Game.DA:
                case Game.DA2:
                    // TODO: Validate Dragon Age installation
                    return Directory.Exists(Path.Combine(path, "data")) ||
                           File.Exists(Path.Combine(path, "DragonAge.exe")) ||
                           File.Exists(Path.Combine(path, "DragonAge2.exe"));

                case Game.ME:
                case Game.ME2:
                case Game.ME3:
                    // TODO: Validate Mass Effect installation
                    return Directory.Exists(Path.Combine(path, "BioGame")) ||
                           File.Exists(Path.Combine(path, "MassEffect.exe")) ||
                           File.Exists(Path.Combine(path, "MassEffect2.exe")) ||
                           File.Exists(Path.Combine(path, "MassEffect3.exe"));

                default:
                    // For unknown games, just check if directory exists
                    return Directory.Exists(path);
            }
        }

        private string GetGameName(Game game)
        {
            switch (game)
            {
                case Game.K1: return "Knights of the Old Republic";
                case Game.K2: return "Knights of the Old Republic II";
                case Game.NWN: return "Neverwinter Nights";
                case Game.NWN2: return "Neverwinter Nights 2";
                case Game.DA: return "Dragon Age: Origins";
                case Game.DA2: return "Dragon Age II";
                case Game.ME: return "Mass Effect";
                case Game.ME2: return "Mass Effect 2";
                case Game.ME3: return "Mass Effect 3";
                default: return "Game";
            }
        }

        /// <summary>
        /// Helper class for game combobox items.
        /// </summary>
        private class GameItem
        {
            public Game Game { get; }
            public string DisplayName { get; }

            public GameItem(Game game, string displayName)
            {
                Game = game;
                DisplayName = displayName;
            }

            public override string ToString()
            {
                return DisplayName;
            }
        }
    }
}
