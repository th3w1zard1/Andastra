using System;
using Stride.Core.Mathematics;
using Stride.Engine;
using Stride.Graphics;
using Stride.UI;
using Stride.UI.Controls;
using Stride.UI.Events;
using Stride.UI.Panels;
using JetBrains.Annotations;

namespace Odyssey.MonoGame.UI
{
    /// <summary>
    /// Pause menu with Resume and Exit options.
    /// </summary>
    public class PauseMenu
    {
        private readonly UIComponent _uiComponent;
        private readonly SpriteFont _font;

        private Grid _rootPanel;
        private StackPanel _menuPanel;
        private Button _resumeButton;
        private Button _exitButton;
        private int _selectedIndex;
        private bool _isVisible;

        /// <summary>
        /// Event fired when Resume is selected.
        /// </summary>
        public event Action OnResume;

        /// <summary>
        /// Event fired when Exit is selected.
        /// </summary>
        public event Action OnExit;

        /// <summary>
        /// Gets or sets whether the menu is visible.
        /// </summary>
        // Control visibility of pause menu
        // Based on Stride API: https://doc.stride3d.net/latest/en/api/Stride.UI.UIElement.html
        // Visibility property controls whether element is rendered (Visible) or hidden (Collapsed)
        public bool IsVisible
        {
            get { return _isVisible; }
            set
            {
                _isVisible = value;
                if (_rootPanel != null)
                {
                    _rootPanel.Visibility = value ? Visibility.Visible : Visibility.Collapsed;
                }
                if (value)
                {
                    _selectedIndex = 0;
                    UpdateSelection();
                }
            }
        }

        /// <summary>
        /// Creates a new pause menu.
        /// </summary>
        // Initialize pause menu with UI component and font
        // Based on Stride API: https://doc.stride3d.net/latest/en/api/Stride.Engine.UIComponent.html
        // UIComponent manages UI rendering and input handling
        // Based on Stride API: https://doc.stride3d.net/latest/en/api/Stride.Graphics.SpriteFont.html
        // SpriteFont provides font rendering capabilities for text
        public PauseMenu([NotNull] UIComponent uiComponent, [NotNull] SpriteFont font)
        {
            _uiComponent = uiComponent ?? throw new ArgumentNullException("uiComponent");
            _font = font ?? throw new ArgumentNullException("font");

            BuildUI();
        }

