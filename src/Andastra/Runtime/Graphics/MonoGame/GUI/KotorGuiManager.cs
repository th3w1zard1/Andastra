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
using Andastra.Runtime.Games.Odyssey.Fonts;
using JetBrains.Annotations;

namespace Andastra.Runtime.MonoGame.GUI
{
    /// <summary>
    /// Manages KOTOR GUI rendering using MonoGame SpriteBatch.
    /// </summary>
    /// <remarks>
    /// KOTOR GUI Manager (MonoGame Implementation - Odyssey Engine):
    /// - Based on swkotor.exe and swkotor2.exe GUI system (modern MonoGame adaptation)
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
    /// - Font rendering: Loads bitmap fonts from ResRef using BitmapFont class with TXI metrics
    /// 
    /// Ghidra Reverse Engineering Analysis Required:
    /// - swkotor.exe: GUI font loading and text rendering functions (needs Ghidra address verification)
    /// - swkotor2.exe: FUN_0070a2e0 @ 0x0070a2e0 demonstrates GUI loading pattern with button initialization (needs verification)
    /// - nwmain.exe: Aurora engine GUI system and font rendering (needs Ghidra analysis for equivalent implementation)
    /// - daorigins.exe: Eclipse engine GUI system and font rendering (needs Ghidra analysis for equivalent implementation)
    /// - DragonAge2.exe: Eclipse engine GUI system and font rendering (needs Ghidra analysis for equivalent implementation)
    /// - :  GUI system and font rendering (needs Ghidra analysis for equivalent implementation)
    /// - :  GUI system and font rendering (needs Ghidra analysis for equivalent implementation)
    /// 
    /// Cross-Engine Inheritance Structure (to be implemented after Ghidra analysis):
    /// - Base Class: BaseGuiManager (Runtime.Games.Common) - Common GUI loading/rendering patterns
    ///   - Odyssey: KotorGuiManager : BaseGuiManager (swkotor.exe: 0x..., swkotor2.exe: 0x0070a2e0)
    ///   - Aurora: AuroraGuiManager : BaseGuiManager (nwmain.exe: 0x...)
    ///   - Eclipse: EclipseGuiManager : BaseGuiManager (daorigins.exe: 0x..., DragonAge2.exe: 0x...)
    ///   - Infinity: InfinityGuiManager : BaseGuiManager (: 0x..., : 0x...)
    /// 
    /// Note: Original engine used DirectX GUI rendering, this is a modern MonoGame adaptation
    /// </remarks>
    public class KotorGuiManager : BaseGuiManager
    {
        private readonly GraphicsDevice _graphicsDevice;
        private readonly Installation _installation;
        private readonly SpriteBatch _spriteBatch;
        private readonly Dictionary<string, LoadedGui> _loadedGuis;
        private readonly Dictionary<string, Texture2D> _textureCache;
        private readonly Dictionary<string, OdysseyBitmapFont> _fontCache;
        private LoadedGui _currentGui;
        private MouseState _previousMouseState;
        private KeyboardState _previousKeyboardState;
        private string _highlightedButtonTag;

