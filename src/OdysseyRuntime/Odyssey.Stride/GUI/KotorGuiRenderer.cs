using System;
using System.Collections.Generic;
using Stride.Core.Mathematics;
using Stride.Engine;
using Stride.Graphics;
using Stride.Rendering;
using Stride.Rendering.Sprites;
using Stride.UI;
using Stride.UI.Controls;
using Stride.UI.Panels;
using CSharpKOTOR.Resource.Generics.GUI;
using CSharpKOTOR.Resources;
using Odyssey.Content.Interfaces;
using Stride.Graphics.Font;

namespace Odyssey.Stride.GUI
{
    /// <summary>
    /// Renders KOTOR GUI controls using Stride's UI system.
    /// Converts CSharpKOTOR.Resource.Generics.GUI controls to Stride UI elements.
    /// Base resolution: 640x480 (KOTOR standard), scaled to actual screen resolution.
    /// </summary>
    public class KotorGuiRenderer
    {
        private const float BaseWidth = 640f;
        private const float BaseHeight = 480f;

        private readonly UIComponent _uiComponent;
        private readonly GraphicsDevice _graphicsDevice;
        private readonly KotorGuiManager _guiManager;
        private readonly Dictionary<string, Texture> _textureCache = new Dictionary<string, Texture>();
        private readonly Dictionary<string, SpriteFont> _fontCache = new Dictionary<string, SpriteFont>();

        private Canvas _rootCanvas;
        private float _scaleX = 1f;
        private float _scaleY = 1f;

        public KotorGuiRenderer(UIComponent uiComponent, GraphicsDevice graphicsDevice, KotorGuiManager guiManager)
        {
            _uiComponent = uiComponent ?? throw new ArgumentNullException(nameof(uiComponent));
            _graphicsDevice = graphicsDevice ?? throw new ArgumentNullException(nameof(graphicsDevice));
            _guiManager = guiManager ?? throw new ArgumentNullException(nameof(guiManager));
        }

        /// <summary>
        /// Renders a KOTOR GUI file to the UI system.
        /// </summary>
        public void RenderGui(CSharpKOTOR.Resource.Generics.GUI.GUI gui, int screenWidth, int screenHeight)
        {
            if (gui == null) throw new ArgumentNullException(nameof(gui));

            // Calculate scaling from base 640x480 to actual screen size
            _scaleX = screenWidth / BaseWidth;
            _scaleY = screenHeight / BaseHeight;

            // Create root canvas
            _rootCanvas = new Canvas
            {
                Width = screenWidth,
                Height = screenHeight,
                HorizontalAlignment = HorizontalAlignment.Stretch,
                VerticalAlignment = VerticalAlignment.Stretch
            };

            // Render all top-level controls
            foreach (var control in gui.Controls)
            {
                var element = RenderControl(control);
                if (element != null)
                {
                    _rootCanvas.Children.Add(element);
                }
            }

            // Set the page
            var page = new UIPage { RootElement = _rootCanvas };
            _uiComponent.Page = page;

            Console.WriteLine($"[KotorGuiRenderer] Rendered GUI '{gui.Tag}' with {gui.Controls.Count} top-level controls");
        }

        /// <summary>
        /// Recursively renders a GUI control and its children.
        /// </summary>
        private UIElement RenderControl(GUIControl control)
        {
            if (control == null) return null;

            UIElement element = null;

            // Create appropriate UI element based on control type
            switch (control.GuiType)
            {
                case GUIControlType.Panel:
                    element = RenderPanel((GUIPanel)control);
                    break;
                case GUIControlType.Button:
                    element = RenderButton((GUIButton)control);
                    break;
                case GUIControlType.Label:
                    element = RenderLabel((GUILabel)control);
                    break;
                case GUIControlType.ListBox:
                    element = RenderListBox((GUIListBox)control);
                    break;
                case GUIControlType.CheckBox:
                    element = RenderCheckBox((GUICheckBox)control);
                    break;
                case GUIControlType.Slider:
                    element = RenderSlider((GUISlider)control);
                    break;
                case GUIControlType.Progress:
                    element = RenderProgressBar((GUIProgressBar)control);
                    break;
                case GUIControlType.ScrollBar:
                    element = RenderScrollBar(control);
                    break;
                default:
                    // Generic control or unsupported type - render as empty container
                    element = RenderGenericControl(control);
                    break;
            }

            if (element != null)
            {
                // Apply common properties
                ApplyCommonProperties(element, control);

                // Recursively render children if this is a container
                if (element is ContentControl contentControl && control.Children.Count > 0)
                {
                    var childPanel = new Canvas();
                    foreach (var child in control.Children)
                    {
                        var childElement = RenderControl(child);
                        if (childElement != null)
                        {
                            childPanel.Children.Add(childElement);
                        }
                    }
                    contentControl.Content = childPanel;
                }
                else if (element is Panel panel && control.Children.Count > 0)
                {
                    foreach (var child in control.Children)
                    {
                        var childElement = RenderControl(child);
                        if (childElement != null)
                        {
                            panel.Children.Add(childElement);
                        }
                    }
                }
            }

            return element;
        }

