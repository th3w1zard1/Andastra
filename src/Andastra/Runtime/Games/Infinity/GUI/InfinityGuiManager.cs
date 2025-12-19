using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Andastra.Parsing;
using Andastra.Parsing.Installation;
using Andastra.Parsing.Resource;
using Andastra.Parsing.Resource.Generics.GUI;
using Andastra.Parsing.Common;
using Andastra.Runtime.Games.Common;
using Andastra.Runtime.Games.Infinity.Fonts;
using Andastra.Runtime.Graphics;
using Andastra.Runtime.MonoGame.Graphics;
using JetBrains.Annotations;

namespace Andastra.Runtime.Games.Infinity.GUI
{
    /// <summary>
    /// Infinity engine (Mass Effect series) GUI manager implementation.
    /// </summary>
    /// <remarks>
    /// Infinity GUI Manager (MassEffect.exe, MassEffect2.exe):
    /// - Based on Infinity engine GUI systems (Unreal Engine-based)
    /// - GUI format: Uses ResourceType.GUI format (Unreal Engine GUI format support pending Ghidra analysis)
    /// - Font rendering: Uses InfinityBitmapFont for text rendering
    /// 
    /// Ghidra Reverse Engineering Analysis:
    /// - MassEffect.exe: GUI loading functions (address verification pending Ghidra analysis)
    /// - MassEffect2.exe: GUI loading functions (address verification pending Ghidra analysis)
    /// 
    /// TODO: PLACEHOLDER - Add Unreal Engine GUI format support when format is determined via Ghidra analysis
    /// Currently uses ResourceType.GUI format as working implementation
    /// </remarks>
    public class InfinityGuiManager : BaseGuiManager
    {
        private readonly IGraphicsDevice _graphicsDevice;
        private readonly Installation _installation;
        private readonly ISpriteBatch _spriteBatch;
        private readonly Dictionary<string, LoadedGui> _loadedGuis;
        private readonly Dictionary<string, ITexture2D> _textureCache;
        private readonly Dictionary<string, InfinityBitmapFont> _fontCache;
        private LoadedGui _currentGui;
        private MouseState _previousMouseState;
        private KeyboardState _previousKeyboardState;
        private string _highlightedButtonTag;

        /// <summary>
        /// Gets the graphics device.
        /// </summary>
        protected override IGraphicsDevice GraphicsDevice => _graphicsDevice;

        /// <summary>
        /// Initializes a new instance of the Infinity GUI manager.
        /// </summary>
        /// <param name="device">Graphics device for rendering.</param>
        /// <param name="installation">Game installation for loading GUI resources.</param>
        public InfinityGuiManager([NotNull] IGraphicsDevice device, [NotNull] Installation installation)
        {
            if (device == null)
            {
                throw new ArgumentNullException("device");
            }
            if (installation == null)
            {
                throw new ArgumentNullException("installation");
            }

            _graphicsDevice = device;
            _installation = installation;
            _spriteBatch = device.CreateSpriteBatch();
            _loadedGuis = new Dictionary<string, LoadedGui>(StringComparer.OrdinalIgnoreCase);
            _textureCache = new Dictionary<string, ITexture2D>(StringComparer.OrdinalIgnoreCase);
            _fontCache = new Dictionary<string, InfinityBitmapFont>(StringComparer.OrdinalIgnoreCase);
            
            if (device is MonoGameGraphicsDevice mgDevice)
            {
                _previousMouseState = Mouse.GetState();
                _previousKeyboardState = Keyboard.GetState();
            }
        }

        /// <summary>
        /// Loads a GUI from Infinity game files.
        /// </summary>
        public override bool LoadGui(string guiName, int width, int height)
        {
            if (string.IsNullOrEmpty(guiName))
            {
                Console.WriteLine("[InfinityGuiManager] ERROR: GUI name cannot be null or empty");
                return false;
            }

            if (_loadedGuis.ContainsKey(guiName))
            {
                Console.WriteLine($"[InfinityGuiManager] GUI already loaded: {guiName}");
                _currentGui = _loadedGuis[guiName];
                return true;
            }

            try
            {
                // TODO: PLACEHOLDER - Infinity may use Unreal Engine GUI format, needs Ghidra analysis
                var resourceResult = _installation.Resources.LookupResource(guiName, ResourceType.GUI, null, null);
                if (resourceResult == null || resourceResult.Data == null || resourceResult.Data.Length == 0)
                {
                    Console.WriteLine($"[InfinityGuiManager] ERROR: GUI resource not found: {guiName}");
                    return false;
                }

                GUIReader guiReader = new GUIReader(resourceResult.Data);
                GUI gui = guiReader.Load();

                if (gui == null || gui.Controls == null || gui.Controls.Count == 0)
                {
                    Console.WriteLine($"[InfinityGuiManager] ERROR: Failed to parse GUI: {guiName}");
                    return false;
                }

                var loadedGui = new LoadedGui
                {
                    Gui = gui,
                    Name = guiName,
                    Width = width,
                    Height = height,
                    ControlMap = new Dictionary<string, GUIControl>(StringComparer.OrdinalIgnoreCase),
                    ButtonMap = new Dictionary<string, GUIButton>(StringComparer.OrdinalIgnoreCase)
                };

                BuildControlMaps(gui.Controls, loadedGui);
                _loadedGuis[guiName] = loadedGui;
                _currentGui = loadedGui;

                Console.WriteLine($"[InfinityGuiManager] Successfully loaded GUI: {guiName} ({width}x{height}) - {gui.Controls.Count} controls");
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[InfinityGuiManager] ERROR: Exception loading GUI {guiName}: {ex.Message}");
                Console.WriteLine($"[InfinityGuiManager] Stack trace: {ex.StackTrace}");
                return false;
            }
        }

