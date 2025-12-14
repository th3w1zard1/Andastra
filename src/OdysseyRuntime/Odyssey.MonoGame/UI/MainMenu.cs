using System;
using System.IO;
using Stride.Core.Mathematics;
using Stride.Engine;
using Stride.Graphics;
using Stride.UI;
using Stride.UI.Controls;
using Stride.UI.Panels;
using JetBrains.Annotations;

namespace Odyssey.MonoGame.UI
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
        // Control visibility of UI element
        // Based on Stride API: https://doc.stride3d.net/latest/en/api/Stride.UI.UIElement.html
        // Visibility property controls whether element is rendered (Visible) or hidden (Collapsed)
        // Source: https://doc.stride3d.net/latest/en/manual/user-interface/index.html
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
        // Initialize main menu with UI component and font
        // Based on Stride API: https://doc.stride3d.net/latest/en/api/Stride.Engine.UIComponent.html
        // UIComponent manages UI rendering and input handling
        // Based on Stride API: https://doc.stride3d.net/latest/en/api/Stride.Graphics.SpriteFont.html
        // SpriteFont provides font rendering capabilities for text
        // Source: https://doc.stride3d.net/latest/en/manual/graphics/low-level-api/spritefont.html
        public MainMenu([NotNull] UIComponent uiComponent, [NotNull] SpriteFont font)
        {
            _uiComponent = uiComponent ?? throw new ArgumentNullException("uiComponent");
            _font = font ?? throw new ArgumentNullException("font");

            BuildUI();
        }

        private void BuildUI()
        {
            // Create root canvas for UI layout
            // Based on Stride API: https://doc.stride3d.net/latest/en/api/Stride.UI.Panels.Canvas.html
            // Canvas is a panel that allows absolute positioning of child elements
            // Source: https://doc.stride3d.net/latest/en/manual/user-interface/index.html
            _rootCanvas = new Canvas();

            // Main panel - centered
            // Based on Stride API: https://doc.stride3d.net/latest/en/api/Stride.UI.Panels.Grid.html
            // Grid is a panel that arranges children in rows and columns
            // Width/Height set dimensions, BackgroundColor sets background, HorizontalAlignment/VerticalAlignment control layout
            // Source: https://doc.stride3d.net/latest/en/manual/user-interface/layout-and-panels.html
            _mainPanel = new Grid
            {
                Width = 600,
                Height = 400,
                // Based on Stride API: https://doc.stride3d.net/latest/en/api/Stride.Core.Mathematics.Color.html
                // Color(byte r, byte g, byte b, byte a) constructor creates a color from RGBA byte values (0-255)
                // Method signature: Color(byte r, byte g, byte b, byte a)
                // Source: https://doc.stride3d.net/latest/en/manual/graphics/colors.html
                BackgroundColor = new Color(0, 0, 0, 200),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };

            // Define grid rows using StripDefinition
            // Based on Stride API: https://doc.stride3d.net/latest/en/api/Stride.UI.Panels.StripDefinition.html
            // StripDefinition(StripType, float) constructor creates a row/column definition
            // Method signature: StripDefinition(StripType type, float value)
            // StripType.Fixed sets fixed size in pixels, StripType.Star sets proportional size
            // Source: https://doc.stride3d.net/latest/en/manual/user-interface/layout-and-panels.html
            _mainPanel.RowDefinitions.Add(new StripDefinition(StripType.Fixed, 60)); // Title
            // Based on Stride API: https://doc.stride3d.net/latest/en/api/Stride.UI.Panels.StripDefinition.html
            // StripDefinition(StripType, float) - same constructor as above
            _mainPanel.RowDefinitions.Add(new StripDefinition(StripType.Fixed, 40)); // Install path label
            // Based on Stride API: https://doc.stride3d.net/latest/en/api/Stride.UI.Panels.StripDefinition.html
            // StripDefinition(StripType, float) - same constructor as above
            _mainPanel.RowDefinitions.Add(new StripDefinition(StripType.Fixed, 50)); // Install path button
            // Based on Stride API: https://doc.stride3d.net/latest/en/api/Stride.UI.Panels.StripDefinition.html
            // StripDefinition(StripType, float) - same constructor as above
            _mainPanel.RowDefinitions.Add(new StripDefinition(StripType.Fixed, 40)); // Module label
            // Based on Stride API: https://doc.stride3d.net/latest/en/api/Stride.UI.Panels.StripDefinition.html
            // StripDefinition(StripType, float) - same constructor as above
            _mainPanel.RowDefinitions.Add(new StripDefinition(StripType.Fixed, 50)); // Module combo
            // Based on Stride API: https://doc.stride3d.net/latest/en/api/Stride.UI.Panels.StripDefinition.html
            // StripDefinition(StripType, float) - same constructor as above
            _mainPanel.RowDefinitions.Add(new StripDefinition(StripType.Fixed, 60)); // Start button
            // Based on Stride API: https://doc.stride3d.net/latest/en/api/Stride.UI.Panels.StripDefinition.html
            // StripDefinition(StripType.Star, float) - Star type fills remaining space proportionally
            _mainPanel.RowDefinitions.Add(new StripDefinition(StripType.Star, 1));  // Status text

            // Title
            // Based on Stride API: https://doc.stride3d.net/latest/en/api/Stride.UI.Controls.TextBlock.html
            // TextBlock displays text, Font sets the font, TextSize sets size, TextColor sets color, Text sets content
            // HorizontalAlignment/VerticalAlignment control text alignment
            // Source: https://doc.stride3d.net/latest/en/manual/user-interface/controls.html
            _titleText = new TextBlock
            {
                Font = _font,
                TextSize = 32,
                TextColor = Color.White,
                Text = "Odyssey Engine",
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            // Set grid row position
            // Based on Stride API: https://doc.stride3d.net/latest/en/api/Stride.UI.Panels.Grid.html
            // SetGridRow sets which row the element occupies in the grid
            _titleText.SetGridRow(0);
            // Add to panel children
            // Based on Stride API: https://doc.stride3d.net/latest/en/api/Stride.UI.Panels.Panel.html
            // Children property contains the child UI elements
            _mainPanel.Children.Add(_titleText);

            // Install path label
            // Based on Stride API: https://doc.stride3d.net/latest/en/api/Stride.UI.Controls.TextBlock.html
            // TextBlock() constructor creates a new text block element
            // Method signature: TextBlock()
            // Margin property sets spacing around the element using Thickness
            // Based on Stride API: https://doc.stride3d.net/latest/en/api/Stride.UI.Thickness.html
            // Thickness(float left, float top, float right, float bottom) constructor creates a thickness with left, top, right, bottom values
            // Method signature: Thickness(float left, float top, float right, float bottom)
            // Based on Stride API: https://doc.stride3d.net/latest/en/api/Stride.Core.Mathematics.Color.html
            // Color.White is a static property representing white color (R=255, G=255, B=255, A=255)
            // Source: https://doc.stride3d.net/latest/en/manual/user-interface/layout-and-panels.html
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
            // Based on Stride API: https://doc.stride3d.net/latest/en/api/Stride.UI.Panels.Grid.html
            // Grid() constructor creates a new grid panel
            // Method signature: Grid()
            // ColumnDefinitions defines grid columns, StripType.Star makes column fill remaining space
            // Source: https://doc.stride3d.net/latest/en/manual/user-interface/layout-and-panels.html
            var installPathPanel = new Grid();
            // Based on Stride API: https://doc.stride3d.net/latest/en/api/Stride.UI.Panels.StripDefinition.html
            // StripDefinition(StripType.Star, float) - Star type fills remaining space proportionally
            installPathPanel.ColumnDefinitions.Add(new StripDefinition(StripType.Star, 1));
            // Based on Stride API: https://doc.stride3d.net/latest/en/api/Stride.UI.Panels.StripDefinition.html
            // StripDefinition(StripType.Fixed, float) - Fixed type sets fixed pixel width
            installPathPanel.ColumnDefinitions.Add(new StripDefinition(StripType.Fixed, 120));

            _installPathText = new TextBlock
            {
                Font = _font,
                TextSize = 14,
                // Based on Stride API: https://doc.stride3d.net/latest/en/api/Stride.Core.Mathematics.Color.html
                // Color.LightGray is a static property representing light gray color
                // Source: https://doc.stride3d.net/latest/en/manual/graphics/colors.html
                TextColor = Color.LightGray,
                Text = "No path selected",
                Margin = new Thickness(20, 5, 10, 5),
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Center
            };
            _installPathText.SetGridColumn(0);
            installPathPanel.Children.Add(_installPathText);

            // Create button with text content
            // Based on Stride API: https://doc.stride3d.net/latest/en/api/Stride.UI.Controls.Button.html
            // Button is a clickable control, Content property sets the button's content (TextBlock in this case)
            // BackgroundColor sets button background, Click event fires when button is clicked
            // Source: https://doc.stride3d.net/latest/en/manual/user-interface/controls.html
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
            // Set grid column position
            // Based on Stride API: https://doc.stride3d.net/latest/en/api/Stride.UI.Panels.Grid.html
            // SetGridColumn sets which column the element occupies
            _selectInstallButton.SetGridColumn(1);
            installPathPanel.Children.Add(_selectInstallButton);

            installPathPanel.SetGridRow(2);
            _mainPanel.Children.Add(installPathPanel);

            // Module label
            // Based on Stride API: https://doc.stride3d.net/latest/en/api/Stride.UI.Controls.TextBlock.html
            // TextBlock() constructor creates a new text block element
            // Method signature: TextBlock()
            // Based on Stride API: https://doc.stride3d.net/latest/en/api/Stride.Core.Mathematics.Color.html
            // Color.White static property - same as documented above
            // Based on Stride API: https://doc.stride3d.net/latest/en/api/Stride.UI.Thickness.html
            // Thickness(float, float, float, float) - same constructor as above
            // Source: https://doc.stride3d.net/latest/en/manual/user-interface/controls.html
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
                // Based on Stride API: https://doc.stride3d.net/latest/en/api/Stride.Core.Mathematics.Color.html
                // Color.Yellow is a static property representing yellow color
                // Source: https://doc.stride3d.net/latest/en/manual/graphics/colors.html
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
            // Based on Stride API: https://doc.stride3d.net/latest/en/api/Stride.Engine.UIComponent.html
            // Page property gets/sets the active UI page
            // Based on Stride API: https://doc.stride3d.net/latest/en/api/Stride.UI.UIPage.html
            // RootElement property sets the root UI element for the page
            // Source: https://doc.stride3d.net/latest/en/manual/user-interface/index.html
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
                // Enable/disable button
                // Based on Stride API: https://doc.stride3d.net/latest/en/api/Stride.UI.Controls.Button.html
                // IsEnabled property controls whether button can be clicked
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
