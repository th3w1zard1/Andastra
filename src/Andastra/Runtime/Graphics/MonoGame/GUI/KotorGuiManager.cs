using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using XnaVector2 = Microsoft.Xna.Framework.Vector2;
using XnaColor = Microsoft.Xna.Framework.Color;
using XnaRectangle = Microsoft.Xna.Framework.Rectangle;
using XnaSpriteEffects = Microsoft.Xna.Framework.Graphics.SpriteEffects;
using ParsingColor = Andastra.Parsing.Common.Color;
using Andastra.Parsing;
using Andastra.Parsing.Installation;
using Andastra.Parsing.Resource;
using Andastra.Parsing.Resource.Generics.GUI;
using GuiResource = Andastra.Parsing.Resource.Generics.GUI.GUI;
using Andastra.Parsing.Common;
using Andastra.Parsing.Formats.TPC;
using Andastra.Runtime.Games.Common;
using Andastra.Runtime.MonoGame.Converters;
using Andastra.Runtime.MonoGame.Graphics;
using Andastra.Runtime.Graphics;
using GraphicsVector2 = Andastra.Runtime.Graphics.Vector2;
using Andastra.Runtime.Graphics.MonoGame.Graphics;
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
        private readonly Dictionary<string, BaseBitmapFont> _fontCache;
        private LoadedGui _currentGui;
        private MouseState _previousMouseState;
        private KeyboardState _previousKeyboardState;
        private string _highlightedButtonTag;

        /// <summary>
        /// Event fired when a GUI button is clicked.
        /// </summary>
        public event Action<string, int> OnButtonClicked;

        /// <summary>
        /// Event fired when a GUI checkbox is clicked.
        /// </summary>
        /// <remarks>
        /// Checkbox Click Event:
        /// - Based on swkotor.exe and swkotor2.exe: Checkbox click handling in options menu
        /// - Original implementation: Checkboxes toggle state when clicked (CB_VSYNC, CB_FRAMEBUFF, etc.)
        /// - Based on swkotor2.exe: OptionsGraphicsAdvancedMenu::callbackActive handles CB_VSYNC @ 0x006e3e80
        /// - When checkbox is clicked, its IsSelected state is toggled
        /// </remarks>
        public event Action<string, bool> OnCheckBoxClicked;

        /// <summary>
        /// Gets the tag of the currently highlighted button (mouse over).
        /// </summary>
        /// <remarks>
        /// Highlighted Button Tag:
        /// - Based on swkotor.exe and swkotor2.exe: Button hover detection for sound effects
        /// - Updated during Update() method when mouse moves over buttons
        /// - Used for playing hover sound effects ("gui_actscroll" or "gui_actscroll1")
        /// - Returns null if no button is currently highlighted
        /// - Original implementation: Button hover state tracked internally for rendering and sound effects
        /// - Based on swkotor.exe FUN_0067ace0 @ 0x0067ace0: Button hover state tracking
        /// - Based on swkotor2.exe FUN_006d0790 @ 0x006d0790: Button hover state tracking
        /// </remarks>
        [CanBeNull]
        public string HighlightedButtonTag => _highlightedButtonTag;

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
            _fontCache = new Dictionary<string, BaseBitmapFont>(StringComparer.OrdinalIgnoreCase);
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
                GuiResource gui = guiReader.Load();

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
                    ButtonMap = new Dictionary<string, GUIButton>(StringComparer.OrdinalIgnoreCase),
                    CheckBoxMap = new Dictionary<string, GUICheckBox>(StringComparer.OrdinalIgnoreCase)
                };

                // Build control and button maps for quick lookup
                BuildControlMaps(gui.Controls, loadedGui);

                // Store loaded GUI
                _loadedGuis[guiName] = loadedGui;
                _currentGui = loadedGui;

                // Note: RIM file loading (e.g., "RIMS:MAINMENU") is handled automatically by InstallationResourceManager
                // Based on swkotor.exe FUN_0067c4c0 @ 0x0067c4c0:65-69 and swkotor2.exe FUN_006d2350 @ 0x006d2350:76-80
                // Original engines explicitly load "RIMS:MAINMENU" RIM file after GUI load (FUN_004087c0/FUN_004089f0)
                // Our resource system automatically searches RIM files during resource lookup, so explicit loading is not required
                // The RIM file contains additional resources (textures, etc.) needed for the menu, which are loaded on-demand

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
        /// Updates the border fill texture for a control by tag.
        /// Based on swkotor.exe and swkotor2.exe: Control texture updates
        /// - Original implementation: Controls can have their border fill textures updated dynamically
        /// - Used for loading screen images, dynamic backgrounds, etc.
        /// - This method updates the Border.Fill ResRef and invalidates texture cache for the control
        /// </summary>
        /// <param name="controlTag">Tag of the control to update. If null or empty, updates root control.</param>
        /// <param name="textureResRef">New texture ResRef to set as border fill (TPC format).</param>
        /// <returns>True if control was found and updated, false otherwise.</returns>
        public bool SetControlTexture(string controlTag, string textureResRef)
        {
            if (_currentGui == null || _currentGui.Gui == null)
            {
                return false;
            }

            GUIControl control = null;

            // If controlTag is null/empty, update root control
            if (string.IsNullOrEmpty(controlTag))
            {
                // Get root control from GUI
                if (_currentGui.Gui.Root != null)
                {
                    control = _currentGui.Gui.Root;
                }
                else if (_currentGui.Gui.Controls != null && _currentGui.Gui.Controls.Count > 0)
                {
                    // Fallback: use first top-level control as root
                    control = _currentGui.Gui.Controls[0];
                }
            }
            else
            {
                // Find control by tag
                control = GetControl(controlTag);
            }

            if (control == null)
            {
                Console.WriteLine($"[KotorGuiManager] WARNING: Control not found for tag: {controlTag ?? "(root)"}");
                return false;
            }

            // Ensure border exists
            if (control.Border == null)
            {
                control.Border = new GUIBorder();
            }

            // Update border fill ResRef
            if (string.IsNullOrEmpty(textureResRef))
            {
                control.Border.Fill = ResRef.FromBlank();
            }
            else
            {
                control.Border.Fill = ResRef.FromString(textureResRef);
            }

            // Invalidate texture cache for this texture to force reload on next render
            string textureKey = textureResRef?.ToLowerInvariant() ?? string.Empty;
            if (!string.IsNullOrEmpty(textureKey) && _textureCache.ContainsKey(textureKey))
            {
                // Remove from cache so it will be reloaded with new texture
                var oldTexture = _textureCache[textureKey];
                if (oldTexture != null && !oldTexture.IsDisposed)
                {
                    oldTexture.Dispose();
                }
                _textureCache.Remove(textureKey);
            }

            Console.WriteLine($"[KotorGuiManager] Updated control texture: tag={controlTag ?? "(root)"}, texture={textureResRef}");
            return true;
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
            if (_previousMouseState.LeftButton == Microsoft.Xna.Framework.Input.ButtonState.Released && currentMouseState.LeftButton == Microsoft.Xna.Framework.Input.ButtonState.Pressed)
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

            _spriteBatch.Begin(Microsoft.Xna.Framework.Graphics.SpriteSortMode.Deferred, Microsoft.Xna.Framework.Graphics.BlendState.AlphaBlend, SamplerState.LinearClamp, DepthStencilState.None, RasterizerState.CullNone);

            // Render all controls recursively
            foreach (var control in _currentGui.Gui.Controls)
            {
                RenderControl(control, XnaVector2.Zero);
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

                // Add to checkbox map if it's a checkbox
                if (control is GUICheckBox checkBox)
                {
                    if (!string.IsNullOrEmpty(checkBox.Tag))
                    {
                        loadedGui.CheckBoxMap[checkBox.Tag] = checkBox;
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
        /// Handles mouse click input and checks for button and checkbox hits.
        /// </summary>
        /// <remarks>
        /// Mouse Click Handling:
        /// - Based on swkotor.exe and swkotor2.exe: Mouse click handling for GUI controls
        /// - Original implementation: Buttons and checkboxes respond to mouse clicks
        /// - Checkboxes toggle their IsSelected state when clicked
        /// - Based on swkotor2.exe: OptionsGraphicsAdvancedMenu::callbackActive handles CB_VSYNC checkbox clicks
        /// </remarks>
        private void HandleMouseClick(int mouseX, int mouseY)
        {
            if (_currentGui == null)
            {
                return;
            }

            // Check all checkboxes for hit first (checkboxes are typically on top of buttons)
            // Based on swkotor2.exe: Checkbox click handling takes priority
            foreach (var kvp in _currentGui.CheckBoxMap)
            {
                var checkBox = kvp.Value;
                if (checkBox == null)
                {
                    continue;
                }

                // Check if mouse is within checkbox bounds
                int left = (int)checkBox.Position.X;
                int top = (int)checkBox.Position.Y;
                int right = left + (int)checkBox.Size.X;
                int bottom = top + (int)checkBox.Size.Y;

                if (mouseX >= left && mouseX <= right && mouseY >= top && mouseY <= bottom)
                {
                    // Checkbox clicked - toggle state
                    // Based on swkotor2.exe: Checkbox state toggled on click (OptionsGraphicsAdvancedMenu::callbackActive)
                    bool wasSelected = checkBox.IsSelected.HasValue && checkBox.IsSelected.Value != 0;
                    bool newState = !wasSelected;
                    checkBox.IsSelected = newState ? 1 : 0;

                    // Fire the OnCheckBoxClicked event for external handlers
                    OnCheckBoxClicked?.Invoke(checkBox.Tag, newState);

                    Console.WriteLine($"[KotorGuiManager] Checkbox clicked: {checkBox.Tag} -> {(newState ? "checked" : "unchecked")}");
                    return; // Checkbox click handled, don't process buttons
                }
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

                    // Fire the OnButtonClicked event for external handlers
                    OnButtonClicked?.Invoke(button.Tag, button.Id ?? -1);

                    Console.WriteLine($"[KotorGuiManager] Button clicked: {button.Tag} (ID: {button.Id})");
                    break; // Only handle first button hit
                }
            }
        }

        /// <summary>
        /// Recursively renders a GUI control and its children.
        /// </summary>
        private void RenderControl(GUIControl control, XnaVector2 parentOffset)
        {
            if (control == null)
            {
                return;
            }

            XnaVector2 controlPosition = new XnaVector2(control.Position.X, control.Position.Y) + parentOffset;
            XnaVector2 controlSize = new XnaVector2(control.Size.X, control.Size.Y);

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
        private void RenderPanel(GUIPanel panel, XnaVector2 position, XnaVector2 size)
        {
            // Render panel background using border fill texture if available
            if (panel.Border != null && !panel.Border.Fill.IsBlank())
            {
                Texture2D fillTexture = LoadTexture(panel.Border.Fill.ToString());
                if (fillTexture != null)
                {
                    float alpha = panel.Alpha;
                    XnaColor tint = Microsoft.Xna.Framework.Color.White * alpha;
                    _spriteBatch.Draw(fillTexture, new XnaRectangle((int)position.X, (int)position.Y, (int)size.X, (int)size.Y), tint);
                }
            }
            else
            {
                // Render solid color background if no texture
                ParsingColor bgColor = panel.Color;
                if (bgColor.A > 0)
                {
                    Texture2D pixel = GetPixelTexture();
                    float alpha = panel.Alpha;
                    XnaColor tint = new Microsoft.Xna.Framework.Color(bgColor.R, bgColor.G, bgColor.B, (byte)(bgColor.A * alpha));
                    _spriteBatch.Draw(pixel, new XnaRectangle((int)position.X, (int)position.Y, (int)size.X, (int)size.Y), tint);
                }
            }
        }

        /// <summary>
        /// Renders a button control.
        /// </summary>
        private void RenderButton(GUIButton button, XnaVector2 position, XnaVector2 size)
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
            if (borderToUse != null && !borderToUse.Fill.IsBlank())
            {
                Texture2D fillTexture = LoadTexture(borderToUse.Fill.ToString());
                if (fillTexture != null)
                {
                    XnaColor tint = Microsoft.Xna.Framework.Color.White;
                    _spriteBatch.Draw(fillTexture, new XnaRectangle((int)position.X, (int)position.Y, (int)size.X, (int)size.Y), tint);
                }
            }
            else if (button.Border != null && !button.Border.Fill.IsBlank())
            {
                Texture2D fillTexture = LoadTexture(button.Border.Fill.ToString());
                if (fillTexture != null)
                {
                    XnaColor tint = Microsoft.Xna.Framework.Color.White;
                    _spriteBatch.Draw(fillTexture, new XnaRectangle((int)position.X, (int)position.Y, (int)size.X, (int)size.Y), tint);
                }
            }
            else
            {
                // Render solid color background
                ParsingColor bgColor = button.Color;
                if (bgColor.A > 0)
                {
                    Texture2D pixel = GetPixelTexture();
                    XnaColor tint = new Microsoft.Xna.Framework.Color(bgColor.R, bgColor.G, bgColor.B, bgColor.A);
                    _spriteBatch.Draw(pixel, new XnaRectangle((int)position.X, (int)position.Y, (int)size.X, (int)size.Y), tint);
                }
            }

            // Render button text if available
            if (button.GuiText != null && !string.IsNullOrEmpty(button.GuiText.Text))
            {
                string text = button.GuiText.Text;
                XnaColor textColor = new Microsoft.Xna.Framework.Color(
                    button.GuiText.Color.R,
                    button.GuiText.Color.G,
                    button.GuiText.Color.B,
                    button.GuiText.Color.A);

                // Load font from button.GuiText.Font ResRef
                BaseBitmapFont font = LoadFont(button.GuiText.Font.ToString());
                if (font != null)
                {
                    // Measure text size
                    GraphicsVector2 textSize = font.MeasureString(text);

                    // Calculate text position based on alignment
                    XnaVector2 textPos = CalculateTextPosition(button.GuiText.Alignment, position, size, new XnaVector2(textSize.X, textSize.Y)); 
                        new GraphicsVector2(position.X, position.Y), 
                        new GraphicsVector2(size.X, size.Y), 
                        new GraphicsVector2(textSize.X, textSize.Y));

                    // Render text using bitmap font (convert Graphics.Vector2 to XnaVector2)
                    RenderBitmapText(font, text, new XnaVector2(textPos.X, textPos.Y), textColor);
                }
            }
        }

        /// <summary>
        /// Renders a label control.
        /// </summary>
        private void RenderLabel(GUILabel label, XnaVector2 position, XnaVector2 size)
        {
            // Render label background if it has a border
            if (label.Border != null && !label.Border.Fill.IsBlank())
            {
                Texture2D fillTexture = LoadTexture(label.Border.Fill.ToString());
                if (fillTexture != null)
                {
                    XnaColor tint = Microsoft.Xna.Framework.Color.White;
                    _spriteBatch.Draw(fillTexture, new XnaRectangle((int)position.X, (int)position.Y, (int)size.X, (int)size.Y), tint);
                }
            }

            // Render label text
            if (label.GuiText != null && !string.IsNullOrEmpty(label.GuiText.Text))
            {
                string text = label.GuiText.Text;
                XnaColor textColor = new Microsoft.Xna.Framework.Color(
                    label.GuiText.Color.R,
                    label.GuiText.Color.G,
                    label.GuiText.Color.B,
                    label.GuiText.Color.A);

                // Load font from label.GuiText.Font ResRef
                BaseBitmapFont font = LoadFont(label.GuiText.Font.ToString());
                if (font != null)
                {
                    // Measure text size
                    System.Numerics.Vector2 textSize = font.MeasureString(text);

                    // Calculate text position based on alignment (convert to Graphics.Vector2)
                    GraphicsVector2 textPos = CalculateTextPosition(label.GuiText.Alignment, 
                        new GraphicsVector2(position.X, position.Y), 
                        new GraphicsVector2(size.X, size.Y), 
                        new GraphicsVector2(textSize.X, textSize.Y));

                    // Render text using bitmap font (convert Graphics.Vector2 to XnaVector2)
                    RenderBitmapText(font, text, new XnaVector2(textPos.X, textPos.Y), textColor);
                }
            }
        }

        /// <summary>
        /// Renders a list box control.
        /// Based on swkotor.exe and swkotor2.exe: CSWGuiListBox::Draw
        /// Original implementation: Items rendered using ProtoItem template with proper states and scrolling
        /// </summary>
        private void RenderListBox(GUIListBox listBox, XnaVector2 position, XnaVector2 size)
        {
            // Render list box background
            if (listBox.Border != null && !listBox.Border.Fill.IsBlank())
            {
                Texture2D fillTexture = LoadTexture(listBox.Border.Fill.ToString());
                if (fillTexture != null)
                {
                    XnaColor tint = Microsoft.Xna.Framework.Color.White;
                    _spriteBatch.Draw(fillTexture, new XnaRectangle((int)position.X, (int)position.Y, (int)size.X, (int)size.Y), tint);
                }
            }

            // Cannot render items without ProtoItem template
            if (listBox.ProtoItem == null)
            {
                return;
            }

            // Get list of items to render
            List<string> items = GetListBoxItems(listBox);
            if (items == null || items.Count == 0)
            {
                // No items to render - scrollbar still might be needed if it's visible by default
                if (listBox.ScrollBar != null)
                {
                    RenderListBoxScrollbar(listBox, position, size, 0, 0);
                }
                return;
            }

            // Get scroll offset (which item index to start rendering from)
            int scrollOffset = GetListBoxScrollOffset(listBox, items.Count);
            int visibleItemCount = GetVisibleItemCount(listBox, items.Count, size);

            // Calculate item height from ProtoItem template
            float itemHeight = listBox.ProtoItem.Size.Y > 0 ? listBox.ProtoItem.Size.Y : 20.0f; // Default to 20 if not set
            int padding = listBox.Padding;

            // Get selected item index
            int selectedIndex = GetSelectedListBoxItemIndex(listBox, items.Count);

            // Render visible items
            float currentY = position.Y;
            int itemsRendered = 0;

            for (int i = scrollOffset; i < items.Count && itemsRendered < visibleItemCount; i++)
            {
                string itemText = items[i];
                bool isSelected = (i == selectedIndex);
                bool isHighlighted = IsListBoxItemHighlighted(listBox, position, size, itemsRendered, itemHeight, padding);

                // Render proto item at current position
                XnaVector2 itemPosition = new XnaVector2(position.X, currentY);
                XnaVector2 itemSize = new XnaVector2(size.X, itemHeight);
                RenderProtoItem(listBox.ProtoItem, itemPosition, itemSize, itemText, isSelected, isHighlighted);

                // Move to next item position
                currentY += itemHeight + padding;
                itemsRendered++;
            }

            // Render scrollbar if present and needed
            if (listBox.ScrollBar != null && items.Count > visibleItemCount)
            {
                RenderListBoxScrollbar(listBox, position, size, items.Count, visibleItemCount);
            }
        }

        /// <summary>
        /// Gets the list of items to render in a list box.
        /// Items can be stored in Properties["Items"] or as child ProtoItem controls.
        /// Based on swkotor.exe and swkotor2.exe: CSWGuiListBox item storage
        /// </summary>
        private List<string> GetListBoxItems(GUIListBox listBox)
        {
            // Check if items are stored in Properties dictionary
            if (listBox.Properties != null && listBox.Properties.ContainsKey("Items"))
            {
                object itemsObj = listBox.Properties["Items"];
                if (itemsObj is List<string> itemsList)
                {
                    return itemsList;
                }
                if (itemsObj is string[] itemsArray)
                {
                    return new List<string>(itemsArray);
                }
            }

            // Check if there are child ProtoItem controls (some GUIs pre-create items)
            if (listBox.Children != null && listBox.Children.Count > 0)
            {
                List<string> childItems = new List<string>();
                foreach (var child in listBox.Children)
                {
                    if (child is GUIProtoItem protoItem && protoItem.GuiText != null && !string.IsNullOrEmpty(protoItem.GuiText.Text))
                    {
                        childItems.Add(protoItem.GuiText.Text);
                    }
                }
                if (childItems.Count > 0)
                {
                    return childItems;
                }
            }

            // No items found
            return new List<string>();
        }

        /// <summary>
        /// Gets the scroll offset (starting item index) for a list box.
        /// Based on swkotor.exe and swkotor2.exe: Scroll offset calculation
        /// </summary>
        private int GetListBoxScrollOffset(GUIListBox listBox, int totalItemCount)
        {
            // Check if scroll offset is stored in Properties
            if (listBox.Properties != null && listBox.Properties.ContainsKey("ScrollOffset"))
            {
                object scrollOffsetObj = listBox.Properties["ScrollOffset"];
                if (scrollOffsetObj is int offset)
                {
                    return Math.Max(0, Math.Min(offset, totalItemCount - 1));
                }
            }

            // Calculate from scrollbar current value if available
            if (listBox.ScrollBar != null && listBox.ScrollBar.CurrentValue.HasValue)
            {
                int scrollValue = listBox.ScrollBar.CurrentValue.Value;
                return Math.Max(0, Math.Min(scrollValue, totalItemCount - 1));
            }

            // Default: start at first item
            return 0;
        }

        /// <summary>
        /// Calculates how many items can fit in the visible area of the list box.
        /// Based on swkotor.exe and swkotor2.exe: Visible item count calculation
        /// </summary>
        private int GetVisibleItemCount(GUIListBox listBox, int totalItemCount, XnaVector2 listBoxSize)
        {
            if (listBox.ProtoItem == null)
            {
                return 0;
            }

            float itemHeight = listBox.ProtoItem.Size.Y > 0 ? listBox.ProtoItem.Size.Y : 20.0f;
            int padding = listBox.Padding;

            // Account for scrollbar width if present
            float availableHeight = listBoxSize.Y;
            if (listBox.ScrollBar != null && !listBox.ScrollBar.Horizontal)
            {
                // Vertical scrollbar takes up space (typically ~20 pixels)
                availableHeight -= 20.0f;
            }

            // Calculate how many items fit
            if (itemHeight + padding <= 0)
            {
                return totalItemCount; // Prevent division by zero
            }

            int visibleCount = (int)Math.Floor((availableHeight + padding) / (itemHeight + padding));
            return Math.Max(0, Math.Min(visibleCount, totalItemCount));
        }

        /// <summary>
        /// Gets the index of the currently selected item in a list box.
        /// Based on swkotor.exe and swkotor2.exe: Selected item tracking
        /// </summary>
        private int GetSelectedListBoxItemIndex(GUIListBox listBox, int totalItemCount)
        {
            // Check if selected index is stored in Properties
            if (listBox.Properties != null && listBox.Properties.ContainsKey("SelectedIndex"))
            {
                object selectedIndexObj = listBox.Properties["SelectedIndex"];
                if (selectedIndexObj is int selectedIndex)
                {
                    if (selectedIndex >= 0 && selectedIndex < totalItemCount)
                    {
                        return selectedIndex;
                    }
                }
            }

            // Check CurrentValue as fallback
            if (listBox.CurrentValue.HasValue)
            {
                int selectedIndex = listBox.CurrentValue.Value;
                if (selectedIndex >= 0 && selectedIndex < totalItemCount)
                {
                    return selectedIndex;
                }
            }

            // No item selected
            return -1;
        }

        /// <summary>
        /// Checks if a list box item at a specific render position is currently highlighted (mouse over).
        /// Based on swkotor.exe and swkotor2.exe: Mouse hover detection
        /// </summary>
        private bool IsListBoxItemHighlighted(GUIListBox listBox, XnaVector2 listBoxPosition, XnaVector2 listBoxSize, int itemIndex, float itemHeight, int padding)
        {
            MouseState currentMouseState = Mouse.GetState();
            int mouseX = currentMouseState.X;
            int mouseY = currentMouseState.Y;

            // Calculate item bounds
            float itemY = listBoxPosition.Y + (itemIndex * (itemHeight + padding));
            float itemBottom = itemY + itemHeight;

            // Account for scrollbar position if present
            float itemLeft = listBoxPosition.X;
            float itemRight = listBoxPosition.X + listBoxSize.X;
            if (listBox.ScrollBar != null && !listBox.ScrollBar.Horizontal && listBox.LeftScrollbar != null && listBox.LeftScrollbar.Value != 0)
            {
                // Scrollbar on left side - adjust item left edge
                itemLeft += 20.0f; // Approximate scrollbar width
            }
            else if (listBox.ScrollBar != null && !listBox.ScrollBar.Horizontal)
            {
                // Scrollbar on right side - adjust item right edge
                itemRight -= 20.0f; // Approximate scrollbar width
            }

            // Check if mouse is within item bounds
            return mouseX >= itemLeft && mouseX <= itemRight && mouseY >= itemY && mouseY <= itemBottom;
        }

        /// <summary>
        /// Renders a proto item control.
        /// Based on swkotor.exe and swkotor2.exe: ProtoItem rendering with state support
        /// Original implementation: ProtoItem renders differently based on selected/highlighted states
        /// </summary>
        private void RenderProtoItem(GUIProtoItem protoItem, XnaVector2 position, XnaVector2 size, string itemText, bool isSelected, bool isHighlighted)
        {
            // Determine which border state to use (normal, hilight, selected, hilight+selected)
            // Priority: hilight+selected > selected > hilight > normal
            GUIBorder borderToUse = protoItem.Border;
            if (isHighlighted && isSelected && protoItem.HilightSelected != null)
            {
                // Item is both highlighted and selected - use hilight+selected border
                borderToUse = ConvertHilightSelectedToBorder(protoItem.HilightSelected);
            }
            else if (isSelected && protoItem.Selected != null)
            {
                // Item is selected (but not highlighted) - use selected border
                borderToUse = ConvertSelectedToBorder(protoItem.Selected);
            }
            else if (isHighlighted && protoItem.Hilight != null)
            {
                // Item is highlighted (but not selected) - use hilight border
                borderToUse = protoItem.Hilight;
            }

            // Render proto item background
            if (borderToUse != null && !borderToUse.Fill.IsBlank())
            {
                Texture2D fillTexture = LoadTexture(borderToUse.Fill.ToString());
                if (fillTexture != null)
                {
                    XnaColor tint = Microsoft.Xna.Framework.Color.White;
                    _spriteBatch.Draw(fillTexture, new XnaRectangle((int)position.X, (int)position.Y, (int)size.X, (int)size.Y), tint);
                }
            }
            else if (protoItem.Border != null && !protoItem.Border.Fill.IsBlank())
            {
                Texture2D fillTexture = LoadTexture(protoItem.Border.Fill.ToString());
                if (fillTexture != null)
                {
                    XnaColor tint = Microsoft.Xna.Framework.Color.White;
                    _spriteBatch.Draw(fillTexture, new XnaRectangle((int)position.X, (int)position.Y, (int)size.X, (int)size.Y), tint);
                }
            }
            else
            {
                // Render solid color background if available
                ParsingColor bgColor = protoItem.Color;
                if (bgColor.A > 0)
                {
                    Texture2D pixel = GetPixelTexture();
                    XnaColor tint = new Microsoft.Xna.Framework.Color(bgColor.R, bgColor.G, bgColor.B, bgColor.A);
                    _spriteBatch.Draw(pixel, new XnaRectangle((int)position.X, (int)position.Y, (int)size.X, (int)size.Y), tint);
                }
            }

            // Render proto item text if available
            if (!string.IsNullOrEmpty(itemText))
            {
                // Use proto item's text properties if available, otherwise use defaults
                Color textColor;
                int alignment;
                ResRef fontResRef;

                if (protoItem.GuiText != null)
                {
                    textColor = new Microsoft.Xna.Framework.Color(
                        protoItem.GuiText.Color.R,
                        protoItem.GuiText.Color.G,
                        protoItem.GuiText.Color.B,
                        protoItem.GuiText.Color.A);
                    alignment = protoItem.GuiText.Alignment;
                    fontResRef = protoItem.GuiText.Font;
                }
                else
                {
                    // Default text properties
                    textColor = Microsoft.Xna.Framework.Color.White;
                    alignment = 1; // Left align
                    fontResRef = protoItem.Font;
                }

                // Load font
                BaseBitmapFont font = LoadFont(fontResRef.ToString());
                if (font != null)
                {
                    // Measure text size
                    System.Numerics.Vector2 textSize = font.MeasureString(itemText);

                    // Calculate text position based on alignment (convert to Graphics.Vector2)
                    GraphicsVector2 textPos = CalculateTextPosition(alignment, 
                        new GraphicsVector2(position.X, position.Y), 
                        new GraphicsVector2(size.X, size.Y), 
                        new GraphicsVector2(textSize.X, textSize.Y));

                    // Render text using bitmap font (convert Graphics.Vector2 to XnaVector2)
                    RenderBitmapText(font, itemText, new XnaVector2(textPos.X, textPos.Y), textColor);
                }
            }
        }

        /// <summary>
        /// Renders the scrollbar for a list box.
        /// Based on swkotor.exe and swkotor2.exe: ListBox scrollbar rendering
        /// </summary>
        private void RenderListBoxScrollbar(GUIListBox listBox, XnaVector2 listBoxPosition, XnaVector2 listBoxSize, int totalItemCount, int visibleItemCount)
        {
            if (listBox.ScrollBar == null || totalItemCount <= visibleItemCount)
            {
                return;
            }

            GUIScrollbar scrollBar = listBox.ScrollBar;

            // Determine scrollbar position (left or right)
            bool isLeftScrollbar = listBox.LeftScrollbar.HasValue && listBox.LeftScrollbar.Value != 0;
            float scrollbarX = isLeftScrollbar ? listBoxPosition.X : listBoxPosition.X + listBoxSize.X - 20.0f; // Approximate scrollbar width
            float scrollbarY = listBoxPosition.Y;
            float scrollbarWidth = 20.0f; // Approximate scrollbar width
            float scrollbarHeight = listBoxSize.Y;

            XnaVector2 scrollbarPosition = new XnaVector2(scrollbarX, scrollbarY);
            XnaVector2 scrollbarSize = new XnaVector2(scrollbarWidth, scrollbarHeight);

            // Render scrollbar background if available
            if (scrollBar.Border != null && !scrollBar.Border.Fill.IsBlank())
            {
                Texture2D scrollbarBgTexture = LoadTexture(scrollBar.Border.Fill.ToString());
                if (scrollbarBgTexture != null)
                {
                    XnaColor tint = Microsoft.Xna.Framework.Color.White;
                    _spriteBatch.Draw(scrollbarBgTexture, new XnaRectangle((int)scrollbarPosition.X, (int)scrollbarPosition.Y, (int)scrollbarSize.X, (int)scrollbarSize.Y), tint);
                }
            }

            // Render scrollbar thumb
            if (scrollBar.GuiThumb != null && !scrollBar.GuiThumb.Image.IsBlank())
            {
                Texture2D thumbTexture = LoadTexture(scrollBar.GuiThumb.Image.ToString());
                if (thumbTexture != null)
                {
                    // Calculate thumb position based on current scroll value
                    int currentValue = scrollBar.CurrentValue ?? 0;
                    int maxValue = Math.Max(1, totalItemCount - visibleItemCount);
                    float scrollRatio = maxValue > 0 ? (float)currentValue / maxValue : 0.0f;
                    scrollRatio = Math.Max(0.0f, Math.Min(1.0f, scrollRatio)); // Clamp to [0, 1]

                    // Calculate thumb height based on visible vs total items ratio
                    float thumbHeight = Math.Max(10.0f, scrollbarSize.Y * (visibleItemCount / (float)totalItemCount));
                    float availableTrackHeight = scrollbarSize.Y - thumbHeight;
                    float thumbY = scrollbarPosition.Y + (scrollRatio * availableTrackHeight);

                    // Render thumb
                    XnaColor thumbTint = Microsoft.Xna.Framework.Color.White;
                    XnaRectangle thumbRect = new XnaRectangle((int)scrollbarPosition.X, (int)thumbY, (int)scrollbarSize.X, (int)thumbHeight);
                    _spriteBatch.Draw(thumbTexture, thumbRect, thumbTint);
                }
            }

            // Render scrollbar direction arrows if available
            if (scrollBar.GuiDirection != null && !scrollBar.GuiDirection.Image.IsBlank())
            {
                Texture2D arrowTexture = LoadTexture(scrollBar.GuiDirection.Image.ToString());
                if (arrowTexture != null)
                {
                    // Render up arrow (top of scrollbar)
                    float upArrowSize = Math.Min(20.0f, scrollbarSize.Y * 0.1f);
                    XnaRectangle upArrowRect = new XnaRectangle((int)scrollbarPosition.X, (int)scrollbarPosition.Y, (int)scrollbarSize.X, (int)upArrowSize);
                    _spriteBatch.Draw(arrowTexture, upArrowRect, Microsoft.Xna.Framework.Color.White);

                    // Render down arrow (bottom of scrollbar)
                    float downArrowY = scrollbarPosition.Y + scrollbarSize.Y - upArrowSize;
                    XnaRectangle downArrowRect = new XnaRectangle((int)scrollbarPosition.X, (int)downArrowY, (int)scrollbarSize.X, (int)upArrowSize);
                    _spriteBatch.Draw(arrowTexture, downArrowRect, Microsoft.Xna.Framework.Color.White);
                }
            }
        }

        /// <summary>
        /// Renders a progress bar control.
        /// </summary>
        private void RenderProgressBar(GUIProgressBar progressBar, XnaVector2 position, XnaVector2 size)
        {
            // Render progress bar background
            if (progressBar.Border != null && !progressBar.Border.Fill.IsBlank())
            {
                Texture2D fillTexture = LoadTexture(progressBar.Border.Fill.ToString());
                if (fillTexture != null)
                {
                    XnaColor tint = Microsoft.Xna.Framework.Color.White;
                    _spriteBatch.Draw(fillTexture, new XnaRectangle((int)position.X, (int)position.Y, (int)size.X, (int)size.Y), tint);
                }
            }

            // Render progress fill
            if (progressBar.Progress != null && progressBar.MaxValue > 0)
            {
                float progress = (float)progressBar.CurrentValue / progressBar.MaxValue;
                int fillWidth = (int)(size.X * progress);

                if (fillWidth > 0 && progressBar.Progress.Fill != null && !progressBar.Progress.Fill.IsBlank())
                {
                    Texture2D progressTexture = LoadTexture(progressBar.Progress.Fill.ToString());
                    if (progressTexture != null)
                    {
                        XnaColor tint = Microsoft.Xna.Framework.Color.White;
                        _spriteBatch.Draw(progressTexture, new XnaRectangle((int)position.X, (int)position.Y, fillWidth, (int)size.Y), tint);
                    }
                }
            }
        }

        /// <summary>
        /// Renders a checkbox control.
        /// </summary>
        private void RenderCheckBox(GUICheckBox checkBox, XnaVector2 position, XnaVector2 size)
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
            if (borderToUse != null && !borderToUse.Fill.IsBlank())
            {
                Texture2D fillTexture = LoadTexture(borderToUse.Fill.ToString());
                if (fillTexture != null)
                {
                    XnaColor tint = Microsoft.Xna.Framework.Color.White;
                    _spriteBatch.Draw(fillTexture, new XnaRectangle((int)position.X, (int)position.Y, (int)size.X, (int)size.Y), tint);
                }
            }
            else if (checkBox.Border != null && !checkBox.Border.Fill.IsBlank())
            {
                Texture2D fillTexture = LoadTexture(checkBox.Border.Fill.ToString());
                if (fillTexture != null)
                {
                    XnaColor tint = Microsoft.Xna.Framework.Color.White;
                    _spriteBatch.Draw(fillTexture, new XnaRectangle((int)position.X, (int)position.Y, (int)size.X, (int)size.Y), tint);
                }
            }
            else
            {
                // Render solid color background
                Color bgColor = checkBox.Color;
                if (bgColor.A > 0)
                {
                    Texture2D pixel = GetPixelTexture();
                    XnaColor tint = new Microsoft.Xna.Framework.Color(bgColor.R, bgColor.G, bgColor.B, bgColor.A);
                    _spriteBatch.Draw(pixel, new XnaRectangle((int)position.X, (int)position.Y, (int)size.X, (int)size.Y), tint);
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
        private void RenderCheckmark(GUICheckBox checkBox, XnaVector2 position, XnaVector2 size)
        {
            // Try to load checkmark texture from Selected or HilightSelected if available
            Texture2D checkmarkTexture = null;

            if (checkBox.Selected != null && !checkBox.Selected.Fill.IsBlank())
            {
                // Try Selected.Fill as checkmark texture
                checkmarkTexture = LoadTexture(checkBox.Selected.Fill.ToString());
            }

            if (checkmarkTexture == null && checkBox.HilightSelected != null && !checkBox.HilightSelected.Fill.IsBlank())
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

                XnaColor tint = Microsoft.Xna.Framework.Color.White;
                _spriteBatch.Draw(checkmarkTexture, new XnaRectangle(checkmarkX, checkmarkY, checkmarkSize, checkmarkSize), tint);
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
                XnaVector2 point1 = new XnaVector2(centerX - offset, centerY);
                XnaVector2 point2 = new XnaVector2(centerX - offset * 0.3f, centerY + offset * 0.5f);
                XnaVector2 point3 = new XnaVector2(centerX + offset, centerY - offset * 0.5f);

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
        private void DrawLine(Texture2D pixel, XnaVector2 start, XnaVector2 end, float thickness, XnaColor color)
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
            XnaRectangle sourceRect = new XnaRectangle(0, 0, 1, 1);
            XnaVector2 origin = new XnaVector2(0.5f, 0.5f);
            XnaVector2 scale = new XnaVector2(length, thickness);

            _spriteBatch.Draw(
                pixel,
                start,
                sourceRect,
                color,
                angle,
                origin,
                scale,
                XnaSpriteEffects.None,
                0);
        }

        /// <summary>
        /// Renders a slider control.
        /// Based on swkotor.exe and swkotor2.exe: Slider rendering with thumb positioning
        /// Original implementation: Slider thumb position calculated from CURVALUE/MAXVALUE ratio
        /// Thumb position = (CURVALUE / MAXVALUE) × track length
        /// </summary>
        private void RenderSlider(GUISlider slider, XnaVector2 position, XnaVector2 size)
        {
            // Render slider track
            if (slider.Border != null && !slider.Border.Fill.IsBlank())
            {
                Texture2D fillTexture = LoadTexture(slider.Border.Fill.ToString());
                if (fillTexture != null)
                {
                    XnaColor tint = Microsoft.Xna.Framework.Color.White;
                    _spriteBatch.Draw(fillTexture, new XnaRectangle((int)position.X, (int)position.Y, (int)size.X, (int)size.Y), tint);
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

            if (thumb == null || thumb.Image.IsBlank())
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

                thumbPosition = new XnaVector2(thumbX, thumbY);
                thumbSize = new XnaVector2(thumbWidth, thumbTexture.Height);
            }
            else
            {
                // Vertical slider: thumb moves top-bottom
                float thumbHeight = Math.Min(size.Y * 0.1f, 20.0f); // 10% of track height or max 20 pixels
                float trackLength = size.Y - thumbHeight; // Available space for thumb movement
                float thumbX = position.X + (size.X - thumbTexture.Width) / 2.0f; // Center horizontally
                float thumbY = position.Y + (normalizedPosition * trackLength);

                thumbPosition = new XnaVector2(thumbX, thumbY);
                thumbSize = new XnaVector2(thumbTexture.Width, thumbHeight);
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
            XnaSpriteEffects spriteEffects = XnaSpriteEffects.None;
            if (thumb.FlipStyle.HasValue)
            {
                // FlipStyle: 0=none, 1=horizontal, 2=vertical, 3=both
                int flipStyle = thumb.FlipStyle.Value;
                if ((flipStyle & 1) != 0)
                {
                    spriteEffects |= XnaSpriteEffects.FlipHorizontally;
                }
                if ((flipStyle & 2) != 0)
                {
                    spriteEffects |= XnaSpriteEffects.FlipVertically;
                }
            }

            // Render thumb with optional rotation and flip
            if (rotation != 0.0f)
            {
                // Render with rotation
                XnaVector2 thumbOrigin = new XnaVector2(thumbTexture.Width / 2.0f, thumbTexture.Height / 2.0f);
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
        private void RenderGenericControl(GUIControl control, XnaVector2 position, XnaVector2 size)
        {
            // Render background if border is available
            if (control.Border != null && !control.Border.Fill.IsBlank())
            {
                Texture2D fillTexture = LoadTexture(control.Border.Fill.ToString());
                if (fillTexture != null)
                {
                    XnaColor tint = Microsoft.Xna.Framework.Color.White;
                    _spriteBatch.Draw(fillTexture, new XnaRectangle((int)position.X, (int)position.Y, (int)size.X, (int)size.Y), tint);
                }
            }
            else if (control.Color.A > 0)
            {
                // Render solid color background
                Texture2D pixel = GetPixelTexture();
                XnaColor tint = new Microsoft.Xna.Framework.Color(control.Color.R, control.Color.G, control.Color.B, control.Color.A);
                _spriteBatch.Draw(pixel, new XnaRectangle((int)position.X, (int)position.Y, (int)size.X, (int)size.Y), tint);
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

                // Parse TPC from resource data
                // Based on swkotor.exe and swkotor2.exe: GUI texture loading pattern
                // Original engine loads TPC textures for GUI elements (buttons, panels, backgrounds)
                // TPC format: BioWare texture format supporting DXT1/DXT3/DXT5 compression, RGB/RGBA, grayscale
                // Original implementation: Uses DirectX texture creation APIs (D3DXCreateTextureFromFileInMemory)
                // This MonoGame implementation: Uses TpcToMonoGameTextureConverter to convert TPC to Texture2D
                TPC tpc = TPCAuto.ReadTpc(resourceResult.Data);
                if (tpc == null || tpc.Layers.Count == 0 || tpc.Layers[0].Mipmaps.Count == 0)
                {
                    Console.WriteLine($"[KotorGuiManager] ERROR: Failed to parse TPC texture: {textureName}");
                    return null;
                }

                // Convert TPC to MonoGame Texture2D
                // GUI textures are always 2D (not cube maps), so set generateMipmaps to false for better performance
                // Based on swkotor.exe and swkotor2.exe: GUI textures loaded without mipmaps for immediate rendering
                // Original engine: DirectX GUI textures created with D3DX_DEFAULT (no mipmap generation for GUI)
                Texture convertedTexture = TpcToMonoGameTextureConverter.Convert(tpc, _graphicsDevice, false);
                if (convertedTexture is TextureCube)
                {
                    Console.WriteLine($"[KotorGuiManager] ERROR: GUI texture cannot be a cube map: {textureName}");
                    return null;
                }

                Texture2D texture2D = convertedTexture as Texture2D;
                if (texture2D == null)
                {
                    Console.WriteLine($"[KotorGuiManager] ERROR: Failed to convert texture to Texture2D: {textureName}");
                    return null;
                }

                // Cache the converted texture
                _textureCache[key] = texture2D;
                return texture2D;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[KotorGuiManager] ERROR loading texture {textureName}: {ex.Message}");
                Console.WriteLine($"[KotorGuiManager] Stack trace: {ex.StackTrace}");
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
            if (_fontCache.TryGetValue(key, out BaseBitmapFont cached))
            {
                return cached;
            }

            // Load font using reflection to avoid circular dependency
            BaseBitmapFont font = null;
            try
            {
                System.Type odysseyFontType = System.Type.GetType("Andastra.Runtime.Games.Odyssey.Fonts.OdysseyBitmapFont, Andastra.Runtime.Games.Odyssey");
                if (odysseyFontType != null)
                {
                    System.Reflection.MethodInfo loadMethod = odysseyFontType.GetMethod("Load", new System.Type[] { typeof(string), typeof(Installation), typeof(GraphicsDevice) });
                    if (loadMethod != null)
                    {
                        object fontObj = loadMethod.Invoke(null, new object[] { fontResRef, _installation, _graphicsDevice });
                        font = fontObj as BaseBitmapFont;
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[KotorGuiManager] ERROR: Failed to load font {fontResRef} via reflection: {ex.Message}");
            }

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
        private void RenderBitmapText([NotNull] BaseBitmapFont font, string text, XnaVector2 position, XnaColor color)
        {
            if (font == null || string.IsNullOrEmpty(text))
            {
                return;
            }

            // Get MonoGame texture from font for rendering
            MonoGameTexture2D mgTexture = font.Texture as MonoGameTexture2D;
            if (mgTexture == null)
            {
                return;
            }
            Texture2D fontTexture = mgTexture.Texture;

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
                        fontTexture,
                        new XnaRectangle((int)currentX, (int)currentY, (int)g.Width, (int)g.Height),
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
            public GuiResource Gui { get; set; }
            public string Name { get; set; }
            public int Width { get; set; }
            public int Height { get; set; }
            public Dictionary<string, GUIControl> ControlMap { get; set; }
            public Dictionary<string, GUIButton> ButtonMap { get; set; }
            public Dictionary<string, GUICheckBox> CheckBoxMap { get; set; }
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

