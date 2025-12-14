using System;
using System.IO;
using Stride.Core.Mathematics;
using Stride.Engine;
using Stride.Graphics;
using Stride.UI;
using Stride.UI.Controls;
using Stride.UI.Panels;
using JetBrains.Annotations;

namespace Odyssey.Stride.UI
{
    /// <summary>
    /// Main menu UI for selecting install path, module, and starting the game.
    /// </summary>
    public class MainMenu
    {
        private readonly UIComponent _uiComponent;
        private readonly SpriteFont _font;

        private Canvas _rootCanvas;
        private Grid _mainPanel;
        private TextBlock _titleText;
        private TextBlock _installPathText;
        private Button _selectInstallButton;
        private TextBlock _moduleText;
        private Button _moduleButton;
        private Button _startGameButton;
        private TextBlock _statusText;

        private string[] _availableModules = {
            "end_m01aa", // Endar Spire - Command Module (K1)
            "001EBO",    // Ebon Hawk - Prologue (K2)
            "tar_m02aa", // Taris - Upper City (K1)
            "dan_m13aa"  // Dantooine - Jedi Enclave (K1)
        };
        private string[] _moduleDisplayNames = {
            "Endar Spire - Command Module",
            "Ebon Hawk - Prologue",
            "Taris - Upper City",
            "Dantooine - Jedi Enclave"
        };
        private int _selectedModuleIndex = 0;

        private string _selectedInstallPath;
        private string _selectedModule = "end_m01aa"; // Default for K1

        /// <summary>
        /// Event fired when the user wants to start the game.
        /// </summary>
        public event EventHandler<GameStartEventArgs> OnStartGame;

        /// <summary>
        /// Gets or sets whether the main menu is visible.
        /// </summary>
        public bool IsVisible
        {
            get { return _rootCanvas?.Visibility == Visibility.Visible; }
            set
            {
                if (_rootCanvas != null)
                {
                    _rootCanvas.Visibility = value ? Visibility.Visible : Visibility.Collapsed;
                }
            }
        }

        /// <summary>
        /// Creates a new main menu.
        /// </summary>
        public MainMenu([NotNull] UIComponent uiComponent, [NotNull] SpriteFont font)
        {
            _uiComponent = uiComponent ?? throw new ArgumentNullException("uiComponent");
            _font = font ?? throw new ArgumentNullException("font");

            BuildUI();
        }