        /// <summary>
        /// Renders a panel control (container with optional background).
        /// </summary>
        private UIElement RenderPanel(GUIPanel panel)
        {
            var grid = new Grid
            {
                BackgroundColor = GetBackgroundColorFromBorder(panel.Border)
            };

            // TODO: Border texture rendering is skipped for now; we rely on solid colors derived from border data.
            // TODO: Add border texture rendering.

            return grid;
        }

        /// <summary>
        /// Renders a button control.
        /// </summary>
        private UIElement RenderButton(GUIButton button)
        {
            var strideButton = new Button
            {
                BackgroundColor = GetBackgroundColorFromBorder(button.Border)
            };

            // Set button text
            if (!string.IsNullOrEmpty(button.Text))
            {
                var textBlock = new TextBlock
                {
                    Text = button.Text,
                    TextColor = ConvertColor(button.TextColor),
                    TextSize = 14, // Default size, should be from font
                    TextAlignment = TextAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center,
                    HorizontalAlignment = HorizontalAlignment.Center
                };
                strideButton.Content = textBlock;
            }
            else if (button.GuiText != null && !string.IsNullOrEmpty(button.GuiText.Text))
            {
                // Use text from GuiText if button.Text is empty
                var textBlock = new TextBlock
                {
                    Text = button.GuiText.Text,
                    TextColor = ConvertColor(button.GuiText.Color),
                    TextSize = 14,
                    TextAlignment = TextAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center,
                    HorizontalAlignment = HorizontalAlignment.Center
                };
                strideButton.Content = textBlock;
            }

            // Store control reference for event handling
            strideButton.Name = button.Tag ?? $"Button_{button.Id}";

            // Hook up click event
            var buttonTag = button.Tag;
            var buttonId = button.Id;
            strideButton.Click += (sender, args) =>
            {
                Console.WriteLine($"[KotorGuiRenderer] Button clicked: Tag='{buttonTag}', ID={buttonId}");
                _guiManager.TriggerButtonClick(buttonTag, buttonId);
            };

            return strideButton;
        }

        /// <summary>
        /// Renders a label control (static text).
        /// </summary>
        private UIElement RenderLabel(GUILabel label)
        {
            var textBlock = new TextBlock
            {
                Text = label.Text ?? string.Empty,
                TextColor = ConvertColor(label.TextColor),
                TextSize = 12, // Default size
                TextAlignment = ConvertAlignment(label.Alignment),
                VerticalAlignment = VerticalAlignment.Center
            };

            return textBlock;
        }

        /// <summary>
        /// Renders a list box control.
        /// </summary>
        private UIElement RenderListBox(GUIListBox listBox)
        {
            // For now, render as a simple scrollable panel
            var scrollViewer = new ScrollViewer
            {
                ScrollMode = ScrollingMode.Vertical
            };

            var stackPanel = new StackPanel
            {
                Orientation = Orientation.Vertical
            };

            scrollViewer.Content = stackPanel;

            return scrollViewer;
        }

        /// <summary>
        /// Renders a checkbox control.
        /// </summary>
        private UIElement RenderCheckBox(GUICheckBox checkBox)
        {
            // Stride doesn't have a built-in checkbox, so we'll use a toggle button
            var button = new Button
            {
                BackgroundColor = GetBackgroundColorFromBorder(checkBox.Border)
            };

            return button;
        }

