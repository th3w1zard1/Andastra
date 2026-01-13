using System;
using System.Collections.Generic;
using Andastra.Runtime.Core;
using Andastra.Runtime.Core.Audio;
using Andastra.Runtime.Core.Dialogue;
using Andastra.Runtime.Graphics;

namespace Andastra.Game
{
    /// <summary>
    /// Handles options menu rendering and input.
    /// </summary>
    /// <remarks>
    /// Options Menu:
    /// - Based on swkotor.exe and swkotor2.exe options menu system
    /// - Located via string references: "BTN_OPTIONS" (options button in main menu)
    /// - GUI Panel: "options" (options menu panel)
    /// - Settings categories: Graphics (resolution, fullscreen), Audio (volume), Game (skip intro, debug)
    /// - Original implementation: Options menu allows configuration of game settings
    /// - Settings persistence: Settings saved to swkotor.ini (K1) or swkotor2.ini (K2)
    /// - Function: 0x00633270 @ 0x00633270 (loads configuration from INI file in swkotor2.exe)
    /// - Function: 0x00631ff0 @ 0x00631ff0 (writes INI values in swkotor2.exe)
    /// </remarks>
    public static class OptionsMenu
    {
        /// <summary>
        /// Options menu category.
        /// </summary>
        public enum OptionsCategory
        {
            Graphics,
            Audio,
            Game,
            Feedback,
            Autopause,
            Controls
        }

        /// <summary>
        /// Updates options menu state and handles input.
        /// </summary>
        public static void UpdateOptionsMenu(
            float deltaTime,
            IKeyboardState currentKeyboard,
            IKeyboardState previousKeyboard,
            IMouseState currentMouse,
            IMouseState previousMouse,
            ref int selectedCategoryIndex,
            ref int selectedOptionIndex,
            ref bool isEditingValue,
            ref string editingValue,
            ref bool isRebindingKey,
            ref string rebindingActionName,
            GameSettings settings,
            Dictionary<OptionsCategory, List<OptionItem>> optionsByCategory,
            Action<GameSettings> onApply,
            Action onCancel)
        {
            if (isRebindingKey)
            {
                // Handle key binding rebinding - wait for any key press
                HandleKeyBindingRebind(currentKeyboard, previousKeyboard, ref isRebindingKey, rebindingActionName, optionsByCategory, selectedCategoryIndex, selectedOptionIndex);
                return;
            }

            if (isEditingValue)
            {
                // Handle text input for numeric values
                HandleValueInput(currentKeyboard, previousKeyboard, ref editingValue, ref isEditingValue);
                return;
            }

            // Handle ESC to cancel
            if (IsKeyJustPressed(previousKeyboard, currentKeyboard, Keys.Escape))
            {
                onCancel();
                return;
            }

            // Handle Enter to apply
            if (IsKeyJustPressed(previousKeyboard, currentKeyboard, Keys.Enter))
            {
                ApplySettings(settings, optionsByCategory);
                onApply(settings);
                return;
            }

            // Category navigation (Left/Right)
            if (IsKeyJustPressed(previousKeyboard, currentKeyboard, Keys.Left))
            {
                selectedCategoryIndex = Math.Max(0, selectedCategoryIndex - 1);
                selectedOptionIndex = 0;
            }
            if (IsKeyJustPressed(previousKeyboard, currentKeyboard, Keys.Right))
            {
                selectedCategoryIndex = Math.Min(optionsByCategory.Count - 1, selectedCategoryIndex + 1);
                selectedOptionIndex = 0;
            }

            // Option navigation (Up/Down)
            if (IsKeyJustPressed(previousKeyboard, currentKeyboard, Keys.Up))
            {
                selectedOptionIndex = Math.Max(0, selectedOptionIndex - 1);
            }
            if (IsKeyJustPressed(previousKeyboard, currentKeyboard, Keys.Down))
            {
                OptionsCategory currentCategory = (OptionsCategory)selectedCategoryIndex;
                int maxOptions = optionsByCategory[currentCategory].Count;
                selectedOptionIndex = Math.Min(maxOptions - 1, selectedOptionIndex + 1);
            }

            // Option value modification (Left/Right on option value)
            if (IsKeyJustPressed(previousKeyboard, currentKeyboard, Keys.A) ||
                IsKeyJustPressed(previousKeyboard, currentKeyboard, Keys.D))
            {
                OptionsCategory currentCategory = (OptionsCategory)selectedCategoryIndex;
                if (selectedOptionIndex >= 0 && selectedOptionIndex < optionsByCategory[currentCategory].Count)
                {
                    OptionItem option = optionsByCategory[currentCategory][selectedOptionIndex];
                    ModifyOptionValue(option, IsKeyJustPressed(previousKeyboard, currentKeyboard, Keys.D));
                }
            }

            // Enter to edit numeric values or start key binding rebind
            if (IsKeyJustPressed(previousKeyboard, currentKeyboard, Keys.Enter) &&
                selectedCategoryIndex >= 0 && selectedCategoryIndex < optionsByCategory.Count)
            {
                OptionsCategory currentCategory = (OptionsCategory)selectedCategoryIndex;
                if (selectedOptionIndex >= 0 && selectedOptionIndex < optionsByCategory[currentCategory].Count)
                {
                    OptionItem option = optionsByCategory[currentCategory][selectedOptionIndex];
                    if (option.Type == OptionType.Numeric)
                    {
                        isEditingValue = true;
                        editingValue = option.GetStringValue();
                    }
                    else if (option.Type == OptionType.KeyBinding)
                    {
                        // Start key binding rebind
                        isRebindingKey = true;
                        rebindingActionName = option.Name;
                    }
                    else if (option.Type == OptionType.MouseButtonBinding)
                    {
                        // Start mouse button binding rebind
                        isRebindingKey = true;
                        rebindingActionName = option.Name;
                    }
                }
            }
        }

