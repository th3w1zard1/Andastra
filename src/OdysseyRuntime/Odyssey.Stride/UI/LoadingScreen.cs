using System;
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
        public LoadingScreen([NotNull] UIComponent uiComponent, [NotNull] SpriteFont font)
        {
            _uiComponent = uiComponent ?? throw new ArgumentNullException("uiComponent");
            _font = font ?? throw new ArgumentNullException("font");

            BuildUI();
        }

        private void BuildUI()
        {
            // Full-screen black overlay
            _rootPanel = new Grid
            {
                Width = float.NaN,
                Height = float.NaN,
                BackgroundColor = Color.Black,
                Visibility = Visibility.Collapsed
            };

            // Center content
            var contentPanel = new StackPanel
            {
                Orientation = Orientation.Vertical,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                Width = 400
            };

            // Loading title
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
            var progressContainer = new Grid
            {
                Width = 350,
                Height = 30,
                HorizontalAlignment = HorizontalAlignment.Center
            };

            _progressBarBackground = new Border
            {
                BackgroundColor = new Color(40, 40, 60, 255),
                BorderColor = Color.DarkGoldenrod,
                BorderThickness = new Thickness(2, 2, 2, 2),
                Width = 350,
                Height = 30
            };
            progressContainer.Children.Add(_progressBarBackground);

            // Progress bar fill
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

