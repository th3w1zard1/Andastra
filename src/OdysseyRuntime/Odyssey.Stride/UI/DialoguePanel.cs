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

namespace Odyssey.Stride.UI
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
            _rootPanel = new Grid
            {
                Width = float.NaN, // Fill
                Height = 280,
                VerticalAlignment = VerticalAlignment.Bottom,
                BackgroundColor = new Color(0, 0, 0, 200)
            };

            // Define columns: portrait (left), dialogue area (center)
            _rootPanel.ColumnDefinitions.Add(new StripDefinition(StripType.Fixed, 150));
            _rootPanel.ColumnDefinitions.Add(new StripDefinition(StripType.Star, 1));

            // Portrait area
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
            var dialogueArea = new Grid();
            dialogueArea.SetGridColumn(1);
            dialogueArea.RowDefinitions.Add(new StripDefinition(StripType.Fixed, 30)); // Speaker name
            dialogueArea.RowDefinitions.Add(new StripDefinition(StripType.Star, 1));   // Dialogue text
            dialogueArea.RowDefinitions.Add(new StripDefinition(StripType.Star, 1));   // Replies

            // Speaker name
            _speakerNameText = new TextBlock
            {
                Font = _font,
                TextSize = 20,
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
                BorderColor = Color.DarkGoldenrod,
                BorderThickness = new Thickness(1, 1, 1, 1),
                Margin = new Thickness(10, 5, 10, 5)
            };
            _dialogueBackground.SetGridRow(1);

            _dialogueText = new TextBlock
            {
                Font = _font,
                TextSize = 16,
                TextColor = Color.White,
                Margin = new Thickness(10, 10, 10, 10),
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Top
            };
            _dialogueBackground.Content = _dialogueText;
            dialogueArea.Children.Add(_dialogueBackground);

            // Replies panel
            var repliesContainer = new ScrollViewer
            {
                ScrollMode = ScrollingMode.Vertical,
                Margin = new Thickness(10, 5, 10, 5)
            };
            repliesContainer.SetGridRow(2);

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

