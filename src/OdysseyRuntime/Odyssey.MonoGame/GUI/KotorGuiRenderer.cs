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

namespace Odyssey.MonoGame.GUI
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
            // Based on Stride API: https://doc.stride3d.net/latest/en/api/Stride.UI.Panels.Canvas.html
            // Canvas is a panel that allows absolute positioning of child elements
            // Width/Height set the canvas dimensions, HorizontalAlignment/VerticalAlignment control layout
            // Source: https://doc.stride3d.net/latest/en/manual/user-interface/index.html
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
            // Based on Stride API: https://doc.stride3d.net/latest/en/api/Stride.UI.UIPage.html
            // UIPage represents a complete UI page with a root element
            // RootElement property sets the root UI element (Canvas in this case)
            // Source: https://doc.stride3d.net/latest/en/manual/user-interface/index.html
            var page = new UIPage { RootElement = _rootCanvas };
            // Based on Stride API: https://doc.stride3d.net/latest/en/api/Stride.Engine.UIComponent.html
            // UIComponent.Page property sets the active UI page to render
            // Method signature: UIPage Page { get; set; }
            // Source: https://doc.stride3d.net/latest/en/manual/user-interface/index.html
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
            // Create Grid panel for container
            // Based on Stride API: https://doc.stride3d.net/latest/en/api/Stride.UI.Panels.Grid.html
            // Grid is a panel that arranges children in rows and columns
            // BackgroundColor sets the background color of the grid
            // Source: https://doc.stride3d.net/latest/en/manual/user-interface/index.html
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
            // Create Button control
            // Based on Stride API: https://doc.stride3d.net/latest/en/api/Stride.UI.Controls.Button.html
            // Button is a clickable UI control that can contain content
            // BackgroundColor sets the button's background color
            // Source: https://doc.stride3d.net/latest/en/manual/user-interface/index.html
            var strideButton = new Button
            {
                BackgroundColor = GetBackgroundColorFromBorder(button.Border)
            };

            // Set button text
            if (!string.IsNullOrEmpty(button.Text))
            {
                // Create TextBlock for button text
                // Based on Stride API: https://doc.stride3d.net/latest/en/api/Stride.UI.Controls.TextBlock.html
                // TextBlock displays text with configurable font, size, color, and alignment
                // Text property sets the displayed text, TextColor sets the text color
                // TextSize sets font size, TextAlignment controls horizontal text alignment
                // Source: https://doc.stride3d.net/latest/en/manual/user-interface/index.html
                var textBlock = new TextBlock
                {
                    Text = button.Text,
                    TextColor = ConvertColor(button.TextColor),
                    TextSize = 14, // Default size, should be from font
                    TextAlignment = TextAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center,
                    HorizontalAlignment = HorizontalAlignment.Center
                };
                // Based on Stride API: https://doc.stride3d.net/latest/en/api/Stride.UI.Controls.ContentControl.html
                // ContentControl.Content property sets the content of the control
                // Method signature: UIElement Content { get; set; }
                // Source: https://doc.stride3d.net/latest/en/manual/user-interface/index.html
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
            // Based on Stride API: https://doc.stride3d.net/latest/en/api/Stride.UI.Controls.Button.html
            // Button.Click event fires when the button is clicked
            // Event signature: event EventHandler<RoutedEventArgs> Click
            // Source: https://doc.stride3d.net/latest/en/manual/user-interface/index.html
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
            // Create TextBlock for label text
            // Based on Stride API: https://doc.stride3d.net/latest/en/api/Stride.UI.Controls.TextBlock.html
            // TextBlock displays static text with configurable properties
            // Text property sets the displayed text, TextColor sets the text color
            // TextSize sets font size, TextAlignment controls horizontal text alignment
            // VerticalAlignment controls vertical alignment within parent
            // Source: https://doc.stride3d.net/latest/en/manual/user-interface/index.html
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
            // Create ScrollViewer for scrollable content
            // Based on Stride API: https://doc.stride3d.net/latest/en/api/Stride.UI.Controls.ScrollViewer.html
            // ScrollViewer provides scrolling functionality for content that exceeds viewport
            // ScrollMode property sets scrolling direction (Vertical, Horizontal, or Both)
            // Source: https://doc.stride3d.net/latest/en/manual/user-interface/index.html
            var scrollViewer = new ScrollViewer
            {
                ScrollMode = ScrollingMode.Vertical
            };

            // Create StackPanel for vertical list layout
            // Based on Stride API: https://doc.stride3d.net/latest/en/api/Stride.UI.Panels.StackPanel.html
            // StackPanel arranges children in a single line (horizontal or vertical)
            // Orientation property sets layout direction (Vertical or Horizontal)
            // Source: https://doc.stride3d.net/latest/en/manual/user-interface/index.html
            var stackPanel = new StackPanel
            {
                Orientation = Orientation.Vertical
            };

            // Based on Stride API: https://doc.stride3d.net/latest/en/api/Stride.UI.Controls.ContentControl.html
            // ContentControl.Content property sets the scrollable content
            // Method signature: UIElement Content { get; set; }
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
            // Create Border for progress bar background
            // Based on Stride API: https://doc.stride3d.net/latest/en/api/Stride.UI.Controls.Border.html
            // Border is a control that draws a border and/or background around content
            // BackgroundColor sets the background color, HorizontalAlignment/VerticalAlignment control sizing
            // Source: https://doc.stride3d.net/latest/en/manual/user-interface/index.html
            var background = new Border
            {
                BackgroundColor = new Color(50, 50, 50, 255),
                HorizontalAlignment = HorizontalAlignment.Stretch,
                VerticalAlignment = VerticalAlignment.Stretch
            };
            // Based on Stride API: https://doc.stride3d.net/latest/en/api/Stride.UI.Panels.Panel.html
            // Panel.Children collection contains child UI elements
            // Add(UIElement) adds a child element to the panel
            // Source: https://doc.stride3d.net/latest/en/manual/user-interface/index.html
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
            // Based on Stride API: https://doc.stride3d.net/latest/en/api/Stride.UI.UIElement.html
            // UIElement.Width/Height properties set element dimensions
            // Margin property sets spacing around the element
            // Source: https://doc.stride3d.net/latest/en/manual/user-interface/index.html
            element.Width = control.Size.X * _scaleX;
            element.Height = control.Size.Y * _scaleY;
            // Based on Stride API: https://doc.stride3d.net/latest/en/api/Stride.UI.Thickness.html
            // Thickness(float left, float top, float right, float bottom) constructor creates a thickness with left, top, right, bottom values
            // Source: https://doc.stride3d.net/latest/en/manual/user-interface/layout-and-panels.html
            element.Margin = new Thickness(
                control.Position.X * _scaleX,
                control.Position.Y * _scaleY,
                0,
                0
            );

            // Apply visibility
            // Based on Stride API: https://doc.stride3d.net/latest/en/api/Stride.UI.UIElement.html
            // UIElement.Visibility property controls element visibility
            // Visibility.Visible = shown, Visibility.Collapsed = hidden and doesn't take space
            // Source: https://doc.stride3d.net/latest/en/manual/user-interface/index.html
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
                // Set element opacity
                // Based on Stride API: https://doc.stride3d.net/latest/en/api/Stride.UI.UIElement.html
                // Opacity property controls element transparency (0.0 = fully transparent, 1.0 = fully opaque)
                // Source: https://doc.stride3d.net/latest/en/manual/user-interface/index.html
                element.Opacity = alpha;
            }

            // Set name for identification
            // Based on Stride API: https://doc.stride3d.net/latest/en/api/Stride.UI.UIElement.html
            // Name property sets the element's name for identification and event handling
            // Source: https://doc.stride3d.net/latest/en/manual/user-interface/index.html
            element.Name = control.Tag ?? $"Control_{control.Id}";
        }

        /// <summary>
        /// Converts CSharpKOTOR Color to Stride Color.
        /// </summary>
        // Convert CSharpKOTOR color to Stride Color
        // Based on Stride API: https://doc.stride3d.net/latest/en/api/Stride.Core.Mathematics.Color.html
        // Color represents RGBA color values, Color.White is a static property for white color
        // Color constructor: Color(byte r, byte g, byte b, byte a) creates a color from RGBA components
        // Source: https://doc.stride3d.net/latest/en/manual/graphics/colors.html
        private Color ConvertColor(CSharpKOTOR.Common.Color color)
        {
            if (color == null) return Color.White;
            return new Color(color.R, color.G, color.B, color.A);
        }

        /// <summary>
        /// Gets background color from border properties.
        /// </summary>
        // Get background color from border, defaulting to transparent
        // Based on Stride API: https://doc.stride3d.net/latest/en/api/Stride.Core.Mathematics.Color.html
        // Color.Transparent is a static property for fully transparent color (alpha = 0)
        // Color constructor creates a color from RGBA byte values (0-255)
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
        // Convert KOTOR alignment to Stride TextAlignment enum
        // Based on Stride API: https://doc.stride3d.net/latest/en/api/Stride.UI.TextAlignment.html
        // TextAlignment enum defines horizontal text alignment: Left, Center, Right
        // Source: https://doc.stride3d.net/latest/en/manual/user-interface/controls.html
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
            // Based on Stride API: https://doc.stride3d.net/latest/en/api/Stride.UI.Panels.Canvas.html
            // Canvas allows absolute positioning of child elements
            // HorizontalAlignment/VerticalAlignment.Stretch makes canvas fill parent
            // BackgroundColor sets the canvas background color
            // Source: https://doc.stride3d.net/latest/en/manual/user-interface/index.html
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

            // Create main panel as Border
            // Based on Stride API: https://doc.stride3d.net/latest/en/api/Stride.UI.Controls.Border.html
            // Border draws a border and/or background around content
            // Width/Height set dimensions, BackgroundColor sets background, BorderColor sets border color
            // BorderThickness sets border width (Thickness: left, top, right, bottom)
            // Source: https://doc.stride3d.net/latest/en/manual/user-interface/index.html
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
            // Based on Stride API: https://doc.stride3d.net/latest/en/api/Stride.UI.Panels.Canvas.html
            // SetCanvasRelativePosition(Vector3) sets absolute position within Canvas
            // Method signature: void SetCanvasRelativePosition(Vector3 position)
            // Source: https://doc.stride3d.net/latest/en/manual/user-interface/index.html
            mainPanel.SetCanvasRelativePosition(new Vector3(panelX, panelY, 0));
            // Based on Stride API: https://doc.stride3d.net/latest/en/api/Stride.UI.Panels.Panel.html
            // Panel.Children.Add(UIElement) adds child element to panel
            _rootCanvas.Children.Add(mainPanel);

            // Create a grid to hold buttons - better spacing
            // Based on Stride API: https://doc.stride3d.net/latest/en/api/Stride.UI.Panels.Grid.html
            // Grid arranges children in rows and columns
            // RowDefinitions collection defines row layout, StripDefinition defines row size
            // StripType.Fixed sets fixed pixel size, StripType.Star sets proportional sizing
            // Source: https://doc.stride3d.net/latest/en/manual/user-interface/index.html
            var contentGrid = new Grid
            {
                HorizontalAlignment = HorizontalAlignment.Stretch,
                VerticalAlignment = VerticalAlignment.Stretch
            };
            // Based on Stride API: https://doc.stride3d.net/latest/en/api/Stride.UI.Panels.StripDefinition.html
            // StripDefinition(StripType, float) creates a row/column definition
            // StripType.Fixed with value 80 = fixed 80 pixel height
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
            // Based on Stride API: https://doc.stride3d.net/latest/en/api/Stride.UI.Panels.Grid.html
            // SetGridRow(int) sets which grid row the element occupies
            // Method signature: void SetGridRow(UIElement element, int row)
            // Source: https://doc.stride3d.net/latest/en/manual/user-interface/index.html
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

            // Set border content
            // Based on Stride API: https://doc.stride3d.net/latest/en/api/Stride.UI.Controls.ContentControl.html
            // ContentControl.Content property sets the content of the control (Border in this case)
            // Method signature: UIElement Content { get; set; }
            mainPanel.Content = contentGrid;

            // Set the page
            // Based on Stride API: https://doc.stride3d.net/latest/en/api/Stride.UI.UIPage.html
            // UIPage represents a complete UI page, RootElement property sets the root UI element
            // Source: https://doc.stride3d.net/latest/en/manual/user-interface/index.html
            var page = new UIPage { RootElement = _rootCanvas };
            // Based on Stride API: https://doc.stride3d.net/latest/en/api/Stride.Engine.UIComponent.html
            // UIComponent.Page property sets the active UI page to render
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
            // Set button content to inner border
            // Based on Stride API: https://doc.stride3d.net/latest/en/api/Stride.UI.Controls.ContentControl.html
            // ContentControl.Content property sets the content of the button
            button.Content = innerBorder;

            return button;
        }

        /// <summary>
        /// Clears the currently rendered GUI.
        /// </summary>
        public void Clear()
        {
            // Clear UI page
            // Based on Stride API: https://doc.stride3d.net/latest/en/api/Stride.Engine.UIComponent.html
            // Setting UIComponent.Page to null removes the active UI page
            // Method signature: UIPage Page { get; set; }
            if (_uiComponent.Page != null)
            {
                _uiComponent.Page = null;
            }
            _rootCanvas = null;
        }
    }
}

