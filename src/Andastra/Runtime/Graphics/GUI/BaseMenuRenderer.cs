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
    /// - Based on exhaustive reverse engineering of swkotor.exe and swkotor2.exe menu initialization
    /// - swkotor2.exe: 0x006d2350 @ 0x006d2350 (menu constructor/initializer)
    /// - swkotor.exe: 0x0067c4c0 @ 0x0067c4c0 (menu constructor/initializer)
    /// 
    /// Initialization Sequence (matching original engines):
    /// 1. Load "MAINMENU" GUI file first (swkotor2.exe: 0x006d2350:73, swkotor.exe: 0x0067c4c0:62)
    /// 2. If GUI load succeeds, load "RIMS:MAINMENU" RIM file (swkotor2.exe: 0x006d2350:76-80, swkotor.exe: 0x0067c4c0:65-69)
    /// 3. Clear menu flag at DAT_008283c0+0x34 (bit 2 = 0xfd mask) (swkotor2.exe: 0x006d2350:82)
    /// 4. Set up event handlers for menu buttons (0x27, 0x2d, 0, 1 events) (swkotor2.exe: 0x006d2350:89-95)
    /// 
    /// String References:
    /// - "RIMS:MAINMENU" @ swkotor2.exe:0x007b6044, swkotor.exe:0x0073e0a4 (main menu RIM file)
    /// - "MAINMENU" @ swkotor2.exe:0x007cc030, swkotor.exe:0x00752f64 (main menu GUI constant)
    /// - "mainmenu_p" @ swkotor2.exe:0x007cc000 (main menu panel)
    /// - "mainmenu8x6_p" @ swkotor2.exe:0x007cc00c (main menu 8x6 panel)
    /// - "mainmenu01" @ swkotor2.exe:0x007cc108 (menu variant 1)
    /// - "mainmenu02" @ swkotor2.exe:0x007cc138 (menu variant 2)
    /// - "mainmenu03" @ swkotor2.exe:0x007cc12c (menu variant 3)
    /// - "mainmenu04" @ swkotor2.exe:0x007cc120 (menu variant 4)
    /// - "mainmenu05" @ swkotor2.exe:0x007cc114 (menu variant 5)
    /// - "mainmenu" @ swkotor.exe:0x00752f4c (single menu panel, no variants)
    /// - "LBL_MENUBG" @ swkotor2.exe:0x007cbf80, swkotor.exe:0x00752ec8 (menu background label)
    /// - "Action Menu" @ swkotor2.exe:0x007c8480, swkotor.exe:0x0074f9e0 (action menu)
    /// - "CB_ACTIONMENU" @ swkotor2.exe:0x007d29d4, swkotor.exe:0x00758e84 (action menu checkbox)
    /// - "gui3D_room" @ swkotor2.exe:0x007cc144 (used to determine menu variant)
    /// 
    /// Menu Variants (swkotor2.exe only):
    /// - Menu variant selection based on "gui3D_room" condition (swkotor2.exe: 0x006d2350:120-150)
    /// - Variants: mainmenu01 (default), mainmenu02, mainmenu03, mainmenu04, mainmenu05
    /// - swkotor.exe uses single "mainmenu" panel (no variants)
    /// 
    /// Event Handlers:
    /// - Menu buttons register handlers for events: 0x27, 0x2d, 0, 1 (swkotor2.exe: 0x006d2350:89-95)
    /// - Event 0x27: Button press/select
    /// - Event 0x2d: Button release/deselect
    /// - Event 0: Unknown (likely hover/enter)
    /// - Event 1: Unknown (likely leave/exit)
    /// 
    /// Original Implementation:
    /// - Renders main menu with buttons (Start Game, Options, Exit), handles input and selection
    /// - Menu panels: Uses GUI panel system with background textures and button overlays
    /// - [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): Main menu uses GUI system with panel-based rendering
    /// - Menu state tracked via flag at DAT_008283c0+0x34 (swkotor2.exe) or DAT_007a39e8+0x34 (swkotor.exe)
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

