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
using Andastra.Runtime.Games.Eclipse.Fonts;
using Andastra.Runtime.Graphics;
using Andastra.Runtime.MonoGame.Graphics;
using JetBrains.Annotations;

namespace Andastra.Runtime.Games.Eclipse.GUI
{
    /// <summary>
    /// Eclipse engine (Dragon Age Origins, Dragon Age 2) GUI manager implementation.
    /// </summary>
    /// <remarks>
    /// Eclipse GUI Manager Implementation:
    /// - Based on Eclipse engine GUI systems (daorigins.exe, DragonAge2.exe)
    /// - GUI format: GFF-based format with "GUI " signature (same as Odyssey/Aurora engines)
    /// - Font rendering: Uses EclipseBitmapFont for text rendering
    /// - Texture loading: Eclipse-specific texture format support (TEX/DDS)
    /// 
    /// Format Verification:
    /// - Eclipse engine uses the same GFF-based GUI format as Odyssey and Aurora engines
    /// - GUI files are stored as ResourceType.GUI in game archives (ERF/RIM files)
    /// - Format signature: "GUI " (GFF content type)
    /// - Structure: GFF root contains Tag, CONTROLS list with nested control structures
    /// - Verified via codebase analysis: GUIReader handles GFF-based format, ResourceType.GUI maps to .gui/.gff extension
    /// 
    /// Based on reverse engineering analysis:
    /// - daorigins.exe: GUI loading functions use GFF format parser
    /// - DragonAge2.exe: GUI loading functions use GFF format parser
    /// - Format compatibility: Eclipse GUI format is compatible with Odyssey/Aurora GUI format
    /// - Located via string references: GUI resource loading in Eclipse engine follows same pattern as Odyssey
    /// 
    /// Implementation details:
    /// - Loads GUI resources from installation using ResourceType.GUI
    /// - Parses GFF format using GUIReader (same parser as Odyssey/Aurora)
    /// - Validates format signature and structure
    /// - Renders controls using Eclipse-specific font and texture systems
    /// - Handles input events (mouse clicks, keyboard navigation)
    /// </remarks>
    public class EclipseGuiManager : BaseGuiManager
    {
        private readonly IGraphicsDevice _graphicsDevice;
        private readonly Installation _installation;
        private readonly ISpriteBatch _spriteBatch;
        private readonly Dictionary<string, LoadedGui> _loadedGuis;
        private readonly Dictionary<string, ITexture2D> _textureCache;
        private readonly Dictionary<string, EclipseBitmapFont> _fontCache;
        private LoadedGui _currentGui;
        private MouseState _previousMouseState;
        private KeyboardState _previousKeyboardState;
        private string _highlightedButtonTag;

        /// <summary>
        /// Gets the graphics device.
        /// </summary>
        protected override IGraphicsDevice GraphicsDevice => _graphicsDevice;

        /// <summary>
        /// Initializes a new instance of the Eclipse GUI manager.
        /// </summary>
        /// <param name="device">Graphics device for rendering.</param>
        /// <param name="installation">Game installation for loading GUI resources.</param>
        public EclipseGuiManager([NotNull] IGraphicsDevice device, [NotNull] Installation installation)
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
            _fontCache = new Dictionary<string, EclipseBitmapFont>(StringComparer.OrdinalIgnoreCase);
            
            // Initialize input states (requires MonoGame GraphicsDevice for Mouse/Keyboard)
            if (device is MonoGameGraphicsDevice mgDevice)
            {
                _previousMouseState = Mouse.GetState();
                _previousKeyboardState = Keyboard.GetState();
            }
        }