        /// <summary>
        /// Handles key binding rebinding input.
        /// </summary>
        /// <remarks>
        /// Key Binding Rebind Handler:
        /// - Based on swkotor.exe and swkotor2.exe key binding system
        /// - Original implementation: When rebinding a key, waits for next key press and assigns it to the action
        /// - [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): Key binding UI captures next key press and updates binding
        /// </remarks>
        private static void HandleKeyBindingRebind(
            IKeyboardState currentKeyboard,
            IKeyboardState previousKeyboard,
            ref bool isRebindingKey,
            string rebindingActionName,
            Dictionary<OptionsCategory, List<OptionItem>> optionsByCategory,
            int selectedCategoryIndex,
            int selectedOptionIndex)
        {
            // Check for Escape to cancel rebinding
            if (IsKeyJustPressed(previousKeyboard, currentKeyboard, Keys.Escape))
            {
                isRebindingKey = false;
                return;
            }

            // Check for any key press (excluding Escape which we already handled)
            Keys[] pressedKeys = currentKeyboard.GetPressedKeys();
            foreach (Keys key in pressedKeys)
            {
                if (!previousKeyboard.IsKeyDown(key) && key != Keys.Escape)
                {
                    // Found a new key press - assign it to the binding
                    OptionsCategory currentCategory = (OptionsCategory)selectedCategoryIndex;
                    if (selectedOptionIndex >= 0 && selectedOptionIndex < optionsByCategory[currentCategory].Count)
                    {
                        OptionItem option = optionsByCategory[currentCategory][selectedOptionIndex];
                        if (option is KeyBindingOptionItem keyBindingOption)
                        {
                            string keyName = key.ToString();
                            keyBindingOption.SetKeyName(keyName);
                        }
                        else if (option is MouseButtonBindingOptionItem mouseButtonOption)
                        {
                            // For mouse buttons, we need to check mouse state
                            // This is a simplified version - in a full implementation, we'd wait for mouse button press
                            // For now, we'll skip mouse button rebinding via keyboard
                        }
                    }
                    isRebindingKey = false;
                    return;
                }
            }
        }

