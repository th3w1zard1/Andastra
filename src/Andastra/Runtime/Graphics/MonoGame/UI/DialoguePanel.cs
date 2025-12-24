using System;
using System.Collections.Generic;
using Andastra.Runtime.Core.Dialogue;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;

namespace Andastra.Runtime.MonoGame.UI
{
    /// <summary>
    /// Dialogue panel UI component using MonoGame SpriteBatch rendering.
    /// Renders dialogue text, speaker name, and player reply options.
    /// </summary>
    /// <remarks>
    /// Dialogue Panel Rendering:
    /// - Based on swkotor2.exe dialogue UI system
    /// - Located via string references: "CSWSSCRIPTEVENT_EVENTTYPE_ON_DIALOGUE" @ 0x007bcac4
    /// - "ScriptDialogue" @ 0x007bee40, "ScriptEndDialogue" @ 0x007bede0
    /// - "OnEndDialogue" @ 0x007c1f60 (end dialogue script event)
    /// - Dialogue message fields: "PT_DLG_MSG_MSG" @ 0x007c1630, "PT_DLG_MSG_SPKR" @ 0x007c1640
    /// - "PT_DLG_MSG_LIST" @ 0x007c1650 (dialogue message list in PARTYTABLE)
    /// - Script hooks: "k_level_dlg" @ 0x007c3f88, "000_Level_Dlg_Fired" @ 0x007c3f94 (level dialogue script)
    /// - "k_hen_dialogue01" @ 0x007bf548 (dialogue script example)
    /// - Error message: "Error: dialogue can't find object '%s'!" @ 0x007c3730
    /// - Original implementation: Renders dialogue text box at bottom of screen with speaker name and replies
    /// - Dialogue text: NPC/PC lines displayed in main text area
    /// - Reply options: Numbered list of player choices (1-9 keys for quick selection)
    /// - Speaker name: Displayed above dialogue text (NPC name or "Player")
    /// - Dialogue system: Uses DLG (dialogue) file format, tracks conversation state
    /// - Based on KOTOR dialogue UI conventions from vendor/PyKotor/wiki/
    /// </remarks>
    public class DialoguePanel
    {
        private SpriteBatch _spriteBatch;
        private SpriteFont _font;
        private bool _isVisible = false;
        private int _selectedReplyIndex = 0;
        private string _currentText = string.Empty;
        private string _speakerName = string.Empty;
        private List<DialogueReply> _availableReplies = new List<DialogueReply>();
        private Texture2D _panelTexture;
        private Color _panelColor = new Color(0, 0, 0, 200); // Semi-transparent black
        private Color _selectedColor = Color.Yellow;
        private Color _normalColor = Color.White;
        private Color _textColor = Color.White;

        public bool IsVisible
        {
            get { return _isVisible; }
            set { _isVisible = value; }
        }

        public int SelectedReplyIndex
        {
            get { return _selectedReplyIndex; }
            set
            {
                if (value >= 0 && value < _availableReplies.Count)
                {
                    _selectedReplyIndex = value;
                }
            }
        }

        public DialoguePanel(GraphicsDevice device, SpriteFont font)
        {
            _spriteBatch = new SpriteBatch(device);
            _font = font ?? throw new ArgumentNullException("font");

            // Create a simple 1x1 texture for drawing rectangles
            _panelTexture = new Texture2D(device, 1, 1);
            _panelTexture.SetData(new[] { Color.White });
        }

        /// <summary>
        /// Updates the dialogue display with new text and replies.
        /// </summary>
        public void UpdateDialogue(string speakerName, string text, IReadOnlyList<DialogueReply> replies)
        {
            _speakerName = speakerName ?? string.Empty;
            _currentText = text ?? string.Empty;
            _availableReplies = new List<DialogueReply>(replies ?? new List<DialogueReply>());
            _selectedReplyIndex = 0;
            _isVisible = true;
        }

        /// <summary>
        /// Clears the dialogue display.
        /// </summary>
        public void ClearDialogue()
        {
            _currentText = string.Empty;
            _speakerName = string.Empty;
            _availableReplies.Clear();
            _selectedReplyIndex = 0;
            _isVisible = false;
        }