        /// <summary>
        /// Renders a slider control.
        /// </summary>
        private UIElement RenderSlider(GUISlider slider)
        {
            // Stride doesn't have a built-in slider in the UI system
            // For now, render as a simple bar
            var grid = new Grid
            {
                BackgroundColor = new Color(100, 100, 100, 255)
            };

            return grid;
        }

        /// <summary>
        /// Renders a progress bar control.
        /// </summary>
        private UIElement RenderProgressBar(GUIProgressBar progressBar)
        {
            var grid = new Grid();

            // Background
            var background = new Border
            {
                BackgroundColor = new Color(50, 50, 50, 255),
                HorizontalAlignment = HorizontalAlignment.Stretch,
                VerticalAlignment = VerticalAlignment.Stretch
            };
            grid.Children.Add(background);

            // Progress fill
            float progressPercent = progressBar.CurrentValue / progressBar.MaxValue;
            var progressFill = new Border
            {
                BackgroundColor = new Color(0, 150, 255, 255),
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Stretch,
                Width = progressBar.Size.X * progressPercent * _scaleX
            };
            grid.Children.Add(progressFill);

            return grid;
        }

        /// <summary>
        /// Renders a scrollbar control.
        /// </summary>
        private UIElement RenderScrollBar(GUIControl control)
        {
            // Simple scrollbar representation
            var grid = new Grid
            {
                BackgroundColor = new Color(80, 80, 80, 255)
            };

            return grid;
        }

        /// <summary>
        /// Renders a generic control (fallback for unsupported types).
        /// </summary>
        private UIElement RenderGenericControl(GUIControl control)
        {
            var grid = new Grid
            {
                BackgroundColor = GetBackgroundColorFromBorder(control.Border)
            };

            return grid;
        }

        /// <summary>
        /// Applies common properties (position, size, visibility, etc.) to a UI element.
        /// </summary>
        private void ApplyCommonProperties(UIElement element, GUIControl control)
        {
            // Apply position and size (scaled from 640x480 base)
            element.Width = control.Size.X * _scaleX;
            element.Height = control.Size.Y * _scaleY;
            element.Margin = new Thickness(
                control.Position.X * _scaleX,
                control.Position.Y * _scaleY,
                0,
                0
            );

            // Apply visibility
            element.Visibility = control.Locked == true ? Visibility.Collapsed : Visibility.Visible;

            // Apply color modulation if available
            if (control.Color != null)
            {
                // Color modulation would be applied to textures
                // For now, we apply it as opacity
                float alpha = 1.0f;
                if (control.Properties.ContainsKey("ALPHA"))
                {
                    alpha = (float)control.Properties["ALPHA"];
                }
                element.Opacity = alpha;
            }

            // Set name for identification
            element.Name = control.Tag ?? $"Control_{control.Id}";
        }

        /// <summary>
        /// Converts CSharpKOTOR Color to Stride Color.
        /// </summary>
        private Color ConvertColor(CSharpKOTOR.Common.Color color)
        {
            if (color == null) return Color.White;
            return new Color(color.R, color.G, color.B, color.A);
        }

        /// <summary>
        /// Gets background color from border properties.
        /// </summary>
        private Color GetBackgroundColorFromBorder(GUIBorder border)
        {
            if (border == null) return Color.Transparent;

            if (border.Color != null)
            {
                return ConvertColor(border.Color);
            }

            // Default based on fill style
            if (border.FillStyle == 1) // Solid
            {
                return new Color(50, 50, 50, 200);
            }

            return Color.Transparent;
        }

        /// <summary>
        /// Converts KOTOR alignment value to Stride TextAlignment.
        /// </summary>
        private TextAlignment ConvertAlignment(int alignment)
        {
            // KOTOR alignment values:
            // 1=TopLeft, 2=TopCenter, 3=TopRight
            // 17=CenterLeft, 18=Center, 19=CenterRight
            // 33=BottomLeft, 34=BottomCenter, 35=BottomRight

            int horizontal = alignment % 16;
            switch (horizontal)
            {
                case 1: return TextAlignment.Left;
                case 2: return TextAlignment.Center;
                case 3: return TextAlignment.Right;
                default: return TextAlignment.Center;
            }
        }