        /// <summary>
        /// Draws the options menu.
        /// </summary>
        public static void DrawOptionsMenu(
            ISpriteBatch spriteBatch,
            IFont font,
            ITexture2D menuTexture,
            IGraphicsDevice graphicsDevice,
            int viewportWidth,
            int viewportHeight,
            int selectedCategoryIndex,
            int selectedOptionIndex,
            bool isEditingValue,
            string editingValue,
            bool isRebindingKey,
            string rebindingActionName,
            Dictionary<OptionsCategory, List<OptionItem>> optionsByCategory)
        {
            // Clear to dark background
            graphicsDevice.Clear(new Color(20, 20, 30, 255));

            spriteBatch.Begin();

            // Title
            string title = "Options";
            Vector2 titleSize = font.MeasureString(title);
            Vector2 titlePos = new Vector2((viewportWidth - titleSize.X) / 2, 30);
            spriteBatch.DrawString(font, title, titlePos, new Color(255, 255, 255, 255));

            // Category tabs
            int categoryStartX = 50;
            int categoryY = 80;
            int categoryWidth = 150;
            int categoryHeight = 40;
            int categorySpacing = 10;

            OptionsCategory[] categories = (OptionsCategory[])Enum.GetValues(typeof(OptionsCategory));
            for (int i = 0; i < categories.Length; i++)
            {
                int x = categoryStartX + i * (categoryWidth + categorySpacing);
                bool isSelected = (i == selectedCategoryIndex);
                Color bgColor = isSelected ? new Color(100, 100, 150) : new Color(50, 50, 70);
                Rectangle categoryRect = new Rectangle(x, categoryY, categoryWidth, categoryHeight);
                spriteBatch.Draw(menuTexture, categoryRect, bgColor);

                string categoryName = categories[i].ToString();
                Vector2 categoryNameSize = font.MeasureString(categoryName);
                Vector2 categoryNamePos = new Vector2(
                    x + (categoryWidth - categoryNameSize.X) / 2,
                    categoryY + (categoryHeight - font.LineSpacing) / 2);
                spriteBatch.DrawString(font, categoryName, categoryNamePos, new Color(255, 255, 255, 255));
            }

            // Options list
            if (selectedCategoryIndex >= 0 && selectedCategoryIndex < categories.Length)
            {
                OptionsCategory currentCategory = categories[selectedCategoryIndex];
                List<OptionItem> options = optionsByCategory[currentCategory];

                int optionsStartX = 50;
                int optionsStartY = categoryY + categoryHeight + 30;
                int optionWidth = viewportWidth - 100;
                int optionHeight = 50;
                int optionSpacing = 5;
                int maxVisible = (viewportHeight - optionsStartY - 100) / (optionHeight + optionSpacing);
                int startIdx = Math.Max(0, selectedOptionIndex - maxVisible / 2);
                int endIdx = Math.Min(options.Count, startIdx + maxVisible);

                for (int i = startIdx; i < endIdx; i++)
                {
                    int y = optionsStartY + (i - startIdx) * (optionHeight + optionSpacing);
                    bool isSelected = (i == selectedOptionIndex);
                    Color bgColor = isSelected ? new Color(80, 80, 120) : new Color(40, 40, 60);
                    Rectangle optionRect = new Rectangle(optionsStartX, y, optionWidth, optionHeight);
                    spriteBatch.Draw(menuTexture, optionRect, bgColor);

                    OptionItem option = options[i];
                    string valueText;
                    if (isRebindingKey && i == selectedOptionIndex && rebindingActionName == option.Name)
                    {
                        // Show "Press any key..." when rebinding
                        valueText = "Press any key...";
                    }
                    else if (isEditingValue && i == selectedOptionIndex)
                    {
                        valueText = editingValue + "_";
                    }
                    else
                    {
                        valueText = option.GetStringValue();
                    }
                    string optionText = option.Name + ": " + valueText;
                    Vector2 textPos = new Vector2(optionRect.X + 20, optionRect.Y + (optionHeight - font.LineSpacing) / 2);
                    spriteBatch.DrawString(font, optionText, textPos, new Color(255, 255, 255, 255));

                    // Draw arrows for navigation hint
                    if (isSelected && option.Type != OptionType.Numeric)
                    {
                        string arrows = "< A/D >";
                        Vector2 arrowsSize = font.MeasureString(arrows);
                        Vector2 arrowsPos = new Vector2(optionRect.Right - arrowsSize.X - 20, optionRect.Y + (optionHeight - font.LineSpacing) / 2);
                        spriteBatch.DrawString(font, arrows, arrowsPos, new Color(200, 200, 200, 255));
                    }
                }
            }

            // Instructions
            string instructions;
            if (isRebindingKey)
            {
                instructions = "Press any key to bind, or Escape to cancel";
            }
            else if (isEditingValue)
            {
                instructions = "Enter new value, then press Enter to confirm or Escape to cancel";
            }
            else
            {
                instructions = "Use Arrow Keys to navigate, A/D to change values, Enter to rebind keys, Escape to cancel";
            }
            Vector2 instSize = font.MeasureString(instructions);
            Vector2 instPos = new Vector2((viewportWidth - instSize.X) / 2, viewportHeight - 40);
            spriteBatch.DrawString(font, instructions, instPos, new Color(211, 211, 211, 255));

            spriteBatch.End();
        }

