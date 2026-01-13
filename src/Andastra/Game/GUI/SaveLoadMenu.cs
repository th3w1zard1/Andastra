using System;
using System.Collections.Generic;
using System.Linq;
using Andastra.Runtime.Core.Save;
using Andastra.Runtime.Graphics;

namespace Andastra.Game.Game.GUI
{
    /// <summary>
    /// Handles save/load menu rendering and input.
    /// </summary>
    /// <remarks>
    /// Save/Load Menu:
    /// - [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address) save/load menu system
    /// - Located via string references: "LoadSavegame" @ 0x007bdc90, "SaveGame" @ 0x007be1d0
    /// - "SAVEGAME" @ 0x007be28c, "SAVES:" @ 0x007be284, "savenfo" @ 0x007be1f0
    /// - "SavegameList" @ 0x007bdca0, "GetSavegameList" @ 0x007bdcb0, "SAVEGAMENAME" @ 0x007be1a8
    /// - "Mod_IsSaveGame" @ 0x007bea48, ":: Module savegame list: %s.\n" @ 0x007cbbb4, "BTN_SAVEGAME" @ 0x007d0dbc
    /// - Original implementation: Shows list of save games with metadata (name, time, module, play time)
    /// - Save slots: 1-99 (numbered saves), quick save slot, auto save slot
    /// - Save display: Shows save name, module name, play time, save time, screenshot thumbnail
    /// - Load save menu: FUN_00708990 @ 0x00708990 (loads save game list, displays save metadata)
    /// - Save menu: FUN_004eb750 @ 0x004eb750 (creates save game ERF archive, writes savenfo.res GFF)
    /// - Save list loading: FUN_0070a020 @ 0x0070a020 (enumerates save directories, reads savenfo.res files)
    /// - Save validation: Checks for savenfo.res GFF file in each save directory to determine valid saves
    /// - Save directory structure: "SAVES:\{saveName}\" contains savegame.sav (ERF) and savenfo.res (GFF metadata)
    /// </remarks>
    public static class SaveLoadMenu
    {
        /// <summary>
        /// Updates save menu state and handles input.
        /// </summary>
        public static void UpdateSaveMenu(
            float deltaTime,
            IKeyboardState currentKeyboard,
            IKeyboardState previousKeyboard,
            IMouseState currentMouse,
            IMouseState previousMouse,
            ref int selectedIndex,
            ref string newSaveName,
            ref bool isEnteringName,
            List<SaveGameInfo> availableSaves,
            Action<int> onSelectSlot,
            Action<string> onSave,
            Action onCancel)
        {
            if (isEnteringName)
            {
                // Handle text input for save name
                HandleTextInput(currentKeyboard, previousKeyboard, ref newSaveName, onSave, onCancel);
                return;
            }

            // Navigation
            if (IsKeyJustPressed(previousKeyboard, currentKeyboard, Keys.Up))
            {
                selectedIndex = Math.Max(0, selectedIndex - 1);
            }
            if (IsKeyJustPressed(previousKeyboard, currentKeyboard, Keys.Down))
            {
                selectedIndex = Math.Min(availableSaves.Count, selectedIndex + 1);
            }

            // Selection
            if (IsKeyJustPressed(previousKeyboard, currentKeyboard, Keys.Enter) ||
                IsKeyJustPressed(previousKeyboard, currentKeyboard, Keys.Space))
            {
                if (selectedIndex < availableSaves.Count)
                {
                    // Overwrite existing save
                    onSelectSlot(selectedIndex);
                }
                else
                {
                    // New save - enter name
                    isEnteringName = true;
                    newSaveName = string.Empty;
                }
            }

            // Cancel
            if (IsKeyJustPressed(previousKeyboard, currentKeyboard, Keys.Escape))
            {
                onCancel();
            }
        }

