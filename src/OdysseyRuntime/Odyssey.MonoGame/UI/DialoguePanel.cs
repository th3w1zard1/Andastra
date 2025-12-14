using System;
using System.Collections.Generic;
using Stride.Core.Mathematics;
using Stride.Engine;
using Stride.Graphics;
using Stride.UI;
using Stride.UI.Controls;
using Stride.UI.Panels;
using Odyssey.Kotor.Dialogue;
using JetBrains.Annotations;

namespace Odyssey.MonoGame.UI
{
    /// <summary>
    /// UI panel for displaying dialogue conversations.
    /// Shows speaker text, portrait, and player reply choices.
    /// </summary>
    public class DialoguePanel
    {
        private readonly UIComponent _uiComponent;
        private readonly SpriteFont _font;

        private Grid _rootPanel;
        private TextBlock _speakerNameText;
        private TextBlock _dialogueText;
        private StackPanel _repliesPanel;
        private ImageElement _portraitImage;
        private Border _dialogueBackground;

        private List<Button> _replyButtons;
        private int _selectedReply = -1;
        private bool _isVisible;

        /// <summary>
        /// Event fired when a reply is selected.
        /// </summary>
        public event Action<int> OnReplySelected;

        /// <summary>
        /// Event fired when the user skips dialogue.
        /// </summary>
        public event Action OnSkipRequested;

        /// <summary>
        /// Gets or sets whether the panel is visible.
        /// </summary>
        // Control visibility of dialogue panel
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
        /// Creates a new dialogue panel.
        /// </summary>
        /// <param name="uiComponent">The UI component to attach to.</param>
        /// <param name="font">The font to use for text.</param>
        // Initialize dialogue panel with UI component and font
        // Based on Stride API: https://doc.stride3d.net/latest/en/api/Stride.Engine.UIComponent.html
        // UIComponent manages UI rendering and input handling
        // Based on Stride API: https://doc.stride3d.net/latest/en/api/Stride.Graphics.SpriteFont.html
        // SpriteFont provides font rendering capabilities for text
        public DialoguePanel([NotNull] UIComponent uiComponent, [NotNull] SpriteFont font)
        {
            _uiComponent = uiComponent ?? throw new ArgumentNullException("uiComponent");
            _font = font ?? throw new ArgumentNullException("font");
            _replyButtons = new List<Button>();

            BuildUI();
        }