        /// <summary>
        /// Creates default options structure.
        /// </summary>
        /// <summary>
        /// Creates default options structure.
        /// </summary>
        /// <remarks>
        /// Audio Volume Implementation:
        /// - Master Volume: Applied to all audio via ISoundPlayer.SetMasterVolume() and IMusicPlayer.Volume
        /// - Music Volume: Stored in GameSettings.Audio.MusicVolume (applied when music is played)
        /// - Effects Volume: Stored in GameSettings.Audio.SfxVolume (applied when sounds are played)
        /// - Voice Volume: Stored in GameSettings.Audio.VoiceVolume (applied when voice-overs are played)
        /// - [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): 0x00631ff0 @ 0x00631ff0 (writes INI values for audio settings)
        /// - [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): 0x00633270 @ 0x00633270 (loads audio settings from INI file)
        /// </remarks>
        public static Dictionary<OptionsCategory, List<OptionItem>> CreateDefaultOptions(GameSettings settings, ISoundPlayer soundPlayer = null, IMusicPlayer musicPlayer = null, Runtime.Core.Audio.IVoicePlayer voicePlayer = null)
        {
            var options = new Dictionary<OptionsCategory, List<OptionItem>>();

            // Graphics options
            // Based on swkotor.exe and swkotor2.exe: Graphics options menu (swkotor2.exe: CSWGuiOptionsMain @ 0x006e3e80)
            // Original implementation: Graphics options include Resolution, Texture Quality, Shadow Quality, VSync, Fullscreen
            // VSync: Controlled via DirectX Present parameters (swkotor2.exe: DirectX device presentation)
            // VSync synchronizes frame rendering with monitor refresh rate to prevent screen tearing
            var graphicsOptions = new List<OptionItem>
            {
                new OptionItem("Window Width", OptionType.Numeric, () => settings.Width, v => settings.Width = (int)v, 320, 7680),
                new OptionItem("Window Height", OptionType.Numeric, () => settings.Height, v => settings.Height = (int)v, 240, 4320),
                new OptionItem("Fullscreen", OptionType.Boolean, () => settings.Fullscreen ? 1 : 0, v => settings.Fullscreen = v > 0, 0, 1),
                new OptionItem("VSync", OptionType.Boolean,
                    () => (settings.Graphics != null && settings.Graphics.VSync) ? 1 : 0,
                    v =>
                    {
                        if (settings.Graphics != null)
                        {
                            settings.Graphics.VSync = v > 0;
                        }
                    }, 0, 1),
                new OptionItem("Debug Render", OptionType.Boolean, () => settings.DebugRender ? 1 : 0, v => settings.DebugRender = v > 0, 0, 1)
            };
            options[OptionsCategory.Graphics] = graphicsOptions;

            // Audio options - based on swkotor2.exe audio configuration system
            var audioOptions = new List<OptionItem>
            {
                new OptionItem("Master Volume", OptionType.Numeric, () => (int)(settings.Audio.MasterVolume * 100.0f),
                    v =>
                    {
                        settings.Audio.MasterVolume = (float)v / 100.0f;
                        // Apply master volume to all audio systems immediately if available
                        if (soundPlayer != null)
                        {
                            soundPlayer.SetMasterVolume(settings.Audio.MasterVolume);
                        }
                        if (musicPlayer != null)
                        {
                            musicPlayer.Volume = settings.Audio.MasterVolume * settings.Audio.MusicVolume;
                        }
                    }, 0, 100),
                new OptionItem("Music Volume", OptionType.Numeric, () => (int)(settings.Audio.MusicVolume * 100.0f), v =>
                {
                    float volume = (float)v / 100.0f;
                    settings.Audio.MusicVolume = volume;
                    // Apply volume to music player immediately if available
                    if (musicPlayer != null)
                    {
                        musicPlayer.Volume = volume;
                    }
                }, 0, 100),
                new OptionItem("SFX Volume", OptionType.Numeric, () => (int)(settings.Audio.SfxVolume * 100.0f),
                    v =>
                    {
                        float volume = (float)v / 100.0f;
                        settings.Audio.SfxVolume = volume;
                        // Apply SFX volume to sound player immediately if available
                        if (soundPlayer != null)
                        {
                            soundPlayer.SetMasterVolume(volume);
                        }
                    }, 0, 100),
                new OptionItem("Voice Volume", OptionType.Numeric, () => (int)(settings.Audio.VoiceVolume * 100.0f),
                    v =>
                    {
                        float volume = (float)v / 100.0f;
                        settings.Audio.VoiceVolume = volume;
                        // Apply voice volume to voice player immediately if available
                        // [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): VoiceVolume setting applied to voice-over playback
                        if (voicePlayer != null)
                        {
                            voicePlayer.SetMasterVolume(volume);
                        }
                    }, 0, 100)
            };
            options[OptionsCategory.Audio] = audioOptions;

            // Game options
            var gameOptions = new List<OptionItem>
            {
                new OptionItem("Skip Intro", OptionType.Boolean, () => settings.SkipIntro ? 1 : 0, v => settings.SkipIntro = v > 0, 0, 1)
            };
            options[OptionsCategory.Game] = gameOptions;

            // Feedback options - based on swkotor2.exe interface/feedback options
            var feedbackOptions = new List<OptionItem>
            {
                new OptionItem("Show Damage Numbers", OptionType.Boolean, () => settings.Feedback.ShowDamageNumbers ? 1 : 0, v => settings.Feedback.ShowDamageNumbers = v > 0, 0, 1),
                new OptionItem("Show Hit/Miss Feedback", OptionType.Boolean, () => settings.Feedback.ShowHitMissFeedback ? 1 : 0, v => settings.Feedback.ShowHitMissFeedback = v > 0, 0, 1),
                new OptionItem("Show Subtitles", OptionType.Boolean, () => settings.Feedback.ShowSubtitles ? 1 : 0, v => settings.Feedback.ShowSubtitles = v > 0, 0, 1),
                new OptionItem("Show Action Queue", OptionType.Boolean, () => settings.Feedback.ShowActionQueue ? 1 : 0, v => settings.Feedback.ShowActionQueue = v > 0, 0, 1),
                new OptionItem("Show Minimap", OptionType.Boolean, () => settings.Feedback.ShowMinimap ? 1 : 0, v => settings.Feedback.ShowMinimap = v > 0, 0, 1),
                new OptionItem("Show Party Health Bars", OptionType.Boolean, () => settings.Feedback.ShowPartyHealthBars ? 1 : 0, v => settings.Feedback.ShowPartyHealthBars = v > 0, 0, 1),
                new OptionItem("Show Floating Combat Text", OptionType.Boolean, () => settings.Feedback.ShowFloatingCombatText ? 1 : 0, v => settings.Feedback.ShowFloatingCombatText = v > 0, 0, 1)
            };
            options[OptionsCategory.Feedback] = feedbackOptions;

            // Autopause options - based on swkotor2.exe autopause system
            var autopauseOptions = new List<OptionItem>
            {
                new OptionItem("Pause on Lost Focus", OptionType.Boolean, () => settings.Autopause.PauseOnLostFocus ? 1 : 0, v => settings.Autopause.PauseOnLostFocus = v > 0, 0, 1),
                new OptionItem("Pause on Conversation", OptionType.Boolean, () => settings.Autopause.PauseOnConversation ? 1 : 0, v => settings.Autopause.PauseOnConversation = v > 0, 0, 1),
                new OptionItem("Pause on Container", OptionType.Boolean, () => settings.Autopause.PauseOnContainer ? 1 : 0, v => settings.Autopause.PauseOnContainer = v > 0, 0, 1),
                new OptionItem("Pause on Corpse", OptionType.Boolean, () => settings.Autopause.PauseOnCorpse ? 1 : 0, v => settings.Autopause.PauseOnCorpse = v > 0, 0, 1),
                new OptionItem("Pause on Area Transition", OptionType.Boolean, () => settings.Autopause.PauseOnAreaTransition ? 1 : 0, v => settings.Autopause.PauseOnAreaTransition = v > 0, 0, 1),
                new OptionItem("Pause on Party Death", OptionType.Boolean, () => settings.Autopause.PauseOnPartyDeath ? 1 : 0, v => settings.Autopause.PauseOnPartyDeath = v > 0, 0, 1),
                new OptionItem("Pause on Player Death", OptionType.Boolean, () => settings.Autopause.PauseOnPlayerDeath ? 1 : 0, v => settings.Autopause.PauseOnPlayerDeath = v > 0, 0, 1)
            };
            options[OptionsCategory.Autopause] = autopauseOptions;

            // Controls options - based on swkotor.exe and swkotor2.exe controls system
            // Located via string references: "Mouse Sensitivity" @ 0x007c85cc, "Mouse Look" @ 0x007c8608, "Reverse Mouse Buttons" @ 0x007c8628
            // "keymap" @ 0x007c4cbc (keymap.2da file reference), "Pause" @ 0x007c4de8
            // Original implementation: Key bindings stored in keymap.2da, mouse settings in INI file
            // [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): CExoInputInternal input system (exoinputinternal.cpp @ 0x007c64dc)
            // Initialize default controls settings if not already initialized
            if (settings.Controls.KeyBindings.Count == 0)
            {
                settings.Controls.InitializeDefaults();
            }

            var controlsOptions = new List<OptionItem>
            {
                // Mouse settings
                new OptionItem("Mouse Sensitivity", OptionType.Numeric, () => (int)(settings.MouseSensitivity * 100), v => settings.MouseSensitivity = (float)v / 100.0f, 1, 100),
                new OptionItem("Invert Mouse Y", OptionType.Boolean, () => settings.InvertMouseY ? 1 : 0, v => settings.InvertMouseY = v > 0, 0, 1),

                // Key bindings - based on swkotor.exe and swkotor2.exe keymap.2da system
                // All key bindings can be rebound by selecting the option and pressing a new key
                new KeyBindingOptionItem("Pause", () => settings.Controls.GetKeyBinding("Pause", "Space"), k => settings.Controls.KeyBindings["Pause"] = k),
                new KeyBindingOptionItem("Cycle Party Leader", () => settings.Controls.GetKeyBinding("CycleParty", "Tab"), k => settings.Controls.KeyBindings["CycleParty"] = k),
                new KeyBindingOptionItem("Quick Slot 1", () => settings.Controls.GetKeyBinding("QuickSlot1", "D1"), k => settings.Controls.KeyBindings["QuickSlot1"] = k),
                new KeyBindingOptionItem("Quick Slot 2", () => settings.Controls.GetKeyBinding("QuickSlot2", "D2"), k => settings.Controls.KeyBindings["QuickSlot2"] = k),
                new KeyBindingOptionItem("Quick Slot 3", () => settings.Controls.GetKeyBinding("QuickSlot3", "D3"), k => settings.Controls.KeyBindings["QuickSlot3"] = k),
                new KeyBindingOptionItem("Quick Slot 4", () => settings.Controls.GetKeyBinding("QuickSlot4", "D4"), k => settings.Controls.KeyBindings["QuickSlot4"] = k),
                new KeyBindingOptionItem("Quick Slot 5", () => settings.Controls.GetKeyBinding("QuickSlot5", "D5"), k => settings.Controls.KeyBindings["QuickSlot5"] = k),
                new KeyBindingOptionItem("Quick Slot 6", () => settings.Controls.GetKeyBinding("QuickSlot6", "D6"), k => settings.Controls.KeyBindings["QuickSlot6"] = k),
                new KeyBindingOptionItem("Quick Slot 7", () => settings.Controls.GetKeyBinding("QuickSlot7", "D7"), k => settings.Controls.KeyBindings["QuickSlot7"] = k),
                new KeyBindingOptionItem("Quick Slot 8", () => settings.Controls.GetKeyBinding("QuickSlot8", "D8"), k => settings.Controls.KeyBindings["QuickSlot8"] = k),
                new KeyBindingOptionItem("Quick Slot 9", () => settings.Controls.GetKeyBinding("QuickSlot9", "D9"), k => settings.Controls.KeyBindings["QuickSlot9"] = k),
                new KeyBindingOptionItem("Solo Mode", () => settings.Controls.GetKeyBinding("SoloMode", "V"), k => settings.Controls.KeyBindings["SoloMode"] = k),
                new KeyBindingOptionItem("Character Screen", () => settings.Controls.GetKeyBinding("Character", "C"), k => settings.Controls.KeyBindings["Character"] = k),
                new KeyBindingOptionItem("Inventory", () => settings.Controls.GetKeyBinding("Inventory", "I"), k => settings.Controls.KeyBindings["Inventory"] = k),
                new KeyBindingOptionItem("Journal", () => settings.Controls.GetKeyBinding("Journal", "J"), k => settings.Controls.KeyBindings["Journal"] = k),
                new KeyBindingOptionItem("Map", () => settings.Controls.GetKeyBinding("Map", "M"), k => settings.Controls.KeyBindings["Map"] = k),

                // Mouse button bindings - based on swkotor.exe and swkotor2.exe mouse input system
                new MouseButtonBindingOptionItem("Move/Attack Button", () => settings.Controls.GetMouseButtonBinding("Move", "Left"), b => settings.Controls.MouseButtonBindings["Move"] = b),
                new MouseButtonBindingOptionItem("Context Action Button", () => settings.Controls.GetMouseButtonBinding("ContextAction", "Right"), b => settings.Controls.MouseButtonBindings["ContextAction"] = b)
            };
            options[OptionsCategory.Controls] = controlsOptions;

            return options;
        }