        private void BuildUI()
        {
            // Full-screen overlay with semi-transparent background
            // Based on Stride API: https://doc.stride3d.net/latest/en/api/Stride.UI.Panels.Grid.html
            // Grid with Width/Height = float.NaN fills available space
            // BackgroundColor sets semi-transparent background, Visibility.Collapsed initially hides
            // Source: https://doc.stride3d.net/latest/en/manual/user-interface/layout-and-panels.html
            _rootPanel = new Grid
            {
                Width = float.NaN,
                Height = float.NaN,
                // Based on Stride API: https://doc.stride3d.net/latest/en/api/Stride.Core.Mathematics.Color.html
                // Color(byte r, byte g, byte b, byte a) constructor creates a color from RGBA byte values (0-255)
                // Source: https://doc.stride3d.net/latest/en/manual/graphics/colors.html
                BackgroundColor = new Color(0, 0, 0, 180),
                Visibility = Visibility.Collapsed
            };

            // Center panel
            // Based on Stride API: https://doc.stride3d.net/latest/en/api/Stride.UI.Controls.Border.html
            // Border() constructor creates a new border control
            // Border draws a border around content, BackgroundColor/BorderColor/BorderThickness set appearance
            // Method signature: Border()
            // Based on Stride API: https://doc.stride3d.net/latest/en/api/Stride.Core.Mathematics.Color.html
            // Color(byte r, byte g, byte b, byte a) constructor creates background color
            // Method signature: Color(byte r, byte g, byte b, byte a)
            // HorizontalAlignment/VerticalAlignment.Center centers the border
            // Based on Stride API: https://doc.stride3d.net/latest/en/api/Stride.UI.Thickness.html
            // Thickness(float, float, float, float) constructor creates border thickness
            // Method signature: Thickness(float left, float top, float right, float bottom)
            // Source: https://doc.stride3d.net/latest/en/manual/user-interface/controls.html
            var centerPanel = new Border
            {
                BackgroundColor = new Color(20, 20, 40, 220),
                // Based on Stride API: https://doc.stride3d.net/latest/en/api/Stride.Core.Mathematics.Color.html
                // Color.Gold is a static property representing gold color
                // Source: https://doc.stride3d.net/latest/en/manual/graphics/colors.html
                BorderColor = Color.Gold,
                BorderThickness = new Thickness(2, 2, 2, 2),
                Width = 300,
                Height = 250,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };

            // Based on Stride API: https://doc.stride3d.net/latest/en/api/Stride.UI.Panels.StackPanel.html
            // StackPanel() constructor creates a new stack panel
            // StackPanel arranges children vertically, Margin sets spacing
            // Method signature: StackPanel()
            // Based on Stride API: https://doc.stride3d.net/latest/en/api/Stride.UI.Thickness.html
            // Thickness(float, float, float, float) - same constructor as above
            // Source: https://doc.stride3d.net/latest/en/manual/user-interface/layout-and-panels.html
            var contentPanel = new StackPanel
            {
                Orientation = Orientation.Vertical,
                Margin = new Thickness(20, 20, 20, 20)
            };

            // Title
            // Based on Stride API: https://doc.stride3d.net/latest/en/api/Stride.UI.Controls.TextBlock.html
            // TextBlock() constructor creates a new text block element
            // Method signature: TextBlock()
            // Based on Stride API: https://doc.stride3d.net/latest/en/api/Stride.Core.Mathematics.Color.html
            // Color.Gold static property - same as documented above
            // Based on Stride API: https://doc.stride3d.net/latest/en/api/Stride.UI.Thickness.html
            // Thickness(float, float, float, float) - same constructor as above
            // Source: https://doc.stride3d.net/latest/en/manual/user-interface/controls.html
            var title = new TextBlock
            {
                Font = _font,
                TextSize = 28,
                TextColor = Color.Gold,
                Text = "PAUSED",
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 0, 0, 30)
            };
            contentPanel.Children.Add(title);

            // Menu buttons
            _menuPanel = new StackPanel
            {
                Orientation = Orientation.Vertical
            };

            _resumeButton = CreateMenuButton("Resume", OnResumeClick);
            _exitButton = CreateMenuButton("Exit to Desktop", OnExitClick);

            _menuPanel.Children.Add(_resumeButton);
            _menuPanel.Children.Add(_exitButton);

            contentPanel.Children.Add(_menuPanel);
            centerPanel.Content = contentPanel;
            _rootPanel.Children.Add(centerPanel);

            // Add to UI
            if (_uiComponent.Page != null && _uiComponent.Page.RootElement != null)
            {
                var existingRoot = _uiComponent.Page.RootElement as Canvas;
                if (existingRoot != null)
                {
                    existingRoot.Children.Add(_rootPanel);
                }
            }
        }

