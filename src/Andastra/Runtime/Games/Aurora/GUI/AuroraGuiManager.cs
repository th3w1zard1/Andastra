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
using Andastra.Parsing.Formats.TPC;
using Andastra.Runtime.Games.Common;
using Andastra.Runtime.Games.Aurora.Fonts;
using Andastra.Runtime.Graphics;
using Andastra.Runtime.MonoGame.Converters;
using Andastra.Runtime.MonoGame.Graphics;
using JetBrains.Annotations;

namespace Andastra.Runtime.Games.Aurora.GUI
{
    /// <summary>
    /// Aurora engine (Neverwinter Nights) GUI manager implementation.
    /// </summary>
    /// <remarks>
    /// Aurora GUI Manager (nwmain.exe):
    /// - Based on nwmain.exe GUI system
    /// - GUI format: Uses ResourceType.GUI format (Aurora-specific format support pending Ghidra analysis)
    /// - Font rendering: Uses AuroraBitmapFont for text rendering
    /// 
    /// Ghidra Reverse Engineering Analysis:
    /// - nwmain.exe: GUI loading functions (address verification pending Ghidra analysis)
    /// - nwmain.exe: GUI rendering functions (address verification pending Ghidra analysis)
    /// - nwmain.exe: Font loading and text rendering (address verification pending Ghidra analysis)
    /// 
    /// TODO: PLACEHOLDER - Add Aurora-specific GUI format support when format is determined via Ghidra analysis
    /// Currently uses ResourceType.GUI format as working implementation
    /// </remarks>
    public class AuroraGuiManager : BaseGuiManager
    {
        private readonly IGraphicsDevice _graphicsDevice;
        private readonly Installation _installation;
        private readonly ISpriteBatch _spriteBatch;
        private readonly Dictionary<string, LoadedGui> _loadedGuis;
        private readonly Dictionary<string, ITexture2D> _textureCache;
        private readonly Dictionary<string, AuroraBitmapFont> _fontCache;
        private LoadedGui _currentGui;
        private MouseState _previousMouseState;
        private KeyboardState _previousKeyboardState;
        private string _highlightedButtonTag;

        /// <summary>
        /// Gets the graphics device.
        /// </summary>
        protected override IGraphicsDevice GraphicsDevice => _graphicsDevice;

        /// <summary>
        /// Initializes a new instance of the Aurora GUI manager.
        /// </summary>
        /// <param name="device">Graphics device for rendering.</param>
        /// <param name="installation">Game installation for loading GUI resources.</param>
        public AuroraGuiManager([NotNull] IGraphicsDevice device, [NotNull] Installation installation)
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
            _fontCache = new Dictionary<string, AuroraBitmapFont>(StringComparer.OrdinalIgnoreCase);
            
            // Initialize input states (requires MonoGame GraphicsDevice for Mouse/Keyboard)
            if (device is MonoGameGraphicsDevice mgDevice)
            {
                _previousMouseState = Mouse.GetState();
                _previousKeyboardState = Keyboard.GetState();
            }
        }

        /// <summary>
        /// Loads a GUI from Aurora game files.
        /// </summary>
        public override bool LoadGui(string guiName, int width, int height)
        {
            if (string.IsNullOrEmpty(guiName))
            {
                Console.WriteLine("[AuroraGuiManager] ERROR: GUI name cannot be null or empty");
                return false;
            }

            // Check if already loaded
            if (_loadedGuis.ContainsKey(guiName))
            {
                Console.WriteLine($"[AuroraGuiManager] GUI already loaded: {guiName}");
                _currentGui = _loadedGuis[guiName];
                return true;
            }

            try
            {
                // Lookup GUI resource from installation
                // TODO: PLACEHOLDER - Aurora may use different GUI format, needs Ghidra analysis
                // Currently using ResourceType.GUI as working implementation
                var resourceResult = _installation.Resources.LookupResource(guiName, ResourceType.GUI, null, null);
                if (resourceResult == null || resourceResult.Data == null || resourceResult.Data.Length == 0)
                {
                    Console.WriteLine($"[AuroraGuiManager] ERROR: GUI resource not found: {guiName}");
                    return false;
                }

                // Parse GUI file using GUIReader
                GUIReader guiReader = new GUIReader(resourceResult.Data);
                GUI gui = guiReader.Load();

                if (gui == null || gui.Controls == null || gui.Controls.Count == 0)
                {
                    Console.WriteLine($"[AuroraGuiManager] ERROR: Failed to parse GUI: {guiName}");
                    return false;
                }

                // Create loaded GUI structure
                var loadedGui = new LoadedGui
                {
                    Gui = gui,
                    Name = guiName,
                    Width = width,
                    Height = height,
                    ControlMap = new Dictionary<string, GUIControl>(StringComparer.OrdinalIgnoreCase),
                    ButtonMap = new Dictionary<string, GUIButton>(StringComparer.OrdinalIgnoreCase)
                };

                // Build control and button maps for quick lookup
                BuildControlMaps(gui.Controls, loadedGui);

                // Store loaded GUI
                _loadedGuis[guiName] = loadedGui;
                _currentGui = loadedGui;

                Console.WriteLine($"[AuroraGuiManager] Successfully loaded GUI: {guiName} ({width}x{height}) - {gui.Controls.Count} controls");
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[AuroraGuiManager] ERROR: Exception loading GUI {guiName}: {ex.Message}");
                Console.WriteLine($"[AuroraGuiManager] Stack trace: {ex.StackTrace}");
                return false;
            }
        }

