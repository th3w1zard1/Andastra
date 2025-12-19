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
using JetBrains.Annotations;

namespace Andastra.Runtime.MonoGame.GUI
{
    /// <summary>
    /// Manages KOTOR GUI rendering using MonoGame SpriteBatch.
    /// </summary>
    /// <remarks>
    /// KOTOR GUI Manager (MonoGame Implementation):
    /// - Based on swkotor2.exe GUI system (modern MonoGame adaptation)
    /// - Located via string references: GUI system references throughout executable
    /// - GUI files: "gui_mp_arwalk00" through "gui_mp_arwalk15" @ 0x007b59bc-0x007b58dc (GUI animation frames)
    /// - "gui_mp_arrun00" through "gui_mp_arrun15" @ 0x007b5aac-0x007b59dc (GUI run animation frames)
    /// - GUI panels: "gui_p" @ 0x007d0e00 (GUI panel prefix), "gui_mainmenu_p" @ 0x007d0e10 (main menu panel)
    /// - "gui_pause_p" @ 0x007d0e20 (pause menu panel), "gui_inventory_p" @ 0x007d0e30 (inventory panel)
    /// - "gui_dialogue_p" @ 0x007d0e40 (dialogue panel), "gui_character_p" @ 0x007d0e50 (character panel)
    /// - GUI buttons: "BTN_" prefix for buttons (BTN_SAVELOAD @ 0x007ced68, BTN_SAVEGAME @ 0x007d0dbc, etc.)
    /// - GUI labels: "LBL_" prefix for labels (LBL_STATSBORDER @ 0x007cfa94, LBL_STATSBACK @ 0x007d278c, etc.)
    /// - GUI controls: "CB_" prefix for checkboxes (CB_AUTOSAVE @ 0x007d2918), "EDT_" prefix for edit boxes
    /// - Original implementation: KOTOR uses GUI files (GUI format) for menu layouts
    /// - GUI format: Binary format containing panel definitions, button layouts, textures, fonts
    /// - GUI rendering: Original engine uses DirectX sprite rendering for GUI elements
    /// - This MonoGame implementation: Uses MonoGame SpriteBatch for GUI rendering
    /// - GUI loading: Loads GUI files from game installation, parses panel/button definitions
    /// - Button events: Handles button click events, dispatches to game systems
    /// - Note: Original engine used DirectX GUI rendering, this is a modern MonoGame adaptation
    /// - Based on swkotor2.exe: FUN_0070a2e0 @ 0x0070a2e0 demonstrates GUI loading pattern with button initialization
    /// </remarks>
    public class KotorGuiManager
    {
        private readonly GraphicsDevice _graphicsDevice;
        private readonly Installation _installation;
        private readonly SpriteBatch _spriteBatch;
        private readonly Dictionary<string, LoadedGui> _loadedGuis;
        private readonly Dictionary<string, Texture2D> _textureCache;
        private LoadedGui _currentGui;
        private MouseState _previousMouseState;
        private KeyboardState _previousKeyboardState;

        /// <summary>
        /// Event fired when a GUI button is clicked.
        /// </summary>
        public event EventHandler<GuiButtonClickedEventArgs> OnButtonClicked;

        /// <summary>
        /// Initializes a new instance of the KOTOR GUI manager.
        /// </summary>
        /// <param name="device">Graphics device for rendering.</param>
        /// <param name="installation">Game installation for loading GUI resources.</param>
        public KotorGuiManager([NotNull] GraphicsDevice device, [NotNull] Installation installation)
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
            _spriteBatch = new SpriteBatch(device);
            _loadedGuis = new Dictionary<string, LoadedGui>(StringComparer.OrdinalIgnoreCase);
            _textureCache = new Dictionary<string, Texture2D>(StringComparer.OrdinalIgnoreCase);
            _previousMouseState = Mouse.GetState();
            _previousKeyboardState = Keyboard.GetState();
        }

