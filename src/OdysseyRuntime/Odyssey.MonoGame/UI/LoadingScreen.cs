using System;
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
    /// Loading screen shown during module transitions.
    /// </summary>
    public class LoadingScreen
    {
        private readonly UIComponent _uiComponent;
        private readonly SpriteFont _font;

        private Grid _rootPanel;
        private TextBlock _loadingText;
        private TextBlock _statusText;
        private Border _progressBar;
        private Border _progressBarBackground;
        private float _progress;
        private bool _isVisible;

        /// <summary>
        /// Gets or sets whether the loading screen is visible.
        /// </summary>
        // Control visibility of loading screen
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
            }
        }

        /// <summary>
        /// Gets or sets the current progress (0-1).
        /// </summary>
        public float Progress
        {
            get { return _progress; }
            set
            {
                _progress = Math.Max(0, Math.Min(1, value));
                UpdateProgressBar();
            }
        }

        /// <summary>
        /// Creates a new loading screen.
        /// </summary>
        // Initialize loading screen with UI component and font
        // Based on Stride API: https://doc.stride3d.net/latest/en/api/Stride.Engine.UIComponent.html
        // UIComponent manages UI rendering and input handling
        // Based on Stride API: https://doc.stride3d.net/latest/en/api/Stride.Graphics.SpriteFont.html
        // SpriteFont provides font rendering capabilities for text
        public LoadingScreen([NotNull] UIComponent uiComponent, [NotNull] SpriteFont font)
        {
            _uiComponent = uiComponent ?? throw new ArgumentNullException("uiComponent");
            _font = font ?? throw new ArgumentNullException("font");

            BuildUI();
        }

        private void BuildUI()
        {
            // Full-screen black overlay
            // Based on Stride API: https://doc.stride3d.net/latest/en/api/Stride.UI.Panels.Grid.html
            // Grid with Width/Height = float.NaN fills available space
            // BackgroundColor sets background, Visibility.Collapsed initially hides the panel
            // Source: https://doc.stride3d.net/latest/en/manual/user-interface/layout-and-panels.html
            _rootPanel = new Grid
            {
                Width = float.NaN,
                Height = float.NaN,
                // Based on Stride API: https://doc.stride3d.net/latest/en/api/Stride.Core.Mathematics.Color.html
                // Color.Black is a static property representing black color (R=0, G=0, B=0, A=255)
                // Source: https://doc.stride3d.net/latest/en/manual/graphics/colors.html
                BackgroundColor = Color.Black,
                Visibility = Visibility.Collapsed
            };

            // Center content
            // Based on Stride API: https://doc.stride3d.net/latest/en/api/Stride.UI.Panels.StackPanel.html
            // StackPanel() constructor creates a new stack panel
            // StackPanel arranges children vertically, HorizontalAlignment/VerticalAlignment.Center centers content
            // Method signature: StackPanel()
            // Orientation.Vertical stacks children vertically
            // Source: https://doc.stride3d.net/latest/en/manual/user-interface/layout-and-panels.html
            var contentPanel = new StackPanel
            {
                Orientation = Orientation.Vertical,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                Width = 400
            };

            // Loading title
            // Based on Stride API: https://doc.stride3d.net/latest/en/api/Stride.UI.Controls.TextBlock.html
            // TextBlock() constructor creates a new text block element
            // Method signature: TextBlock()
            // Based on Stride API: https://doc.stride3d.net/latest/en/api/Stride.Core.Mathematics.Color.html
            // Color.Gold is a static property representing gold color
            // Source: https://doc.stride3d.net/latest/en/manual/graphics/colors.html
            // Based on Stride API: https://doc.stride3d.net/latest/en/api/Stride.UI.Thickness.html
            // Thickness(float, float, float, float) - same constructor as above
            _loadingText = new TextBlock
            {
                Font = _font,
                TextSize = 32,
                TextColor = Color.Gold,
                Text = "LOADING",
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 0, 0, 20)
            };
            contentPanel.Children.Add(_loadingText);

            // Status text
            // Based on Stride API: https://doc.stride3d.net/latest/en/api/Stride.UI.Controls.TextBlock.html
            // TextBlock() constructor creates a new text block element
            // Method signature: TextBlock()
            // Based on Stride API: https://doc.stride3d.net/latest/en/api/Stride.Core.Mathematics.Color.html
            // Color.LightGray is a static property representing light gray color
            // Source: https://doc.stride3d.net/latest/en/manual/graphics/colors.html
            // Based on Stride API: https://doc.stride3d.net/latest/en/api/Stride.UI.Thickness.html
            // Thickness(float, float, float, float) - same constructor as above
            _statusText = new TextBlock
            {
                Font = _font,
                TextSize = 16,
                TextColor = Color.LightGray,
                Text = "Please wait...",
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 0, 0, 30)
            };
            contentPanel.Children.Add(_statusText);

            // Progress bar background
            // Based on Stride API: https://doc.stride3d.net/latest/en/api/Stride.UI.Panels.Grid.html
            // Grid() constructor creates a new grid panel
            // Method signature: Grid()
            // Source: https://doc.stride3d.net/latest/en/manual/user-interface/layout-and-panels.html
            var progressContainer = new Grid
            {
                Width = 350,
                Height = 30,
                HorizontalAlignment = HorizontalAlignment.Center
            };

            // Based on Stride API: https://doc.stride3d.net/latest/en/api/Stride.UI.Controls.Border.html
            // Border() constructor creates a new border control
            // Method signature: Border()
            // Based on Stride API: https://doc.stride3d.net/latest/en/api/Stride.Core.Mathematics.Color.html
            // Color(byte r, byte g, byte b, byte a) constructor creates background color
            // Method signature: Color(byte r, byte g, byte b, byte a)
            // Based on Stride API: https://doc.stride3d.net/latest/en/api/Stride.UI.Thickness.html
            // Thickness(float, float, float, float) constructor creates border thickness
            // Method signature: Thickness(float left, float top, float right, float bottom)
            // Source: https://doc.stride3d.net/latest/en/manual/user-interface/controls.html
            _progressBarBackground = new Border
            {
                BackgroundColor = new Color(40, 40, 60, 255),
                // Based on Stride API: https://doc.stride3d.net/latest/en/api/Stride.Core.Mathematics.Color.html
                // Color.DarkGoldenrod is a static property representing dark goldenrod color
                // Source: https://doc.stride3d.net/latest/en/manual/graphics/colors.html
                BorderColor = Color.DarkGoldenrod,
                BorderThickness = new Thickness(2, 2, 2, 2),
                Width = 350,
                Height = 30
            };
            progressContainer.Children.Add(_progressBarBackground);

            // Progress bar fill
            // Based on Stride API: https://doc.stride3d.net/latest/en/api/Stride.UI.Controls.Border.html
            // Border() constructor creates a new border control used as progress fill
            // Method signature: Border()
            // Based on Stride API: https://doc.stride3d.net/latest/en/api/Stride.Core.Mathematics.Color.html
            // Color.Goldenrod is a static property representing goldenrod color
            // Source: https://doc.stride3d.net/latest/en/manual/graphics/colors.html
            // Based on Stride API: https://doc.stride3d.net/latest/en/api/Stride.UI.Thickness.html
            // Thickness(float, float, float, float) - same constructor as above
            // Source: https://doc.stride3d.net/latest/en/manual/user-interface/controls.html
            _progressBar = new Border
            {
                BackgroundColor = Color.Goldenrod,
                Width = 0,
                Height = 26,
                Margin = new Thickness(2, 2, 2, 2),
                HorizontalAlignment = HorizontalAlignment.Left
            };
            progressContainer.Children.Add(_progressBar);

            contentPanel.Children.Add(progressContainer);

            _rootPanel.Children.Add(contentPanel);

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

        /// <summary>
        /// Shows the loading screen with the specified message.
        /// </summary>
        public void Show(string moduleName = null)
        {
            if (!string.IsNullOrEmpty(moduleName))
            {
                _loadingText.Text = "LOADING: " + moduleName.ToUpperInvariant();
            }
            else
            {
                _loadingText.Text = "LOADING";
            }

            _progress = 0;
            UpdateProgressBar();
            IsVisible = true;
        }

        /// <summary>
        /// Updates the status text.
        /// </summary>
        public void SetStatus(string status)
        {
            if (_statusText != null)
            {
                _statusText.Text = status ?? "";
            }
        }

        /// <summary>
        /// Hides the loading screen.
        /// </summary>
        public void Hide()
        {
            IsVisible = false;
        }

        private void UpdateProgressBar()
        {
            if (_progressBar == null)
            {
                return;
            }

            // Update progress bar width
            // Based on Stride API: https://doc.stride3d.net/latest/en/api/Stride.UI.UIElement.html
            // Width property sets the element's width
            float maxWidth = 346; // 350 - 4 for margins
            _progressBar.Width = maxWidth * _progress;
        }

        /// <summary>
        /// Updates progress with a status message.
        /// </summary>
        public void UpdateProgress(float progress, string status)
        {
            Progress = progress;
            SetStatus(status);
        }
    }
}

