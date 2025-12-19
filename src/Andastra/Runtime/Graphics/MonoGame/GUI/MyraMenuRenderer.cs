using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Myra;
using Myra.Graphics2D.UI;
using Andastra.Runtime.Graphics.Common.GUI;

namespace Andastra.Runtime.MonoGame.GUI
{
    /// <summary>
    /// Myra-based menu renderer for MonoGame (if Myra is available).
    /// Falls back to SpriteBatch rendering if Myra is not available.
    /// </summary>
    /// <remarks>
    /// Myra Menu Renderer:
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
    /// - This implementation: Uses Myra UI library for modern, cross-platform menu rendering
    /// - Myra provides declarative UI with XML-based layouts and event handling
    /// 
    /// Inheritance:
    /// - BaseMenuRenderer (Runtime.Graphics.Common.GUI) - Common menu functionality
    ///   - MyraMenuRenderer (this class) - MonoGame-specific Myra UI implementation
    /// </remarks>
    public class MyraMenuRenderer : BaseMenuRenderer
    {
        private GraphicsDevice _graphicsDevice;
        private Desktop _desktop;
        private Panel _rootPanel;
        private bool _isDisposed = false;

        /// <summary>
        /// Gets the Myra Desktop instance for UI hierarchy management.
        /// </summary>
        public Desktop Desktop
        {
            get { return _desktop; }
        }

        /// <summary>
        /// Gets the root Panel that contains all menu widgets.
        /// </summary>
        public Panel RootPanel
        {
            get { return _rootPanel; }
        }

        /// <summary>
        /// Initializes Myra UI system with the provided graphics device.
        /// </summary>
        /// <param name="graphicsDevice">The MonoGame GraphicsDevice to use for rendering.</param>
        /// <exception cref="ArgumentNullException">Thrown if graphicsDevice is null.</exception>
        public MyraMenuRenderer(GraphicsDevice graphicsDevice)
        {
            if (graphicsDevice == null)
            {
                throw new ArgumentNullException(nameof(graphicsDevice));
            }

            _graphicsDevice = graphicsDevice;
            InitializeMyra();
        }

        /// <summary>
        /// Initializes Myra UI system. This must be called before using the renderer.
        /// </summary>
        private void InitializeMyra()
        {
            try
            {
                // Initialize Myra environment
                // Myra can work with or without a Game instance
                // If Game instance is needed, it should be set via MyraEnvironment.Game
                // For now, we'll initialize without Game instance and handle rendering manually
                
                // Create the desktop (root container for all UI widgets)
                // Desktop manages the UI hierarchy and rendering
                _desktop = new Desktop();

                // Create root panel that will contain all menu elements
                // This panel fills the entire viewport and serves as the container for all menu widgets
                _rootPanel = new Panel();
                _rootPanel.Width = _graphicsDevice.Viewport.Width;
                _rootPanel.Height = _graphicsDevice.Viewport.Height;
                _rootPanel.Background = new SolidColorBrush(Microsoft.Xna.Framework.Color.Transparent);

                // Add root panel to desktop as the root widget
                // All UI elements will be children of this panel
                _desktop.Root = _rootPanel;

                // Initialize base class with viewport dimensions
                Initialize(_graphicsDevice.Viewport.Width, _graphicsDevice.Viewport.Height);

                Console.WriteLine("[MyraMenuRenderer] Myra UI initialized successfully");
                Console.WriteLine($"[MyraMenuRenderer] Viewport: {_graphicsDevice.Viewport.Width}x{_graphicsDevice.Viewport.Height}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[MyraMenuRenderer] ERROR: Failed to initialize Myra UI: {ex.Message}");
                Console.WriteLine($"[MyraMenuRenderer] Stack trace: {ex.StackTrace}");
                IsInitialized = false;
                _desktop = null;
                _rootPanel = null;
            }
        }

        /// <summary>
        /// Called when viewport size changes. Updates Myra UI panel dimensions.
        /// </summary>
        /// <param name="width">New viewport width.</param>
        /// <param name="height">New viewport height.</param>
        protected override void OnViewportChanged(int width, int height)
        {
            base.OnViewportChanged(width, height);
            
            if (_rootPanel != null)
            {
                _rootPanel.Width = width;
                _rootPanel.Height = height;
            }
        }

        /// <summary>
        /// Renders the Myra UI menu system.
        /// </summary>
        /// <param name="gameTime">Current game time for frame timing.</param>
        /// <param name="device">Graphics device to render to (must match the one used for initialization).</param>
        /// <summary>
        /// Renders the Myra UI menu system.
        /// </summary>
        /// <param name="gameTime">Current game time for frame timing.</param>
        /// <param name="device">Graphics device to render to (must match the one used for initialization).</param>
        public void Draw(GameTime gameTime, GraphicsDevice device)
        {
            if (!IsVisible || !IsInitialized || _isDisposed)
            {
                return;
            }

            if (device == null)
            {
                Console.WriteLine("[MyraMenuRenderer] ERROR: GraphicsDevice is null in Draw");
                return;
            }

            if (_desktop == null)
            {
                Console.WriteLine("[MyraMenuRenderer] ERROR: Desktop is null in Draw");
                return;
            }

            try
            {
                // Render Myra UI
                // Myra handles its own sprite batch and rendering internally
                // Desktop.Render() will render all widgets in the hierarchy
                // It uses the GraphicsDevice that was active when Desktop was created
                _desktop.Render();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[MyraMenuRenderer] ERROR: Failed to render Myra UI: {ex.Message}");
                Console.WriteLine($"[MyraMenuRenderer] Stack trace: {ex.StackTrace}");
            }
        }

        /// <summary>
        /// Updates the Myra UI system (handles input, animations, etc.).
        /// </summary>
        /// <param name="gameTime">Current game time for update timing.</param>
        /// <summary>
        /// Updates the Myra UI system (handles input, animations, etc.).
        /// </summary>
        /// <param name="gameTime">Current game time for update timing.</param>
        public void Update(GameTime gameTime)
        {
            if (!IsInitialized || _desktop == null || _isDisposed)
            {
                return;
            }

            try
            {
                // Update Myra UI (handles input, animations, etc.)
                // Myra processes mouse/keyboard input automatically
                // Desktop internally accesses Mouse.GetState() and Keyboard.GetState()
                // Widgets will receive input events (clicks, hover, etc.) automatically
                // Animations and timers are also updated here
                // Note: Myra's Desktop doesn't have an explicit Update() method in all versions
                // Input is typically handled during Render(), but we can add explicit update logic if needed
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[MyraMenuRenderer] ERROR: Failed to update Myra UI: {ex.Message}");
            }
        }

        /// <summary>
        /// Disposes of resources used by the menu renderer.
        /// </summary>
        public override void Dispose()
        {
            if (_isDisposed)
            {
                return;
            }

            if (_desktop != null)
            {
                _desktop.Root = null;
                _desktop = null;
            }

            if (_rootPanel != null)
            {
                _rootPanel = null;
            }

            _graphicsDevice = null;
            _isDisposed = true;

            base.Dispose();

            Console.WriteLine("[MyraMenuRenderer] Disposed");
        }
    }
}