        /// <summary>
        /// Applies settings from options to GameSettings.
        /// </summary>
        private static void ApplySettings(GameSettings settings, Dictionary<OptionsCategory, List<OptionItem>> optionsByCategory)
        {
            foreach (var category in optionsByCategory.Values)
            {
                foreach (var option in category)
                {
                    option.ApplyValue();
                }
            }
        }

        /// <summary>
        /// Modifies an option value.
        /// </summary>
        private static void ModifyOptionValue(OptionItem option, bool increase)
        {
            if (option.Type == OptionType.Boolean)
            {
                option.SetValue(increase ? 1 : 0);
            }
            else if (option.Type == OptionType.Numeric)
            {
                double currentValue = option.GetValue();
                double step = (option.MaxValue - option.MinValue) / 20.0; // 20 steps
                if (step < 1.0) step = 1.0;
                double newValue = increase ? Math.Min(option.MaxValue, currentValue + step) : Math.Max(option.MinValue, currentValue - step);
                option.SetValue(newValue);
            }
        }

        /// <summary>
        /// Handles value input for numeric editing.
        /// </summary>
        private static void HandleValueInput(
            IKeyboardState current,
            IKeyboardState previous,
            ref string text,
            ref bool isEditing)
        {
            // Handle character input
            Keys[] pressedKeys = current.GetPressedKeys();
            foreach (Keys key in pressedKeys)
            {
                if (!previous.IsKeyDown(key))
                {
                    if (key >= Keys.D0 && key <= Keys.D9)
                    {
                        text += (key - Keys.D0).ToString();
                    }
                    else if (key == Keys.Back && text.Length > 0)
                    {
                        text = text.Substring(0, text.Length - 1);
                    }
                    else if (key == Keys.Enter)
                    {
                        isEditing = false;
                    }
                    else if (key == Keys.Escape)
                    {
                        isEditing = false;
                        text = string.Empty;
                    }
                }
            }
        }

