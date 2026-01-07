using System;
using System.Collections.Generic;
using System.Numerics;
using Andastra.Runtime.Graphics;
using JetBrains.Annotations;

namespace Andastra.Runtime.Games.Common
{
    /// <summary>
    /// Base class for GUI managers across all engines.
    /// Contains common GUI loading, rendering, and input handling patterns.
    /// </summary>
    /// <remarks>
    /// Base GUI Manager System:
    /// - Common patterns: GUI loading, control rendering, input handling, font caching
    /// - All engines use GUI files with panel/button/label definitions
    /// - Common operations: LoadGui, RenderControl, HandleInput, font loading
    /// - Engine-specific: GUI format (GUI/GFF/other), font format, rendering backend
    /// 
    /// Inheritance Structure:
    /// - BaseGuiManager (Runtime.Games.Common) - Common GUI operations
    ///   - Odyssey: KotorGuiManager : BaseGuiManager (swkotor.exe, swkotor2.exe)
    ///   - Aurora: AuroraGuiManager : BaseGuiManager (nwmain.exe)
    ///   - Eclipse: EclipseGuiManager : BaseGuiManager (daorigins.exe, DragonAge2.exe)
    ///   - Infinity: InfinityGuiManager : BaseGuiManager (, )
    /// </remarks>
    public abstract class BaseGuiManager : IDisposable
    {
        /// <summary>
        /// Gets the graphics device.
        /// </summary>
        protected abstract IGraphicsDevice GraphicsDevice { get; }

        /// <summary>
        /// Event fired when a GUI button is clicked.
        /// </summary>
        public event EventHandler<GuiButtonClickedEventArgs> OnButtonClicked;

        /// <summary>
        /// Loads a GUI from game files.
        /// </summary>
        /// <param name="guiName">Name of the GUI file to load (without extension).</param>
        /// <param name="width">Screen width for GUI scaling.</param>
        /// <param name="height">Screen height for GUI scaling.</param>
        /// <returns>True if GUI was loaded successfully, false otherwise.</returns>
        public abstract bool LoadGui(string guiName, int width, int height);

        /// <summary>
        /// Unloads a GUI from memory.
        /// </summary>
        /// <param name="guiName">Name of the GUI to unload.</param>
        public abstract void UnloadGui(string guiName);

        /// <summary>
        /// Sets the current active GUI.
        /// </summary>
        /// <param name="guiName">Name of the GUI to set as current.</param>
        /// <returns>True if GUI was found and set, false otherwise.</returns>
        public abstract bool SetCurrentGui(string guiName);

        /// <summary>
        /// Updates GUI input handling (mouse/keyboard).
        /// </summary>
        /// <param name="gameTime">Current game time (engine-specific type).</param>
        public abstract void Update(object gameTime);

        /// <summary>
        /// Renders the current GUI.
        /// </summary>
        /// <param name="gameTime">Current game time (engine-specific type).</param>
        public abstract void Draw(object gameTime);

        /// <summary>
        /// Loads a bitmap font from a ResRef, with caching.
        /// </summary>
        /// <param name="fontResRef">The font resource reference.</param>
        /// <returns>The loaded font, or null if loading failed.</returns>
        [CanBeNull]
        protected abstract BaseBitmapFont LoadFont(string fontResRef);

        /// <summary>
        /// Calculates text position based on alignment flags.
        /// Common implementation that works for all engines.
        /// </summary>
        /// <param name="alignment">The alignment flags.</param>
        /// <param name="controlPosition">The control position.</param>
        /// <param name="controlSize">The control size.</param>
        /// <param name="textSize">The text size.</param>
        /// <returns>The calculated text position.</returns>
        protected virtual Graphics.Vector2 CalculateTextPosition(int alignment, Graphics.Vector2 controlPosition, Graphics.Vector2 controlSize, Graphics.Vector2 textSize)
        {
            float x = controlPosition.X;
            float y = controlPosition.Y;

            // Horizontal alignment
            // 1, 17, 33 = left
            // 2, 18, 34 = center
            // 3, 19, 35 = right
            if (alignment == 2 || alignment == 18 || alignment == 34)
            {
                // Center horizontally
                x = controlPosition.X + (controlSize.X - textSize.X) / 2.0f;
            }
            else if (alignment == 3 || alignment == 19 || alignment == 35)
            {
                // Right align
                x = controlPosition.X + controlSize.X - textSize.X;
            }
            // else: left align (default)

            // Vertical alignment
            // 1, 2, 3 = top
            // 17, 18, 19 = center
            // 33, 34, 35 = bottom
            if (alignment >= 17 && alignment <= 19)
            {
                // Center vertically
                y = controlPosition.Y + (controlSize.Y - textSize.Y) / 2.0f;
            }
            else if (alignment >= 33 && alignment <= 35)
            {
                // Bottom align
                y = controlPosition.Y + controlSize.Y - textSize.Y;
            }
            // else: top align (default)

            return new Graphics.Vector2(x, y);
        }

        /// <summary>
        /// Fires the OnButtonClicked event.
        /// </summary>
        protected void FireButtonClicked(string buttonTag, int buttonId)
        {
            OnButtonClicked?.Invoke(this, new GuiButtonClickedEventArgs
            {
                ButtonTag = buttonTag,
                ButtonId = buttonId
            });
        }

        /// <summary>
        /// Disposes resources.
        /// </summary>
        public abstract void Dispose();
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