        /// <summary>
        /// Unloads a GUI from memory.
        /// </summary>
        public override void UnloadGui(string guiName)
        {
            if (string.IsNullOrEmpty(guiName)) return;

            if (_loadedGuis.TryGetValue(guiName, out var loadedGui))
            {
                if (_currentGui == loadedGui) _currentGui = null;
                _loadedGuis.Remove(guiName);
                Console.WriteLine($"[InfinityGuiManager] Unloaded GUI: {guiName}");
            }
        }

        /// <summary>
        /// Sets the current active GUI.
        /// </summary>
        public override bool SetCurrentGui(string guiName)
        {
            if (string.IsNullOrEmpty(guiName))
            {
                _currentGui = null;
                return false;
            }

            if (_loadedGuis.TryGetValue(guiName, out var loadedGui))
            {
                _currentGui = loadedGui;
                return true;
            }

            Console.WriteLine($"[InfinityGuiManager] WARNING: GUI not loaded: {guiName}");
            return false;
        }

        /// <summary>
        /// Updates GUI input handling.
        /// </summary>
        public override void Update(object gameTime)
        {
            if (_currentGui == null)
            {
                _highlightedButtonTag = null;
                return;
            }

            if (!(_graphicsDevice is MonoGameGraphicsDevice)) return;

            MouseState currentMouseState = Mouse.GetState();
            KeyboardState currentKeyboardState = Keyboard.GetState();

            UpdateHighlightedButton(currentMouseState.X, currentMouseState.Y);

            if (_previousMouseState.LeftButton == ButtonState.Released && currentMouseState.LeftButton == ButtonState.Pressed)
            {
                HandleMouseClick(currentMouseState.X, currentMouseState.Y);
            }

            _previousMouseState = currentMouseState;
            _previousKeyboardState = currentKeyboardState;
        }

        /// <summary>
        /// Renders the current GUI.
        /// </summary>
        public override void Draw(object gameTime)
        {
            if (_currentGui == null || _currentGui.Gui == null) return;

            _spriteBatch.Begin();

            foreach (var control in _currentGui.Gui.Controls)
            {
                RenderControl(control, Vector2.Zero);
            }

            _spriteBatch.End();
        }

        /// <summary>
        /// Loads a bitmap font from a ResRef, with caching.
        /// </summary>
        protected override BaseBitmapFont LoadFont(string fontResRef)
        {
            if (string.IsNullOrEmpty(fontResRef) || fontResRef == "****" || fontResRef.Trim().Length == 0)
            {
                return null;
            }

            string key = fontResRef.ToLowerInvariant();

            if (_fontCache.TryGetValue(key, out InfinityBitmapFont cached))
            {
                return cached;
            }

            InfinityBitmapFont font = InfinityBitmapFont.Load(fontResRef, _installation, _graphicsDevice);
            if (font != null)
            {
                _fontCache[key] = font;
            }

            return font;
        }

        /// <summary>
        /// Disposes resources.
        /// </summary>
        public override void Dispose()
        {
            _fontCache.Clear();
            _textureCache.Clear();
            _loadedGuis.Clear();
            _spriteBatch?.Dispose();
        }

        #region Private Helper Methods

        private void BuildControlMaps(List<GUIControl> controls, LoadedGui loadedGui)
        {
            if (controls == null) return;

            foreach (var control in controls)
            {
                if (control == null) continue;

                if (!string.IsNullOrEmpty(control.Tag))
                {
                    loadedGui.ControlMap[control.Tag] = control;
                }

                if (control is GUIButton button && !string.IsNullOrEmpty(button.Tag))
                {
                    loadedGui.ButtonMap[button.Tag] = button;
                }

                if (control.Children != null && control.Children.Count > 0)
                {
                    BuildControlMaps(control.Children, loadedGui);
                }
            }
        }