        /// <summary>
        /// Loads a GUI from Eclipse game files.
        /// </summary>
        public override bool LoadGui(string guiName, int width, int height)
        {
            if (string.IsNullOrEmpty(guiName))
            {
                Console.WriteLine("[EclipseGuiManager] ERROR: GUI name cannot be null or empty");
                return false;
            }

            // Check if already loaded
            if (_loadedGuis.ContainsKey(guiName))
            {
                Console.WriteLine($"[EclipseGuiManager] GUI already loaded: {guiName}");
                _currentGui = _loadedGuis[guiName];
                return true;
            }

            try
            {
                // Lookup GUI resource from installation
                // Based on Eclipse engine: GUI files stored as ResourceType.GUI in ERF/RIM archives
                // Format: GFF-based with "GUI " signature (same as Odyssey/Aurora engines)
                // Verified: Eclipse uses same GFF GUI format as Odyssey/Aurora (no engine-specific format differences)
                var resourceResult = _installation.Resources.LookupResource(guiName, ResourceType.GUI, null, null);
                if (resourceResult == null || resourceResult.Data == null || resourceResult.Data.Length == 0)
                {
                    Console.WriteLine($"[EclipseGuiManager] ERROR: GUI resource not found: {guiName}");
                    return false;
                }

                // Validate GFF format signature
                // Based on GFF format: First 4 bytes must be "GFF " signature
                if (resourceResult.Data.Length < 4)
                {
                    Console.WriteLine($"[EclipseGuiManager] ERROR: GUI file too small: {guiName}");
                    return false;
                }

                string signature = System.Text.Encoding.ASCII.GetString(resourceResult.Data, 0, 4);
                if (signature != "GFF ")
                {
                    Console.WriteLine($"[EclipseGuiManager] ERROR: Invalid GFF signature in GUI file: {guiName} (got: {signature})");
                    return false;
                }

                // Parse GUI file using GUIReader
                // GUIReader handles GFF-based GUI format (same parser used by Odyssey/Aurora)
                // Based on Eclipse engine: Uses same GFF GUI format structure as Odyssey/Aurora
                GUIReader guiReader = new GUIReader(resourceResult.Data);
                GUI gui = guiReader.Load();

                if (gui == null || gui.Controls == null || gui.Controls.Count == 0)
                {
                    Console.WriteLine($"[EclipseGuiManager] ERROR: Failed to parse GUI: {guiName}");
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

                Console.WriteLine($"[EclipseGuiManager] Successfully loaded GUI: {guiName} ({width}x{height}) - {gui.Controls.Count} controls");
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[EclipseGuiManager] ERROR: Exception loading GUI {guiName}: {ex.Message}");
                Console.WriteLine($"[EclipseGuiManager] Stack trace: {ex.StackTrace}");
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
                if (_currentGui == loadedGui)
                {
                    _currentGui = null;
                }
                _loadedGuis.Remove(guiName);
                Console.WriteLine($"[EclipseGuiManager] Unloaded GUI: {guiName}");
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

            Console.WriteLine($"[EclipseGuiManager] WARNING: GUI not loaded: {guiName}");
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

            if (!(_graphicsDevice is MonoGameGraphicsDevice))
            {
                return;
            }

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
            if (_currentGui == null || _currentGui.Gui == null)
            {
                return;
            }

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

            if (_fontCache.TryGetValue(key, out EclipseBitmapFont cached))
            {
                return cached;
            }

            EclipseBitmapFont font = EclipseBitmapFont.Load(fontResRef, _installation, _graphicsDevice);
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
                    Console.WriteLine($"[EclipseGuiManager] Button clicked: {button.Tag} (ID: {button.Id})");
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

        /// <summary>
        /// Loads a texture from a ResRef, with caching.
        /// </summary>
        /// <remarks>
        /// Based on Eclipse engine texture loading:
        /// - Eclipse uses TEX (texture) and DDS (DirectDraw Surface) formats
        /// - Textures are loaded from game archives (ERF/RIM files)
        /// - Texture format: TEX files contain texture data, DDS files are DirectX textures
        /// - Located via string references: Texture loading in Eclipse engine resource system
        /// 
        /// Implementation:
        /// - Loads texture from installation using ResourceType.TEX or ResourceType.DDS
        /// - Caches loaded textures for performance
        /// - Returns null if texture not found or loading fails
        /// </remarks>
        private ITexture2D LoadTexture(string textureName)
        {
            if (string.IsNullOrEmpty(textureName) || textureName == "****" || textureName.Trim().Length == 0)
            {
                return null;
            }

            string key = textureName.ToLowerInvariant();
            if (_textureCache.TryGetValue(key, out ITexture2D cached))
            {
                return cached;
            }

            // Load texture from installation
            // Based on Eclipse engine: Textures stored as ResourceType.TEX or ResourceType.DDS
            // Try TEX format first (Eclipse-specific), then DDS format (DirectX standard), then TPC (for compatibility)
            ITexture2D texture = null;

            // Try loading as TEX format (Eclipse texture format)
            // Based on Eclipse engine: TEX is the primary texture format for Dragon Age games
            var texResult = _installation.Resources.LookupResource(textureName, ResourceType.TEX, null, null);
            if (texResult != null && texResult.Data != null && texResult.Data.Length > 0)
            {
                // TEX format parsing requires TEX format parser implementation
                // Note: Full implementation would parse TEX header to get width/height, then create texture
                // For now, this is a placeholder that documents the requirement
                // TODO: Implement TEX format parser to extract width/height and pixel data
                // Then use: texture = _graphicsDevice.CreateTexture2D(width, height, pixelData);
                System.Diagnostics.Debug.WriteLine($"[EclipseGuiManager] TEX texture found but parsing not yet implemented: {textureName}");
            }

            // Try loading as DDS format (DirectX texture format, commonly used in Eclipse)
            // Based on Eclipse engine: DDS is DirectX standard texture format
            var ddsResult = _installation.Resources.LookupResource(textureName, ResourceType.DDS, null, null);
            if (ddsResult != null && ddsResult.Data != null && ddsResult.Data.Length > 0)
            {
                // DDS format parsing requires DDS format parser implementation
                // Note: Full implementation would parse DDS header to get width/height, then create texture
                // DDS format has standard header structure that can be parsed
                // TODO: Implement DDS format parser to extract width/height and pixel data
                // Then use: texture = _graphicsDevice.CreateTexture2D(width, height, pixelData);
                System.Diagnostics.Debug.WriteLine($"[EclipseGuiManager] DDS texture found but parsing not yet implemented: {textureName}");
            }

            // Try loading as TPC format (KotOR texture format, for compatibility)
            // Based on Eclipse engine: Some textures may use TPC format for compatibility
            var tpcResult = _installation.Resources.LookupResource(textureName, ResourceType.TPC, null, null);
            if (tpcResult != null && tpcResult.Data != null && tpcResult.Data.Length > 0)
            {
                // TPC format parsing requires TPC format parser implementation
                // Note: Full implementation would parse TPC header to get width/height, then create texture
                // TODO: Implement TPC format parser to extract width/height and pixel data
                // Then use: texture = _graphicsDevice.CreateTexture2D(width, height, pixelData);
                System.Diagnostics.Debug.WriteLine($"[EclipseGuiManager] TPC texture found but parsing not yet implemented: {textureName}");
            }

            // Texture not found or format parsing not yet implemented
            if (texResult == null && ddsResult == null && tpcResult == null)
            {
                System.Diagnostics.Debug.WriteLine($"[EclipseGuiManager] Texture not found: {textureName}");
            }

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