        private void BuildUI()
        {
            // Create root panel (bottom portion of screen)
            // Based on Stride API: https://doc.stride3d.net/latest/en/api/Stride.UI.Panels.Grid.html
            // Grid arranges children in rows and columns, Width = float.NaN fills available space
            // VerticalAlignment.Bottom aligns to bottom, BackgroundColor sets background
            // Source: https://doc.stride3d.net/latest/en/manual/user-interface/layout-and-panels.html
            _rootPanel = new Grid
            {
                Width = float.NaN, // Fill
                Height = 280,
                VerticalAlignment = VerticalAlignment.Bottom,
                // Based on Stride API: https://doc.stride3d.net/latest/en/api/Stride.Core.Mathematics.Color.html
                // Color(byte r, byte g, byte b, byte a) constructor creates a color from RGBA byte values (0-255)
                // Source: https://doc.stride3d.net/latest/en/manual/graphics/colors.html
                BackgroundColor = new Color(0, 0, 0, 200)
            };

            // Define columns: portrait (left), dialogue area (center)
            // Based on Stride API: https://doc.stride3d.net/latest/en/api/Stride.UI.Panels.StripDefinition.html
            // StripType.Fixed sets fixed width, StripType.Star fills remaining space
            _rootPanel.ColumnDefinitions.Add(new StripDefinition(StripType.Fixed, 150));
            _rootPanel.ColumnDefinitions.Add(new StripDefinition(StripType.Star, 1));

            // Portrait area
            // Based on Stride API: https://doc.stride3d.net/latest/en/api/Stride.UI.Controls.ImageElement.html
            // ImageElement displays an image, Width/Height set dimensions
            // StretchType.Uniform maintains aspect ratio while fitting within bounds
            // Source: https://doc.stride3d.net/latest/en/manual/user-interface/controls.html
            _portraitImage = new ImageElement
            {
                Width = 128,
                Height = 128,
                Margin = new Thickness(10, 10, 10, 10),
                StretchType = StretchType.Uniform
            };
            _portraitImage.SetGridColumn(0);
            _rootPanel.Children.Add(_portraitImage);

            // Dialogue content area
            // Based on Stride API: https://doc.stride3d.net/latest/en/api/Stride.UI.Panels.Grid.html
            // Grid() constructor creates a new grid panel
            // Method signature: Grid()
            // Source: https://doc.stride3d.net/latest/en/manual/user-interface/layout-and-panels.html
            var dialogueArea = new Grid();
            dialogueArea.SetGridColumn(1);
            // Based on Stride API: https://doc.stride3d.net/latest/en/api/Stride.UI.Panels.StripDefinition.html
            // StripDefinition(StripType.Fixed, float) - Fixed type sets fixed pixel height
            dialogueArea.RowDefinitions.Add(new StripDefinition(StripType.Fixed, 30)); // Speaker name
            // Based on Stride API: https://doc.stride3d.net/latest/en/api/Stride.UI.Panels.StripDefinition.html
            // StripDefinition(StripType.Star, float) - Star type fills remaining space proportionally
            dialogueArea.RowDefinitions.Add(new StripDefinition(StripType.Star, 1));   // Dialogue text
            // Based on Stride API: https://doc.stride3d.net/latest/en/api/Stride.UI.Panels.StripDefinition.html
            // StripDefinition(StripType.Star, float) - same constructor as above
            dialogueArea.RowDefinitions.Add(new StripDefinition(StripType.Star, 1));   // Replies

            // Speaker name
            _speakerNameText = new TextBlock
            {
                Font = _font,
                TextSize = 20,
                // Based on Stride API: https://doc.stride3d.net/latest/en/api/Stride.Core.Mathematics.Color.html
                // Color.Gold is a static property representing gold color
                // Source: https://doc.stride3d.net/latest/en/manual/graphics/colors.html
                TextColor = Color.Gold,
                Margin = new Thickness(10, 5, 10, 5),
                HorizontalAlignment = HorizontalAlignment.Left
            };
            _speakerNameText.SetGridRow(0);
            dialogueArea.Children.Add(_speakerNameText);

            // Dialogue text with border
            _dialogueBackground = new Border
            {
                BackgroundColor = new Color(20, 20, 40, 180),
                // Based on Stride API: https://doc.stride3d.net/latest/en/api/Stride.Core.Mathematics.Color.html
                // Color.DarkGoldenrod is a static property representing dark goldenrod color
                // Source: https://doc.stride3d.net/latest/en/manual/graphics/colors.html
                BorderColor = Color.DarkGoldenrod,
                BorderThickness = new Thickness(1, 1, 1, 1),
                Margin = new Thickness(10, 5, 10, 5)
            };
            _dialogueBackground.SetGridRow(1);

            _dialogueText = new TextBlock
            {
                Font = _font,
                TextSize = 16,
                // Based on Stride API: https://doc.stride3d.net/latest/en/api/Stride.Core.Mathematics.Color.html
                // Color.White static property - same as documented above
                TextColor = Color.White,
                Margin = new Thickness(10, 10, 10, 10),
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Top
            };
            _dialogueBackground.Content = _dialogueText;
            dialogueArea.Children.Add(_dialogueBackground);

            // Replies panel
            // Based on Stride API: https://doc.stride3d.net/latest/en/api/Stride.UI.Controls.ScrollViewer.html
            // ScrollViewer() constructor creates a new scroll viewer
            // ScrollViewer provides scrolling for content that exceeds visible area
            // Method signature: ScrollViewer()
            // ScrollMode.Vertical enables vertical scrolling, Content property sets scrollable content
            // Based on Stride API: https://doc.stride3d.net/latest/en/api/Stride.UI.Thickness.html
            // Thickness(float, float, float, float) - same constructor as above
            // Source: https://doc.stride3d.net/latest/en/manual/user-interface/controls.html
            var repliesContainer = new ScrollViewer
            {
                ScrollMode = ScrollingMode.Vertical,
                Margin = new Thickness(10, 5, 10, 5)
            };
            repliesContainer.SetGridRow(2);

            // Based on Stride API: https://doc.stride3d.net/latest/en/api/Stride.UI.Panels.StackPanel.html
            // StackPanel() constructor creates a new stack panel
            // StackPanel arranges children in a single line (horizontal or vertical)
            // Method signature: StackPanel()
            // Orientation.Vertical stacks children vertically
            // Source: https://doc.stride3d.net/latest/en/manual/user-interface/layout-and-panels.html
            _repliesPanel = new StackPanel
            {
                Orientation = Orientation.Vertical
            };
            repliesContainer.Content = _repliesPanel;
            dialogueArea.Children.Add(repliesContainer);

            _rootPanel.Children.Add(dialogueArea);

            // Initially hidden
            _rootPanel.Visibility = Visibility.Collapsed;

            // Add to UI page
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
        /// Shows an NPC dialogue entry.
        /// </summary>
        /// <param name="speakerName">Name of the speaker.</param>
        /// <param name="dialogueText">The dialogue text.</param>
        /// <param name="portrait">Optional portrait texture.</param>
        public void ShowEntry(string speakerName, string dialogueText, Texture portrait = null)
        {
            _speakerNameText.Text = speakerName ?? "Unknown";
            _dialogueText.Text = dialogueText ?? "";

            // Set portrait
            // Based on Stride API: https://doc.stride3d.net/latest/en/api/Stride.UI.Controls.ImageElement.html
            // Visibility property controls whether image is displayed
            // FIXME: ImageElement requires SpriteFromTexture conversion - currently not implemented
            if (portrait != null)
            {
                _portraitImage.Visibility = Visibility.Visible;
                // Note: Would need SpriteFromTexture conversion
            }
            else
            {
                _portraitImage.Visibility = Visibility.Collapsed;
            }

            IsVisible = true;
        }

        /// <summary>
        /// Sets the available reply options.
        /// </summary>
        /// <param name="replies">List of reply text options.</param>
        public void SetReplies(IList<string> replies)
        {
            // Clear existing buttons
            ClearReplies();

            if (replies == null || replies.Count == 0)
            {
                // No replies - show "Continue" or just wait
                return;
            }

            // Create buttons for each reply
            for (int i = 0; i < replies.Count; i++)
            {
                int index = i; // Capture for closure
                string replyText = replies[i];

                // Format reply number
                string displayText = (i + 1).ToString() + ". " + (replyText ?? "[Continue]");

                // Create button for reply option
                // Based on Stride API: https://doc.stride3d.net/latest/en/api/Stride.UI.Controls.Button.html
                // Button() constructor creates a new button control
                // Method signature: Button()
                // Based on Stride API: https://doc.stride3d.net/latest/en/api/Stride.UI.Controls.TextBlock.html
                // TextBlock() constructor creates text content for button
                // Method signature: TextBlock()
                // Based on Stride API: https://doc.stride3d.net/latest/en/api/Stride.Core.Mathematics.Color.html
                // Color.LightGoldenrodYellow is a static property representing light goldenrod yellow color
                // Source: https://doc.stride3d.net/latest/en/manual/graphics/colors.html
                // Based on Stride API: https://doc.stride3d.net/latest/en/api/Stride.UI.Thickness.html
                // Thickness(float, float, float, float) - same constructor as above
                // Based on Stride API: https://doc.stride3d.net/latest/en/api/Stride.Core.Mathematics.Color.html
                // Color(byte r, byte g, byte b, byte a) constructor creates button background color
                // Method signature: Color(byte r, byte g, byte b, byte a)
                // Source: https://doc.stride3d.net/latest/en/manual/user-interface/controls.html
                var button = new Button
                {
                    Content = new TextBlock
                    {
                        Font = _font,
                        TextSize = 14,
                        TextColor = Color.LightGoldenrodYellow,
                        Text = displayText,
                        Margin = new Thickness(5, 2, 5, 2)
                    },
                    BackgroundColor = new Color(30, 30, 60, 180),
                    Margin = new Thickness(0, 2, 0, 2),
                    HorizontalAlignment = HorizontalAlignment.Stretch
                };

                // Based on Stride API: https://doc.stride3d.net/latest/en/api/Stride.UI.Controls.Button.html
                // Click event fires when button is clicked
                // MouseOverStateChanged event fires when mouse enters/leaves button
                // MouseOverState.MouseOverElement indicates mouse is over the element
                // Source: https://doc.stride3d.net/latest/en/manual/user-interface/controls.html
                button.Click += (sender, args) => SelectReply(index);
                button.MouseOverStateChanged += (sender, args) =>
                {
                    if (args.NewValue == MouseOverState.MouseOverElement)
                    {
                        button.BackgroundColor = new Color(60, 60, 100, 200);
                    }
                    else
                    {
                        button.BackgroundColor = new Color(30, 30, 60, 180);
                    }
                };

                _replyButtons.Add(button);
                _repliesPanel.Children.Add(button);
            }

            // Select first reply by default
            if (_replyButtons.Count > 0)
            {
                _selectedReply = 0;
                HighlightReply(0);
            }
        }

        /// <summary>
        /// Clears reply options.
        /// </summary>
        public void ClearReplies()
        {
            foreach (var button in _replyButtons)
            {
                _repliesPanel.Children.Remove(button);
            }
            _replyButtons.Clear();
            _selectedReply = -1;
        }

        /// <summary>
        /// Hides the dialogue panel.
        /// </summary>
        public void Hide()
        {
            IsVisible = false;
            ClearReplies();
        }

        /// <summary>
        /// Handles keyboard input for dialogue navigation.
        /// </summary>
        public void HandleInput(bool up, bool down, bool confirm, bool skip)
        {
            if (!_isVisible)
            {
                return;
            }

            if (_replyButtons.Count > 0)
            {
                // Navigate replies
                if (up && _selectedReply > 0)
                {
                    HighlightReply(_selectedReply - 1);
                }
                else if (down && _selectedReply < _replyButtons.Count - 1)
                {
                    HighlightReply(_selectedReply + 1);
                }
                else if (confirm && _selectedReply >= 0)
                {
                    SelectReply(_selectedReply);
                }
            }
            else if (skip || confirm)
            {
                // No replies - skip button advances
                OnSkipRequested?.Invoke();
            }
        }

        /// <summary>
        /// Handles numeric key input for quick reply selection.
        /// </summary>
        public void HandleNumberKey(int number)
        {
            if (!_isVisible || number < 1 || number > _replyButtons.Count)
            {
                return;
            }

            SelectReply(number - 1);
        }

        private void HighlightReply(int index)
        {
            // Unhighlight previous
            if (_selectedReply >= 0 && _selectedReply < _replyButtons.Count)
            {
                _replyButtons[_selectedReply].BackgroundColor = new Color(30, 30, 60, 180);
            }

            // Highlight new
            _selectedReply = index;
            if (_selectedReply >= 0 && _selectedReply < _replyButtons.Count)
            {
                _replyButtons[_selectedReply].BackgroundColor = new Color(80, 80, 120, 200);
            }
        }

        private void SelectReply(int index)
        {
            if (index >= 0 && index < _replyButtons.Count)
            {
                OnReplySelected?.Invoke(index);
            }
        }
    }
}

