using System;
using System.Collections.Generic;
using Andastra.Runtime.Core;
using Andastra.Runtime.Core.Audio;
using Andastra.Runtime.Core.Dialogue;
using Andastra.Runtime.Graphics;
using Vector2 = Odyssey.Graphics.Vector2;
using Rectangle = Odyssey.Graphics.Rectangle;
using Color = Odyssey.Graphics.Color;

namespace Andastra.Runtime.Game.GUI
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
    /// - Function: FUN_00633270 @ 0x00633270 (loads configuration from INI file in swkotor2.exe)
    /// - Function: FUN_00631ff0 @ 0x00631ff0 (writes INI values in swkotor2.exe)
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
            GameSettings settings,
            Dictionary<OptionsCategory, List<OptionItem>> optionsByCategory,
            Action<GameSettings> onApply,
            Action onCancel)
        {
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

            // Enter to edit numeric values
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
                    string optionText = option.Name + ": " + (isEditingValue && i == selectedOptionIndex ? editingValue + "_" : option.GetStringValue());
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
            string instructions = isEditingValue
                ? "Enter new value, then press Enter to confirm or Escape to cancel"
                : "Use Arrow Keys to navigate, A/D to change values, Enter to apply, Escape to cancel";
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
        /// - Based on swkotor2.exe: FUN_00631ff0 @ 0x00631ff0 (writes INI values for audio settings)
        /// - Based on swkotor2.exe: FUN_00633270 @ 0x00633270 (loads audio settings from INI file)
        /// </remarks>
        public static Dictionary<OptionsCategory, List<OptionItem>> CreateDefaultOptions(GameSettings settings, ISoundPlayer soundPlayer = null, IMusicPlayer musicPlayer = null, IVoicePlayer voicePlayer = null)
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
                new OptionItem("Master Volume", OptionType.Numeric, () => (int)(settings.MasterVolume * 100.0f),
                    v =>
                    {
                        settings.MasterVolume = (float)v / 100.0f;
                        // Apply master volume to all audio systems immediately if available
                        if (soundPlayer != null)
                        {
                            soundPlayer.Volume = settings.MasterVolume;
                        }
                        if (musicPlayer != null)
                        {
                            musicPlayer.Volume = settings.MasterVolume * settings.Audio.MusicVolume;
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
                new OptionItem("SFX Volume", OptionType.Numeric, () => (int)(settings.Audio.EffectsVolume * 100.0f),
                    v =>
                    {
                        float volume = (float)v / 100.0f;
                        settings.Audio.EffectsVolume = volume;
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
                        // Based on swkotor2.exe: VoiceVolume setting applied to voice-over playback
                        if (voicePlayer != null)
                        {
                            voicePlayer.Volume = volume;
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

            // Controls options (placeholder for future controls system)
            var controlsOptions = new List<OptionItem>
            {
                new OptionItem("Mouse Sensitivity", OptionType.Numeric, () => (int)(settings.MouseSensitivity * 100), v => settings.MouseSensitivity = v / 100.0f, 1, 100),
                new OptionItem("Invert Mouse Y", OptionType.Boolean, () => settings.InvertMouseY ? 1 : 0, v => settings.InvertMouseY = v > 0, 0, 1)
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
            Enum
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
    }
}