        private Button CreateMenuButton(string text, EventHandler<RoutedEventArgs> clickHandler)
        {
            // Create menu button
            // Based on Stride API: https://doc.stride3d.net/latest/en/api/Stride.UI.Controls.Button.html
            // Button() constructor creates a new button control
            // Method signature: Button()
            // Based on Stride API: https://doc.stride3d.net/latest/en/api/Stride.UI.Controls.TextBlock.html
            // TextBlock() constructor creates text content for button
            // Method signature: TextBlock()
            // Based on Stride API: https://doc.stride3d.net/latest/en/api/Stride.Core.Mathematics.Color.html
            // Color.White is a static property representing white color (R=255, G=255, B=255, A=255)
            // Source: https://doc.stride3d.net/latest/en/manual/graphics/colors.html
            // Based on Stride API: https://doc.stride3d.net/latest/en/api/Stride.Core.Mathematics.Color.html
            // Color(byte r, byte g, byte b, byte a) constructor creates button background color
            // Method signature: Color(byte r, byte g, byte b, byte a)
            // Based on Stride API: https://doc.stride3d.net/latest/en/api/Stride.UI.Thickness.html
            // Thickness(float, float, float, float) - same constructor as above
            // Source: https://doc.stride3d.net/latest/en/manual/user-interface/controls.html
            var button = new Button
            {
                Content = new TextBlock
                {
                    Font = _font,
                    TextSize = 20,
                    TextColor = Color.White,
                    Text = text,
                    HorizontalAlignment = HorizontalAlignment.Center
                },
                BackgroundColor = new Color(40, 40, 80, 200),
                Width = 200,
                Height = 40,
                Margin = new Thickness(0, 5, 0, 5),
                HorizontalAlignment = HorizontalAlignment.Center
            };

            // Based on Stride API: https://doc.stride3d.net/latest/en/api/Stride.UI.Controls.Button.html
            // Click event fires when button is clicked
            // MouseOverStateChanged event fires when mouse enters/leaves button
            // MouseOverState.MouseOverElement indicates mouse is over the element
            // Source: https://doc.stride3d.net/latest/en/manual/user-interface/controls.html
            button.Click += clickHandler;
            button.MouseOverStateChanged += (sender, args) =>
            {
                if (args.NewValue == MouseOverState.MouseOverElement)
                {
                    button.BackgroundColor = new Color(80, 80, 120, 220);
                }
                else
                {
                    UpdateButtonStyle(button, false);
                }
            };

            return button;
        }

        // Handle button click events
        // Based on Stride API: https://doc.stride3d.net/latest/en/api/Stride.UI.Events.RoutedEventArgs.html
        // RoutedEventArgs provides event data for routed events like button clicks
        // Source: https://doc.stride3d.net/latest/en/manual/user-interface/events.html
        private void OnResumeClick(object sender, RoutedEventArgs e)
        {
            IsVisible = false;
            OnResume?.Invoke();
        }

        private void OnExitClick(object sender, RoutedEventArgs e)
        {
            OnExit?.Invoke();
        }

        /// <summary>
        /// Handles keyboard input for menu navigation.
        /// </summary>
        public void HandleInput(bool up, bool down, bool confirm, bool cancel)
        {
            if (!_isVisible)
            {
                return;
            }

            if (cancel)
            {
                IsVisible = false;
                OnResume?.Invoke();
                return;
            }

            if (up && _selectedIndex > 0)
            {
                _selectedIndex--;
                UpdateSelection();
            }
            else if (down && _selectedIndex < 1)
            {
                _selectedIndex++;
                UpdateSelection();
            }
            else if (confirm)
            {
                ActivateSelection();
            }
        }

        private void UpdateSelection()
        {
            UpdateButtonStyle(_resumeButton, _selectedIndex == 0);
            UpdateButtonStyle(_exitButton, _selectedIndex == 1);
        }

        private void UpdateButtonStyle(Button button, bool selected)
        {
            if (button == null)
            {
                return;
            }

            if (selected)
            {
                button.BackgroundColor = new Color(100, 100, 140, 220);
                var textBlock = button.Content as TextBlock;
                if (textBlock != null)
                {
                    textBlock.TextColor = Color.Yellow;
                }
            }
            else
            {
                button.BackgroundColor = new Color(40, 40, 80, 200);
                var textBlock = button.Content as TextBlock;
                if (textBlock != null)
                {
                    textBlock.TextColor = Color.White;
                }
            }
        }

        private void ActivateSelection()
        {
            switch (_selectedIndex)
            {
                case 0:
                    IsVisible = false;
                    OnResume?.Invoke();
                    break;
                case 1:
                    OnExit?.Invoke();
                    break;
            }
        }
    }
}