        /// <summary>
        /// Unloads a GUI from memory.
        /// </summary>
        public override void UnloadGui(string guiName)
        {
            if (string.IsNullOrEmpty(guiName))
            {
                return;
            }

            if (_loadedGuis.TryGetValue(guiName, out var loadedGui))
            {
                // Clear current GUI if it's the one being unloaded
                if (_currentGui == loadedGui)
                {
                    _currentGui = null;
                }

                _loadedGuis.Remove(guiName);
                Console.WriteLine($"[AuroraGuiManager] Unloaded GUI: {guiName}");
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

            Console.WriteLine($"[AuroraGuiManager] WARNING: GUI not loaded: {guiName}");
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

            // Only update input if we have MonoGame GraphicsDevice
            if (!(_graphicsDevice is MonoGameGraphicsDevice))
            {
                return;
            }

            MouseState currentMouseState = Mouse.GetState();
            KeyboardState currentKeyboardState = Keyboard.GetState();

            // Update highlighted button based on mouse position
            UpdateHighlightedButton(currentMouseState.X, currentMouseState.Y);

            // Handle mouse clicks on buttons
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
            if (_currentGui == null || _currentGui.Gui == null)
            {
                return;
            }

            _spriteBatch.Begin();

            // Render all controls recursively
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

            // Check cache
            if (_fontCache.TryGetValue(key, out AuroraBitmapFont cached))
            {
                return cached;
            }

            // Load font
            AuroraBitmapFont font = AuroraBitmapFont.Load(fontResRef, _installation, _graphicsDevice);
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

        /// <summary>
        /// Recursively builds control and button maps for quick lookup.
        /// </summary>
        private void BuildControlMaps(List<GUIControl> controls, LoadedGui loadedGui)
        {
            if (controls == null)
            {
                return;
            }

            foreach (var control in controls)
            {
                if (control == null)
                {
                    continue;
                }

                // Add to control map if it has a tag
                if (!string.IsNullOrEmpty(control.Tag))
                {
                    loadedGui.ControlMap[control.Tag] = control;
                }

                // Add to button map if it's a button
                if (control is GUIButton button)
                {
                    if (!string.IsNullOrEmpty(button.Tag))
                    {
                        loadedGui.ButtonMap[button.Tag] = button;
                    }
                }

                // Recursively process children
                if (control.Children != null && control.Children.Count > 0)
                {
                    BuildControlMaps(control.Children, loadedGui);
                }
            }
        }

        /// <summary>
        /// Updates which button is currently highlighted based on mouse position.
        /// </summary>
        private void UpdateHighlightedButton(int mouseX, int mouseY)
        {
            if (_currentGui == null)
            {
                _highlightedButtonTag = null;
                return;
            }

            string newHighlightedTag = null;

            // Check all buttons to find which one the mouse is over
            var buttonsList = _currentGui.ButtonMap.ToList();
            for (int i = buttonsList.Count - 1; i >= 0; i--)
            {
                var kvp = buttonsList[i];
                var button = kvp.Value;
                if (button == null)
                {
                    continue;
                }

                // Check if mouse is within button bounds
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

        /// <summary>
        /// Handles mouse click input and checks for button hits.
        /// </summary>
        private void HandleMouseClick(int mouseX, int mouseY)
        {
            if (_currentGui == null)
            {
                return;
            }

            // Check all buttons for hit
            foreach (var kvp in _currentGui.ButtonMap)
            {
                var button = kvp.Value;
                if (button == null)
                {
                    continue;
                }

                // Check if mouse is within button bounds
                int left = (int)button.Position.X;
                int top = (int)button.Position.Y;
                int right = left + (int)button.Size.X;
                int bottom = top + (int)button.Size.Y;

                if (mouseX >= left && mouseX <= right && mouseY >= top && mouseY <= bottom)
                {
                    // Button clicked - fire event
                    FireButtonClicked(button.Tag, button.Id ?? -1);
                    Console.WriteLine($"[AuroraGuiManager] Button clicked: {button.Tag} (ID: {button.Id})");
                    break;
                }
            }
        }

        /// <summary>
        /// Recursively renders a GUI control and its children.
        /// </summary>
        private void RenderControl(GUIControl control, Vector2 parentOffset)
        {
            if (control == null)
            {
                return;
            }

            Vector2 controlPosition = control.Position + parentOffset;
            Vector2 controlSize = control.Size;

            // Render control based on type
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
                    // Render generic control
                    RenderGenericControl(control, controlPosition, controlSize);
                    break;
            }

            // Render children
            if (control.Children != null)
            {
                foreach (var child in control.Children)
                {
                    RenderControl(child, controlPosition);
                }
            }
        }

        /// <summary>
        /// Renders a panel control.
        /// </summary>
        private void RenderPanel(GUIPanel panel, Vector2 position, Vector2 size)
        {
            // Render panel background using border fill texture if available
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

        /// <summary>
        /// Renders a button control.
        /// </summary>
        private void RenderButton(GUIButton button, Vector2 position, Vector2 size)
        {
            bool isHighlighted = string.Equals(_highlightedButtonTag, button.Tag, StringComparison.OrdinalIgnoreCase);
            bool isSelected = button.IsSelected.HasValue && button.IsSelected.Value != 0;

            // Determine which border state to use
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

            // Render button background
            if (borderToUse != null && !borderToUse.Fill.IsBlank)
            {
                ITexture2D fillTexture = LoadTexture(borderToUse.Fill.ToString());
                if (fillTexture != null)
                {
                    var color = new Andastra.Runtime.Graphics.Color(255, 255, 255, 255);
                    _spriteBatch.Draw(fillTexture, new Andastra.Runtime.Graphics.Rectangle((int)position.X, (int)position.Y, (int)size.X, (int)size.Y), color);
                }
            }

            // Render button text if available
            if (button.GuiText != null && !string.IsNullOrEmpty(button.GuiText.Text))
            {
                string text = button.GuiText.Text;
                var textColor = new Andastra.Runtime.Graphics.Color(
                    button.GuiText.Color.R,
                    button.GuiText.Color.G,
                    button.GuiText.Color.B,
                    button.GuiText.Color.A);

                BaseBitmapFont font = LoadFont(button.GuiText.Font.ToString());
                if (font != null)
                {
                    Vector2 textSize = font.MeasureString(text);
                    Vector2 textPos = CalculateTextPosition(button.GuiText.Alignment, position, size, textSize);
                    RenderBitmapText(font, text, textPos, textColor);
                }
            }
        }

        /// <summary>
        /// Renders a label control.
        /// </summary>
        private void RenderLabel(GUILabel label, Vector2 position, Vector2 size)
        {
            if (label.GuiText != null && !string.IsNullOrEmpty(label.GuiText.Text))
            {
                string text = label.GuiText.Text;
                var textColor = new Andastra.Runtime.Graphics.Color(
                    label.GuiText.Color.R,
                    label.GuiText.Color.G,
                    label.GuiText.Color.B,
                    label.GuiText.Color.A);

                BaseBitmapFont font = LoadFont(label.GuiText.Font.ToString());
                if (font != null)
                {
                    Vector2 textSize = font.MeasureString(text);
                    Vector2 textPos = CalculateTextPosition(label.GuiText.Alignment, position, size, textSize);
                    RenderBitmapText(font, text, textPos, textColor);
                }
            }
        }

        /// <summary>
        /// Renders a generic control.
        /// </summary>
        private void RenderGenericControl(GUIControl control, Vector2 position, Vector2 size)
        {
            // Render border/background if available
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

        /// <summary>
        /// Renders bitmap text character by character.
        /// </summary>
        private void RenderBitmapText(BaseBitmapFont font, string text, Vector2 position, Andastra.Runtime.Graphics.Color color)
        {
            if (font == null || string.IsNullOrEmpty(text))
            {
                return;
            }

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
                    
                    // Draw character glyph
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

        /// <summary>
        /// Loads a texture from a ResRef, with caching.
        /// Based on nwmain.exe texture loading system (Aurora Engine texture loading)
        /// Aurora Engine uses TGA format primarily, with TPC as fallback for compressed textures.
        /// </summary>
        /// <param name="textureName">The texture resource reference (without extension).</param>
        /// <returns>The loaded texture, or null if loading failed.</returns>
        /// <remarks>
        /// Texture Loading (nwmain.exe):
        /// - Aurora Engine texture loading follows resource precedence: OVERRIDE > MODULE > HAK > BASE_GAME
        /// - Texture formats: TGA (primary) and TPC (compressed/alternative)
        /// - Texture lookup: Searches for TGA first, then TPC as fallback
        /// - Texture conversion: Converts TPC/TGA format to MonoGame Texture2D using TpcToMonoGameTextureConverter
        /// - Caching: Textures are cached by lowercase ResRef to avoid reloading
        /// - Error handling: Returns null on failure (missing resource, parsing error, conversion error)
        /// 
        /// Implementation pattern matches AuroraBitmapFont.Load for consistency.
        /// </remarks>
        private ITexture2D LoadTexture(string textureName)
        {
            if (string.IsNullOrEmpty(textureName) || textureName == "****")
            {
                return null;
            }

            string key = textureName.ToLowerInvariant();

            // Check cache
            if (_textureCache.TryGetValue(key, out ITexture2D cached))
            {
                return cached;
            }

            try
            {
                // Aurora Engine uses TGA format primarily (unlike Odyssey which uses TPC)
                // Try TGA first, then TPC as fallback
                TPC texture = null;
                ResourceResult textureResult = _installation.Resources.LookupResource(textureName, ResourceType.TGA, null, null);
                if (textureResult != null && textureResult.Data != null && textureResult.Data.Length > 0)
                {
                    texture = TPCAuto.ReadTpc(textureResult.Data);
                }

                // If TGA not found, try TPC format
                if (texture == null)
                {
                    textureResult = _installation.Resources.LookupResource(textureName, ResourceType.TPC, null, null);
                    if (textureResult != null && textureResult.Data != null && textureResult.Data.Length > 0)
                    {
                        texture = TPCAuto.ReadTpc(textureResult.Data);
                    }
                }

                if (texture == null)
                {
                    Console.WriteLine($"[AuroraGuiManager] WARNING: Texture not found: {textureName} (searched TGA and TPC)");
                    return null;
                }

                // Get MonoGame GraphicsDevice for texture conversion
                GraphicsDevice mgDevice = _graphicsDevice as GraphicsDevice;
                if (mgDevice == null && _graphicsDevice is MonoGameGraphicsDevice mgGfxDevice)
                {
                    mgDevice = mgGfxDevice.Device;
                }
                if (mgDevice == null)
                {
                    Console.WriteLine($"[AuroraGuiManager] ERROR: Graphics device must be MonoGame GraphicsDevice for texture loading");
                    return null;
                }

                // Convert TPC/TGA to MonoGame Texture2D
                // GUI textures are always 2D (not cube maps), so set generateMipmaps to false for better performance
                Texture convertedTexture = TpcToMonoGameTextureConverter.Convert(texture, mgDevice, false);
                if (convertedTexture is TextureCube)
                {
                    Console.WriteLine($"[AuroraGuiManager] ERROR: GUI texture cannot be a cube map: {textureName}");
                    return null;
                }
                Texture2D texture2D = (Texture2D)convertedTexture;
                if (texture2D == null)
                {
                    Console.WriteLine($"[AuroraGuiManager] ERROR: Failed to convert texture: {textureName}");
                    return null;
                }

                // Wrap in MonoGameTexture2D and cache
                MonoGameTexture2D wrappedTexture = new MonoGameTexture2D(texture2D);
                _textureCache[key] = wrappedTexture;

                Console.WriteLine($"[AuroraGuiManager] Successfully loaded texture: {textureName} ({texture2D.Width}x{texture2D.Height})");
                return wrappedTexture;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[AuroraGuiManager] ERROR: Exception loading texture {textureName}: {ex.Message}");
                Console.WriteLine($"[AuroraGuiManager] Stack trace: {ex.StackTrace}");
                return null;
            }
        }

        /// <summary>
        /// Converts GUISelected to GUIBorder for rendering.
        /// </summary>
        private GUIBorder ConvertSelectedToBorder(GUISelected selected)
        {
            return new GUIBorder
            {
                Corner = selected.Corner,
                Edge = selected.Edge,
                Fill = selected.Fill,
                FillStyle = selected.FillStyle,
                Dimension = selected.Dimension,
                InnerOffset = selected.InnerOffset,
                InnerOffsetY = selected.InnerOffsetY,
                Color = selected.Color,
                Pulsing = selected.Pulsing
            };
        }

        /// <summary>
        /// Converts GUIHilightSelected to GUIBorder for rendering.
        /// </summary>
        private GUIBorder ConvertHilightSelectedToBorder(GUIHilightSelected hilightSelected)
        {
            return new GUIBorder
            {
                Corner = hilightSelected.Corner,
                Edge = hilightSelected.Edge,
                Fill = hilightSelected.Fill,
                FillStyle = hilightSelected.FillStyle,
                Dimension = hilightSelected.Dimension,
                InnerOffset = hilightSelected.InnerOffset,
                InnerOffsetY = hilightSelected.InnerOffsetY,
                Color = hilightSelected.Color,
                Pulsing = hilightSelected.Pulsing
            };
        }

        /// <summary>
        /// Internal structure for loaded GUI data.
        /// </summary>
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
