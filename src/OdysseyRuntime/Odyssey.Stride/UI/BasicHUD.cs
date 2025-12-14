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
    /// Basic HUD showing player health, force points, and debug info.
    /// </summary>
    public class BasicHUD
    {
        private readonly UIComponent _uiComponent;
        private readonly SpriteFont _font;

        private Canvas _rootCanvas;
        private Grid _healthPanel;
        private Border _healthBar;
        private Border _healthBarBackground;
        private TextBlock _healthText;

        private Border _forceBar;
        private Border _forceBarBackground;
        private TextBlock _forceText;

        private TextBlock _debugText;
        private bool _showDebug;

        private float _currentHealth = 100;
        private float _maxHealth = 100;
        private float _currentForce = 100;
        private float _maxForce = 100;

        /// <summary>
        /// Gets or sets whether debug info is displayed.
        /// </summary>
        // Control visibility of debug text
        // Based on Stride API: https://doc.stride3d.net/latest/en/api/Stride.UI.UIElement.html
        // Visibility property controls whether element is rendered (Visible) or hidden (Collapsed)
        public bool ShowDebug
        {
            get { return _showDebug; }
            set
            {
                _showDebug = value;
                if (_debugText != null)
                {
                    _debugText.Visibility = value ? Visibility.Visible : Visibility.Collapsed;
                }
            }
        }

        /// <summary>
        /// Gets or sets whether the HUD is visible.
        /// </summary>
        // Control visibility of HUD root canvas
        // Based on Stride API: https://doc.stride3d.net/latest/en/api/Stride.UI.UIElement.html
        // Visibility property controls whether element is rendered
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
        /// Creates a new basic HUD.
        /// </summary>
        // Initialize HUD with UI component and font
        // Based on Stride API: https://doc.stride3d.net/latest/en/api/Stride.Engine.UIComponent.html
        // UIComponent manages UI rendering and input handling
        // Based on Stride API: https://doc.stride3d.net/latest/en/api/Stride.Graphics.SpriteFont.html
        // SpriteFont provides font rendering capabilities for text
        // Source: https://doc.stride3d.net/latest/en/manual/graphics/low-level-api/spritefont.html
        public BasicHUD([NotNull] UIComponent uiComponent, [NotNull] SpriteFont font)
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

            // Health/Force panel in top-left
            // Based on Stride API: https://doc.stride3d.net/latest/en/api/Stride.UI.Panels.Grid.html
            // Grid arranges children in rows and columns, Width/Height set dimensions
            // Margin sets spacing, VerticalAlignment/HorizontalAlignment control positioning
            // BackgroundColor sets background color
            // Source: https://doc.stride3d.net/latest/en/manual/user-interface/layout-and-panels.html
            _healthPanel = new Grid
            {
                Width = 250,
                Height = 80,
                Margin = new Thickness(20, 20, 20, 20),
                VerticalAlignment = VerticalAlignment.Top,
                HorizontalAlignment = HorizontalAlignment.Left,
                BackgroundColor = new Color(0, 0, 0, 150)
            };

            // Define grid rows using StripDefinition
            // Based on Stride API: https://doc.stride3d.net/latest/en/api/Stride.UI.Panels.StripDefinition.html
            // StripType.Star makes rows fill available space proportionally
            _healthPanel.RowDefinitions.Add(new StripDefinition(StripType.Star, 1));
            _healthPanel.RowDefinitions.Add(new StripDefinition(StripType.Star, 1));

            // Health bar
            var healthRow = CreateStatBar(
                "HP",
                Color.Red,
                out _healthBarBackground,
                out _healthBar,
                out _healthText);
            healthRow.SetGridRow(0);
            _healthPanel.Children.Add(healthRow);

            // Force bar
            var forceRow = CreateStatBar(
                "FP",
                Color.Blue,
                out _forceBarBackground,
                out _forceBar,
                out _forceText);
            forceRow.SetGridRow(1);
            _healthPanel.Children.Add(forceRow);

            _rootCanvas.Children.Add(_healthPanel);

            // Debug text in top-right
            // Based on Stride API: https://doc.stride3d.net/latest/en/api/Stride.UI.Controls.TextBlock.html
            // TextBlock displays text, Visibility.Collapsed hides the element
            // Source: https://doc.stride3d.net/latest/en/manual/user-interface/controls.html
            _debugText = new TextBlock
            {
                Font = _font,
                TextSize = 12,
                TextColor = Color.Yellow,
                Margin = new Thickness(0, 20, 20, 0),
                HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Top,
                Visibility = Visibility.Collapsed
            };
            _rootCanvas.Children.Add(_debugText);

            // Add to UI page
            // Based on Stride API: https://doc.stride3d.net/latest/en/api/Stride.Engine.UIComponent.html
            // Page property gets/sets the active UI page
            // Based on Stride API: https://doc.stride3d.net/latest/en/api/Stride.UI.UIPage.html
            // RootElement property sets the root UI element for the page
            if (_uiComponent.Page != null)
            {
                _uiComponent.Page.RootElement = _rootCanvas;
            }
        }

        private Grid CreateStatBar(
            string label,
            Color barColor,
            out Border background,
            out Border bar,
            out TextBlock text)
        {
            var grid = new Grid
            {
                Margin = new Thickness(10, 5, 10, 5),
                Height = 25
            };

            grid.ColumnDefinitions.Add(new StripDefinition(StripType.Fixed, 30));  // Label
            grid.ColumnDefinitions.Add(new StripDefinition(StripType.Star, 1));    // Bar
            grid.ColumnDefinitions.Add(new StripDefinition(StripType.Fixed, 60));  // Text

            // Label
            var labelText = new TextBlock
            {
                Font = _font,
                TextSize = 14,
                TextColor = Color.White,
                Text = label,
                VerticalAlignment = VerticalAlignment.Center
            };
            labelText.SetGridColumn(0);
            grid.Children.Add(labelText);

            // Bar background
            // Based on Stride API: https://doc.stride3d.net/latest/en/api/Stride.UI.Controls.Border.html
            // Border is a control that draws a border around content
            // BackgroundColor sets background, BorderColor sets border color, BorderThickness sets border width
            // Source: https://doc.stride3d.net/latest/en/manual/user-interface/controls.html
            background = new Border
            {
                BackgroundColor = new Color(40, 40, 40, 200),
                BorderColor = Color.Gray,
                BorderThickness = new Thickness(1, 1, 1, 1),
                Height = 16,
                VerticalAlignment = VerticalAlignment.Center
            };
            background.SetGridColumn(1);
            grid.Children.Add(background);

            // Bar fill
            // Border used as progress bar fill, HorizontalAlignment.Left aligns to left edge
            bar = new Border
            {
                BackgroundColor = barColor,
                Height = 14,
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(1, 0, 0, 0)
            };
            bar.SetGridColumn(1);
            grid.Children.Add(bar);

            // Text value
            text = new TextBlock
            {
                Font = _font,
                TextSize = 12,
                TextColor = Color.White,
                Text = "100/100",
                HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(5, 0, 0, 0)
            };
            text.SetGridColumn(2);
            grid.Children.Add(text);

            return grid;
        }

        /// <summary>
        /// Updates the health display.
        /// </summary>
        public void SetHealth(float current, float max)
        {
            _currentHealth = current;
            _maxHealth = max;
            UpdateHealthBar();
        }

        /// <summary>
        /// Updates the force points display.
        /// </summary>
        public void SetForcePoints(float current, float max)
        {
            _currentForce = current;
            _maxForce = max;
            UpdateForceBar();
        }

        /// <summary>
        /// Sets the debug text.
        /// </summary>
        public void SetDebugText(string text)
        {
            if (_debugText != null)
            {
                _debugText.Text = text ?? "";
            }
        }

        private void UpdateHealthBar()
        {
            if (_healthBar == null || _healthBarBackground == null || _healthText == null)
            {
                return;
            }

            float ratio = _maxHealth > 0 ? _currentHealth / _maxHealth : 0;
            ratio = Math.Max(0, Math.Min(1, ratio));

            // Update bar width (relative to background)
            // Based on Stride API: https://doc.stride3d.net/latest/en/api/Stride.UI.UIElement.html
            // Width property sets the element's width
            float maxWidth = 160; // Approximate width
            _healthBar.Width = maxWidth * ratio;

            // Update text
            _healthText.Text = ((int)_currentHealth).ToString() + "/" + ((int)_maxHealth).ToString();

            // Change color based on health
            if (ratio < 0.25f)
            {
                _healthBar.BackgroundColor = Color.DarkRed;
            }
            else if (ratio < 0.5f)
            {
                _healthBar.BackgroundColor = Color.Orange;
            }
            else
            {
                _healthBar.BackgroundColor = Color.Red;
            }
        }

        private void UpdateForceBar()
        {
            if (_forceBar == null || _forceBarBackground == null || _forceText == null)
            {
                return;
            }

            float ratio = _maxForce > 0 ? _currentForce / _maxForce : 0;
            ratio = Math.Max(0, Math.Min(1, ratio));

            float maxWidth = 160;
            _forceBar.Width = maxWidth * ratio;

            _forceText.Text = ((int)_currentForce).ToString() + "/" + ((int)_maxForce).ToString();
        }

        /// <summary>
        /// Gets the root canvas for this HUD.
        /// </summary>
        public Canvas GetRootCanvas()
        {
            return _rootCanvas;
        }
    }
}

