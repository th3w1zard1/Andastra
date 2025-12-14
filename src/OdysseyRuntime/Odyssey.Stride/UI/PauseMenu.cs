using System;
using Stride.Core.Mathematics;
using Stride.Engine;
using Stride.Graphics;
using Stride.UI;
using Stride.UI.Controls;
using Stride.UI.Events;
using Stride.UI.Panels;
using JetBrains.Annotations;

namespace Odyssey.Stride.UI
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
        public PauseMenu([NotNull] UIComponent uiComponent, [NotNull] SpriteFont font)
        {
            _uiComponent = uiComponent ?? throw new ArgumentNullException("uiComponent");
            _font = font ?? throw new ArgumentNullException("font");

            BuildUI();
        }

        private void BuildUI()
        {
            // Full-screen overlay with semi-transparent background
            _rootPanel = new Grid
            {
                Width = float.NaN,
                Height = float.NaN,
                BackgroundColor = new Color(0, 0, 0, 180),
                Visibility = Visibility.Collapsed
            };

            // Center panel
            var centerPanel = new Border
            {
                BackgroundColor = new Color(20, 20, 40, 220),
                BorderColor = Color.Gold,
                BorderThickness = new Thickness(2, 2, 2, 2),
                Width = 300,
                Height = 250,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };

            var contentPanel = new StackPanel
            {
                Orientation = Orientation.Vertical,
                Margin = new Thickness(20, 20, 20, 20)
            };

            // Title
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

