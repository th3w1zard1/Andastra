using System;
using JetBrains.Annotations;

namespace Andastra.Runtime.Graphics.Common.GUI
{
    /// <summary>
    /// Base class for menu renderers across all graphics backends.
    /// Provides common menu functionality and interface.
    /// </summary>
    /// <remarks>
    /// Base Menu Renderer:
    /// - Based on swkotor2.exe menu rendering system
    /// - Located via string references: "RIMS:MAINMENU" @ 0x007b6044 (main menu RIM), "MAINMENU" @ 0x007cc030 (main menu constant)
    /// - "mainmenu_p" @ 0x007cc000 (main menu panel), "mainmenu8x6_p" @ 0x007cc00c (main menu 8x6 panel)
    /// - "mainmenu01" @ 0x007cc108, "mainmenu02" @ 0x007cc138, "mainmenu03" @ 0x007cc12c, "mainmenu04" @ 0x007cc120, "mainmenu05" @ 0x007cc114 (main menu variants)
    /// - "LBL_MENUBG" @ 0x007cbf80 (menu background label), "Action Menu" @ 0x007c8480 (action menu)
    /// - "CB_ACTIONMENU" @ 0x007d29d4 (action menu checkbox)
    /// - GUI elements: "gui_mp_arwalk00" through "gui_mp_arwalk15" @ 0x007b59cc-0x007b58dc (area walk GUI elements)
    /// - "gui_mp_arrun00" through "gui_mp_arrun15" @ 0x007b5a0c-0x007b59dc (area run GUI elements)
    /// - Original implementation: Renders main menu with buttons (Start Game, Options, Exit), handles input and selection
    /// - Menu panels: Uses GUI panel system with background textures and button overlays
    /// - Based on swkotor2.exe: Main menu uses GUI system with panel-based rendering
    /// 
    /// Inheritance Structure:
    /// - BaseMenuRenderer (this class) - Common menu functionality (visibility, viewport management)
    ///   - MyraMenuRenderer (Runtime.MonoGame.GUI) - MonoGame-specific Myra UI implementation
    ///   - StrideMenuRenderer (Runtime.Stride.GUI) - Stride-specific SpriteBatch implementation
    /// </remarks>
    public abstract class BaseMenuRenderer : IDisposable
    {
        private bool _isVisible = false;
        private int _viewportWidth = 0;
        private int _viewportHeight = 0;
        private bool _isInitialized = false;

        /// <summary>
        /// Gets or sets whether the menu is visible.
        /// </summary>
        public bool IsVisible
        {
            get { return _isVisible; }
            set
            {
                if (_isVisible != value)
                {
                    _isVisible = value;
                    OnVisibilityChanged(value);
                }
            }
        }

        /// <summary>
        /// Gets whether the menu renderer is initialized.
        /// </summary>
        public bool IsInitialized
        {
            get { return _isInitialized; }
            protected set { _isInitialized = value; }
        }

        /// <summary>
        /// Gets the current viewport width.
        /// </summary>
        public int ViewportWidth
        {
            get { return _viewportWidth; }
        }

        /// <summary>
        /// Gets the current viewport height.
        /// </summary>
        public int ViewportHeight
        {
            get { return _viewportHeight; }
        }

        /// <summary>
        /// Updates the viewport size when the window is resized.
        /// </summary>
        /// <param name="width">New viewport width.</param>
        /// <param name="height">New viewport height.</param>
        public virtual void UpdateViewport(int width, int height)
        {
            if (width <= 0 || height <= 0)
            {
                throw new ArgumentException("Viewport dimensions must be positive", nameof(width));
            }

            if (_viewportWidth != width || _viewportHeight != height)
            {
                _viewportWidth = width;
                _viewportHeight = height;
                OnViewportChanged(width, height);
            }
        }

        /// <summary>
        /// Sets the visibility of the menu.
        /// </summary>
        /// <param name="visible">True to show the menu, false to hide it.</param>
        public void SetVisible(bool visible)
        {
            IsVisible = visible;
        }

        /// <summary>
        /// Called when visibility changes. Override to handle visibility change events.
        /// </summary>
        /// <param name="visible">New visibility state.</param>
        protected virtual void OnVisibilityChanged(bool visible)
        {
            // Override in derived classes to handle visibility changes
        }

        /// <summary>
        /// Called when viewport size changes. Override to handle viewport resize events.
        /// </summary>
        /// <param name="width">New viewport width.</param>
        /// <param name="height">New viewport height.</param>
        protected virtual void OnViewportChanged(int width, int height)
        {
            // Override in derived classes to handle viewport changes
        }

        /// <summary>
        /// Initializes the menu renderer. Must be called before use.
        /// </summary>
        /// <param name="viewportWidth">Initial viewport width.</param>
        /// <param name="viewportHeight">Initial viewport height.</param>
        public virtual void Initialize(int viewportWidth, int viewportHeight)
        {
            if (viewportWidth <= 0 || viewportHeight <= 0)
            {
                throw new ArgumentException("Viewport dimensions must be positive", nameof(viewportWidth));
            }

            _viewportWidth = viewportWidth;
            _viewportHeight = viewportHeight;
            _isInitialized = true;
        }

        /// <summary>
        /// Disposes of resources used by the menu renderer.
        /// </summary>
        public virtual void Dispose()
        {
            _isInitialized = false;
            _isVisible = false;
        }
    }
}