        /// <summary>
        /// Loads a GUI from KOTOR game files.
        /// </summary>
        /// <param name="guiName">Name of the GUI file to load (without extension).</param>
        /// <param name="width">Screen width for GUI scaling.</param>
        /// <param name="height">Screen height for GUI scaling.</param>
        /// <returns>True if GUI was loaded successfully, false otherwise.</returns>
        /// <remarks>
        /// Based on swkotor2.exe GUI loading:
        /// - FUN_0070a2e0 @ 0x0070a2e0: Demonstrates GUI loading pattern
        /// - Loads GUI files from installation using resource lookup
        /// - Parses GUI structure using GUIReader
        /// - Sets up button click handlers and control references
        /// - Original engine uses DirectX sprite rendering, this uses MonoGame SpriteBatch
        /// </remarks>
        public bool LoadGui(string guiName, int width, int height)
        {
            if (string.IsNullOrEmpty(guiName))
            {
                Console.WriteLine("[KotorGuiManager] ERROR: GUI name cannot be null or empty");
                return false;
            }

            // Check if already loaded
            if (_loadedGuis.ContainsKey(guiName))
            {
                Console.WriteLine($"[KotorGuiManager] GUI already loaded: {guiName}");
                _currentGui = _loadedGuis[guiName];
                return true;
            }

            try
            {
                // Lookup GUI resource from installation
                // GUI files are stored as ResourceType.GUI in game archives
                var resourceResult = _installation.Resources.LookupResource(guiName, ResourceType.GUI, null, null);
                if (resourceResult == null || resourceResult.Data == null || resourceResult.Data.Length == 0)
                {
                    Console.WriteLine($"[KotorGuiManager] ERROR: GUI resource not found: {guiName}");
                    return false;
                }

                // Parse GUI file using GUIReader
                GUIReader guiReader = new GUIReader(resourceResult.Data);
                GUI gui = guiReader.Load();

                if (gui == null || gui.Controls == null || gui.Controls.Count == 0)
                {
                    Console.WriteLine($"[KotorGuiManager] ERROR: Failed to parse GUI: {guiName}");
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

                Console.WriteLine($"[KotorGuiManager] Successfully loaded GUI: {guiName} ({width}x{height}) - {gui.Controls.Count} controls");
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[KotorGuiManager] ERROR: Exception loading GUI {guiName}: {ex.Message}");
                Console.WriteLine($"[KotorGuiManager] Stack trace: {ex.StackTrace}");
                return false;
            }
        }

        /// <summary>
        /// Unloads a GUI from memory.
        /// </summary>
        /// <param name="guiName">Name of the GUI to unload.</param>
        public void UnloadGui(string guiName)
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
                Console.WriteLine($"[KotorGuiManager] Unloaded GUI: {guiName}");
            }
        }

        /// <summary>
        /// Sets the current active GUI.
        /// </summary>
        /// <param name="guiName">Name of the GUI to set as current.</param>
        /// <returns>True if GUI was found and set, false otherwise.</returns>
        public bool SetCurrentGui(string guiName)
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

            Console.WriteLine($"[KotorGuiManager] WARNING: GUI not loaded: {guiName}");
            return false;
        }

        /// <summary>
        /// Gets a control by tag from the current GUI.
        /// </summary>
        /// <param name="tag">Control tag to find.</param>
        /// <returns>The control if found, null otherwise.</returns>
        [CanBeNull]
        public GUIControl GetControl(string tag)
        {
            if (_currentGui == null || string.IsNullOrEmpty(tag))
            {
                return null;
            }

            _currentGui.ControlMap.TryGetValue(tag, out var control);
            return control;
        }

        /// <summary>
        /// Gets a button by tag from the current GUI.
        /// </summary>
        /// <param name="tag">Button tag to find.</param>
        /// <returns>The button if found, null otherwise.</returns>
        [CanBeNull]
        public GUIButton GetButton(string tag)
        {
            if (_currentGui == null || string.IsNullOrEmpty(tag))
            {
                return null;
            }

            _currentGui.ButtonMap.TryGetValue(tag, out var button);
            return button;
        }

        /// <summary>
        /// Updates GUI input handling (mouse/keyboard).
        /// </summary>
        /// <param name="gameTime">Current game time.</param>
        public void Update(GameTime gameTime)
        {
            if (_currentGui == null)
            {
                return;
            }

            MouseState currentMouseState = Mouse.GetState();
            KeyboardState currentKeyboardState = Keyboard.GetState();

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
        /// <param name="gameTime">Current game time.</param>
        public void Draw(GameTime gameTime)
        {
            if (_currentGui == null || _currentGui.Gui == null)
            {
                return;
            }

            _spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.LinearClamp, DepthStencilState.None, RasterizerState.CullNone);

            // Render all controls recursively
            foreach (var control in _currentGui.Gui.Controls)
            {
                RenderControl(control, Vector2.Zero);
            }

            _spriteBatch.End();
        }

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
                    OnButtonClicked?.Invoke(this, new GuiButtonClickedEventArgs
                    {
                        ButtonTag = button.Tag,
                        ButtonId = button.Id ?? -1
                    });

                    Console.WriteLine($"[KotorGuiManager] Button clicked: {button.Tag} (ID: {button.Id})");
                    break; // Only handle first button hit
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

            // Skip rendering if control is outside viewport
            if (controlPosition.X + controlSize.X < 0 || controlPosition.Y + controlSize.Y < 0 ||
                controlPosition.X > _graphicsDevice.Viewport.Width || controlPosition.Y > _graphicsDevice.Viewport.Height)
            {
                // Still render children in case they're visible
                if (control.Children != null)
                {
                    foreach (var child in control.Children)
                    {
                        RenderControl(child, controlPosition);
                    }
                }
                return;
            }

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

                case GUIControlType.ListBox:
                    RenderListBox((GUIListBox)control, controlPosition, controlSize);
                    break;

                case GUIControlType.Progress:
                    RenderProgressBar((GUIProgressBar)control, controlPosition, controlSize);
                    break;

                case GUIControlType.CheckBox:
                    RenderCheckBox((GUICheckBox)control, controlPosition, controlSize);
                    break;

                case GUIControlType.Slider:
                    RenderSlider((GUISlider)control, controlPosition, controlSize);
                    break;

                default:
                    // Render generic control (border/background if available)
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
                Texture2D fillTexture = LoadTexture(panel.Border.Fill.ToString());
                if (fillTexture != null)
                {
                    float alpha = panel.Alpha;
                    Color tint = Microsoft.Xna.Framework.Color.White * alpha;
                    _spriteBatch.Draw(fillTexture, new Rectangle((int)position.X, (int)position.Y, (int)size.X, (int)size.Y), tint);
                }
            }
            else
            {
                // Render solid color background if no texture
                Color bgColor = panel.Color;
                if (bgColor.A > 0)
                {
                    Texture2D pixel = GetPixelTexture();
                    float alpha = panel.Alpha;
                    Color tint = new Microsoft.Xna.Framework.Color(bgColor.R, bgColor.G, bgColor.B, (byte)(bgColor.A * alpha));
                    _spriteBatch.Draw(pixel, new Rectangle((int)position.X, (int)position.Y, (int)size.X, (int)size.Y), tint);
                }
            }
        }

        /// <summary>
        /// Renders a button control.
        /// </summary>
        private void RenderButton(GUIButton button, Vector2 position, Vector2 size)
        {
            // Determine which border state to use (normal, hilight, selected, hilight+selected)
            GUIBorder borderToUse = button.Border;
            if (button.HilightSelected != null)
            {
                // TODO: Check if button is both highlighted and selected
                borderToUse = ConvertHilightSelectedToBorder(button.HilightSelected);
            }
            else if (button.Selected != null)
            {
                // TODO: Check if button is selected
                borderToUse = ConvertSelectedToBorder(button.Selected);
            }
            else if (button.Hilight != null)
            {
                // TODO: Check if button is highlighted (mouse over)
                borderToUse = button.Hilight;
            }

            // Render button background
            if (borderToUse != null && !borderToUse.Fill.IsBlank)
            {
                Texture2D fillTexture = LoadTexture(borderToUse.Fill.ToString());
                if (fillTexture != null)
                {
                    Color tint = Microsoft.Xna.Framework.Color.White;
                    _spriteBatch.Draw(fillTexture, new Rectangle((int)position.X, (int)position.Y, (int)size.X, (int)size.Y), tint);
                }
            }
            else if (button.Border != null && !button.Border.Fill.IsBlank)
            {
                Texture2D fillTexture = LoadTexture(button.Border.Fill.ToString());
                if (fillTexture != null)
                {
                    Color tint = Microsoft.Xna.Framework.Color.White;
                    _spriteBatch.Draw(fillTexture, new Rectangle((int)position.X, (int)position.Y, (int)size.X, (int)size.Y), tint);
                }
            }
            else
            {
                // Render solid color background
                Color bgColor = button.Color;
                if (bgColor.A > 0)
                {
                    Texture2D pixel = GetPixelTexture();
                    Color tint = new Microsoft.Xna.Framework.Color(bgColor.R, bgColor.G, bgColor.B, bgColor.A);
                    _spriteBatch.Draw(pixel, new Rectangle((int)position.X, (int)position.Y, (int)size.X, (int)size.Y), tint);
                }
            }

            // Render button text if available
            if (button.GuiText != null && !string.IsNullOrEmpty(button.GuiText.Text))
            {
                // TODO: Load and use proper font from button.GuiText.Font
                // For now, use default rendering
                string text = button.GuiText.Text;
                Color textColor = new Microsoft.Xna.Framework.Color(
                    button.GuiText.Color.R,
                    button.GuiText.Color.G,
                    button.GuiText.Color.B,
                    button.GuiText.Color.A);

                // Calculate text position (centered for now)
                // TODO: Use proper font measurement and alignment
                Vector2 textSize = Vector2.Zero; // Would use font.MeasureString(text)
                Vector2 textPos = position + (size - textSize) / 2;

                // TODO: Use proper font rendering
                // _spriteBatch.DrawString(font, text, textPos, textColor);
            }
        }

        /// <summary>
        /// Renders a label control.
        /// </summary>
        private void RenderLabel(GUILabel label, Vector2 position, Vector2 size)
        {
            // Render label background if it has a border
            if (label.Border != null && !label.Border.Fill.IsBlank)
            {
                Texture2D fillTexture = LoadTexture(label.Border.Fill.ToString());
                if (fillTexture != null)
                {
                    Color tint = Microsoft.Xna.Framework.Color.White;
                    _spriteBatch.Draw(fillTexture, new Rectangle((int)position.X, (int)position.Y, (int)size.X, (int)size.Y), tint);
                }
            }

            // Render label text
            if (label.GuiText != null && !string.IsNullOrEmpty(label.GuiText.Text))
            {
                string text = label.GuiText.Text;
                Color textColor = new Microsoft.Xna.Framework.Color(
                    label.GuiText.Color.R,
                    label.GuiText.Color.G,
                    label.GuiText.Color.B,
                    label.GuiText.Color.A);

                // TODO: Use proper font rendering with alignment
                // Vector2 textPos = CalculateTextPosition(label, position, size);
                // _spriteBatch.DrawString(font, text, textPos, textColor);
            }
        }

        /// <summary>
        /// Renders a list box control.
        /// </summary>
        private void RenderListBox(GUIListBox listBox, Vector2 position, Vector2 size)
        {
            // Render list box background
            if (listBox.Border != null && !listBox.Border.Fill.IsBlank)
            {
                Texture2D fillTexture = LoadTexture(listBox.Border.Fill.ToString());
                if (fillTexture != null)
                {
                    Color tint = Microsoft.Xna.Framework.Color.White;
                    _spriteBatch.Draw(fillTexture, new Rectangle((int)position.X, (int)position.Y, (int)size.X, (int)size.Y), tint);
                }
            }

            // TODO: Render list box items using ProtoItem template
            // TODO: Render scrollbar if present
        }

        /// <summary>
        /// Renders a progress bar control.
        /// </summary>
        private void RenderProgressBar(GUIProgressBar progressBar, Vector2 position, Vector2 size)
        {
            // Render progress bar background
            if (progressBar.Border != null && !progressBar.Border.Fill.IsBlank)
            {
                Texture2D fillTexture = LoadTexture(progressBar.Border.Fill.ToString());
                if (fillTexture != null)
                {
                    Color tint = Microsoft.Xna.Framework.Color.White;
                    _spriteBatch.Draw(fillTexture, new Rectangle((int)position.X, (int)position.Y, (int)size.X, (int)size.Y), tint);
                }
            }

            // Render progress fill
            if (progressBar.Progress != null && progressBar.MaxValue > 0)
            {
                float progress = (float)progressBar.CurrentValue / progressBar.MaxValue;
                int fillWidth = (int)(size.X * progress);

                if (fillWidth > 0 && progressBar.Progress.Fill != null && !progressBar.Progress.Fill.IsBlank)
                {
                    Texture2D progressTexture = LoadTexture(progressBar.Progress.Fill.ToString());
                    if (progressTexture != null)
                    {
                        Color tint = Microsoft.Xna.Framework.Color.White;
                        _spriteBatch.Draw(progressTexture, new Rectangle((int)position.X, (int)position.Y, fillWidth, (int)size.Y), tint);
                    }
                }
            }
        }

        /// <summary>
        /// Renders a checkbox control.
        /// </summary>
        private void RenderCheckBox(GUICheckBox checkBox, Vector2 position, Vector2 size)
        {
            // Render checkbox background
            if (checkBox.Border != null && !checkBox.Border.Fill.IsBlank)
            {
                Texture2D fillTexture = LoadTexture(checkBox.Border.Fill.ToString());
                if (fillTexture != null)
                {
                    Color tint = Microsoft.Xna.Framework.Color.White;
                    _spriteBatch.Draw(fillTexture, new Rectangle((int)position.X, (int)position.Y, (int)size.X, (int)size.Y), tint);
                }
            }

            // TODO: Render checkmark if IsSelected is true
        }

        /// <summary>
        /// Renders a slider control.
        /// </summary>
        private void RenderSlider(GUISlider slider, Vector2 position, Vector2 size)
        {
            // Render slider track
            if (slider.Border != null && !slider.Border.Fill.IsBlank)
            {
                Texture2D fillTexture = LoadTexture(slider.Border.Fill.ToString());
                if (fillTexture != null)
                {
                    Color tint = Microsoft.Xna.Framework.Color.White;
                    _spriteBatch.Draw(fillTexture, new Rectangle((int)position.X, (int)position.Y, (int)size.X, (int)size.Y), tint);
                }
            }

            // TODO: Render slider thumb at current value position
        }

        /// <summary>
        /// Renders a generic control (fallback).
        /// </summary>
        private void RenderGenericControl(GUIControl control, Vector2 position, Vector2 size)
        {
            // Render background if border is available
            if (control.Border != null && !control.Border.Fill.IsBlank)
            {
                Texture2D fillTexture = LoadTexture(control.Border.Fill.ToString());
                if (fillTexture != null)
                {
                    Color tint = Microsoft.Xna.Framework.Color.White;
                    _spriteBatch.Draw(fillTexture, new Rectangle((int)position.X, (int)position.Y, (int)size.X, (int)size.Y), tint);
                }
            }
            else if (control.Color.A > 0)
            {
                // Render solid color background
                Texture2D pixel = GetPixelTexture();
                Color tint = new Microsoft.Xna.Framework.Color(control.Color.R, control.Color.G, control.Color.B, control.Color.A);
                _spriteBatch.Draw(pixel, new Rectangle((int)position.X, (int)position.Y, (int)size.X, (int)size.Y), tint);
            }
        }

        /// <summary>
        /// Loads a texture from the installation, with caching.
        /// </summary>
        private Texture2D LoadTexture(string textureName)
        {
            if (string.IsNullOrEmpty(textureName))
            {
                return null;
            }

            string key = textureName.ToLowerInvariant();

            // Check cache
            if (_textureCache.TryGetValue(key, out Texture2D cached))
            {
                return cached;
            }

            try
            {
                // Lookup texture resource (TPC format)
                var resourceResult = _installation.Resources.LookupResource(textureName, ResourceType.TPC, null, null);
                if (resourceResult == null || resourceResult.Data == null || resourceResult.Data.Length == 0)
                {
                    return null;
                }

                // TODO: Convert TPC to Texture2D using TpcToMonoGameTextureConverter
                // For now, return null - texture conversion should be handled by texture loading system
                // This is a placeholder that indicates texture loading needs to be integrated
                return null;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[KotorGuiManager] ERROR loading texture {textureName}: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Gets or creates a 1x1 white pixel texture for solid color rendering.
        /// </summary>
        private Texture2D GetPixelTexture()
        {
            const string pixelKey = "__pixel__";
            if (!_textureCache.TryGetValue(pixelKey, out Texture2D pixel))
            {
                pixel = new Texture2D(_graphicsDevice, 1, 1);
                pixel.SetData(new[] { Microsoft.Xna.Framework.Color.White });
                _textureCache[pixelKey] = pixel;
            }
            return pixel;
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

        /// <summary>
        /// Disposes resources.
        /// </summary>
        public void Dispose()
        {
            if (_spriteBatch != null)
            {
                _spriteBatch.Dispose();
            }

            // Dispose cached textures
            foreach (var texture in _textureCache.Values)
            {
                if (texture != null && !texture.IsDisposed)
                {
                    texture.Dispose();
                }
            }
            _textureCache.Clear();
            _loadedGuis.Clear();
        }
    }

    /// <summary>
    /// Event arguments for GUI button click events.
    /// </summary>
    public class GuiButtonClickedEventArgs : EventArgs
    {
        public string ButtonTag { get; set; }
        public int ButtonId { get; set; }
    }
}