        private void BuildUI()
        {
            _rootCanvas = new Canvas();

            // Main panel - centered
            _mainPanel = new Grid
            {
                Width = 600,
                Height = 400,
                BackgroundColor = new Color(0, 0, 0, 200),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };

            _mainPanel.RowDefinitions.Add(new StripDefinition(StripType.Fixed, 60)); // Title
            _mainPanel.RowDefinitions.Add(new StripDefinition(StripType.Fixed, 40)); // Install path label
            _mainPanel.RowDefinitions.Add(new StripDefinition(StripType.Fixed, 50)); // Install path button
            _mainPanel.RowDefinitions.Add(new StripDefinition(StripType.Fixed, 40)); // Module label
            _mainPanel.RowDefinitions.Add(new StripDefinition(StripType.Fixed, 50)); // Module combo
            _mainPanel.RowDefinitions.Add(new StripDefinition(StripType.Fixed, 60)); // Start button
            _mainPanel.RowDefinitions.Add(new StripDefinition(StripType.Star, 1));  // Status text

            // Title
            _titleText = new TextBlock
            {
                Font = _font,
                TextSize = 32,
                TextColor = Color.White,
                Text = "Odyssey Engine",
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            _titleText.SetGridRow(0);
            _mainPanel.Children.Add(_titleText);

            // Install path label
            var installLabel = new TextBlock
            {
                Font = _font,
                TextSize = 16,
                TextColor = Color.White,
                Text = "KOTOR Installation Path:",
                Margin = new Thickness(20, 5, 20, 5),
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Center
            };
            installLabel.SetGridRow(1);
            _mainPanel.Children.Add(installLabel);

            // Install path text and button
            var installPathPanel = new Grid();
            installPathPanel.ColumnDefinitions.Add(new StripDefinition(StripType.Star, 1));
            installPathPanel.ColumnDefinitions.Add(new StripDefinition(StripType.Fixed, 120));

            _installPathText = new TextBlock
            {
                Font = _font,
                TextSize = 14,
                TextColor = Color.LightGray,
                Text = "No path selected",
                Margin = new Thickness(20, 5, 10, 5),
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Center
            };
            _installPathText.SetGridColumn(0);
            installPathPanel.Children.Add(_installPathText);

            _selectInstallButton = new Button
            {
                Content = new TextBlock
                {
                    Font = _font,
                    TextSize = 14,
                    TextColor = Color.White,
                    Text = "Select Path"
                },
                BackgroundColor = new Color(64, 64, 64, 255),
                Margin = new Thickness(5, 5, 20, 5),
                HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Stretch
            };
            _selectInstallButton.Click += OnSelectInstallPath;
            _selectInstallButton.SetGridColumn(1);
            installPathPanel.Children.Add(_selectInstallButton);

            installPathPanel.SetGridRow(2);
            _mainPanel.Children.Add(installPathPanel);

            // Module label
            var moduleLabel = new TextBlock
            {
                Font = _font,
                TextSize = 16,
                TextColor = Color.White,
                Text = "Starting Module:",
                Margin = new Thickness(20, 5, 20, 5),
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Center
            };
            moduleLabel.SetGridRow(3);
            _mainPanel.Children.Add(moduleLabel);

            // Module selection button
            _moduleButton = new Button
            {
                Content = new TextBlock
                {
                    Font = _font,
                    TextSize = 14,
                    TextColor = Color.White,
                    Text = _moduleDisplayNames[_selectedModuleIndex]
                },
                BackgroundColor = new Color(64, 64, 64, 255),
                Margin = new Thickness(20, 5, 20, 5),
                HorizontalAlignment = HorizontalAlignment.Stretch,
                VerticalAlignment = VerticalAlignment.Stretch
            };
            _moduleButton.Click += OnModuleButtonClicked;

            _moduleButton.SetGridRow(4);
            _mainPanel.Children.Add(_moduleButton);

            // Start game button
            _startGameButton = new Button
            {
                Content = new TextBlock
                {
                    Font = _font,
                    TextSize = 18,
                    TextColor = Color.White,
                    Text = "Start Game"
                },
                BackgroundColor = new Color(0, 128, 0, 255),
                Margin = new Thickness(20, 10, 20, 10),
                HorizontalAlignment = HorizontalAlignment.Stretch,
                VerticalAlignment = VerticalAlignment.Stretch,
                IsEnabled = false // Disabled until install path is selected
            };
            _startGameButton.Click += OnStartGameClicked;
            _startGameButton.SetGridRow(5);
            _mainPanel.Children.Add(_startGameButton);

            // Status text
            _statusText = new TextBlock
            {
                Font = _font,
                TextSize = 12,
                TextColor = Color.Yellow,
                Text = "",
                Margin = new Thickness(20, 5, 20, 20),
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Top
            };
            _statusText.SetGridRow(6);
            _mainPanel.Children.Add(_statusText);

            _rootCanvas.Children.Add(_mainPanel);

            // Add to UI page
            if (_uiComponent.Page != null)
            {
                _uiComponent.Page.RootElement = _rootCanvas;
            }
        }

        private void OnSelectInstallPath(object sender, EventArgs e)
        {
            // For now, just show a placeholder
            // In a real implementation, this would open a file dialog
            SetStatusText("Install path selection not implemented yet. Using placeholder path.");
            _selectedInstallPath = @"C:\Program Files (x86)\Steam\steamapps\common\swkotor"; // Placeholder
            UpdateInstallPathDisplay();
            UpdateStartButtonState();
        }

        private void OnModuleButtonClicked(object sender, EventArgs e)
        {
            // Cycle to next module
            _selectedModuleIndex = (_selectedModuleIndex + 1) % _availableModules.Length;
            _selectedModule = _availableModules[_selectedModuleIndex];

            // Update button text
            if (_moduleButton?.Content is TextBlock textBlock)
            {
                textBlock.Text = _moduleDisplayNames[_selectedModuleIndex];
            }

            SetStatusText($"Selected module: {_selectedModule}");
        }

        private void OnStartGameClicked(object sender, EventArgs e)
        {
            if (string.IsNullOrEmpty(_selectedInstallPath))
            {
                SetStatusText("Please select a KOTOR installation path first.");
                return;
            }

            // Validate install path exists
            if (!Directory.Exists(_selectedInstallPath))
            {
                SetStatusText($"Installation path does not exist: {_selectedInstallPath}");
                return;
            }

            // Fire event to start the game
            OnStartGame?.Invoke(this, new GameStartEventArgs(_selectedInstallPath, _selectedModule));
        }

        private void UpdateInstallPathDisplay()
        {
            if (_installPathText != null)
            {
                _installPathText.Text = string.IsNullOrEmpty(_selectedInstallPath)
                    ? "No path selected"
                    : _selectedInstallPath;
            }
        }

        private void UpdateStartButtonState()
        {
            if (_startGameButton != null)
            {
                _startGameButton.IsEnabled = !string.IsNullOrEmpty(_selectedInstallPath);
            }
        }

        /// <summary>
        /// Sets the status message displayed at the bottom.
        /// </summary>
        public void SetStatusText(string text)
        {
            if (_statusText != null)
            {
                _statusText.Text = text ?? "";
            }
        }

        /// <summary>
        /// Sets the selected install path programmatically.
        /// </summary>
        public void SetInstallPath(string path)
        {
            _selectedInstallPath = path;
            UpdateInstallPathDisplay();
            UpdateStartButtonState();
        }

        /// <summary>
        /// Gets the root canvas for this main menu.
        /// </summary>
        public Canvas GetRootCanvas()
        {
            return _rootCanvas;
        }
    }

    /// <summary>
    /// Event arguments for game start events.
    /// </summary>
    public class GameStartEventArgs : EventArgs
    {
        public string InstallPath { get; }
        public string ModuleName { get; }

        public GameStartEventArgs(string installPath, string moduleName)
        {
            InstallPath = installPath;
            ModuleName = moduleName;
        }
    }
}