        public void Draw(GameTime gameTime)
        {
            if (!_isVisible || _font == null)
            {
                return;
            }

            int viewportWidth = _spriteBatch.GraphicsDevice.Viewport.Width;
            int viewportHeight = _spriteBatch.GraphicsDevice.Viewport.Height;

            // Dialogue panel dimensions (bottom of screen)
            int panelHeight = viewportHeight / 3;
            int panelY = viewportHeight - panelHeight;
            int panelPadding = 20;
            int textStartX = panelPadding;
            int textStartY = panelY + panelPadding;
            int lineHeight = (int)(_font.LineSpacing * 1.2f);

            _spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend);

            // Draw semi-transparent background panel
            _spriteBatch.Draw(_panelTexture,
                new Rectangle(0, panelY, viewportWidth, panelHeight),
                _panelColor);

            // Draw speaker name
            if (!string.IsNullOrEmpty(_speakerName))
            {
                Vector2 speakerPos = new Vector2(textStartX, textStartY);
                _spriteBatch.DrawString(_font, _speakerName, speakerPos, _selectedColor);
                textStartY += lineHeight + 10;
            }

            // Draw dialogue text (word wrap)
            if (!string.IsNullOrEmpty(_currentText))
            {
                Vector2 textPos = new Vector2(textStartX, textStartY);
                float maxWidth = viewportWidth - (panelPadding * 2);
                DrawWrappedText(_spriteBatch, _font, _currentText, textPos, maxWidth, _textColor);
                textStartY += (int)(_font.MeasureString(_currentText).Y * 1.5f) + 20;
            }

            // Draw reply options
            for (int i = 0; i < _availableReplies.Count; i++)
            {
                if (i >= 9) break; // Max 9 replies (1-9 keys)

                Color replyColor = (i == _selectedReplyIndex) ? _selectedColor : _normalColor;
                string replyText = string.Format("{0}. {1}", i + 1, _availableReplies[i].Text ?? string.Empty);

                Vector2 replyPos = new Vector2(textStartX, textStartY + (i * lineHeight));
                _spriteBatch.DrawString(_font, replyText, replyPos, replyColor);
            }

            _spriteBatch.End();
        }

        private void DrawWrappedText(SpriteBatch spriteBatch, SpriteFont font, string text, Vector2 position, float maxWidth, Color color)
        {
            string[] words = text.Split(' ');
            string line = string.Empty;
            float y = position.Y;

            foreach (string word in words)
            {
                string testLine = line + (line.Length > 0 ? " " : "") + word;
                Vector2 size = font.MeasureString(testLine);

                if (size.X > maxWidth && line.Length > 0)
                {
                    spriteBatch.DrawString(font, line, new Vector2(position.X, y), color);
                    y += font.LineSpacing;
                    line = word;
                }
                else
                {
                    line = testLine;
                }
            }

            if (line.Length > 0)
            {
                spriteBatch.DrawString(font, line, new Vector2(position.X, y), color);
            }
        }

        public void HandleInput(bool up, bool down, bool select, bool cancel)
        {
            if (!_isVisible)
            {
                return;
            }

            if (up && _selectedReplyIndex > 0)
            {
                _selectedReplyIndex--;
            }
            if (down && _selectedReplyIndex < _availableReplies.Count - 1)
            {
                _selectedReplyIndex++;
            }
            if (select && _selectedReplyIndex >= 0 && _selectedReplyIndex < _availableReplies.Count)
            {
                OnReplySelected?.Invoke(_selectedReplyIndex);
            }
            if (cancel)
            {
                OnDialogueCancelled?.Invoke();
            }
        }

        public void HandleNumberKey(int number)
        {
            if (!_isVisible)
            {
                return;
            }

            // Handle number key selection for dialogue replies (1-9)
            int index = number - 1;
            if (index >= 0 && index < _availableReplies.Count)
            {
                _selectedReplyIndex = index;
                OnReplySelected?.Invoke(index);
            }
        }

        /// <summary>
        /// Event fired when a reply is selected.
        /// </summary>
        public event Action<int> OnReplySelected;

        /// <summary>
        /// Event fired when dialogue is cancelled.
        /// </summary>
        public event Action OnDialogueCancelled;

        public void Dispose()
        {
            if (_panelTexture != null)
            {
                _panelTexture.Dispose();
                _panelTexture = null;
            }
        }
    }
}