        private void UpdateHighlightedButton(int mouseX, int mouseY)
        {
            if (_currentGui == null)
            {
                _highlightedButtonTag = null;
                return;
            }

            string newHighlightedTag = null;
            var buttonsList = _currentGui.ButtonMap.ToList();
            for (int i = buttonsList.Count - 1; i >= 0; i--)
            {
                var kvp = buttonsList[i];
                var button = kvp.Value;
                if (button == null) continue;

                int left = (int)button.Position.X;
                int top = (int)button.Position.Y;
                int right = left + (int)button.Size.X;
                int bottom = top + (int)button.Size.Y;

                if (mouseX >= left && mouseX <= right && mouseY >= top && mouseY <= bottom)
                {
                    newHighlightedTag = button.Tag;
                    break;
                }
            }

            _highlightedButtonTag = newHighlightedTag;
        }

        private void HandleMouseClick(int mouseX, int mouseY)
        {
            if (_currentGui == null) return;

            foreach (var kvp in _currentGui.ButtonMap)
            {
                var button = kvp.Value;
                if (button == null) continue;

                int left = (int)button.Position.X;
                int top = (int)button.Position.Y;
                int right = left + (int)button.Size.X;
                int bottom = top + (int)button.Size.Y;

                if (mouseX >= left && mouseX <= right && mouseY >= top && mouseY <= bottom)
                {
                    FireButtonClicked(button.Tag, button.Id ?? -1);
                    Console.WriteLine($"[InfinityGuiManager] Button clicked: {button.Tag} (ID: {button.Id})");
                    break;
                }
            }
        }

        private void RenderControl(GUIControl control, Vector2 parentOffset)
        {
            if (control == null) return;

            Vector2 controlPosition = control.Position + parentOffset;
            Vector2 controlSize = control.Size;

            switch (control.GuiType)
            {
                case GUIControlType.Panel:
                    RenderPanel((GUIPanel)control, controlPosition, controlSize);
                    break;
                case GUIControlType.Button:
                    RenderButton((GUIButton)control, controlPosition, controlSize);
                    break;
                case GUIControlType.Label:
                    RenderLabel((GUILabel)control, controlPosition, controlSize);
                    break;
                default:
                    RenderGenericControl(control, controlPosition, controlSize);
                    break;
            }

            if (control.Children != null)
            {
                foreach (var child in control.Children)
                {
                    RenderControl(child, controlPosition);
                }
            }
        }

        private void RenderPanel(GUIPanel panel, Vector2 position, Vector2 size)
        {
            if (panel.Border != null && !panel.Border.Fill.IsBlank)
            {
                ITexture2D fillTexture = LoadTexture(panel.Border.Fill.ToString());
                if (fillTexture != null)
                {
                    float alpha = panel.Alpha;
                    var color = new Andastra.Runtime.Graphics.Color(255, 255, 255, (byte)(255 * alpha));
                    _spriteBatch.Draw(fillTexture, new Andastra.Runtime.Graphics.Rectangle((int)position.X, (int)position.Y, (int)size.X, (int)size.Y), color);
                }
            }
        }

        private void RenderButton(GUIButton button, Vector2 position, Vector2 size)
        {
            bool isHighlighted = string.Equals(_highlightedButtonTag, button.Tag, StringComparison.OrdinalIgnoreCase);
            bool isSelected = button.IsSelected.HasValue && button.IsSelected.Value != 0;

            GUIBorder borderToUse = button.Border;
            if (isHighlighted && isSelected && button.HilightSelected != null)
            {
                borderToUse = ConvertHilightSelectedToBorder(button.HilightSelected);
            }
            else if (isSelected && button.Selected != null)
            {
                borderToUse = ConvertSelectedToBorder(button.Selected);
            }
            else if (isHighlighted && button.Hilight != null)
            {
                borderToUse = button.Hilight;
            }

            if (borderToUse != null && !borderToUse.Fill.IsBlank)
            {
                ITexture2D fillTexture = LoadTexture(borderToUse.Fill.ToString());
                if (fillTexture != null)
                {
                    var color = new Andastra.Runtime.Graphics.Color(255, 255, 255, 255);
                    _spriteBatch.Draw(fillTexture, new Andastra.Runtime.Graphics.Rectangle((int)position.X, (int)position.Y, (int)size.X, (int)size.Y), color);
                }
            }

            if (button.GuiText != null && !string.IsNullOrEmpty(button.GuiText.Text))
            {
                string text = button.GuiText.Text;
                var textColor = new Andastra.Runtime.Graphics.Color(
                    button.GuiText.Color.R, button.GuiText.Color.G, button.GuiText.Color.B, button.GuiText.Color.A);

                BaseBitmapFont font = LoadFont(button.GuiText.Font.ToString());
                if (font != null)
                {
                    Vector2 textSize = font.MeasureString(text);
                    Vector2 textPos = CalculateTextPosition(button.GuiText.Alignment, position, size, textSize);
                    RenderBitmapText(font, text, textPos, textColor);
                }
            }
        }