        /// <summary>
        /// Updates load menu state and handles input.
        /// </summary>
        public static void UpdateLoadMenu(
            float deltaTime,
            IKeyboardState currentKeyboard,
            IKeyboardState previousKeyboard,
            IMouseState currentMouse,
            IMouseState previousMouse,
            ref int selectedIndex,
            List<SaveGameInfo> availableSaves,
            Action<int> onLoad,
            Action onCancel)
        {
            // Navigation
            if (IsKeyJustPressed(previousKeyboard, currentKeyboard, Keys.Up))
            {
                selectedIndex = Math.Max(0, selectedIndex - 1);
            }
            if (IsKeyJustPressed(previousKeyboard, currentKeyboard, Keys.Down))
            {
                selectedIndex = Math.Min(availableSaves.Count - 1, selectedIndex + 1);
            }

            // Selection
            if (IsKeyJustPressed(previousKeyboard, currentKeyboard, Keys.Enter) ||
                IsKeyJustPressed(previousKeyboard, currentKeyboard, Keys.Space))
            {
                if (selectedIndex >= 0 && selectedIndex < availableSaves.Count)
                {
                    onLoad(selectedIndex);
                }
            }

            // Cancel
            if (IsKeyJustPressed(previousKeyboard, currentKeyboard, Keys.Escape))
            {
                onCancel();
            }
        }

        /// <summary>
        /// Draws the save menu.
        /// </summary>
        public static void DrawSaveMenu(
            ISpriteBatch spriteBatch,
            IFont font,
            ITexture2D menuTexture,
            IGraphicsDevice graphicsDevice,
            int viewportWidth,
            int viewportHeight,
            int selectedIndex,
            string newSaveName,
            bool isEnteringName,
            List<SaveGameInfo> availableSaves)
        {
            // Clear to dark background
            graphicsDevice.Clear(new Color(20, 20, 30, 255));

            spriteBatch.Begin();

            // Title
            string title = "Save Game";
            Vector2 titleSize = font.MeasureString(title);
            Vector2 titlePos = new Vector2((viewportWidth - titleSize.X) / 2, 50);
            spriteBatch.DrawString(font, title, titlePos, Color.White);

            // Save list
            int startY = 150;
            int itemHeight = 60;
            int itemSpacing = 10;
            int maxVisible = (viewportHeight - startY - 100) / (itemHeight + itemSpacing);
            int startIndex = Math.Max(0, selectedIndex - maxVisible / 2);
            int endIndex = Math.Min(availableSaves.Count + 1, startIndex + maxVisible);

            for (int i = startIndex; i < endIndex; i++)
            {
                int y = startY + (i - startIndex) * (itemHeight + itemSpacing);
                bool isSelected = (i == selectedIndex);
                Color bgColor = isSelected ? new Color(100, 100, 150) : new Color(50, 50, 70);

                Rectangle itemRect = new Rectangle(100, y, viewportWidth - 200, itemHeight);
                spriteBatch.Draw(menuTexture, itemRect, bgColor);

                if (i < availableSaves.Count)
                {
                    SaveGameInfo save = availableSaves[i];
                    string saveText = $"{save.Name} - {save.ModuleName} - {save.SaveTime:g}";
                    Vector2 textPos = new Vector2(itemRect.X + 10, itemRect.Y + (itemHeight - font.LineSpacing) / 2);
                    spriteBatch.DrawString(font, saveText, textPos, new Color(255, 255, 255, 255));
                }
                else
                {
                    string newSaveText = isEnteringName ? $"New Save: {newSaveName}_" : "New Save";
                    Vector2 textPos = new Vector2(itemRect.X + 10, itemRect.Y + (itemHeight - font.LineSpacing) / 2);
                    spriteBatch.DrawString(font, newSaveText, textPos, new Color(211, 211, 211, 255));
                }
            }

            // Instructions
            string instructions = isEnteringName
                ? "Enter save name, then press Enter to save or Escape to cancel"
                : "Select a save slot or create a new save. Press Escape to cancel.";
            Vector2 instSize = font.MeasureString(instructions);
            Vector2 instPos = new Vector2((viewportWidth - instSize.X) / 2, viewportHeight - 50);
            spriteBatch.DrawString(font, instructions, instPos, new Color(211, 211, 211, 255));

            spriteBatch.End();
        }

