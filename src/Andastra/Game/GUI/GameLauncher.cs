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

namespace Andastra.Game.GUI
{
    /// <summary>
    /// Cross-platform game launcher dialog with game selection and path configuration.
    /// </summary>
    /// <remarks>
    /// Launcher UI:
    /// - Cross-platform using Eto.Forms (Windows/Mac/Linux)
    /// - Native look-and-feel on each platform
    /// - Game selection combobox (K1, K2, NWN, DA:O, DA2, ME, ME2, ME3)
    /// - Editable installation path combobox with browse button
    /// - Start button to launch the game
    /// - Error dialog for launch failures
    /// </remarks>
    public class GameLauncher : Dialog
    {
        private DropDown _gameComboBox;
        private TextBox _pathTextBox;
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
            Title = "Andastra Game Launcher";
            ClientSize = new Size(600, 200);
            Resizable = false;
            WindowStyle = WindowStyle.Default;

            SelectedGame = Game.K1; // Default

            InitializeComponent();
            PopulateGameComboBox();
            _gameComboBox.SelectedIndex = 0;
            _gameComboBox.SelectedIndexChanged += GameComboBox_SelectedIndexChanged;
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
            if (_gameComboBox.SelectedValue is GameItem gameItem)
            {
                SelectedGame = gameItem.Game;
                UpdatePathComboBox();
            }
        }

        private void UpdatePathComboBox()
        {
            _pathTextBox.Text = string.Empty;

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