        private static bool IsKeyJustPressed(IKeyboardState previous, IKeyboardState current, Keys key)
        {
            return previous.IsKeyUp(key) && current.IsKeyDown(key);
        }

        /// <summary>
        /// Option item type.
        /// </summary>
        public enum OptionType
        {
            Boolean,
            Numeric,
            Enum,
            KeyBinding,
            MouseButtonBinding
        }

        /// <summary>
        /// Represents a single option item.
        /// </summary>
        public class OptionItem
        {
            public string Name { get; }
            public OptionType Type { get; }
            public double MinValue { get; }
            public double MaxValue { get; }
            private Func<double> _getValue;
            private Action<double> _setValue;

            public OptionItem(string name, OptionType type, Func<double> getValue, Action<double> setValue, double minValue, double maxValue)
            {
                Name = name;
                Type = type;
                _getValue = getValue;
                _setValue = setValue;
                MinValue = minValue;
                MaxValue = maxValue;
            }

            public double GetValue()
            {
                return _getValue();
            }

            public void SetValue(double value)
            {
                value = Math.Max(MinValue, Math.Min(MaxValue, value));
                _setValue(value);
            }

            public void ApplyValue()
            {
                // Value is already applied when SetValue is called
            }

            public string GetStringValue()
            {
                double value = GetValue();
                if (Type == OptionType.Boolean)
                {
                    return value > 0 ? "On" : "Off";
                }
                else if (Type == OptionType.Numeric)
                {
                    return ((int)value).ToString();
                }
                return value.ToString();
            }
        }