        /// <summary>
        /// Draws the load menu.
        /// </summary>
        public static void DrawLoadMenu(
            ISpriteBatch spriteBatch,
            IFont font,
            ITexture2D menuTexture,
            IGraphicsDevice graphicsDevice,
            int viewportWidth,
            int viewportHeight,
            int selectedIndex,
            List<SaveGameInfo> availableSaves)
        {
            // Clear to dark background
            graphicsDevice.Clear(new Color(20, 20, 30, 255));

            spriteBatch.Begin();

            // Title
            string title = "Load Game";
            Vector2 titleSize = font.MeasureString(title);
            Vector2 titlePos = new Vector2((viewportWidth - titleSize.X) / 2, 50);
            spriteBatch.DrawString(font, title, titlePos, Color.White);

            // Save list
            int startY = 150;
            int itemHeight = 60;
            int itemSpacing = 10;
            int maxVisible = (viewportHeight - startY - 100) / (itemHeight + itemSpacing);
            int startIdx = Math.Max(0, selectedIndex - maxVisible / 2);
            int endIdx = Math.Min(availableSaves.Count, startIdx + maxVisible);

            for (int i = startIdx; i < endIdx; i++)
            {
                int y = startY + (i - startIdx) * (itemHeight + itemSpacing);
                bool isSelected = (i == selectedIndex);
                Color bgColor = isSelected ? new Color(100, 100, 150) : new Color(50, 50, 70);

                Rectangle itemRect = new Rectangle(100, y, viewportWidth - 200, itemHeight);
                spriteBatch.Draw(menuTexture, itemRect, bgColor);

                SaveGameInfo save = availableSaves[i];
                string saveText = $"{save.Name} - {save.ModuleName} - {save.SaveTime:g}";
                Vector2 textPos = new Vector2(itemRect.X + 10, itemRect.Y + (itemHeight - font.LineSpacing) / 2);
                spriteBatch.DrawString(font, saveText, textPos, new Color(255, 255, 255, 255));
            }

            // Instructions
            string instructions = "Select a save to load. Press Escape to cancel.";
            Vector2 instSize = font.MeasureString(instructions);
            Vector2 instPos = new Vector2((viewportWidth - instSize.X) / 2, viewportHeight - 50);
            spriteBatch.DrawString(font, instructions, instPos, new Color(211, 211, 211, 255));

            spriteBatch.End();
        }

        /// <summary>
        /// Handles text input for save name entry.
        /// </summary>
        private static void HandleTextInput(
            IKeyboardState current,
            IKeyboardState previous,
            ref string text,
            Action<string> onConfirm,
            Action onCancel)
        {
            // Handle character input
            Keys[] pressedKeys = current.GetPressedKeys();
            foreach (Keys key in pressedKeys)
            {
                if (!previous.IsKeyDown(key))
                {
                    if (key >= Keys.A && key <= Keys.Z)
                    {
                        text += key.ToString();
                    }
                    else if (key >= Keys.D0 && key <= Keys.D9)
                    {
                        text += (key - Keys.D0).ToString();
                    }
                    else if (key == Keys.Space)
                    {
                        text += " ";
                    }
                    else if (key == Keys.Back && text.Length > 0)
                    {
                        text = text.Substring(0, text.Length - 1);
                    }
                    else if (key == Keys.Enter)
                    {
                        if (!string.IsNullOrWhiteSpace(text))
                        {
                            onConfirm(text);
                        }
                    }
                    else if (key == Keys.Escape)
                    {
                        onCancel();
                    }
                }
            }
        }

        private static bool IsKeyJustPressed(IKeyboardState previous, IKeyboardState current, Keys key)
        {
            return previous.IsKeyUp(key) && current.IsKeyDown(key);
        }
    }
}