        /// <summary>
        /// Creates a simple fallback UI that works without any external resources or fonts.
        /// Uses purely visual elements (colors, borders) to create an interactive UI.
        /// This is GUARANTEED to work as it doesn't depend on font assets.
        /// </summary>
        public void CreateFallbackUI(int screenWidth, int screenHeight)
        {
            Console.WriteLine("[KotorGuiRenderer] Creating visual-only fallback UI (no font dependency)");

            // Create root canvas - visible background
            _rootCanvas = new Canvas
            {
                HorizontalAlignment = HorizontalAlignment.Stretch,
                VerticalAlignment = VerticalAlignment.Stretch,
                BackgroundColor = new Color(20, 30, 60, 255) // Visible dark blue background
            };

            // Create a centered panel/window - larger and more visible
            float panelWidth = 600;
            float panelHeight = 400;
            float panelX = (screenWidth - panelWidth) / 2;
            float panelY = (screenHeight - panelHeight) / 2;

            var mainPanel = new Border
            {
                Width = panelWidth,
                Height = panelHeight,
                BackgroundColor = new Color(40, 50, 80, 255), // More visible background
                BorderColor = new Color(255, 255, 255, 255), // Bright white border
                BorderThickness = new Thickness(6, 6, 6, 6),
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Top
            };
            mainPanel.SetCanvasRelativePosition(new Vector3(panelX, panelY, 0));
            _rootCanvas.Children.Add(mainPanel);

            // Create a grid to hold buttons - better spacing
            var contentGrid = new Grid
            {
                HorizontalAlignment = HorizontalAlignment.Stretch,
                VerticalAlignment = VerticalAlignment.Stretch
            };
            contentGrid.RowDefinitions.Add(new StripDefinition(StripType.Fixed, 80));  // Header
            contentGrid.RowDefinitions.Add(new StripDefinition(StripType.Fixed, 15));  // Spacer
            contentGrid.RowDefinitions.Add(new StripDefinition(StripType.Fixed, 70));  // New Game
            contentGrid.RowDefinitions.Add(new StripDefinition(StripType.Fixed, 10));  // Spacer
            contentGrid.RowDefinitions.Add(new StripDefinition(StripType.Fixed, 70));  // Load Game
            contentGrid.RowDefinitions.Add(new StripDefinition(StripType.Fixed, 10));  // Spacer
            contentGrid.RowDefinitions.Add(new StripDefinition(StripType.Fixed, 70));  // Options
            contentGrid.RowDefinitions.Add(new StripDefinition(StripType.Fixed, 10));  // Spacer
            contentGrid.RowDefinitions.Add(new StripDefinition(StripType.Fixed, 70));  // Exit
            contentGrid.RowDefinitions.Add(new StripDefinition(StripType.Star, 1));    // Bottom space

            // Header panel - BRIGHT golden bar to represent title
            var headerPanel = new Border
            {
                BackgroundColor = new Color(255, 200, 50, 255), // Bright gold
                BorderColor = new Color(255, 255, 255, 255), // White border
                BorderThickness = new Thickness(4, 4, 4, 4),
                Margin = new Thickness(30, 10, 30, 5),
                HorizontalAlignment = HorizontalAlignment.Stretch,
                VerticalAlignment = VerticalAlignment.Stretch
            };
            headerPanel.SetGridRow(0);
            contentGrid.Children.Add(headerPanel);

            // New Game button - BRIGHT green = start/go
            var newGameButton = CreateFallbackButton(new Color(50, 200, 50, 255), new Color(100, 255, 100, 255));
            newGameButton.SetGridRow(2);
            newGameButton.Margin = new Thickness(40, 3, 40, 3);
            newGameButton.Click += (sender, args) =>
            {
                Console.WriteLine("[FallbackUI] New Game button clicked");
                _guiManager.TriggerButtonClick("BTN_NEWGAME", null);
            };
            contentGrid.Children.Add(newGameButton);

            // Load Game button - BRIGHT blue = load/open
            var loadGameButton = CreateFallbackButton(new Color(50, 100, 200, 255), new Color(100, 150, 255, 255));
            loadGameButton.SetGridRow(4);
            loadGameButton.Margin = new Thickness(40, 3, 40, 3);
            loadGameButton.Click += (sender, args) =>
            {
                Console.WriteLine("[FallbackUI] Load Game button clicked");
                _guiManager.TriggerButtonClick("BTN_LOADGAME", null);
            };
            contentGrid.Children.Add(loadGameButton);

            // Options button - BRIGHT cyan = settings/configure
            var optionsButton = CreateFallbackButton(new Color(50, 180, 180, 255), new Color(100, 220, 220, 255));
            optionsButton.SetGridRow(6);
            optionsButton.Margin = new Thickness(40, 3, 40, 3);
            optionsButton.Click += (sender, args) =>
            {
                Console.WriteLine("[FallbackUI] Options button clicked");
                _guiManager.TriggerButtonClick("BTN_OPTIONS", null);
            };
            contentGrid.Children.Add(optionsButton);

            // Exit button - BRIGHT red = stop/exit
            var exitButton = CreateFallbackButton(new Color(200, 50, 50, 255), new Color(255, 100, 100, 255));
            exitButton.SetGridRow(8);
            exitButton.Margin = new Thickness(40, 3, 40, 3);
            exitButton.Click += (sender, args) =>
            {
                Console.WriteLine("[FallbackUI] Exit button clicked");
                _guiManager.TriggerButtonClick("BTN_EXIT", null);
            };
            contentGrid.Children.Add(exitButton);

            mainPanel.Content = contentGrid;

            // Set the page
            var page = new UIPage { RootElement = _rootCanvas };
            _uiComponent.Page = page;

            Console.WriteLine("[KotorGuiRenderer] ========================================");
            Console.WriteLine("[KotorGuiRenderer] FALLBACK UI CREATED SUCCESSFULLY");
            Console.WriteLine("[KotorGuiRenderer] ========================================");
            Console.WriteLine("[KotorGuiRenderer] UI Layout:");
            Console.WriteLine("[KotorGuiRenderer]   - BRIGHT GOLD header bar");
            Console.WriteLine("[KotorGuiRenderer]   - BRIGHT GREEN = New Game");
            Console.WriteLine("[KotorGuiRenderer]   - BRIGHT BLUE = Load Game");
            Console.WriteLine("[KotorGuiRenderer]   - BRIGHT CYAN = Options");
            Console.WriteLine("[KotorGuiRenderer]   - BRIGHT RED = Exit");
            Console.WriteLine("[KotorGuiRenderer] All buttons have THICK WHITE borders");
            Console.WriteLine("[KotorGuiRenderer] No font dependency - purely visual");
            Console.WriteLine("[KotorGuiRenderer] ========================================");
        }

        /// <summary>
        /// Creates a simple visual button for the fallback UI (no text, color-coded).
        /// Bright colors with thick white borders for maximum visibility.
        /// </summary>
        private Button CreateFallbackButton(Color backgroundColor, Color innerColor)
        {
            var button = new Button
            {
                BackgroundColor = backgroundColor,
                HorizontalAlignment = HorizontalAlignment.Stretch,
                VerticalAlignment = VerticalAlignment.Stretch
            };

            // Inner border to make it visually distinct and clickable - THICK white border
            var innerBorder = new Border
            {
                BackgroundColor = innerColor,
                BorderColor = new Color(255, 255, 255, 255), // Bright white
                BorderThickness = new Thickness(4, 4, 4, 4), // Thicker border
                Margin = new Thickness(10, 10, 10, 10),
                HorizontalAlignment = HorizontalAlignment.Stretch,
                VerticalAlignment = VerticalAlignment.Stretch
            };
            button.Content = innerBorder;

            return button;
        }

        /// <summary>
        /// Clears the currently rendered GUI.
        /// </summary>
        public void Clear()
        {
            if (_uiComponent.Page != null)
            {
                _uiComponent.Page = null;
            }
            _rootCanvas = null;
        }
    }
}