        /// <summary>
        /// Represents a key binding option that can be rebound.
        /// </summary>
        /// <remarks>
        /// Key Binding Option Item:
        /// - Based on swkotor.exe and swkotor2.exe key binding system
        /// - Located via string references: "keymap" @ 0x007c4cbc (keymap.2da file reference)
        /// - Original implementation: Key bindings can be changed in options menu by selecting and pressing a new key
        /// - [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): Key binding UI allows rebinding any action to any key
        /// </remarks>
        public class KeyBindingOptionItem : OptionItem
        {
            private Func<string> _getKeyName;
            private Action<string> _setKeyName;

            public KeyBindingOptionItem(string name, Func<string> getKeyName, Action<string> setKeyName)
                : base(name, OptionType.KeyBinding, () => 0, v => { }, 0, 0)
            {
                _getKeyName = getKeyName;
                _setKeyName = setKeyName;
            }

            public new string GetStringValue()
            {
                return _getKeyName();
            }

            public void SetKeyName(string keyName)
            {
                _setKeyName(keyName);
            }
        }

        /// <summary>
        /// Represents a mouse button binding option that can be rebound.
        /// </summary>
        /// <remarks>
        /// Mouse Button Binding Option Item:
        /// - Based on swkotor.exe and swkotor2.exe mouse input system
        /// - Located via string references: "Reverse Mouse Buttons" @ 0x007c8628
        /// - Original implementation: Mouse buttons can be rebound in options menu
        /// - [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): Mouse button configuration allows rebinding actions to different mouse buttons
        /// </remarks>
        public class MouseButtonBindingOptionItem : OptionItem
        {
            private Func<string> _getButtonName;
            private Action<string> _setButtonName;

            public MouseButtonBindingOptionItem(string name, Func<string> getButtonName, Action<string> setButtonName)
                : base(name, OptionType.MouseButtonBinding, () => 0, v => { }, 0, 0)
            {
                _getButtonName = getButtonName;
                _setButtonName = setButtonName;
            }

            public new string GetStringValue()
            {
                return _getButtonName();
            }

            public void SetButtonName(string buttonName)
            {
                _setButtonName(buttonName);
            }
        }
    }
}