        /// <summary>
        /// Gets the graphics device.
        /// </summary>
        protected override IGraphicsDevice GraphicsDevice => new MonoGameGraphicsDevice(_graphicsDevice);

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
            _fontCache = new Dictionary<string, BitmapFont>(StringComparer.OrdinalIgnoreCase);
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
        public override bool LoadGui(string guiName, int width, int height)
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
                Console.WriteLine($"[KotorGuiManager] Unloaded GUI: {guiName}");
            }
        }

        /// <summary>
        /// Sets the current active GUI.
        /// </summary>
        /// <param name="guiName">Name of the GUI to set as current.</param>
        /// <returns>True if GUI was found and set, false otherwise.</returns>
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
        public override void Update(object gameTime)
        {
            if (_currentGui == null)
            {
                _highlightedButtonTag = null;
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
        /// <param name="gameTime">Current game time.</param>
        public override void Draw(object gameTime)
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
            // Process in reverse order to handle overlapping buttons correctly (topmost first)
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
                    // Found the topmost button under the mouse
                    newHighlightedTag = button.Tag;
                    break;
                }
            }

            _highlightedButtonTag = newHighlightedTag;
        }

        /// <summary>
        /// Checks if a button is currently highlighted (mouse over).
        /// </summary>
        private bool IsButtonHighlighted(GUIButton button)
        {
            if (button == null || string.IsNullOrEmpty(button.Tag))
            {
                return false;
            }

            return string.Equals(_highlightedButtonTag, button.Tag, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Checks if a button is currently selected (programmatically selected).
        /// </summary>
        private bool IsButtonSelected(GUIButton button)
        {
            if (button == null)
            {
                return false;
            }

            // IsSelected is an int? property where non-zero/null means selected
            return button.IsSelected.HasValue && button.IsSelected.Value != 0;
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
            // Check button states: highlighted (mouse over) and selected (programmatically selected)
            bool isHighlighted = IsButtonHighlighted(button);
            bool isSelected = IsButtonSelected(button);

            // Determine which border state to use (normal, hilight, selected, hilight+selected)
            // Priority: hilight+selected > selected > hilight > normal
            GUIBorder borderToUse = button.Border;
            if (isHighlighted && isSelected && button.HilightSelected != null)
            {
                // Button is both highlighted and selected - use hilight+selected border
                borderToUse = ConvertHilightSelectedToBorder(button.HilightSelected);
            }
            else if (isSelected && button.Selected != null)
            {
                // Button is selected (but not highlighted) - use selected border
                borderToUse = ConvertSelectedToBorder(button.Selected);
            }
            else if (isHighlighted && button.Hilight != null)
            {
                // Button is highlighted (but not selected) - use hilight border
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
                string text = button.GuiText.Text;
                Color textColor = new Microsoft.Xna.Framework.Color(
                    button.GuiText.Color.R,
                    button.GuiText.Color.G,
                    button.GuiText.Color.B,
                    button.GuiText.Color.A);

                // Load font from button.GuiText.Font ResRef
                BaseBitmapFont font = LoadFont(button.GuiText.Font.ToString());
                if (font != null)
                {
                    // Measure text size
                    Vector2 textSize = font.MeasureString(text);
                    
                    // Calculate text position based on alignment
                    Vector2 textPos = CalculateTextPosition(button.GuiText.Alignment, position, size, textSize);

                    // Render text using bitmap font
                    RenderBitmapText(font, text, textPos, textColor);
                }
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

                // Load font from label.GuiText.Font ResRef
                BaseBitmapFont font = LoadFont(label.GuiText.Font.ToString());
                if (font != null)
                {
                    // Measure text size
                    Vector2 textSize = font.MeasureString(text);
                    
                    // Calculate text position based on alignment
                    Vector2 textPos = CalculateTextPosition(label.GuiText.Alignment, position, size, textSize);

                    // Render text using bitmap font
                    RenderBitmapText(font, text, textPos, textColor);
                }
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
            // Check if checkbox is selected
            bool isSelected = checkBox.IsSelected.HasValue && checkBox.IsSelected.Value != 0;
            
            // Check if checkbox is highlighted (mouse over) - similar to buttons
            bool isHighlighted = IsCheckBoxHighlighted(checkBox);

            // Determine which border state to use (normal, hilight, selected, hilight+selected)
            // Priority: hilight+selected > selected > hilight > normal
            GUIBorder borderToUse = checkBox.Border;
            if (isHighlighted && isSelected && checkBox.HilightSelected != null)
            {
                // Checkbox is both highlighted and selected - use hilight+selected border
                borderToUse = ConvertHilightSelectedToBorder(checkBox.HilightSelected);
            }
            else if (isSelected && checkBox.Selected != null)
            {
                // Checkbox is selected (but not highlighted) - use selected border
                borderToUse = ConvertSelectedToBorder(checkBox.Selected);
            }
            else if (isHighlighted && checkBox.Hilight != null)
            {
                // Checkbox is highlighted (but not selected) - use hilight border
                borderToUse = checkBox.Hilight;
            }

            // Render checkbox background
            if (borderToUse != null && !borderToUse.Fill.IsBlank)
            {
                Texture2D fillTexture = LoadTexture(borderToUse.Fill.ToString());
                if (fillTexture != null)
                {
                    Color tint = Microsoft.Xna.Framework.Color.White;
                    _spriteBatch.Draw(fillTexture, new Rectangle((int)position.X, (int)position.Y, (int)size.X, (int)size.Y), tint);
                }
            }
            else if (checkBox.Border != null && !checkBox.Border.Fill.IsBlank)
            {
                Texture2D fillTexture = LoadTexture(checkBox.Border.Fill.ToString());
                if (fillTexture != null)
                {
                    Color tint = Microsoft.Xna.Framework.Color.White;
                    _spriteBatch.Draw(fillTexture, new Rectangle((int)position.X, (int)position.Y, (int)size.X, (int)size.Y), tint);
                }
            }
            else
            {
                // Render solid color background
                Color bgColor = checkBox.Color;
                if (bgColor.A > 0)
                {
                    Texture2D pixel = GetPixelTexture();
                    Color tint = new Microsoft.Xna.Framework.Color(bgColor.R, bgColor.G, bgColor.B, bgColor.A);
                    _spriteBatch.Draw(pixel, new Rectangle((int)position.X, (int)position.Y, (int)size.X, (int)size.Y), tint);
                }
            }

            // Render checkmark if IsSelected is true
            if (isSelected)
            {
                RenderCheckmark(checkBox, position, size);
            }
        }

        /// <summary>
        /// Checks if a checkbox is currently highlighted (mouse over).
        /// </summary>
        private bool IsCheckBoxHighlighted(GUICheckBox checkBox)
        {
            if (checkBox == null || string.IsNullOrEmpty(checkBox.Tag))
            {
                return false;
            }

            // Check if mouse is over this checkbox
            MouseState currentMouseState = Mouse.GetState();
            int mouseX = currentMouseState.X;
            int mouseY = currentMouseState.Y;

            int left = (int)checkBox.Position.X;
            int top = (int)checkBox.Position.Y;
            int right = left + (int)checkBox.Size.X;
            int bottom = top + (int)checkBox.Size.Y;

            return mouseX >= left && mouseX <= right && mouseY >= top && mouseY <= bottom;
        }

        /// <summary>
        /// Renders a checkmark for a selected checkbox.
        /// </summary>
        private void RenderCheckmark(GUICheckBox checkBox, Vector2 position, Vector2 size)
        {
            // Try to load checkmark texture from Selected or HilightSelected if available
            Texture2D checkmarkTexture = null;
            
            if (checkBox.Selected != null && !checkBox.Selected.Fill.IsBlank)
            {
                // Try Selected.Fill as checkmark texture
                checkmarkTexture = LoadTexture(checkBox.Selected.Fill.ToString());
            }
            
            if (checkmarkTexture == null && checkBox.HilightSelected != null && !checkBox.HilightSelected.Fill.IsBlank)
            {
                // Try HilightSelected.Fill as checkmark texture
                checkmarkTexture = LoadTexture(checkBox.HilightSelected.Fill.ToString());
            }

            if (checkmarkTexture != null)
            {
                // Render checkmark texture centered in checkbox
                int checkmarkSize = Math.Min((int)size.X, (int)size.Y);
                int checkmarkX = (int)(position.X + (size.X - checkmarkSize) / 2);
                int checkmarkY = (int)(position.Y + (size.Y - checkmarkSize) / 2);
                
                Color tint = Microsoft.Xna.Framework.Color.White;
                _spriteBatch.Draw(checkmarkTexture, new Rectangle(checkmarkX, checkmarkY, checkmarkSize, checkmarkSize), tint);
            }
            else
            {
                // Draw a simple checkmark shape using lines
                // Calculate checkmark size (80% of checkbox size)
                float checkmarkSize = Math.Min(size.X, size.Y) * 0.8f;
                float centerX = position.X + size.X / 2;
                float centerY = position.Y + size.Y / 2;
                
                // Draw checkmark as two lines forming a check
                // Line 1: from bottom-left to center
                // Line 2: from center to top-right
                float lineThickness = Math.Max(2.0f, checkmarkSize * 0.1f);
                float offset = checkmarkSize * 0.3f;
                
                // Calculate checkmark points
                Vector2 point1 = new Vector2(centerX - offset, centerY);
                Vector2 point2 = new Vector2(centerX - offset * 0.3f, centerY + offset * 0.5f);
                Vector2 point3 = new Vector2(centerX + offset, centerY - offset * 0.5f);
                
                // Draw checkmark using pixel texture
                Texture2D pixel = GetPixelTexture();
                Color checkmarkColor = Microsoft.Xna.Framework.Color.White;
                
                // Draw line 1 (bottom-left to center)
                DrawLine(pixel, point1, point2, lineThickness, checkmarkColor);
                
                // Draw line 2 (center to top-right)
                DrawLine(pixel, point2, point3, lineThickness, checkmarkColor);
            }
        }

        /// <summary>
        /// Draws a line using a pixel texture (simplified approach using rectangles).
        /// </summary>
        private void DrawLine(Texture2D pixel, Vector2 start, Vector2 end, float thickness, Color color)
        {
            // Calculate line properties
            Vector2 direction = end - start;
            float length = direction.Length();
            
            if (length <= 0)
            {
                return;
            }
            
            // Normalize direction
            direction = Vector2.Normalize(direction);
            
            // Calculate angle for rotation
            float angle = (float)Math.Atan2(direction.Y, direction.X);
            
            // Draw line as rotated rectangle
            // Use a small rectangle and rotate it
            Rectangle sourceRect = new Rectangle(0, 0, 1, 1);
            Vector2 origin = new Vector2(0.5f, 0.5f);
            Vector2 scale = new Vector2(length, thickness);
            
            _spriteBatch.Draw(
                pixel,
                start,
                sourceRect,
                color,
                angle,
                origin,
                scale,
                SpriteEffects.None,
                0);
        }

        /// <summary>
        /// Renders a slider control.
        /// Based on swkotor.exe and swkotor2.exe: Slider rendering with thumb positioning
        /// Original implementation: Slider thumb position calculated from CURVALUE/MAXVALUE ratio
        /// Thumb position = (CURVALUE / MAXVALUE) × track length
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

            // Render slider thumb at current value position
            // Based on swkotor.exe: Slider thumb rendering
            // Thumb position calculated from Value, MinValue, MaxValue ratio
            // Direction: "horizontal" (0) = left-right, "vertical" (1) = top-bottom
            
            // Get thumb from Properties["THUMB"] or Thumb property
            GUIScrollbarThumb thumb = null;
            if (slider.Properties != null && slider.Properties.ContainsKey("THUMB") && slider.Properties["THUMB"] is GUIScrollbarThumb thumbFromProps)
            {
                thumb = thumbFromProps;
            }
            else if (slider.Thumb != null)
            {
                thumb = slider.Thumb;
            }

            if (thumb == null || thumb.Image.IsBlank)
            {
                // No thumb texture defined - skip thumb rendering
                return;
            }

            // Calculate value range
            float valueRange = slider.MaxValue - slider.MinValue;
            if (valueRange <= 0.0f)
            {
                // Invalid range - cannot calculate position
                return;
            }

            // Clamp current value to valid range
            float currentValue = Math.Max(slider.MinValue, Math.Min(slider.MaxValue, slider.Value));
            
            // Calculate normalized position (0.0 to 1.0)
            float normalizedPosition = (currentValue - slider.MinValue) / valueRange;

            // Load thumb texture
            Texture2D thumbTexture = LoadTexture(thumb.Image.ToString());
            if (thumbTexture == null)
            {
                // Thumb texture not found - skip rendering
                return;
            }

            // Determine slider direction
            bool isHorizontal = slider.Direction == null || slider.Direction == "horizontal" || slider.Direction == "0";
            
            // Calculate thumb position and size
            Vector2 thumbPosition;
            Vector2 thumbSize;
            
            if (isHorizontal)
            {
                // Horizontal slider: thumb moves left-right
                // Thumb width is typically a fixed size or proportional to track
                // For simplicity, use a fixed thumb width (can be made configurable)
                float thumbWidth = Math.Min(size.X * 0.1f, 20.0f); // 10% of track width or max 20 pixels
                float trackLength = size.X - thumbWidth; // Available space for thumb movement
                float thumbX = position.X + (normalizedPosition * trackLength);
                float thumbY = position.Y + (size.Y - thumbTexture.Height) / 2.0f; // Center vertically
                
                thumbPosition = new Vector2(thumbX, thumbY);
                thumbSize = new Vector2(thumbWidth, thumbTexture.Height);
            }
            else
            {
                // Vertical slider: thumb moves top-bottom
                float thumbHeight = Math.Min(size.Y * 0.1f, 20.0f); // 10% of track height or max 20 pixels
                float trackLength = size.Y - thumbHeight; // Available space for thumb movement
                float thumbX = position.X + (size.X - thumbTexture.Width) / 2.0f; // Center horizontally
                float thumbY = position.Y + (normalizedPosition * trackLength);
                
                thumbPosition = new Vector2(thumbX, thumbY);
                thumbSize = new Vector2(thumbTexture.Width, thumbHeight);
            }

            // Apply thumb alignment if specified
            // ALIGNMENT typically affects how the thumb is positioned relative to its calculated position
            // For now, use the calculated position (can be enhanced with alignment support)
            
            // Render thumb texture
            Color thumbTint = Microsoft.Xna.Framework.Color.White;
            
            // Apply rotation if specified (typically unused, but support it)
            float rotation = 0.0f;
            if (thumb.Rotate.HasValue)
            {
                rotation = thumb.Rotate.Value;
            }
            
            // Apply flip style if specified (typically unused, but support it)
            SpriteEffects spriteEffects = SpriteEffects.None;
            if (thumb.FlipStyle.HasValue)
            {
                // FlipStyle: 0=none, 1=horizontal, 2=vertical, 3=both
                int flipStyle = thumb.FlipStyle.Value;
                if ((flipStyle & 1) != 0)
                {
                    spriteEffects |= SpriteEffects.FlipHorizontally;
                }
                if ((flipStyle & 2) != 0)
                {
                    spriteEffects |= SpriteEffects.FlipVertically;
                }
            }
            
            // Render thumb with optional rotation and flip
            if (rotation != 0.0f)
            {
                // Render with rotation
                Vector2 thumbOrigin = new Vector2(thumbTexture.Width / 2.0f, thumbTexture.Height / 2.0f);
                Vector2 thumbCenter = thumbPosition + thumbSize / 2.0f;
                
                _spriteBatch.Draw(
                    thumbTexture,
                    thumbCenter,
                    null,
                    thumbTint,
                    rotation,
                    thumbOrigin,
                    new Vector2(thumbSize.X / thumbTexture.Width, thumbSize.Y / thumbTexture.Height),
                    spriteEffects,
                    0.0f);
            }
            else
            {
                // Render without rotation (simpler and faster)
                _spriteBatch.Draw(
                    thumbTexture,
                    new Rectangle((int)thumbPosition.X, (int)thumbPosition.Y, (int)thumbSize.X, (int)thumbSize.Y),
                    null,
                    thumbTint,
                    0.0f,
                    Vector2.Zero,
                    spriteEffects,
                    0.0f);
            }
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
        /// Loads a bitmap font from a ResRef, with caching.
        /// </summary>
        /// <param name="fontResRef">The font resource reference.</param>
        /// <returns>The loaded font, or null if loading failed.</returns>
        [CanBeNull]
        protected override BaseBitmapFont LoadFont(string fontResRef)
        {
            if (string.IsNullOrEmpty(fontResRef) || fontResRef == "****" || fontResRef.Trim().Length == 0)
            {
                return null;
            }

            string key = fontResRef.ToLowerInvariant();

            // Check cache
            if (_fontCache.TryGetValue(key, out OdysseyBitmapFont cached))
            {
                return cached;
            }

            // Load font
            OdysseyBitmapFont font = OdysseyBitmapFont.Load(fontResRef, _installation, _graphicsDevice);
            if (font != null)
            {
                _fontCache[key] = font;
            }

            return font;
        }


        /// <summary>
        /// Renders text using a bitmap font.
        /// </summary>
        /// <param name="font">The bitmap font to use.</param>
        /// <param name="text">The text to render.</param>
        /// <param name="position">The position to render at.</param>
        /// <param name="color">The text color.</param>
        private void RenderBitmapText([NotNull] BaseBitmapFont font, string text, Vector2 position, Color color)
        {
            if (font == null || string.IsNullOrEmpty(text))
            {
                return;
            }

            // Cast to OdysseyBitmapFont for MonoGame-specific rendering
            if (!(font is OdysseyBitmapFont odysseyFont))
            {
                return;
            }

            float currentX = position.X;
            float currentY = position.Y;
            float lineHeight = font.FontHeight + font.SpacingB;

            foreach (char c in text)
            {
                if (c == '\n')
                {
                    // New line
                    currentX = position.X;
                    currentY += lineHeight;
                    continue;
                }

                int charCode = (int)c;
                BaseBitmapFont.CharacterGlyph? glyph = font.GetCharacter(charCode);
                if (glyph.HasValue)
                {
                    var g = glyph.Value;
                    // Render character glyph
                    _spriteBatch.Draw(
                        odysseyFont.MonoGameTexture,
                        new Rectangle((int)currentX, (int)currentY, (int)g.Width, (int)g.Height),
                        new Rectangle(g.SourceX, g.SourceY, g.SourceWidth, g.SourceHeight),
                        color);

                    currentX += g.Width + font.SpacingR;
                }
                else
                {
                    // Unknown character - skip or use default width
                    currentX += font.FontWidth + font.SpacingR;
                }
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

        /// <summary>
        /// Disposes resources.
        /// </summary>
        public override void Dispose()
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
            
            // Note: Fonts don't need disposal as they reference textures that are already disposed
            _fontCache.Clear();
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