        private void RenderLabel(GUILabel label, Vector2 position, Vector2 size)
        {
            if (label.GuiText != null && !string.IsNullOrEmpty(label.GuiText.Text))
            {
                string text = label.GuiText.Text;
                var textColor = new Andastra.Runtime.Graphics.Color(
                    label.GuiText.Color.R, label.GuiText.Color.G, label.GuiText.Color.B, label.GuiText.Color.A);

                BaseBitmapFont font = LoadFont(label.GuiText.Font.ToString());
                if (font != null)
                {
                    Vector2 textSize = font.MeasureString(text);
                    Vector2 textPos = CalculateTextPosition(label.GuiText.Alignment, position, size, textSize);
                    RenderBitmapText(font, text, textPos, textColor);
                }
            }
        }

        private void RenderGenericControl(GUIControl control, Vector2 position, Vector2 size)
        {
            if (control.Border != null && !control.Border.Fill.IsBlank)
            {
                ITexture2D fillTexture = LoadTexture(control.Border.Fill.ToString());
                if (fillTexture != null)
                {
                    var color = new Andastra.Runtime.Graphics.Color(255, 255, 255, 255);
                    _spriteBatch.Draw(fillTexture, new Andastra.Runtime.Graphics.Rectangle((int)position.X, (int)position.Y, (int)size.X, (int)size.Y), color);
                }
            }
        }

        private void RenderBitmapText(BaseBitmapFont font, string text, Vector2 position, Andastra.Runtime.Graphics.Color color)
        {
            if (font == null || string.IsNullOrEmpty(text)) return;

            float x = position.X;
            float y = position.Y;

            foreach (char c in text)
            {
                if (c == '\n')
                {
                    x = position.X;
                    y += font.FontHeight + font.SpacingB;
                    continue;
                }

                var glyph = font.GetCharacter((int)c);
                if (glyph.HasValue)
                {
                    var g = glyph.Value;
                    ITexture2D texture = font.Texture;
                    var sourceRect = new Andastra.Runtime.Graphics.Rectangle(g.Value.SourceX, g.Value.SourceY, g.Value.SourceWidth, g.Value.SourceHeight);
                    var destRect = new Andastra.Runtime.Graphics.Rectangle((int)x, (int)y, (int)g.Value.Width, (int)g.Value.Height);
                    _spriteBatch.Draw(texture, destRect, sourceRect, color);
                    x += g.Value.Width + font.SpacingR;
                }
                else
                {
                    x += font.FontWidth + font.SpacingR;
                }
            }
        }

        private ITexture2D LoadTexture(string textureName)
        {
            if (string.IsNullOrEmpty(textureName) || textureName == "****") return null;

            string key = textureName.ToLowerInvariant();
            if (_textureCache.TryGetValue(key, out ITexture2D cached)) return cached;

            // TODO: Implement texture loading for Infinity
            return null;
        }

        private GUIBorder ConvertSelectedToBorder(GUISelected selected)
        {
            return new GUIBorder
            {
                Corner = selected.Corner, Edge = selected.Edge, Fill = selected.Fill,
                FillStyle = selected.FillStyle, Dimension = selected.Dimension,
                InnerOffset = selected.InnerOffset, InnerOffsetY = selected.InnerOffsetY,
                Color = selected.Color, Pulsing = selected.Pulsing
            };
        }

        private GUIBorder ConvertHilightSelectedToBorder(GUIHilightSelected hilightSelected)
        {
            return new GUIBorder
            {
                Corner = hilightSelected.Corner, Edge = hilightSelected.Edge, Fill = hilightSelected.Fill,
                FillStyle = hilightSelected.FillStyle, Dimension = hilightSelected.Dimension,
                InnerOffset = hilightSelected.InnerOffset, InnerOffsetY = hilightSelected.InnerOffsetY,
                Color = hilightSelected.Color, Pulsing = hilightSelected.Pulsing
            };
        }

        private class LoadedGui
        {
            public GUI Gui { get; set; }
            public string Name { get; set; }
            public int Width { get; set; }
            public int Height { get; set; }
            public Dictionary<string, GUIControl> ControlMap { get; set; }
            public Dictionary<string, GUIButton> ButtonMap { get; set; }
        }

        #endregion
    }
}
